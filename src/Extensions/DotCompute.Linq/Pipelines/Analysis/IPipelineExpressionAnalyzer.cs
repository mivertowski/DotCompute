// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Linq.Pipelines.Models;
using DotCompute.Linq.Compilation.Analysis;
namespace DotCompute.Linq.Pipelines.Analysis;
/// <summary>
/// Interface for analyzing LINQ expressions and converting them to pipeline configurations.
/// </summary>
public interface IPipelineExpressionAnalyzer
{
    /// <summary>
    /// Analyzes a LINQ expression and generates a pipeline configuration.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="expression">The LINQ expression to analyze</param>
    /// <returns>Pipeline configuration based on the expression</returns>
    PipelineConfiguration AnalyzeExpression<T>(Expression expression) where T : unmanaged;
    /// Converts a LINQ expression to a pipeline execution plan.
    /// <param name="expression">The LINQ expression to convert</param>
    /// <returns>A detailed pipeline execution plan</returns>
    Task<PipelineExecutionPlan> ConvertToPipelinePlanAsync(Expression expression);
    /// Analyzes expression complexity and backend compatibility.
    /// <param name="expression">The expression to analyze</param>
    /// <returns>Analysis results with compatibility information</returns>
    Task<ExpressionAnalysisResult> AnalyzeCompatibilityAsync(Expression expression);
    /// Optimizes an expression for pipeline execution.
    /// <param name="expression">The expression to optimize</param>
    /// <returns>Optimized expression</returns>
    Expression OptimizeExpression(Expression expression);
}
/// Default implementation of pipeline expression analyzer.
public class PipelineExpressionAnalyzer : IPipelineExpressionAnalyzer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PipelineExpressionVisitor _visitor;
    /// Initializes a new instance of the PipelineExpressionAnalyzer class.
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    public PipelineExpressionAnalyzer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        var logger = (Microsoft.Extensions.Logging.ILogger<PipelineExpressionVisitor>?)serviceProvider
            .GetService(typeof(Microsoft.Extensions.Logging.ILogger<PipelineExpressionVisitor>));
        _visitor = new PipelineExpressionVisitor(logger ??
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PipelineExpressionVisitor>.Instance);
    }
    /// <inheritdoc />
    public PipelineConfiguration AnalyzeExpression<T>(Expression expression) where T : unmanaged
    {
        var plan = _visitor.ConvertToPipelinePlan(expression);
        return new PipelineConfiguration
        {
            ElementType = typeof(T),
            Stages = plan.Stages,
            OptimizationHints = plan.OptimizationHints,
            EstimatedComplexity = plan.EstimatedComplexity,
            RecommendedBackend = plan.RecommendedBackend ?? "CPU"
        };
    }

    public async Task<PipelineExecutionPlan> ConvertToPipelinePlanAsync(Expression expression)
    {
        return await Task.Run(() => _visitor.ConvertToPipelinePlan(expression));
    }

    public async Task<ExpressionAnalysisResult> AnalyzeCompatibilityAsync(Expression expression)
    {
        var plan = await ConvertToPipelinePlanAsync(expression);
        return new ExpressionAnalysisResult
        {
            IsGpuCompatible = plan.Stages.Any(s => s.SupportedBackends.Contains("CUDA")),
            IsCpuCompatible = plan.Stages.Any(s => s.SupportedBackends.Contains("CPU")),
            ComplexityScore = plan.EstimatedComplexity,
            MemoryRequirement = plan.EstimatedMemoryRequirement,
            ParallelizationPotential = plan.ParallelizationOpportunities.Count,
            Recommendations = plan.OptimizationHints
        };
    }

    public Expression OptimizeExpression(Expression expression)
    {
        // Apply expression-level optimizations
        var optimizer = new ExpressionOptimizer();
        return optimizer.Optimize(expression);
    }
}

/// Configuration for pipeline creation from LINQ expressions.
public class PipelineConfiguration
{
    /// Gets or sets the element type being processed.
    public Type ElementType { get; set; } = typeof(object);
    /// Gets or sets the pipeline stages.
    public List<PipelineStageInfo> Stages { get; set; } = [];
    /// Gets or sets optimization hints for the pipeline.
    public List<string> OptimizationHints { get; set; } = [];
    /// Gets or sets the estimated complexity.
    public int EstimatedComplexity { get; set; }
    /// Gets or sets the recommended backend.
    public string RecommendedBackend { get; set; } = "CPU";
    /// Gets or sets additional configuration parameters.
    public Dictionary<string, object> Parameters { get; set; } = [];
}

