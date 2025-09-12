// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotCompute.Generators.CodeFixes;

/// <summary>
/// Provides automated code fixes for DotCompute kernel diagnostics.
/// Integrates with Visual Studio and other IDEs to offer quick fixes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(KernelCodeFixProvider)), Shared]
public class KernelCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            "DC001", // Kernel methods must be static
            "DC002", // Kernel method has invalid parameters
            "DC007", // Method should have [Kernel] attribute
            "DC010", // Kernel uses incorrect threading model
            "DC011"  // Kernel missing bounds check
        );

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return;
        }


        foreach (var diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case "DC001":
                    await RegisterMakeStaticFixAsync(context, root, diagnostic);
                    break;


                case "DC002":
                    await RegisterParameterTypeFixAsync(context, root, diagnostic);
                    break;


                case "DC007":
                    await RegisterAddKernelAttributeFixAsync(context, root, diagnostic);
                    break;


                case "DC010":
                    await RegisterFixThreadingModelAsync(context, root, diagnostic);
                    break;


                case "DC011":
                    await RegisterAddBoundsCheckFixAsync(context, root, diagnostic);
                    break;
            }
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async Task RegisterMakeStaticFixAsync(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
#pragma warning restore CS1998
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDecl)
        {
            return;
        }


        if (methodDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            return;
        }


        var action = CodeAction.Create(
            title: "Make method static",
            createChangedDocument: c => MakeMethodStaticAsync(context.Document, methodDecl, c),
            equivalenceKey: "MakeMethodStatic");

        context.RegisterCodeFix(action, diagnostic);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async Task RegisterParameterTypeFixAsync(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
#pragma warning restore CS1998
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node.FirstAncestorOrSelf<ParameterSyntax>() is not ParameterSyntax parameter)
        {
            return;
        }

        // Suggest converting array parameters to Span<T>

        if (parameter.Type?.ToString().Contains("[]") == true)
        {
            var action = CodeAction.Create(
                title: "Convert to Span<T>",
                createChangedDocument: c => ConvertParameterToSpanAsync(context.Document, parameter, c),
                equivalenceKey: "ConvertToSpan");

            context.RegisterCodeFix(action, diagnostic);
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async Task RegisterAddKernelAttributeFixAsync(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
#pragma warning restore CS1998
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDecl)
        {
            return;
        }


        var action = CodeAction.Create(
            title: "Add [Kernel] attribute",
            createChangedDocument: c => AddKernelAttributeAsync(context.Document, methodDecl, c),
            equivalenceKey: "AddKernelAttribute");

        context.RegisterCodeFix(action, diagnostic);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async Task RegisterFixThreadingModelAsync(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
#pragma warning restore CS1998
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDecl)
        {
            return;
        }


        var action = CodeAction.Create(
            title: "Use Kernel.ThreadId for indexing",
            createChangedDocument: c => AddKernelThreadingAsync(context.Document, methodDecl, c),
            equivalenceKey: "AddKernelThreading");

        context.RegisterCodeFix(action, diagnostic);
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async Task RegisterAddBoundsCheckFixAsync(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
#pragma warning restore CS1998
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDecl)
        {
            return;
        }


        var action = CodeAction.Create(
            title: "Add bounds check",
            createChangedDocument: c => AddBoundsCheckAsync(context.Document, methodDecl, c),
            equivalenceKey: "AddBoundsCheck");

        context.RegisterCodeFix(action, diagnostic);
    }

    private static async Task<Document> MakeMethodStaticAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }


        var newModifiers = methodDecl.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        var newMethodDecl = methodDecl.WithModifiers(newModifiers);

        var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> ConvertParameterToSpanAsync(Document document, ParameterSyntax parameter, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null || parameter.Type == null)
        {
            return document;
        }


        var typeString = parameter.Type.ToString();
        if (!typeString.Contains("[]"))
        {
            return document;
        }

        // Extract element type (e.g., "float[]" -> "float")

        var elementType = typeString.Replace("[]", "");

        // Create new Span<T> type

        var newTypeName = parameter.Modifiers.Any(SyntaxKind.InKeyword) || parameter.Modifiers.Any(SyntaxKind.RefKeyword)

            ? $"ReadOnlySpan<{elementType}>"

            : $"Span<{elementType}>";

        var newType = SyntaxFactory.ParseTypeName(newTypeName).WithTriviaFrom(parameter.Type);
        var newParameter = parameter.WithType(newType);

        var newRoot = root.ReplaceNode(parameter, newParameter);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddKernelAttributeAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
        {
            return document;
        }

        // Create [Kernel] attribute

        var kernelAttribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Kernel"));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(kernelAttribute));

        var newMethodDecl = methodDecl.WithAttributeLists(methodDecl.AttributeLists.Add(attributeList));
        var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddKernelThreadingAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null || methodDecl.Body == null)
        {
            return document;
        }

        // Find for loops and add threading model

        var forLoops = methodDecl.Body.DescendantNodes().OfType<ForStatementSyntax>().ToList();
        if (forLoops.Count == 0)
        {
            return document;
        }


        var newMethodDecl = methodDecl;

        // Add index variable declaration at the start of the method

        var indexDeclaration = SyntaxFactory.LocalDeclarationStatement(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.IdentifierName("int"))
            .WithVariables(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier("index"))
                .WithInitializer(SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("Kernel"),
                            SyntaxFactory.IdentifierName("ThreadId")),
                        SyntaxFactory.IdentifierName("X")))))));

        // Add the declaration to the beginning of the method
        var statements = methodDecl.Body.Statements.Insert(0, indexDeclaration);
        var newBody = methodDecl.Body.WithStatements(statements);
        newMethodDecl = newMethodDecl.WithBody(newBody);

        var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddBoundsCheckAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null || methodDecl.Body == null)
        {
            return document;
        }

        // Find array accesses and wrap in bounds check

        var elementAccesses = methodDecl.Body.DescendantNodes().OfType<ElementAccessExpressionSyntax>().ToList();
        if (elementAccesses.Count == 0)
        {
            return document;
        }


        var newMethodDecl = methodDecl;

        // Analyze method to find array/span parameters and create appropriate bounds check
        var parameters = methodDecl.ParameterList.Parameters
            .Where(p => p.Type?.ToString().Contains("Span") == true)
            .ToList();


        if (parameters.Count == 0)
        {
            return document;
        }

        var firstSpanParameter = parameters.First().Identifier.ValueText;

        // Add bounds check at the start of the method

        var boundsCheck = SyntaxFactory.IfStatement(
            SyntaxFactory.BinaryExpression(
                SyntaxKind.GreaterThanOrEqualExpression,
                SyntaxFactory.IdentifierName("index"),
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName(firstSpanParameter),
                    SyntaxFactory.IdentifierName("Length"))),
            SyntaxFactory.ReturnStatement());

        var statements = methodDecl.Body.Statements.Insert(
            methodDecl.Body.Statements.Count > 0 && methodDecl.Body.Statements[0].ToString().Contains("index") ? 1 : 0,

            boundsCheck);

        var newBody = methodDecl.Body.WithStatements(statements);
        newMethodDecl = newMethodDecl.WithBody(newBody);

        var newRoot = root.ReplaceNode(methodDecl, newMethodDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}