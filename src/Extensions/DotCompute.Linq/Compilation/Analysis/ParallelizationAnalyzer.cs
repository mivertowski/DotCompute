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

    /// <summary>
    /// Analyzes an operator for parallelization opportunities.
    /// </summary>
    /// <param name="operatorExpression">The operator to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parallelization opportunity analysis.</returns>
    public async Task<DotCompute.Linq.Compilation.Analysis.ParallelizationOpportunity> AnalyzeOperatorAsync(
        Expression operatorExpression,

        CancellationToken cancellationToken = default)
    {
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
    }

    /// <summary>
    /// Recursively analyzes expression for parallelization opportunities.
    /// </summary>
    private static void AnalyzeExpression(Expression expression, ParallelizationResult result)
    {
        switch (expression)
        {
            case MethodCallExpression methodCall:
                AnalyzeMethodCall(methodCall, result);
                break;

            case BinaryExpression binary:
                AnalyzeBinaryExpression(binary, result);
                break;

            case UnaryExpression unary:
                AnalyzeExpression(unary.Operand, result);
                break;

            case ConditionalExpression conditional:
                AnalyzeConditionalExpression(conditional, result);
                break;

            case LambdaExpression lambda:
                AnalyzeExpression(lambda.Body, result);
                break;

            case ParameterExpression:
            case ConstantExpression:
                // These are typically parallelizable
                result.IsParallelizable = true;
                break;
        }
    }

    /// <summary>
    /// Analyzes a method call for parallelization potential.
    /// </summary>
    private static void AnalyzeMethodCall(MethodCallExpression methodCall, ParallelizationResult result)
    {
        var methodName = methodCall.Method.Name;
        var declaringType = methodCall.Method.DeclaringType;

        // LINQ methods analysis
        if (declaringType == typeof(Enumerable) || declaringType == typeof(Queryable))
        {
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
                    result.IsParallelizable = true;
                    result.ParallelizationType = ParallelizationType.Reduction;
                    result.RecommendedParallelism = Environment.ProcessorCount;
                    result.DataDependencies.Add("Reduction operation");
                    break;

                case "Aggregate":
                    result.IsParallelizable = IsAssociativeAggregate(methodCall);
                    result.ParallelizationType = ParallelizationType.Reduction;
                    if (!result.IsParallelizable)
                    {
                        result.Constraints.Add("Aggregate function must be associative for parallelization");
                    }
                    break;

                case "OrderBy":
                case "OrderByDescending":
                    result.IsParallelizable = true;
                    result.ParallelizationType = ParallelizationType.Sort;
                    result.RecommendedParallelism = Math.Max(2, Environment.ProcessorCount / 2);
                    result.Constraints.Add("Parallel sort requires merge step");
                    break;

                default:
                    result.IsParallelizable = false;
                    result.Constraints.Add($"Method {methodName} is not easily parallelizable");
                    break;
            }
        }

        // Mathematical operations

        else if (declaringType == typeof(Math) || declaringType == typeof(MathF))
        {
            result.IsParallelizable = true;
            result.ParallelizationType = ParallelizationType.ElementWise;
            result.VectorizationSuitable = true;
        }

        // Analyze arguments
        foreach (var arg in methodCall.Arguments)
        {
            AnalyzeExpression(arg, result);
        }

        if (methodCall.Object != null)
        {
            AnalyzeExpression(methodCall.Object, result);
        }

    }

    /// <summary>
    /// Analyzes a binary expression for parallelization.
    /// </summary>
    private static void AnalyzeBinaryExpression(BinaryExpression binary, ParallelizationResult result)
    {
        // Most binary operations are element-wise parallelizable
        if (IsMathematicalOperation(binary.NodeType))
        {
            result.IsParallelizable = true;
            result.ParallelizationType = ParallelizationType.ElementWise;
            result.VectorizationSuitable = true;
            result.RecommendedParallelism = Environment.ProcessorCount * 4; // SIMD potential
        }

        AnalyzeExpression(binary.Left, result);
        AnalyzeExpression(binary.Right, result);
    }

    /// <summary>
    /// Analyzes a conditional expression for parallelization.
    /// </summary>
    private static void AnalyzeConditionalExpression(ConditionalExpression conditional, ParallelizationResult result)
    {
        AnalyzeExpression(conditional.Test, result);
        AnalyzeExpression(conditional.IfTrue, result);
        AnalyzeExpression(conditional.IfFalse, result);

        // Conditionals can be parallelizable with predicated execution
        result.IsParallelizable = true;
        result.ParallelizationType = ParallelizationType.Conditional;
        result.Constraints.Add("Conditional execution may reduce parallel efficiency");
    }

    /// <summary>
    /// Determines if an aggregate operation is associative.
    /// </summary>
    private static bool IsAssociativeAggregate(MethodCallExpression methodCall)
    {
        // This is a simplified heuristic - in practice would need more sophisticated analysis
        return methodCall.Arguments.Count >= 2;
    }

    /// <summary>
    /// Checks if a node type represents a mathematical operation.
    /// </summary>
    private static bool IsMathematicalOperation(ExpressionType nodeType)
    {
        return nodeType switch
        {
            ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or
            ExpressionType.Divide or ExpressionType.Modulo or ExpressionType.Power => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines the optimal parallelization strategy.
    /// </summary>
    private static ParallelizationStrategy DetermineOptimalStrategy(ParallelizationResult result)
    {
        if (!result.IsParallelizable)
        {

            return ParallelizationStrategy.Sequential;
        }


        return result.ParallelizationType switch
        {
            ParallelizationType.ElementWise when result.VectorizationSuitable => ParallelizationStrategy.VectorizedParallel,
            ParallelizationType.ElementWise => ParallelizationStrategy.DataParallel,
            ParallelizationType.Reduction => ParallelizationStrategy.MapReduce,
            ParallelizationType.Sort => ParallelizationStrategy.ParallelSort,
            ParallelizationType.Conditional => ParallelizationStrategy.ConditionalParallel,
            _ => ParallelizationStrategy.DataParallel
        };
    }

    /// <summary>
    /// Estimates potential speedup from parallelization.
    /// </summary>
    private static double EstimateSpeedup(ParallelizationResult result)
    {
        if (!result.IsParallelizable)
        {

            return 1.0;
        }


        var baseSpeedup = Math.Min(result.RecommendedParallelism, Environment.ProcessorCount);

        // Apply Amdahl's law approximation

        var parallelPortion = result.ParallelizationType switch
        {
            ParallelizationType.ElementWise => 0.9, // 90% parallelizable
            ParallelizationType.Reduction => 0.7,   // 70% parallelizable due to reduction overhead
            ParallelizationType.Sort => 0.6,        // 60% due to merge complexity
            ParallelizationType.Conditional => 0.5, // 50% due to branching overhead
            _ => 0.8
        };

        var serialPortion = 1.0 - parallelPortion;
        var speedup = 1.0 / (serialPortion + (parallelPortion / baseSpeedup));

        // Apply vectorization bonus
        if (result.VectorizationSuitable)
        {
            speedup *= 2.0; // Conservative SIMD speedup
        }


        return Math.Round(speedup, 2);
    }

    /// <summary>
    /// Determines thread safety characteristics.
    /// </summary>
    private static ThreadSafetyLevel DetermineThreadSafety(ParallelizationResult result)
    {
        if (result.DataDependencies.Any(d => d.Contains("shared state")))
        {

            return ThreadSafetyLevel.Unsafe;
        }


        if (result.ParallelizationType == ParallelizationType.Reduction)
        {

            return ThreadSafetyLevel.RequiresSynchronization;
        }


        return ThreadSafetyLevel.ThreadSafe;
    }
}

/// <summary>
/// Represents the result of parallelization analysis.
/// </summary>
public class ParallelizationResult
{
    /// <summary>
    /// Gets or sets whether the expression is parallelizable.
    /// </summary>
    public bool IsParallelizable { get; set; }

    /// <summary>
    /// Gets or sets whether vectorization is suitable.
    /// </summary>
    public bool VectorizationSuitable { get; set; }

    /// <summary>
    /// Gets or sets the type of parallelization.
    /// </summary>
    public ParallelizationType ParallelizationType { get; set; }

    /// <summary>
    /// Gets or sets the recommended degree of parallelism.
    /// </summary>
    public int RecommendedParallelism { get; set; } = 1;

    /// <summary>
    /// Gets or sets the optimal parallelization strategy.
    /// </summary>
    public ParallelizationStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets the estimated speedup from parallelization.
    /// </summary>
    public double EstimatedSpeedup { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets thread safety characteristics.
    /// </summary>
    public ThreadSafetyLevel ThreadSafety { get; set; }

    /// <summary>
    /// Gets or sets data dependencies that affect parallelization.
    /// </summary>
    public List<string> DataDependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets constraints that limit parallelization.
    /// </summary>
    public List<string> Constraints { get; set; } = [];
}

/// <summary>
/// Defines types of parallelization.
/// </summary>
public enum ParallelizationType
{
    /// <summary>
    /// No parallelization.
    /// </summary>
    None,

    /// <summary>
    /// Element-wise parallel operations.
    /// </summary>
    ElementWise,

    /// <summary>
    /// Reduction operations (sum, count, etc.).
    /// </summary>
    Reduction,

    /// <summary>
    /// Parallel sorting algorithms.
    /// </summary>
    Sort,

    /// <summary>
    /// Conditional parallel execution.
    /// </summary>
    Conditional,

    /// <summary>
    /// Pipeline parallelization.
    /// </summary>
    Pipeline
}

/// <summary>
/// Defines parallelization strategies.
/// </summary>
public enum ParallelizationStrategy
{
    /// <summary>
    /// Sequential execution (no parallelization).
    /// </summary>
    Sequential,

    /// <summary>
    /// Data parallel execution.
    /// </summary>
    DataParallel,

    /// <summary>
    /// Vectorized parallel execution with SIMD.
    /// </summary>
    VectorizedParallel,

    /// <summary>
    /// Map-reduce pattern.
    /// </summary>
    MapReduce,

    /// <summary>
    /// Parallel sorting.
    /// </summary>
    ParallelSort,

    /// <summary>
    /// Conditional parallel execution.
    /// </summary>
    ConditionalParallel,

    /// <summary>
    /// Task-based parallelization.
    /// </summary>
    TaskParallel
}

/// <summary>
/// Defines thread safety levels.
/// </summary>
public enum ThreadSafetyLevel
{
    /// <summary>
    /// Thread-safe, no synchronization needed.
    /// </summary>
    ThreadSafe,

    /// <summary>
    /// Requires synchronization for thread safety.
    /// </summary>
    RequiresSynchronization,

    /// <summary>
    /// Not thread-safe, sequential execution required.
    /// </summary>
    Unsafe
}