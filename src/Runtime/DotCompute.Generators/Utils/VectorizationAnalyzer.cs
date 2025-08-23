// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Utils;

/// <summary>
/// Analyzes code for vectorization opportunities and SIMD optimization potential.
/// </summary>
public static class VectorizationAnalyzer
{
    /// <summary>
    /// Analyzes a method for vectorization opportunities.
    /// </summary>
    /// <param name="method">The method to analyze.</param>
    /// <returns>Information about vectorization opportunities.</returns>
    public static VectorizationInfo AnalyzeVectorization(MethodDeclarationSyntax method)
    {
        ArgumentNullException.ThrowIfNull(method);
        
        var info = new VectorizationInfo();
        var body = method.Body;

        if (body == null)
        {
            // Try to analyze expression-bodied method
            if (method.ExpressionBody != null)
            {
                return AnalyzeExpressionBody(method.ExpressionBody);
            }
            return info;
        }

        // Analyze loops
        var loops = body.DescendantNodes().OfType<ForStatementSyntax>().ToList();
        info.LoopCount = loops.Count;

        // Analyze array/span accesses
        var elementAccesses = body.DescendantNodes().OfType<ElementAccessExpressionSyntax>().ToList();
        info.HasArrayAccess = elementAccesses.Count > 0;

        // Analyze arithmetic operations
        var binaryOps = body.DescendantNodes().OfType<BinaryExpressionSyntax>()
            .Where(b => IsArithmeticOperation(b.Kind()))
            .ToList();
        info.ArithmeticOperationCount = binaryOps.Count;

        // Check for vectorizable patterns
        foreach (var loop in loops)
        {
            if (IsVectorizableLoop(loop))
            {
                info.VectorizableLoops++;
            }
        }

        // Analyze data dependencies
        AnalyzeDataDependencies(body, info);
        
        // Detect specific vectorization patterns
        DetectVectorizationPatterns(body, info);

        return info;
    }

    /// <summary>
    /// Determines if a loop is suitable for vectorization.
    /// </summary>
    /// <param name="loop">The loop to analyze.</param>
    /// <returns>True if the loop can be vectorized, false otherwise.</returns>
    public static bool IsVectorizableLoop(ForStatementSyntax loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        
        // Check for simple increment pattern
        if (!HasSimpleIncrement(loop))
        {
            return false;
        }

        // Check for loop-carried dependencies
        if (HasLoopCarriedDependency(loop))
        {
            return false;
        }

        // Check for vectorizable operations in loop body
        if (loop.Statement is BlockSyntax block)
        {
            return HasVectorizableOperations(block);
        }

        if (loop.Statement is ExpressionStatementSyntax expr)
        {
            return IsVectorizableExpression(expr.Expression);
        }

        return false;
    }

    /// <summary>
    /// Determines if an operation is arithmetic and suitable for vectorization.
    /// </summary>
    /// <param name="kind">The syntax kind to check.</param>
    /// <returns>True if the operation is arithmetic, false otherwise.</returns>
    public static bool IsArithmeticOperation(SyntaxKind kind)
    {
        return kind is SyntaxKind.AddExpression or
               SyntaxKind.SubtractExpression or
               SyntaxKind.MultiplyExpression or
               SyntaxKind.DivideExpression or
               SyntaxKind.ModuloExpression or
               SyntaxKind.LeftShiftExpression or
               SyntaxKind.RightShiftExpression or
               SyntaxKind.BitwiseAndExpression or
               SyntaxKind.BitwiseOrExpression or
               SyntaxKind.ExclusiveOrExpression;
    }

    /// <summary>
    /// Analyzes data dependencies in a code block.
    /// </summary>
    /// <param name="block">The block to analyze.</param>
    /// <param name="info">The vectorization info to update.</param>
    public static void AnalyzeDataDependencies(BlockSyntax block, VectorizationInfo info)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(info);
        
        var assignments = block.DescendantNodes().OfType<AssignmentExpressionSyntax>().ToList();
        var readVariables = new HashSet<string>();
        var writeVariables = new HashSet<string>();