/// Results of expression analysis for compatibility and optimization.
public class ExpressionAnalysisResult
{
    private readonly List<string> _supportedOperations = [];
    private readonly List<Type> _parameterTypes = [];
    /// Gets or sets the operation signature for this expression.
    public string OperationSignature { get; set; } = string.Empty;
    /// Gets or sets whether the expression can be compiled.
    public bool IsCompilable { get; set; } = true;
    /// Gets or sets whether the expression is compatible with GPU execution.
    public bool IsGpuCompatible { get; set; }
    /// Gets or sets whether the expression is GPU suitable.
    public bool IsGpuSuitable { get; set; }
    /// Gets or sets whether the expression is compatible with CPU execution.
    public bool IsCpuCompatible { get; set; } = true;
    /// Gets or sets whether the expression is parallelizable.
    public bool IsParallelizable { get; set; }
    /// Gets or sets the complexity score of the expression.
    public int ComplexityScore { get; set; }
    /// Gets or sets the estimated memory requirement.
    public long MemoryRequirement { get; set; }
    /// Gets or sets the estimated memory usage.
    public long EstimatedMemoryUsage { get; set; }
    /// Gets or sets the parallelization potential score.
    public int ParallelizationPotential { get; set; }
    /// Gets or sets optimization recommendations.
    public List<string> Recommendations { get; set; } = [];
    /// Gets or sets identified bottlenecks.
    public List<string> Bottlenecks { get; set; } = [];
    /// Gets or sets alternative execution strategies.
    public List<string> AlternativeStrategies { get; set; } = [];
    /// Gets or sets the operator chain analysis for the expression.
    public List<OperatorInfo> OperatorChain { get; set; } = [];
    /// Gets or sets the complexity metrics for the expression.
    public ComplexityMetrics ComplexityMetrics { get; set; } = new();
    /// Gets or sets the operator information for the expression.
    public List<OperatorInfo> OperatorInfo { get; set; } = [];
    /// Gets or sets the dependencies for the expression.
    public List<string> Dependencies { get; set; } = [];
    /// Gets or sets the parallelization information.
    public object? ParallelizationInfo { get; set; }
    /// Gets or sets the analysis timestamp.
    public DateTimeOffset AnalysisTimestamp { get; set; } = DateTimeOffset.UtcNow;
    /// Gets the supported operations in this expression.
    public IReadOnlyList<string> SupportedOperations => _supportedOperations;
    /// Gets the parameter types used in this expression.
    public IReadOnlyList<Type> ParameterTypes => _parameterTypes;
    /// Gets or sets the output type of the expression.
    public Type OutputType { get; set; } = typeof(object);
    /// Gets or sets additional metadata for the analysis.
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// Internal access to supported operations for modification.
    internal List<string> SupportedOperationsInternal => _supportedOperations;
    /// Internal access to parameter types for modification.
    internal List<Type> ParameterTypesInternal => _parameterTypes;
    /// Gets or sets the type usage information for the expression.
    public Dictionary<Type, int> TypeUsage { get; set; } = [];
    /// Gets or sets the memory access pattern analysis.
    public MemoryAccessPattern MemoryAccessPattern { get; set; } = MemoryAccessPattern.Sequential;

    /// Gets or sets the optimization hints for this expression.
    public List<string> OptimizationHints { get; set; } = [];
}

/// Expression optimizer for pipeline-specific optimizations.
internal class ExpressionOptimizer
{
    public Expression Optimize(Expression expression)
    {
        // Apply various optimization passes
        expression = SimplifyPredicates(expression);
        expression = FusePipelineOperations(expression);
        expression = OptimizeConstants(expression);
        expression = EliminateRedundantOperations(expression);
        return expression;
    }

    private Expression SimplifyPredicates(Expression expression)
    {
        // Simplify boolean logic in Where clauses
        var simplifier = new PredicateSimplifier();
        return simplifier.Visit(expression);
    }

