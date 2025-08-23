// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Utils;

/// <summary>
/// Extracts and processes method bodies from syntax trees.
/// </summary>
public static class MethodBodyExtractor
{
    /// <summary>
    /// Options for method body extraction.
    /// </summary>
    public class ExtractionOptions
    {
        public bool PreserveFormatting { get; set; } = true;
        public bool IncludeComments { get; set; } = false;
        public bool ExpandExpressionBodies { get; set; } = true;
        public bool NormalizeWhitespace { get; set; } = false;
        public int IndentSize { get; set; } = 4;
    }

    /// <summary>
    /// Extracts the body of a method as a string.
    /// </summary>
    /// <param name="method">The method declaration to extract from.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>The extracted method body, or null if no body exists.</returns>
    public static string? ExtractMethodBody(MethodDeclarationSyntax method, ExtractionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        options ??= new ExtractionOptions();
        
        // Try block body first
        if (method.Body != null)
        {
            return ExtractBlockBody(method.Body, options);
        }
        
        // Try expression body
        if (method.ExpressionBody != null)
        {
            return ExtractExpressionBody(method.ExpressionBody, options);
        }
        
        return null;
    }

    /// <summary>
    /// Extracts the body from an expression-bodied member.
    /// </summary>
    /// <param name="expressionBody">The arrow expression clause.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>The extracted expression body.</returns>
    public static string ExtractExpressionBody(ArrowExpressionClauseSyntax expressionBody, ExtractionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expressionBody);
        
        options ??= new ExtractionOptions();
        
        var expression = expressionBody.Expression;
        
        if (options.NormalizeWhitespace)
        {
            expression = expression.NormalizeWhitespace();
        }
        
        if (options.ExpandExpressionBodies)
        {
            // Convert to statement form
            return $"return {expression};";
        }
        
