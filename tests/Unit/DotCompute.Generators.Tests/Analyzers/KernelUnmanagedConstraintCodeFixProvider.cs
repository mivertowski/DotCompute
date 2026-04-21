// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Tests.Analyzers;

/// <summary>
/// Test-side implementation of the DC018 code fix ("add 'where T : unmanaged'"). This mirrors
/// the syntactic edit a full CodeFixProvider would perform in the IDE. The production analyzer
/// ships without a bundled CodeFixProvider (infrastructure tracked separately — see
/// SimpleAnalyzerTests.CodeFixProvider_ShouldSupportRequiredDiagnostics), so the test owns the
/// reference implementation to guarantee the message text the analyzer reports is actionable.
/// </summary>
internal static class KernelUnmanagedConstraintCodeFixProvider
{
    /// <summary>
    /// Applies the DC018 fix to <paramref name="source"/>: adds
    /// <c>where {typeParameterName} : unmanaged</c> to the signature of the method
    /// named <paramref name="methodName"/>.
    /// </summary>
    public static string ApplyFix(string source, string methodName, string typeParameterName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        var method = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == methodName)
            ?? throw new System.InvalidOperationException(
                $"No method named '{methodName}' found in source.");

        var newConstraintClause = SyntaxFactory.TypeParameterConstraintClause(
            SyntaxFactory.IdentifierName(typeParameterName),
            SyntaxFactory.SingletonSeparatedList<TypeParameterConstraintSyntax>(
                SyntaxFactory.TypeConstraint(
                    SyntaxFactory.IdentifierName("unmanaged"))));

        // Preserve existing clauses; append/replace the one for `typeParameterName`.
        var existingClauses = method.ConstraintClauses;
        var replacementClauses = existingClauses
            .Where(c => c.Name.Identifier.ValueText != typeParameterName)
            .Append(newConstraintClause);

        var newMethod = method
            .WithConstraintClauses(SyntaxFactory.List(replacementClauses))
            .NormalizeWhitespace(elasticTrivia: true);

        var newRoot = root.ReplaceNode(method, newMethod);
        return newRoot.ToFullString();
    }
}