    private Expression FusePipelineOperations(Expression expression)
    {
        // Fuse adjacent operations that can be combined
        var fusionOptimizer = new OperationFusionOptimizer();
        return fusionOptimizer.Visit(expression);
    }

    private Expression OptimizeConstants(Expression expression)
    {
        // Fold constant expressions
        var constantFolder = new ConstantFoldingOptimizer();
        return constantFolder.Visit(expression);
    }

    private Expression EliminateRedundantOperations(Expression expression)
    {
        // Remove redundant operations
        var redundancyEliminator = new RedundancyEliminator();
        return redundancyEliminator.Visit(expression);
    }
}

/// Simplifies boolean predicates in expressions.
internal class PredicateSimplifier : ExpressionVisitor
{
    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Apply boolean logic simplifications
        if (node.NodeType == ExpressionType.AndAlso)
            // true && x => x
            if (node.Left is ConstantExpression { Value: true })
            {
                return Visit(node.Right);
            }
            // x && true => x
            if (node.Right is ConstantExpression { Value: true })
                return Visit(node.Left);
            // false && x => false
            if (node.Left is ConstantExpression { Value: false })
                return Expression.Constant(false);
            // x && false => false
            if (node.Right is ConstantExpression { Value: false })
            {
                return Expression.Constant(false);
            }
        }
        else if (node.NodeType == ExpressionType.OrElse)
            // true || x => true
            if (node.Left is ConstantExpression { Value: true })
            {
                return Expression.Constant(true);
            }
            // x || true => true
            if (node.Right is ConstantExpression { Value: true })
            {
                return Expression.Constant(true);
            }

            // false || x => x
            if (node.Left is ConstantExpression { Value: false })
            {
                return Visit(node.Right);
            }

            // x || false => x
            if (node.Right is ConstantExpression { Value: false })
            {
                return Visit(node.Left);
            }
        }

        return base.VisitBinary(node);
    }
}

/// Optimizes operation fusion opportunities.
internal class OperationFusionOptimizer : ExpressionVisitor
{
    // This would contain logic to identify and fuse adjacent operations
    // For example, Where followed by Select could be fused into a single kernel
}

/// Performs constant folding optimizations.
internal class ConstantFoldingOptimizer : ExpressionVisitor
{
    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);
        // Fold constants
        if (left is ConstantExpression leftConst && right is ConstantExpression rightConst)
            try
                // Evaluate the constant expression
                var lambda = Expression.Lambda(Expression.MakeBinary(node.NodeType, left, right));
                var compiled = lambda.Compile();
                var result = compiled.DynamicInvoke();
                return Expression.Constant(result, node.Type);
            catch
                // If evaluation fails, return the original expression
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
            }
        }

        return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
    }
}

/// Eliminates redundant operations.
internal class RedundancyEliminator : ExpressionVisitor
{
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Remove redundant operations like .Where(x => true) or .Select(x => x)
        if (node.Method.Name == "Where" && node.Arguments.Count == 2)
            var predicate = node.Arguments[1];
            if (IsAlwaysTruePredicate(predicate))
            {
                // Where(x => true) can be eliminated
                return Visit(node.Arguments[0]);
            }
        else if (node.Method.Name == "Select" && node.Arguments.Count == 2)
            var selector = node.Arguments[1];
            if (IsIdentitySelector(selector))
            {
                // Select(x => x) can be eliminated
                return Visit(node.Arguments[0]);
            }
        }

        return base.VisitMethodCall(node);
    }

    private static bool IsAlwaysTruePredicate(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } unary)
        {
            expression = unary.Operand;
        }
        if (expression is LambdaExpression lambda)
        {
            return lambda.Body is ConstantExpression { Value: true };
        }

        return false;
    }

    private static bool IsIdentitySelector(Expression expression)
    {
        if (expression is LambdaExpression lambda && lambda.Parameters.Count == 1)
        {
            return lambda.Body == lambda.Parameters[0];
        }

        return false;
    }
}

