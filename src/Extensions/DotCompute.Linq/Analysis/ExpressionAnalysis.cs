// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Expression analysis result containing GPU compatibility information.
/// </summary>
/// <remarks>
/// This class encapsulates the results of analyzing a LINQ expression tree
/// for GPU compatibility, including support status, operation types,
/// and complexity estimates.
/// </remarks>
public sealed class ExpressionAnalysis
{
    /// <summary>
    /// Gets or sets whether the expression can execute on GPU.
    /// </summary>
    /// <value>
    /// <c>true</c> if the expression is compatible with GPU execution; otherwise, <c>false</c>.
    /// </value>
    public bool CanExecuteOnGPU { get; set; }

    /// <summary>
    /// Gets or sets the reason why the expression cannot execute on GPU.
    /// </summary>
    /// <value>
    /// A human-readable explanation of incompatibility, or <c>null</c> if compatible.
    /// </value>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the type of operation represented by the expression.
    /// </summary>
    /// <value>
    /// The operation type (e.g., "Map", "Filter", "Reduce"), or <c>null</c> if undetermined.
    /// </value>
    /// <remarks>
    /// This categorization helps the GPU compiler choose the most appropriate
    /// kernel generation strategy.
    /// </remarks>
    public string? OperationType { get; set; }

    /// <summary>
    /// Gets or sets the input types used in the expression.
    /// </summary>
    /// <value>
    /// An array of types representing the inputs to the expression, or <c>null</c> if none detected.
    /// </value>
    public Type[]? InputTypes { get; set; }

    /// <summary>
    /// Gets or sets the output type of the expression.
    /// </summary>
    /// <value>
    /// The type that the expression will produce, or <c>null</c> if undetermined.
    /// </value>
    public Type? OutputType { get; set; }

    /// <summary>
    /// Gets or sets the estimated complexity of the expression.
    /// </summary>
    /// <value>
    /// A numeric estimate of computational complexity, with higher values indicating more complex operations.
    /// </value>
    /// <remarks>
    /// This metric helps determine whether GPU execution would be beneficial
    /// compared to CPU execution for the given workload.
    /// </remarks>
    public int EstimatedComplexity { get; set; }

    /// <summary>
    /// Gets or sets the length of the operation chain.
    /// </summary>
    /// <value>
    /// The number of chained LINQ operations in the expression.
    /// </value>
    public int ChainLength { get; set; }

    /// <summary>
    /// Analyzes an expression tree for complexity and optimization opportunities.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <returns>Analysis results including complexity metrics.</returns>
    public static ExpressionComplexityAnalysis AnalyzeComplexity(System.Linq.Expressions.Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var analyzer = new ExpressionComplexityAnalyzer();
        return analyzer.Analyze(expression);
    }

    /// <summary>
    /// Analyzes an expression tree for GPU compatibility.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <returns>Analysis results including compatibility information.</returns>
    public static ExpressionAnalysis AnalyzeGpuCompatibility(System.Linq.Expressions.Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var visitor = new GPUCompatibilityVisitor();
        var isCompatible = visitor.IsGpuCompatible(expression);

        return new ExpressionAnalysis
        {
            CanExecuteOnGPU = isCompatible,
            Reason = isCompatible ? null : "Contains GPU-incompatible operations",
            OperationType = DetermineOperationType(expression),
            InputTypes = ExtractInputTypes(expression),
            OutputType = expression.Type,
            EstimatedComplexity = EstimateComplexity(expression),
            ChainLength = CountChainLength(expression)
        };
    }

    private static string? DetermineOperationType(System.Linq.Expressions.Expression expression)
    {
        if (expression is System.Linq.Expressions.MethodCallExpression methodCall)
        {
            return methodCall.Method.Name switch
            {
                "Select" => "Map",
                "Where" => "Filter",
                "Sum" or "Average" or "Min" or "Max" => "Reduce",
                "OrderBy" or "OrderByDescending" => "Sort",
                _ => "Unknown"
            };
        }

        return null;
    }

    private static Type[]? ExtractInputTypes(System.Linq.Expressions.Expression expression)
    {
        var types = new List<Type>();


        if (expression is System.Linq.Expressions.MethodCallExpression methodCall)
        {
            foreach (var argument in methodCall.Arguments)
            {
                types.Add(argument.Type);
            }
        }

        return types.Count > 0 ? types.ToArray() : null;
    }

