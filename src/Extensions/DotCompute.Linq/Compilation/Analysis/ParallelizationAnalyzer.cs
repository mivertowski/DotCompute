// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
namespace DotCompute.Linq.Compilation.Analysis;
/// <summary>
/// Analyzes expressions for parallelization opportunities and strategies.
/// </summary>
public class ParallelizationAnalyzer
{
    /// <summary>
    /// Analyzes an expression for parallelization potential.
    /// </summary>
    /// <param name="expression">The expression to analyze.</param>
    /// <returns>Parallelization analysis result.</returns>
    public ParallelizationResult AnalyzeParallelization(Expression expression)
    {
        var result = new ParallelizationResult();
        AnalyzeExpression(expression, result);
        // Determine overall parallelization strategy
        result.Strategy = DetermineOptimalStrategy(result);
        result.EstimatedSpeedup = EstimateSpeedup(result);
        result.ThreadSafety = DetermineThreadSafety(result);
        return result;
    }
    /// Analyzes an operator for parallelization opportunities.
    /// <param name="operatorExpression">The operator to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parallelization opportunity analysis.</returns>
    public async Task<DotCompute.Linq.Compilation.Analysis.ParallelizationOpportunity> AnalyzeOperatorAsync(
        Expression operatorExpression,
        CancellationToken cancellationToken = default)
        // Analyze the operator expression asynchronously
        await Task.Yield(); // Make it properly async
        var result = AnalyzeParallelization(operatorExpression);
        // Convert ParallelizationResult to ParallelizationOpportunity
        return new DotCompute.Linq.Compilation.Analysis.ParallelizationOpportunity
        {
            VectorizationSuitable = result.Strategy == ParallelizationStrategy.VectorizedParallel,
            SupportsParallelExecution = result.Strategy != ParallelizationStrategy.Sequential,
            RecommendedParallelism = result.RecommendedParallelism,
            EstimatedSpeedup = result.EstimatedSpeedup,
            ParallelizationPotential = result.EstimatedSpeedup > 1.0 ? 1.0 : result.EstimatedSpeedup
        };
    /// Recursively analyzes expression for parallelization opportunities.
    private static void AnalyzeExpression(Expression expression, ParallelizationResult result)
        switch (expression)
            case MethodCallExpression methodCall:
                AnalyzeMethodCall(methodCall, result);
                break;
            case BinaryExpression binary:
                AnalyzeBinaryExpression(binary, result);
            case UnaryExpression unary:
                AnalyzeExpression(unary.Operand, result);
            case ConditionalExpression conditional:
                AnalyzeConditionalExpression(conditional, result);
            case LambdaExpression lambda:
                AnalyzeExpression(lambda.Body, result);
            case ParameterExpression:
            case ConstantExpression:
                // These are typically parallelizable
                result.IsParallelizable = true;
        }
    /// Analyzes a method call for parallelization potential.
    private static void AnalyzeMethodCall(MethodCallExpression methodCall, ParallelizationResult result)
        var methodName = methodCall.Method.Name;
        var declaringType = methodCall.Method.DeclaringType;
        // LINQ methods analysis
        if (declaringType == typeof(Enumerable) || declaringType == typeof(Queryable))
            switch (methodName)
            {
                case "Select":
                case "Where":
                    result.IsParallelizable = true;
                    result.ParallelizationType = ParallelizationType.ElementWise;
                    result.RecommendedParallelism = Environment.ProcessorCount;
                    result.DataDependencies.Add("Independent element processing");
                    break;
                case "Sum":
                case "Count":
                case "Average":
                case "Min":
                case "Max":
                    result.ParallelizationType = ParallelizationType.Reduction;
                    result.DataDependencies.Add("Reduction operation");
                case "Aggregate":
                    result.IsParallelizable = IsAssociativeAggregate(methodCall);
                    if (!result.IsParallelizable)
                    {
                        result.Constraints.Add("Aggregate function must be associative for parallelization");
                    }
                case "OrderBy":
                case "OrderByDescending":
                    result.ParallelizationType = ParallelizationType.Sort;
                    result.RecommendedParallelism = Math.Max(2, Environment.ProcessorCount / 2);
                    result.Constraints.Add("Parallel sort requires merge step");
                default:
                    result.IsParallelizable = false;
                    result.Constraints.Add($"Method {methodName} is not easily parallelizable");
            }
        // Mathematical operations
        else if (declaringType == typeof(Math) || declaringType == typeof(MathF))
            result.IsParallelizable = true;
            result.ParallelizationType = ParallelizationType.ElementWise;
            result.VectorizationSuitable = true;
        // Analyze arguments
        foreach (var arg in methodCall.Arguments)
            AnalyzeExpression(arg, result);
        if (methodCall.Object != null)
            AnalyzeExpression(methodCall.Object, result);
    /// Analyzes a binary expression for parallelization.
    private static void AnalyzeBinaryExpression(BinaryExpression binary, ParallelizationResult result)
        // Most binary operations are element-wise parallelizable
        if (IsMathematicalOperation(binary.NodeType))
            result.RecommendedParallelism = Environment.ProcessorCount * 4; // SIMD potential
        AnalyzeExpression(binary.Left, result);
        AnalyzeExpression(binary.Right, result);
    /// Analyzes a conditional expression for parallelization.
    private static void AnalyzeConditionalExpression(ConditionalExpression conditional, ParallelizationResult result)
        AnalyzeExpression(conditional.Test, result);
        AnalyzeExpression(conditional.IfTrue, result);
        AnalyzeExpression(conditional.IfFalse, result);
        // Conditionals can be parallelizable with predicated execution
        result.IsParallelizable = true;
        result.ParallelizationType = ParallelizationType.Conditional;
        result.Constraints.Add("Conditional execution may reduce parallel efficiency");
    /// Determines if an aggregate operation is associative.
    private static bool IsAssociativeAggregate(MethodCallExpression methodCall)
        // This is a simplified heuristic - in practice would need more sophisticated analysis
        return methodCall.Arguments.Count >= 2;
    /// Checks if a node type represents a mathematical operation.
    private static bool IsMathematicalOperation(ExpressionType nodeType)
        return nodeType switch
            ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or
            ExpressionType.Divide or ExpressionType.Modulo or ExpressionType.Power => true,
            _ => false
    /// Determines the optimal parallelization strategy.
    private static ParallelizationStrategy DetermineOptimalStrategy(ParallelizationResult result)
        if (!result.IsParallelizable)
            return ParallelizationStrategy.Sequential;
        return result.ParallelizationType switch
            ParallelizationType.ElementWise when result.VectorizationSuitable => ParallelizationStrategy.VectorizedParallel,
            ParallelizationType.ElementWise => ParallelizationStrategy.DataParallel,
            ParallelizationType.Reduction => ParallelizationStrategy.MapReduce,
            ParallelizationType.Sort => ParallelizationStrategy.ParallelSort,
            ParallelizationType.Conditional => ParallelizationStrategy.ConditionalParallel,
            _ => ParallelizationStrategy.DataParallel
    /// Estimates potential speedup from parallelization.
    private static double EstimateSpeedup(ParallelizationResult result)
            return 1.0;
        var baseSpeedup = Math.Min(result.RecommendedParallelism, Environment.ProcessorCount);
        // Apply Amdahl's law approximation
        var parallelPortion = result.ParallelizationType switch
            ParallelizationType.ElementWise => 0.9, // 90% parallelizable
            ParallelizationType.Reduction => 0.7,   // 70% parallelizable due to reduction overhead
            ParallelizationType.Sort => 0.6,        // 60% due to merge complexity
            ParallelizationType.Conditional => 0.5, // 50% due to branching overhead
            _ => 0.8
        var serialPortion = 1.0 - parallelPortion;
        var speedup = 1.0 / (serialPortion + (parallelPortion / baseSpeedup));
        // Apply vectorization bonus
        if (result.VectorizationSuitable)
            speedup *= 2.0; // Conservative SIMD speedup
        return Math.Round(speedup, 2);
    /// Determines thread safety characteristics.
    private static ThreadSafetyLevel DetermineThreadSafety(ParallelizationResult result)
        if (result.DataDependencies.Any(d => d.Contains("shared state")))
            return ThreadSafetyLevel.Unsafe;
        if (result.ParallelizationType == ParallelizationType.Reduction)
            return ThreadSafetyLevel.RequiresSynchronization;
        return ThreadSafetyLevel.ThreadSafe;
}
/// Represents the result of parallelization analysis.
public class ParallelizationResult
    /// Gets or sets whether the expression is parallelizable.
    public bool IsParallelizable { get; set; }
    /// Gets or sets whether vectorization is suitable.
    public bool VectorizationSuitable { get; set; }
    /// Gets or sets the type of parallelization.
    public ParallelizationType ParallelizationType { get; set; }
    /// Gets or sets the recommended degree of parallelism.
    public int RecommendedParallelism { get; set; } = 1;
    /// Gets or sets the optimal parallelization strategy.
    public ParallelizationStrategy Strategy { get; set; }
    /// Gets or sets the estimated speedup from parallelization.
    public double EstimatedSpeedup { get; set; } = 1.0;
    /// Gets or sets thread safety characteristics.
    public ThreadSafetyLevel ThreadSafety { get; set; }
    /// Gets or sets data dependencies that affect parallelization.
    public List<string> DataDependencies { get; set; } = [];
    /// Gets or sets constraints that limit parallelization.
    public List<string> Constraints { get; set; } = [];
/// Defines types of parallelization.
public enum ParallelizationType
    /// No parallelization.
    None,
    /// Element-wise parallel operations.
    ElementWise,
    /// Reduction operations (sum, count, etc.).
    Reduction,
    /// Parallel sorting algorithms.
    Sort,
    /// Conditional parallel execution.
    Conditional,
    /// Pipeline parallelization.
    Pipeline
/// Defines parallelization strategies.
public enum ParallelizationStrategy
    /// Sequential execution (no parallelization).
    Sequential,
    /// Data parallel execution.
    DataParallel,
    /// Vectorized parallel execution with SIMD.
    VectorizedParallel,
    /// Map-reduce pattern.
    MapReduce,
    /// Parallel sorting.
    ParallelSort,
    ConditionalParallel,
    /// Task-based parallelization.
    TaskParallel
/// Defines thread safety levels.
public enum ThreadSafetyLevel
    /// Thread-safe, no synchronization needed.
    ThreadSafe,
    /// Requires synchronization for thread safety.
    RequiresSynchronization,
    /// Not thread-safe, sequential execution required.
    Unsafe