/// Information about an operator in the expression analysis.
public class OperatorInfo
{
    /// Gets or sets the type of operator.
    public DotCompute.Core.Analysis.UnifiedOperatorType OperatorType { get; set; }
    /// Gets or sets the input types for this operator.
    public List<Type> InputTypes { get; set; } = [];
    /// Gets or sets the output type for this operator.
    public Type? OutputType { get; set; }
    /// Gets or sets the operation name.
    public string Name { get; set; } = string.Empty;
    /// Gets or sets the complexity score for this operator.
    public int ComplexityScore { get; set; }

    /// Gets or sets whether this operator supports GPU execution.
    public bool SupportsGpu { get; set; }
    /// Gets or sets whether this operator supports CPU execution.
    public bool SupportsCpu { get; set; } = true;
    /// Gets or sets additional properties for this operator.
    public Dictionary<string, object> Properties { get; set; } = [];
    /// Gets or sets whether this operator is natively supported by the backend.
    public bool IsNativelySupported { get; set; }
    /// Gets or sets the implementation strategy for this operator.
    public DotCompute.Linq.Analysis.ImplementationMethod Implementation { get; set; } = DotCompute.Linq.Analysis.ImplementationMethod.Library;
    /// Gets or sets the performance cost score for this operator.
    public double PerformanceCost { get; set; }
    /// Gets or sets the accuracy information for this operator.
    public DotCompute.Linq.Analysis.AccuracyInfo Accuracy { get; set; } = new();
}

/// Legacy OperatorType enum - use UnifiedOperatorType instead.
[Obsolete("Use DotCompute.Core.Analysis.UnifiedOperatorType instead. This enum is maintained for backward compatibility.", false)]
public enum OperatorType
    /// Unknown or unspecified operator type.
    Unknown,
    /// Filtering operation (Where).
    Filter,
    /// Projection operation (Select).
    Projection,
    /// Aggregation operation (Sum, Count, etc.).
    Aggregation,
    /// Sorting operation (OrderBy).
    Sort,
    /// Grouping operation (GroupBy).
    Group,
    /// Join operation.
    Join,
    /// Mathematical operation.
    Mathematical,
    /// Logical operation.
    Logical,
    /// Comparison operation.
    Comparison,
    /// Conversion operation.
    Conversion,
    /// Memory operation.
    Memory,
    /// Reduction operation.
    Reduction,
    /// Transformation operation.
    Transformation,
    /// Custom operation.
    Custom
}

/// Complexity metrics for expression analysis.
public class ComplexityMetrics
{
    /// Gets or sets the computational complexity score.
    public int ComputationalComplexity { get; set; }
    /// Gets or sets the memory complexity score.
    public int MemoryComplexity { get; set; }
    /// Gets or sets the parallelization complexity score.
    public int ParallelizationComplexity { get; set; }
    /// Gets or sets the overall complexity score.
    public int OverallComplexity { get; set; }
    /// Gets or sets the estimated operations count.
    public long OperationsCount { get; set; }
    /// Gets or sets the estimated memory usage in bytes.
    public long MemoryUsage { get; set; }
    /// Gets or sets the parallelization potential (0-100).
    public int ParallelizationPotential { get; set; }

    /// Gets or sets the compute complexity score.
    public int ComputeComplexity { get; set; }
    /// Gets or sets the communication complexity score for distributed operations.
    public int CommunicationComplexity { get; set; }
    /// Gets whether the operation is memory-bound.
    public bool MemoryBound => MemoryComplexity > ComputationalComplexity * 2;
    /// Gets whether the operation can benefit from shared memory optimization.
    public bool CanBenefitFromSharedMemory => MemoryBound && ParallelizationPotential > 50;
    /// Gets or sets complexity factors that contribute to the overall score.
    public Dictionary<string, int> ComplexityFactors { get; set; } = [];
    /// Gets or sets performance bottlenecks identified in the analysis.
    public List<string> PerformanceBottlenecks { get; set; } = [];

    /// Gets or sets optimization opportunities.
    public List<string> OptimizationOpportunities { get; set; } = [];
    /// Gets or sets the operation count (alias for OperationsCount).
    public long OperationCount
    {
        get => OperationsCount;
        set => OperationsCount = value;
    }

    /// Gets or sets whether GPU execution is recommended.
    public bool GpuRecommended { get; set; }
    /// Gets or sets complexity by category.
    public Dictionary<string, int> ComplexityByCategory { get; set; } = [];
}
