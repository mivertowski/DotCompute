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

    /// <summary>
    /// Converts a LINQ expression to a pipeline execution plan.
    /// </summary>
    /// <param name="expression">The LINQ expression to convert</param>
    /// <returns>A detailed pipeline execution plan</returns>
    Task<PipelineExecutionPlan> ConvertToPipelinePlanAsync(Expression expression);

    /// <summary>
    /// Analyzes expression complexity and backend compatibility.
    /// </summary>
    /// <param name="expression">The expression to analyze</param>
    /// <returns>Analysis results with compatibility information</returns>
    Task<ExpressionAnalysisResult> AnalyzeCompatibilityAsync(Expression expression);

    /// <summary>
    /// Optimizes an expression for pipeline execution.
    /// </summary>
    /// <param name="expression">The expression to optimize</param>
    /// <returns>Optimized expression</returns>
    Expression OptimizeExpression(Expression expression);
}

/// <summary>
/// Default implementation of pipeline expression analyzer.
/// </summary>
public class PipelineExpressionAnalyzer : IPipelineExpressionAnalyzer
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PipelineExpressionVisitor _visitor;

    /// <summary>
    /// Initializes a new instance of the PipelineExpressionAnalyzer class.
    /// </summary>
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

    /// <inheritdoc />
    public async Task<PipelineExecutionPlan> ConvertToPipelinePlanAsync(Expression expression)
    {
        return await Task.Run(() => _visitor.ConvertToPipelinePlan(expression));
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Expression OptimizeExpression(Expression expression)
    {
        // Apply expression-level optimizations
        var optimizer = new ExpressionOptimizer();
        return optimizer.Optimize(expression);
    }
}

/// <summary>
/// Configuration for pipeline creation from LINQ expressions.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Gets or sets the element type being processed.
    /// </summary>
    public Type ElementType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the pipeline stages.
    /// </summary>
    public List<PipelineStageInfo> Stages { get; set; } = new();

    /// <summary>
    /// Gets or sets optimization hints for the pipeline.
    /// </summary>
    public List<string> OptimizationHints { get; set; } = new();

    /// <summary>
    /// Gets or sets the estimated complexity.
    /// </summary>
    public int EstimatedComplexity { get; set; }

    /// <summary>
    /// Gets or sets the recommended backend.
    /// </summary>
    public string RecommendedBackend { get; set; } = "CPU";

    /// <summary>
    /// Gets or sets additional configuration parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Results of expression analysis for compatibility and optimization.
/// </summary>
public class ExpressionAnalysisResult
{
    private readonly List<string> _supportedOperations = [];
    private readonly List<Type> _parameterTypes = [];