        return expression.ToString();
    }

    /// <summary>
    /// Extracts the body from a block syntax.
    /// </summary>
    /// <param name="block">The block syntax to extract from.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>The extracted block body.</returns>
    public static string ExtractBlockBody(BlockSyntax block, ExtractionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        
        options ??= new ExtractionOptions();
        
        if (options.NormalizeWhitespace)
        {
            block = block.NormalizeWhitespace();
        }
        
        return ProcessStatements(block.Statements, options);
    }

    /// <summary>
    /// Processes a collection of statements into a string.
    /// </summary>
    /// <param name="statements">The statements to process.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>Processed statements as a string.</returns>
    public static string ProcessStatements(IEnumerable<StatementSyntax> statements, ExtractionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(statements);
        
        options ??= new ExtractionOptions();
        
        var sb = new StringBuilder();
        var statementList = statements.ToList();
        
        for (var i = 0; i < statementList.Count; i++)
        {
            var statement = statementList[i];
            
            if (!options.IncludeComments)
            {
                statement = RemoveComments(statement);
            }
            
            var statementStr = statement.ToString();
            
            if (options.PreserveFormatting)
            {
                _ = sb.AppendLine(statementStr);
            }
            else
            {
                _ = sb.Append(statementStr);
                if (i < statementList.Count - 1)
                {
                    _ = sb.Append(' ');
                }
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Extracts variable declarations from a method body.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>List of variable declarations.</returns>
    public static IEnumerable<VariableDeclarationInfo> ExtractVariableDeclarations(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        var declarations = new List<VariableDeclarationInfo>();
        
        var body = method.Body;
        if (body == null)
        {
            return declarations;
        }
        
        var localDeclarations = body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>();
        
        foreach (var localDecl in localDeclarations)
        {
            var declaration = localDecl.Declaration;
            var type = declaration.Type.ToString();
            
            foreach (var variable in declaration.Variables)
            {
                declarations.Add(new VariableDeclarationInfo
                {
                    Name = variable.Identifier.Text,
                    Type = type,
                    HasInitializer = variable.Initializer != null,
                    InitializerExpression = variable.Initializer?.Value.ToString(),
                    IsConst = localDecl.IsConst
                });
            }
        }
        
        return declarations;
    }

    /// <summary>
    /// Extracts return statements from a method body.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>List of return statements.</returns>
    public static IEnumerable<ReturnStatementInfo> ExtractReturnStatements(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        var returns = new List<ReturnStatementInfo>();
        
        // Handle expression body
        if (method.ExpressionBody != null)
        {
            returns.Add(new ReturnStatementInfo
            {
                Expression = method.ExpressionBody.Expression.ToString(),
                IsExpressionBodied = true,
                LineNumber = method.ExpressionBody.GetLocation().GetLineSpan().StartLinePosition.Line
            });
            return returns;
        }
        
        // Handle block body
        var body = method.Body;
        if (body == null)
        {
            return returns;
        }
        
        var returnStatements = body.DescendantNodes().OfType<ReturnStatementSyntax>();
        
        foreach (var returnStmt in returnStatements)
        {
            returns.Add(new ReturnStatementInfo
            {
                Expression = returnStmt.Expression?.ToString() ?? string.Empty,
                IsExpressionBodied = false,
                LineNumber = returnStmt.GetLocation().GetLineSpan().StartLinePosition.Line
            });
        }
        
        return returns;
    }

    /// <summary>
    /// Extracts method calls from a method body.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>List of method invocations.</returns>
    public static IEnumerable<MethodInvocationInfo> ExtractMethodCalls(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        var invocations = new List<MethodInvocationInfo>();
        
        SyntaxNode? searchRoot = method.Body ?? (SyntaxNode?)method.ExpressionBody;
        if (searchRoot == null)
        {
            return invocations;
        }
        
        var methodCalls = searchRoot.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var call in methodCalls)
        {
            var methodName = ExtractMethodName(call.Expression);
            var arguments = call.ArgumentList.Arguments.Select(a => a.ToString()).ToList();
            
            invocations.Add(new MethodInvocationInfo
            {
                MethodName = methodName,
                Arguments = arguments,
                FullExpression = call.ToString()
            });
        }
        
        return invocations;
    }

    /// <summary>
    /// Transforms a method body by applying a custom transformation function.
    /// </summary>
    /// <param name="method">The method to transform.</param>
    /// <param name="transformer">The transformation function.</param>
    /// <returns>Transformed method body.</returns>
    public static string? TransformMethodBody(MethodDeclarationSyntax method, Func<string, string> transformer)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(transformer);
        
        var body = ExtractMethodBody(method);
        return body != null ? transformer(body) : null;
    }

    /// <summary>
    /// Removes comments from a statement.
    /// </summary>
    private static StatementSyntax RemoveComments(StatementSyntax statement)
    {
        var withoutTrivia = statement.WithoutTrivia();
        return withoutTrivia;
    }

    /// <summary>
    /// Extracts the method name from an expression.
    /// </summary>
    private static string ExtractMethodName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => expression.ToString()
        };
    }

    /// <summary>
    /// Analyzes a method body for specific patterns.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>Analysis results.</returns>
    public static MethodBodyAnalysis AnalyzeMethodBody(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        var analysis = new MethodBodyAnalysis
        {
            MethodName = method.Identifier.Text,
            HasBody = method.Body != null || method.ExpressionBody != null,
            IsExpressionBodied = method.ExpressionBody != null
        };
        
        SyntaxNode? searchRoot = method.Body ?? (SyntaxNode?)method.ExpressionBody;
        if (searchRoot == null)
        {
            return analysis;
        }
        
        // Count various statement types
        analysis.StatementCount = searchRoot.DescendantNodes().OfType<StatementSyntax>().Count();
        analysis.LoopCount = searchRoot.DescendantNodes().OfType<ForStatementSyntax>().Count() +
                            searchRoot.DescendantNodes().OfType<WhileStatementSyntax>().Count() +
                            searchRoot.DescendantNodes().OfType<DoStatementSyntax>().Count() +
                            searchRoot.DescendantNodes().OfType<ForEachStatementSyntax>().Count();
        analysis.ConditionalCount = searchRoot.DescendantNodes().OfType<IfStatementSyntax>().Count() +
                                   searchRoot.DescendantNodes().OfType<SwitchStatementSyntax>().Count();
        analysis.TryCatchCount = searchRoot.DescendantNodes().OfType<TryStatementSyntax>().Count();
        
        // Check for async/await
        analysis.HasAsyncOperations = searchRoot.DescendantNodes().OfType<AwaitExpressionSyntax>().Any();
        
        // Check for LINQ
        analysis.HasLinqOperations = searchRoot.DescendantNodes().OfType<QueryExpressionSyntax>().Any() ||
                                    searchRoot.DescendantNodes().OfType<InvocationExpressionSyntax>()
                                        .Any(i => IsLinqMethod(i));
        
        return analysis;
    }

    /// <summary>
    /// Checks if an invocation is a LINQ method.
    /// </summary>
    private static bool IsLinqMethod(InvocationExpressionSyntax invocation)
    {
        var methodName = ExtractMethodName(invocation.Expression);
        var linqMethods = new HashSet<string>
        {
            "Select", "Where", "OrderBy", "OrderByDescending", "GroupBy",
            "Join", "SelectMany", "Take", "Skip", "First", "FirstOrDefault",
            "Last", "LastOrDefault", "Single", "SingleOrDefault", "Any", "All",
            "Count", "Sum", "Average", "Min", "Max", "Aggregate"
        };
        
        return linqMethods.Contains(methodName);
    }

    /// <summary>
    /// Information about a variable declaration.
    /// </summary>
    public class VariableDeclarationInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool HasInitializer { get; set; }
        public string? InitializerExpression { get; set; }
        public bool IsConst { get; set; }
    }

    /// <summary>
    /// Information about a return statement.
    /// </summary>
    public class ReturnStatementInfo
    {
        public string Expression { get; set; } = string.Empty;
        public bool IsExpressionBodied { get; set; }
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// Information about a method invocation.
    /// </summary>
    public class MethodInvocationInfo
    {
        public string MethodName { get; set; } = string.Empty;
        public List<string> Arguments { get; set; } = new();
        public string FullExpression { get; set; } = string.Empty;
    }

    /// <summary>
    /// Results of method body analysis.
    /// </summary>
    public class MethodBodyAnalysis
    {
        public string MethodName { get; set; } = string.Empty;
        public bool HasBody { get; set; }
        public bool IsExpressionBodied { get; set; }
        public int StatementCount { get; set; }
        public int LoopCount { get; set; }
        public int ConditionalCount { get; set; }
        public int TryCatchCount { get; set; }
        public bool HasAsyncOperations { get; set; }
        public bool HasLinqOperations { get; set; }
    }
}