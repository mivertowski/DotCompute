// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Roslyn analyzer for kernel type-safety gotchas that are hard contracts at runtime
/// but easy to catch at compile-time.
/// </summary>
/// <remarks>
/// <para>Reports the following diagnostics:</para>
/// <list type="bullet">
/// <item><description><b>DC018</b> — Kernel type parameters must have an <c>unmanaged</c> constraint.</description></item>
/// <item><description><b>DC019</b> — Potential buffer aliasing when the same buffer is passed to multiple kernel arguments.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KernelTypeSafetyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        KernelDiagnostics.KernelGenericParameterMustBeUnmanaged,
        KernelDiagnostics.KernelPotentialBufferAliasing
    );

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // DC018: Inspect each method declaration for [Kernel] + missing 'unmanaged' constraints.
        context.RegisterSyntaxNodeAction(AnalyzeMethodForUnmanagedConstraint, SyntaxKind.MethodDeclaration);

        // DC019: Inspect each invocation for repeated buffer arguments passed to a kernel call.
        context.RegisterSyntaxNodeAction(AnalyzeInvocationForBufferAliasing, SyntaxKind.InvocationExpression);
    }

    // ---------- DC018: Kernel type parameter must be unmanaged ----------

    private static void AnalyzeMethodForUnmanagedConstraint(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodSyntax)
        {
            return;
        }

        // Only type parameters on generic methods can be constrained per-method.
        if (methodSyntax.TypeParameterList is null || methodSyntax.TypeParameterList.Parameters.Count == 0)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!KernelAnalysisHelpers.HasKernelAttribute(methodSymbol))
        {
            return;
        }

        // Collect the set of type parameters actually referenced in the signature.
        // If a type parameter is declared but unused, we still flag it — its mere
        // presence on a [Kernel] method is a contract violation if callers can bind
        // it to a managed type.
        foreach (var typeParameter in methodSymbol.TypeParameters)
        {
            if (typeParameter.HasUnmanagedTypeConstraint)
            {
                continue;
            }

            var parameterSyntax = methodSyntax.TypeParameterList.Parameters
                .FirstOrDefault(p => p.Identifier.ValueText == typeParameter.Name);

            var location = parameterSyntax?.GetLocation() ?? methodSyntax.Identifier.GetLocation();

            var diagnostic = Diagnostic.Create(
                KernelDiagnostics.KernelGenericParameterMustBeUnmanaged,
                location,
                methodSymbol.Name,
                typeParameter.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    // ---------- DC019: Potential buffer aliasing in kernel call ----------

    private static void AnalyzeInvocationForBufferAliasing(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (!IsKernelLaunchInvocation(invocation, out var firstArgIndex))
        {
            return;
        }

        // Suppress if the enclosing method or type carries [AliasAllowed].
        if (HasAliasAllowedAttribute(context, invocation))
        {
            return;
        }

        // Track distinct buffer-shaped arguments by their text; whenever the same
        // syntactic expression appears twice for a kernel-call invocation, report.
        var seen = new Dictionary<string, ArgumentSyntax>(System.StringComparer.Ordinal);

        var arguments = invocation.ArgumentList.Arguments;
        for (var i = firstArgIndex; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            var expression = argument.Expression;

            if (!LooksLikeBufferArgument(expression))
            {
                continue;
            }

            var normalized = expression.ToString();

            if (seen.TryGetValue(normalized, out var firstOccurrence))
            {
                var diagnostic = Diagnostic.Create(
                    KernelDiagnostics.KernelPotentialBufferAliasing,
                    argument.GetLocation(),
                    additionalLocations: new[] { firstOccurrence.GetLocation() },
                    normalized);

                context.ReportDiagnostic(diagnostic);
            }
            else
            {
                seen[normalized] = argument;
            }
        }
    }

    /// <summary>
    /// Determines whether an invocation expression represents a kernel launch / execute call
    /// (IComputeOrchestrator.ExecuteAsync or ICompiledKernel.LaunchAsync family).
    /// </summary>
    /// <param name="invocation">The invocation expression.</param>
    /// <param name="firstArgIndex">
    /// Out: the index of the first argument that represents a kernel parameter
    /// (ExecuteAsync's kernel name / preferred backend / accelerator are skipped).
    /// </param>
    private static bool IsKernelLaunchInvocation(InvocationExpressionSyntax invocation, out int firstArgIndex)
    {
        firstArgIndex = 0;

        var methodName = GetInvokedMethodName(invocation);
        if (methodName is null)
        {
            return false;
        }

        switch (methodName)
        {
            case "ExecuteAsync":
                // orchestrator.ExecuteAsync("name", args...)
                // orchestrator.ExecuteAsync("name", preferredBackendOrAccelerator, args...)
                firstArgIndex = DetermineExecuteAsyncFirstArgIndex(invocation);
                return firstArgIndex >= 0;
            case "ExecuteWithBuffersAsync":
                // orchestrator.ExecuteWithBuffersAsync("name", buffers, scalarArgs...)
                // The buffers parameter itself is IEnumerable; skip first two slots.
                firstArgIndex = 2;
                return invocation.ArgumentList.Arguments.Count >= 2;
            case "LaunchAsync":
                // ICompiledKernel.LaunchAsync or IRingKernelRuntime.LaunchAsync.
                // We conservatively scan *all* arguments; buffer-shaped ones rise to the
                // top via LooksLikeBufferArgument().
                firstArgIndex = 0;
                return true;
            default:
                return false;
        }
    }

    private static int DetermineExecuteAsyncFirstArgIndex(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0)
        {
            return -1;
        }

        // First positional argument is always the kernel name.
        // The second positional argument *may* be an accelerator or preferred-backend
        // string; if it's a string literal or looks like an identifier, skip it too.
        if (args.Count >= 2 && IsLikelyBackendOrAcceleratorArgument(args[1].Expression))
        {
            return 2;
        }

        return 1;
    }

    private static bool IsLikelyBackendOrAcceleratorArgument(ExpressionSyntax expression)
    {
        // String literal for preferred-backend overload.
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return true;
        }

        // A bare identifier named something like "accelerator" or "backend" is
        // a strong heuristic; without semantic info we lean conservative.
        if (expression is IdentifierNameSyntax id)
        {
            var name = id.Identifier.ValueText;
            if (name.IndexOf("accelerator", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("backend", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax id => id.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null
        };
    }

    /// <summary>
    /// Heuristically determines whether an argument expression looks like a buffer reference
    /// (identifier, member access, or simple cast), as opposed to a scalar literal or constant.
    /// </summary>
    private static bool LooksLikeBufferArgument(ExpressionSyntax expression)
    {
        // Unwrap casts.
        while (expression is CastExpressionSyntax cast)
        {
            expression = cast.Expression;
        }

        switch (expression)
        {
            case IdentifierNameSyntax:
            case MemberAccessExpressionSyntax:
                return true;
            case LiteralExpressionSyntax:
                return false;
            default:
                return false;
        }
    }

    private static bool HasAliasAllowedAttribute(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        // Walk outward from the invocation looking for [AliasAllowed] on the
        // enclosing method, property, or type. We match by simple name so the
        // attribute doesn't need to exist in this PR to be effective once
        // introduced.
        for (SyntaxNode? node = invocation; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case MethodDeclarationSyntax method:
                    if (HasAttributeNamed(method.AttributeLists, "AliasAllowed"))
                    {
                        return true;
                    }
                    break;
                case PropertyDeclarationSyntax property:
                    if (HasAttributeNamed(property.AttributeLists, "AliasAllowed"))
                    {
                        return true;
                    }
                    break;
                case TypeDeclarationSyntax type:
                    if (HasAttributeNamed(type.AttributeLists, "AliasAllowed"))
                    {
                        return true;
                    }
                    break;
            }
        }

        // Also check the declared method symbol if available (covers local functions etc.).
        var enclosingMethod = invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (enclosingMethod is not null &&
            context.SemanticModel.GetDeclaredSymbol(enclosingMethod) is IMethodSymbol methodSymbol)
        {
            if (methodSymbol.GetAttributes().Any(a =>
                    a.AttributeClass?.Name is "AliasAllowedAttribute" or "AliasAllowed"))
            {
                return true;
            }

            if (methodSymbol.ContainingType?.GetAttributes().Any(a =>
                    a.AttributeClass?.Name is "AliasAllowedAttribute" or "AliasAllowed") == true)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttributeNamed(SyntaxList<AttributeListSyntax> attributeLists, string shortName)
    {
        foreach (var list in attributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name switch
                {
                    QualifiedNameSyntax q => q.Right.Identifier.ValueText,
                    IdentifierNameSyntax i => i.Identifier.ValueText,
                    _ => null
                };

                if (name is null)
                {
                    continue;
                }

                if (string.Equals(name, shortName, System.StringComparison.Ordinal) ||
                    string.Equals(name, shortName + "Attribute", System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