        foreach (var assignment in assignments)
        {
            // Collect written variables
            if (assignment.Left is IdentifierNameSyntax writtenId)
            {
                _ = writeVariables.Add(writtenId.Identifier.Text);
            }

            // Collect read variables
            var rightSideIdentifiers = assignment.Right.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(id => id.Identifier.Text);
            
            foreach (var id in rightSideIdentifiers)
            {
                _ = readVariables.Add(id);
            }
        }

        // Check for read-after-write dependencies
        info.HasDataDependency = readVariables.Overlaps(writeVariables);
    }

    /// <summary>
    /// Detects specific vectorization patterns in the code.
    /// </summary>
    /// <param name="node">The syntax node to analyze.</param>
    /// <param name="info">The vectorization info to update.</param>
    public static void DetectVectorizationPatterns(SyntaxNode node, VectorizationInfo info)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(info);
        
        // Detect reduction patterns (sum, min, max, etc.)
        DetectReductionPattern(node, info);
        
        // Detect dot product pattern
        DetectDotProductPattern(node, info);
        
        // Detect matrix multiplication pattern
        DetectMatrixMultiplicationPattern(node, info);
        
        // Detect broadcast patterns
        DetectBroadcastPattern(node, info);
    }

    /// <summary>
    /// Checks if a loop has a simple increment pattern.
    /// </summary>
    private static bool HasSimpleIncrement(ForStatementSyntax loop)
    {
        if (loop.Incrementors.Count != 1)
        {
            return false;
        }

        var incrementor = loop.Incrementors[0];
        return incrementor is PostfixUnaryExpressionSyntax { OperatorToken.Text: "++" } or
               PrefixUnaryExpressionSyntax { OperatorToken.Text: "++" } or
               AssignmentExpressionSyntax { Kind: SyntaxKind.AddAssignmentExpression };
    }

    /// <summary>
    /// Checks for loop-carried dependencies.
    /// </summary>
    private static bool HasLoopCarriedDependency(ForStatementSyntax loop)
    {
        // Simple heuristic: check if loop body references previous iterations
        var body = loop.Statement;
        if (body == null) return false;

        var arrayAccesses = body.DescendantNodes().OfType<ElementAccessExpressionSyntax>();
        var loopVariable = GetLoopVariable(loop);
        
        if (string.IsNullOrEmpty(loopVariable))
        {
            return false;
        }

        foreach (var access in arrayAccesses)
        {
            var argument = access.ArgumentList.Arguments.FirstOrDefault();
            if (argument?.Expression is BinaryExpressionSyntax binary)
            {
                // Check for patterns like arr[i-1] or arr[i+1]
                if (ContainsOffsetFromLoopVariable(binary, loopVariable))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the loop variable name from a for statement.
    /// </summary>
    private static string? GetLoopVariable(ForStatementSyntax loop)
    {
        if (loop.Declaration?.Variables.Count > 0)
        {
            return loop.Declaration.Variables[0].Identifier.Text;
        }

        if (loop.Initializers.Count > 0 && 
            loop.Initializers[0] is AssignmentExpressionSyntax assignment &&
            assignment.Left is IdentifierNameSyntax id)
        {
            return id.Identifier.Text;
        }

        return null;
    }

    /// <summary>
    /// Checks if an expression contains an offset from the loop variable.
    /// </summary>
    private static bool ContainsOffsetFromLoopVariable(BinaryExpressionSyntax binary, string loopVariable)
    {
        var hasLoopVar = binary.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Any(id => id.Identifier.Text == loopVariable);
        
        var hasOffset = binary.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression;
        
        return hasLoopVar && hasOffset;
    }

    /// <summary>
    /// Checks if a block contains vectorizable operations.
    /// </summary>
    private static bool HasVectorizableOperations(BlockSyntax block)
    {
        var hasArrayAccess = block.DescendantNodes().OfType<ElementAccessExpressionSyntax>().Any();
        var hasArithmetic = block.DescendantNodes().OfType<BinaryExpressionSyntax>()
            .Any(b => IsArithmeticOperation(b.Kind()));
        
        return hasArrayAccess && hasArithmetic;
    }

    /// <summary>
    /// Checks if an expression is vectorizable.
    /// </summary>
    private static bool IsVectorizableExpression(ExpressionSyntax expression)
    {
        return expression switch
        {
            AssignmentExpressionSyntax assignment => IsVectorizableAssignment(assignment),
            BinaryExpressionSyntax binary => IsArithmeticOperation(binary.Kind()),
            _ => false
        };
    }

    /// <summary>
    /// Checks if an assignment is vectorizable.
    /// </summary>
    private static bool IsVectorizableAssignment(AssignmentExpressionSyntax assignment)
    {
        return assignment.Left is ElementAccessExpressionSyntax &&
               assignment.Right is BinaryExpressionSyntax binary &&
               IsArithmeticOperation(binary.Kind());
    }

    /// <summary>
    /// Analyzes an expression-bodied method.
    /// </summary>
    private static VectorizationInfo AnalyzeExpressionBody(ArrowExpressionClauseSyntax expressionBody)
    {
        var info = new VectorizationInfo();
        
        var binaryOps = expressionBody.Expression.DescendantNodesAndSelf()
            .OfType<BinaryExpressionSyntax>()
            .Where(b => IsArithmeticOperation(b.Kind()))
            .ToList();
        
        info.ArithmeticOperationCount = binaryOps.Count;
        info.IsArithmetic = binaryOps.Count > 0;
        
        return info;
    }

    /// <summary>
    /// Detects reduction patterns (sum, min, max).
    /// </summary>
    private static void DetectReductionPattern(SyntaxNode node, VectorizationInfo info)
    {
        var assignments = node.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => a.Kind() is SyntaxKind.AddAssignmentExpression or 
                       SyntaxKind.SubtractAssignmentExpression or
                       SyntaxKind.MultiplyAssignmentExpression);
        
        info.HasReductionPattern = assignments.Any();
    }

    /// <summary>
    /// Detects dot product patterns.
    /// </summary>
    private static void DetectDotProductPattern(SyntaxNode node, VectorizationInfo info)
    {
        // Look for pattern: sum += a[i] * b[i]
        var addAssignments = node.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => a.Kind() == SyntaxKind.AddAssignmentExpression);
        
        foreach (var assignment in addAssignments)
        {
            if (assignment.Right is BinaryExpressionSyntax { Kind: SyntaxKind.MultiplyExpression } multiply)
            {
                var leftAccess = multiply.Left is ElementAccessExpressionSyntax;
                var rightAccess = multiply.Right is ElementAccessExpressionSyntax;
                
                if (leftAccess && rightAccess)
                {
                    info.HasDotProductPattern = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Detects matrix multiplication patterns.
    /// </summary>
    private static void DetectMatrixMultiplicationPattern(SyntaxNode node, VectorizationInfo info)
    {
        // Look for nested loops with accumulation
        var forLoops = node.DescendantNodes().OfType<ForStatementSyntax>().ToList();
        
        foreach (var outerLoop in forLoops)
        {
            var innerLoops = outerLoop.Statement?.DescendantNodes().OfType<ForStatementSyntax>().ToList();
            if (innerLoops?.Count > 0)
            {
                // Check for accumulation pattern in innermost loop
                var innermostLoop = innerLoops.Last();
                if (innermostLoop.Statement != null)
                {
                    var hasAccumulation = innermostLoop.Statement.DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>()
                        .Any(a => a.Kind() == SyntaxKind.AddAssignmentExpression);
                    
                    if (hasAccumulation)
                    {
                        info.HasMatrixPattern = true;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Detects broadcast patterns.
    /// </summary>
    private static void DetectBroadcastPattern(SyntaxNode node, VectorizationInfo info)
    {
        // Look for patterns where a scalar is applied to multiple array elements
        var assignments = node.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        
        foreach (var assignment in assignments)
        {
            if (assignment.Left is ElementAccessExpressionSyntax &&
                assignment.Right is BinaryExpressionSyntax binary)
            {
                var hasScalar = binary.Left is IdentifierNameSyntax || 
                               binary.Right is IdentifierNameSyntax ||
                               binary.Left is LiteralExpressionSyntax ||
                               binary.Right is LiteralExpressionSyntax;
                
                var hasArray = binary.Left is ElementAccessExpressionSyntax ||
                              binary.Right is ElementAccessExpressionSyntax;
                
                if (hasScalar && hasArray)
                {
                    info.HasBroadcastPattern = true;
                    break;
                }
            }
        }
    }
}