    /// <summary>
    /// Gets or sets the operation signature for this expression.
    /// </summary>
    public string OperationSignature { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the expression can be compiled.
    /// </summary>
    public bool IsCompilable { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the expression is compatible with GPU execution.
    /// </summary>
    public bool IsGpuCompatible { get; set; }

    /// <summary>
    /// Gets or sets whether the expression is GPU suitable.
    /// </summary>
    public bool IsGpuSuitable { get; set; }

    /// <summary>
    /// Gets or sets whether the expression is compatible with CPU execution.
    /// </summary>
    public bool IsCpuCompatible { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the expression is parallelizable.
    /// </summary>
    public bool IsParallelizable { get; set; }

    /// <summary>
    /// Gets or sets the complexity score of the expression.
    /// </summary>
    public int ComplexityScore { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory requirement.
    /// </summary>
    public long MemoryRequirement { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage.
    /// </summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the parallelization potential score.
    /// </summary>
    public int ParallelizationPotential { get; set; }

    /// <summary>
    /// Gets or sets optimization recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Gets or sets identified bottlenecks.
    /// </summary>
    public List<string> Bottlenecks { get; set; } = new();

    /// <summary>
    /// Gets or sets alternative execution strategies.
    /// </summary>
    public List<string> AlternativeStrategies { get; set; } = new();

    /// <summary>
    /// Gets or sets the operator chain analysis for the expression.
    /// </summary>
    public List<OperatorInfo> OperatorChain { get; set; } = new();

    /// <summary>
    /// Gets or sets the complexity metrics for the expression.
    /// </summary>
    public ComplexityMetrics ComplexityMetrics { get; set; } = new();

    /// <summary>
    /// Gets or sets the operator information for the expression.
    /// </summary>
    public List<OperatorInfo> OperatorInfo { get; set; } = new();

    /// <summary>
    /// Gets or sets the dependencies for the expression.
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the parallelization information.
    /// </summary>
    public object? ParallelizationInfo { get; set; }

    /// <summary>
    /// Gets or sets the analysis timestamp.
    /// </summary>
    public DateTimeOffset AnalysisTimestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the supported operations in this expression.
    /// </summary>
    public IReadOnlyList<string> SupportedOperations => _supportedOperations;

    /// <summary>
    /// Gets the parameter types used in this expression.
    /// </summary>
    public IReadOnlyList<Type> ParameterTypes => _parameterTypes;

    /// <summary>
    /// Gets or sets the output type of the expression.
    /// </summary>
    public Type OutputType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets additional metadata for the analysis.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Internal access to supported operations for modification.
    /// </summary>
    internal List<string> SupportedOperationsInternal => _supportedOperations;

    /// <summary>
    /// Internal access to parameter types for modification.
    /// </summary>
    internal List<Type> ParameterTypesInternal => _parameterTypes;

    /// <summary>
    /// Gets or sets the type usage information for the expression.
    /// </summary>
    public Dictionary<Type, int> TypeUsage { get; set; } = new();

    /// <summary>
    /// Gets or sets the memory access pattern analysis.
    /// </summary>
    public MemoryAccessPattern MemoryAccessPattern { get; set; } = MemoryAccessPattern.Sequential;

    /// <summary>
    /// Gets or sets the optimization hints for this expression.
    /// </summary>
    public List<string> OptimizationHints { get; set; } = new();
}

/// <summary>
/// Expression optimizer for pipeline-specific optimizations.
/// </summary>
internal class ExpressionOptimizer
{
    /// <summary>
    /// Optimizes an expression for pipeline execution.
    /// </summary>
    /// <param name="expression">The expression to optimize</param>
    /// <returns>Optimized expression</returns>
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

/// <summary>
/// Simplifies boolean predicates in expressions.
/// </summary>
internal class PredicateSimplifier : ExpressionVisitor
{
    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Apply boolean logic simplifications
        if (node.NodeType == ExpressionType.AndAlso)
        {
            // true && x => x
            if (node.Left is ConstantExpression { Value: true })
            {
                return Visit(node.Right);
            }

            // x && true => x

            if (node.Right is ConstantExpression { Value: true })
            {
                return Visit(node.Left);
            }

            // false && x => false

            if (node.Left is ConstantExpression { Value: false })
            {
                return Expression.Constant(false);
            }

            // x && false => false

            if (node.Right is ConstantExpression { Value: false })
            {

                return Expression.Constant(false);
            }

        }
        else if (node.NodeType == ExpressionType.OrElse)
        {
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

/// <summary>
/// Optimizes operation fusion opportunities.
/// </summary>
internal class OperationFusionOptimizer : ExpressionVisitor
{
    // This would contain logic to identify and fuse adjacent operations
    // For example, Where followed by Select could be fused into a single kernel
}

/// <summary>
/// Performs constant folding optimizations.
/// </summary>
internal class ConstantFoldingOptimizer : ExpressionVisitor
{
    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        var right = Visit(node.Right);

        // Fold constants
        if (left is ConstantExpression leftConst && right is ConstantExpression rightConst)
        {
            try
            {
                // Evaluate the constant expression
                var lambda = Expression.Lambda(Expression.MakeBinary(node.NodeType, left, right));
                var compiled = lambda.Compile();
                var result = compiled.DynamicInvoke();
                return Expression.Constant(result, node.Type);
            }
            catch
            {
                // If evaluation fails, return the original expression
                return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
            }
        }

        return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
    }
}

/// <summary>
/// Eliminates redundant operations.
/// </summary>
internal class RedundancyEliminator : ExpressionVisitor
{
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Remove redundant operations like .Where(x => true) or .Select(x => x)
        if (node.Method.Name == "Where" && node.Arguments.Count == 2)
        {
            var predicate = node.Arguments[1];
            if (IsAlwaysTruePredicate(predicate))
            {
                // Where(x => true) can be eliminated
                return Visit(node.Arguments[0]);
            }
        }
        else if (node.Method.Name == "Select" && node.Arguments.Count == 2)
        {
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
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } unary)
        {
            expression = unary.Operand;
        }

        if (expression is LambdaExpression lambda && lambda.Parameters.Count == 1)
        {
            return lambda.Body == lambda.Parameters[0];
        }

        return false;
    }
}

/// <summary>
/// Information about an operator in the expression analysis.
/// </summary>
public class OperatorInfo
{
    /// <summary>
    /// Gets or sets the type of operator.
    /// </summary>
    public DotCompute.Core.Analysis.UnifiedOperatorType OperatorType { get; set; }

    /// <summary>
    /// Gets or sets the input types for this operator.
    /// </summary>
    public List<Type> InputTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the output type for this operator.
    /// </summary>
    public Type? OutputType { get; set; }

    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the complexity score for this operator.
    /// </summary>
    public int ComplexityScore { get; set; }

    /// <summary>
    /// Gets or sets whether this operator supports GPU execution.
    /// </summary>
    public bool SupportsGpu { get; set; }

    /// <summary>
    /// Gets or sets whether this operator supports CPU execution.
    /// </summary>
    public bool SupportsCpu { get; set; } = true;

    /// <summary>
    /// Gets or sets additional properties for this operator.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this operator is natively supported by the backend.
    /// </summary>
    public bool IsNativelySupported { get; set; }

    /// <summary>
    /// Gets or sets the implementation strategy for this operator.
    /// </summary>
    public DotCompute.Linq.Analysis.ImplementationMethod Implementation { get; set; } = DotCompute.Linq.Analysis.ImplementationMethod.Library;

    /// <summary>
    /// Gets or sets the performance cost score for this operator.
    /// </summary>
    public double PerformanceCost { get; set; }

    /// <summary>
    /// Gets or sets the accuracy information for this operator.
    /// </summary>
    public DotCompute.Linq.Analysis.AccuracyInfo Accuracy { get; set; } = new();
}

/// <summary>
/// Legacy OperatorType enum - use UnifiedOperatorType instead.
/// </summary>
[Obsolete("Use DotCompute.Core.Analysis.UnifiedOperatorType instead. This enum is maintained for backward compatibility.", false)]
public enum OperatorType
{
    /// <summary>
    /// Unknown or unspecified operator type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Filtering operation (Where).
    /// </summary>
    Filter,

    /// <summary>
    /// Projection operation (Select).
    /// </summary>
    Projection,

    /// <summary>
    /// Aggregation operation (Sum, Count, etc.).
    /// </summary>
    Aggregation,

    /// <summary>
    /// Sorting operation (OrderBy).
    /// </summary>
    Sort,

    /// <summary>
    /// Grouping operation (GroupBy).
    /// </summary>
    Group,

    /// <summary>
    /// Join operation.
    /// </summary>
    Join,

    /// <summary>
    /// Mathematical operation.
    /// </summary>
    Mathematical,

    /// <summary>
    /// Logical operation.
    /// </summary>
    Logical,

    /// <summary>
    /// Comparison operation.
    /// </summary>
    Comparison,

    /// <summary>
    /// Conversion operation.
    /// </summary>
    Conversion,

    /// <summary>
    /// Memory operation.
    /// </summary>
    Memory,

    /// <summary>
    /// Reduction operation.
    /// </summary>
    Reduction,

    /// <summary>
    /// Transformation operation.
    /// </summary>
    Transformation,

    /// <summary>
    /// Custom operation.
    /// </summary>
    Custom
}

/// <summary>
/// Complexity metrics for expression analysis.
/// </summary>
public class ComplexityMetrics
{
    /// <summary>
    /// Gets or sets the computational complexity score.
    /// </summary>
    public int ComputationalComplexity { get; set; }

    /// <summary>
    /// Gets or sets the memory complexity score.
    /// </summary>
    public int MemoryComplexity { get; set; }

    /// <summary>
    /// Gets or sets the parallelization complexity score.
    /// </summary>
    public int ParallelizationComplexity { get; set; }

    /// <summary>
    /// Gets or sets the overall complexity score.
    /// </summary>
    public int OverallComplexity { get; set; }

    /// <summary>
    /// Gets or sets the estimated operations count.
    /// </summary>
    public long OperationsCount { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the parallelization potential (0-100).
    /// </summary>
    public int ParallelizationPotential { get; set; }

    /// <summary>
    /// Gets or sets the compute complexity score.
    /// </summary>
    public int ComputeComplexity { get; set; }

    /// <summary>
    /// Gets or sets the communication complexity score for distributed operations.
    /// </summary>
    public int CommunicationComplexity { get; set; }

    /// <summary>
    /// Gets whether the operation is memory-bound.
    /// </summary>
    public bool MemoryBound => MemoryComplexity > ComputationalComplexity * 2;

    /// <summary>
    /// Gets whether the operation can benefit from shared memory optimization.
    /// </summary>
    public bool CanBenefitFromSharedMemory => MemoryBound && ParallelizationPotential > 50;

    /// <summary>
    /// Gets or sets complexity factors that contribute to the overall score.
    /// </summary>
    public Dictionary<string, int> ComplexityFactors { get; set; } = new();

    /// <summary>
    /// Gets or sets performance bottlenecks identified in the analysis.
    /// </summary>
    public List<string> Bottlenecks { get; set; } = new();

    /// <summary>
    /// Gets or sets optimization opportunities.
    /// </summary>
    public List<string> OptimizationOpportunities { get; set; } = new();

    /// <summary>
    /// Gets or sets the operation count (alias for OperationsCount).
    /// </summary>
    public long OperationCount 
    { 
        get => OperationsCount; 
        set => OperationsCount = value; 
    }

    /// <summary>
    /// Gets or sets whether GPU execution is recommended.
    /// </summary>
    public bool GpuRecommended { get; set; }

    /// <summary>
    /// Gets or sets complexity by category.
    /// </summary>
    public Dictionary<string, int> ComplexityByCategory { get; set; } = new();
}