    private static int EstimateComplexity(System.Linq.Expressions.Expression expression)
    {
        // Simple heuristic: count the number of nested operations
        var visitor = new ComplexityCountingVisitor();
        visitor.Visit(expression);
        return visitor.Complexity;
    }

    private static int CountChainLength(System.Linq.Expressions.Expression expression)
    {
        var count = 0;
        var current = expression;

        while (current is System.Linq.Expressions.MethodCallExpression methodCall &&
               (methodCall.Method.DeclaringType == typeof(System.Linq.Queryable) ||
                methodCall.Method.DeclaringType == typeof(System.Linq.Enumerable)))
        {
            count++;
            current = methodCall.Arguments.FirstOrDefault();
        }

        return count;
    }
}

/// <summary>
/// Analysis results for expression complexity.
/// </summary>
public class ExpressionComplexityAnalysis
{
    /// <summary>
    /// Gets or sets the number of chained operations.
    /// </summary>
    public int ChainLength { get; set; }

    /// <summary>
    /// Gets or sets the estimated computational complexity.
    /// </summary>
    public int ComputationalComplexity { get; set; }

    /// <summary>
    /// Gets or sets whether the expression has branching logic.
    /// </summary>
    public bool HasBranching { get; set; }

    /// <summary>
    /// Gets or sets whether the expression has aggregations.
    /// </summary>
    public bool HasAggregations { get; set; }
}

/// <summary>
/// Analyzes expression trees for complexity metrics.
/// </summary>
internal class ExpressionComplexityAnalyzer
{
    /// <summary>
    /// Analyzes an expression for complexity.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <returns>Complexity analysis results.</returns>
    public ExpressionComplexityAnalysis Analyze(System.Linq.Expressions.Expression expression)
    {
        var visitor = new ComplexityAnalysisVisitor();
        visitor.Visit(expression);

        return new ExpressionComplexityAnalysis
        {
            ChainLength = visitor.ChainLength,
            ComputationalComplexity = visitor.ComputationalComplexity,
            HasBranching = visitor.HasBranching,
            HasAggregations = visitor.HasAggregations
        };
    }
}

/// <summary>
/// Expression visitor that analyzes complexity metrics.
/// </summary>
internal class ComplexityAnalysisVisitor : System.Linq.Expressions.ExpressionVisitor
{
    public int ChainLength { get; private set; }
    public int ComputationalComplexity { get; private set; }
    public bool HasBranching { get; private set; }
    public bool HasAggregations { get; private set; }

    protected override System.Linq.Expressions.Expression VisitMethodCall(System.Linq.Expressions.MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(System.Linq.Queryable) ||
            node.Method.DeclaringType == typeof(System.Linq.Enumerable))
        {
            ChainLength++;

            // Check for different operation types

            switch (node.Method.Name)
            {
                case "Sum":
                case "Average":
                case "Min":
                case "Max":
                case "Count":
                    HasAggregations = true;
                    ComputationalComplexity += 2;
                    break;
                case "OrderBy":
                case "OrderByDescending":
                    ComputationalComplexity += 5; // Sorting is expensive
                    break;
                case "GroupBy":
                    ComputationalComplexity += 4;
                    HasBranching = true;
                    break;
                case "Where":
                case "Select":
                    ComputationalComplexity += 1;
                    break;
                default:
                    ComputationalComplexity += 1;
                    break;
            }
        }

        return base.VisitMethodCall(node);
    }

    protected override System.Linq.Expressions.Expression VisitConditional(System.Linq.Expressions.ConditionalExpression node)
    {
        HasBranching = true;
        ComputationalComplexity += 1;
        return base.VisitConditional(node);
    }
}

/// <summary>
/// Simple visitor that counts complexity.
/// </summary>
internal class ComplexityCountingVisitor : System.Linq.Expressions.ExpressionVisitor
{
    public int Complexity { get; private set; }

    protected override System.Linq.Expressions.Expression VisitMethodCall(System.Linq.Expressions.MethodCallExpression node)
    {
        Complexity++;
        return base.VisitMethodCall(node);
    }

    protected override System.Linq.Expressions.Expression VisitBinary(System.Linq.Expressions.BinaryExpression node)
    {
        Complexity++;
        return base.VisitBinary(node);
    }

    protected override System.Linq.Expressions.Expression VisitUnary(System.Linq.Expressions.UnaryExpression node)
    {
        Complexity++;
        return base.VisitUnary(node);
    }
}