// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Pipelines.Analysis;

/// <summary>
/// Visitor that converts LINQ expression trees into kernel pipeline execution plans.
/// Analyzes complex nested expressions and optimizes the pipeline structure for GPU execution.
/// </summary>
public class PipelineExpressionVisitor : ExpressionVisitor
{
    private readonly ILogger<PipelineExpressionVisitor> _logger;
    private readonly Stack<PipelineStageInfo> _stageStack;
    private readonly List<PipelineStageInfo> _stages;
    private readonly Dictionary<Expression, string> _intermediateResults;
    private readonly PipelineOptimizationContext _optimizationContext;
    private int _stageCounter;

    /// <summary>
    /// Initializes a new instance of the PipelineExpressionVisitor class.
    /// </summary>
    /// <param name="logger">Logger for diagnostics and debugging</param>
    public PipelineExpressionVisitor(ILogger<PipelineExpressionVisitor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stageStack = new Stack<PipelineStageInfo>();
        _stages = new List<PipelineStageInfo>();
        _intermediateResults = new Dictionary<Expression, string>();
        _optimizationContext = new PipelineOptimizationContext();
        _stageCounter = 0;
    }

    /// <summary>
    /// Converts a LINQ expression tree to a pipeline execution plan.
    /// </summary>
    /// <param name="expression">The LINQ expression to convert</param>
    /// <returns>A pipeline execution plan with optimized kernel stages</returns>
    public PipelineExecutionPlan ConvertToPipelinePlan(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _logger.LogDebug("Converting LINQ expression to pipeline plan: {Expression}", expression);

        // Clear previous state
        _stageStack.Clear();
        _stages.Clear();
        _intermediateResults.Clear();
        _stageCounter = 0;

        // Visit the expression tree
        Visit(expression);

        // Build the final execution plan
        var plan = new PipelineExecutionPlan
        {
            Stages = _stages.ToList(),
            OptimizationHints = _optimizationContext.GetOptimizationHints(),
            EstimatedComplexity = CalculateComplexity(_stages),
            ParallelizationOpportunities = IdentifyParallelizationOpportunities(_stages)
        };

        _logger.LogInformation("Generated pipeline plan with {StageCount} stages", _stages.Count);
        return plan;
    }

    /// <summary>
    /// Visits method call expressions (Where, Select, GroupBy, etc.).
    /// </summary>
    /// <param name="node">The method call expression</param>
    /// <returns>The visited expression</returns>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        // Handle different LINQ methods
        return node.Method.Name switch
        {
            "Where" => VisitWhereExpression(node),
            "Select" => VisitSelectExpression(node),
            "GroupBy" => VisitGroupByExpression(node),
            "Join" => VisitJoinExpression(node),
            "Aggregate" => VisitAggregateExpression(node),
            "Sum" => VisitSumExpression(node),
            "Average" => VisitAverageExpression(node),
            "Count" => VisitCountExpression(node),
            "Min" => VisitMinExpression(node),
            "Max" => VisitMaxExpression(node),
            "OrderBy" => VisitOrderByExpression(node),
            "OrderByDescending" => VisitOrderByExpression(node, true),
            "Take" => VisitTakeExpression(node),
            "Skip" => VisitSkipExpression(node),
            "Distinct" => VisitDistinctExpression(node),
            "Union" => VisitUnionExpression(node),
            "Intersect" => VisitIntersectExpression(node),
            "Except" => VisitExceptExpression(node),
            _ => VisitGenericMethodCall(node)
        };
    }

    #region LINQ Method Visitors

    private Expression VisitWhereExpression(MethodCallExpression node)
    {
        var stage = CreateStage("WhereKernel", PipelineStageType.Filter);
        var predicate = ExtractLambdaExpression(node.Arguments[1]);


        stage.Parameters.Add("predicate", predicate);
        stage.KernelComplexity = AnalyzeLambdaComplexity(predicate);
        stage.SupportedBackends = ["CPU", "CUDA"]; // Where operations work on both


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitSelectExpression(MethodCallExpression node)
    {
        var stage = CreateStage("SelectKernel", PipelineStageType.Transform);
        var selector = ExtractLambdaExpression(node.Arguments[1]);


        stage.Parameters.Add("selector", selector);
        stage.KernelComplexity = AnalyzeLambdaComplexity(selector);
        stage.SupportedBackends = DetermineSelectorBackends(selector);

        // Check for projection complexity

        if (IsComplexProjection(selector))
        {
            stage.OptimizationHints.Add("Consider kernel fusion for complex projections");
        }


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitGroupByExpression(MethodCallExpression node)
    {
        var stage = CreateStage("GroupByKernel", PipelineStageType.Grouping);
        var keySelector = ExtractLambdaExpression(node.Arguments[1]);


        stage.Parameters.Add("keySelector", keySelector);
        stage.KernelComplexity = KernelComplexity.High; // GroupBy is inherently complex
        stage.SupportedBackends = ["CUDA"]; // GPU-optimized grouping
        stage.RequiredMemory = EstimateGroupingMemory(keySelector);


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitJoinExpression(MethodCallExpression node)
    {
        var stage = CreateStage("JoinKernel", PipelineStageType.Join);

        // Extract join parameters

        var outerKeySelector = ExtractLambdaExpression(node.Arguments[2]);
        var innerKeySelector = ExtractLambdaExpression(node.Arguments[3]);
        var resultSelector = ExtractLambdaExpression(node.Arguments[4]);


        stage.Parameters.Add("outerKeySelector", outerKeySelector);
        stage.Parameters.Add("innerKeySelector", innerKeySelector);
        stage.Parameters.Add("resultSelector", resultSelector);
        stage.KernelComplexity = KernelComplexity.VeryHigh; // Joins are very complex
        stage.SupportedBackends = ["CUDA"]; // GPU-optimized joins


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit outer source
        Visit(node.Arguments[1]); // Visit inner source


        return node;
    }

    private Expression VisitAggregateExpression(MethodCallExpression node)
    {
        var stage = CreateStage("AggregateKernel", PipelineStageType.Reduction);


        if (node.Arguments.Count > 1)
        {
            var aggregateFunc = ExtractLambdaExpression(node.Arguments[1]);
            stage.Parameters.Add("aggregateFunc", aggregateFunc);
            stage.KernelComplexity = AnalyzeLambdaComplexity(aggregateFunc);
        }


        if (node.Arguments.Count > 2)
        {
            var selector = ExtractLambdaExpression(node.Arguments[2]);
            stage.Parameters.Add("selector", selector);
        }


        stage.SupportedBackends = ["CUDA", "CPU"]; // Both support reductions
        stage.OptimizationHints.Add("Consider parallel reduction for large datasets");


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitSumExpression(MethodCallExpression node)
    {
        var stage = CreateStage("SumKernel", PipelineStageType.Reduction);
        stage.KernelComplexity = KernelComplexity.Low; // Sum is simple
        stage.SupportedBackends = ["CUDA", "CPU"]; // Highly optimized on both
        stage.OptimizationHints.Add("Use SIMD instructions for CPU, parallel reduction for GPU");


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitAverageExpression(MethodCallExpression node)
    {
        // Average can be decomposed into Sum and Count
        var sumStage = CreateStage("SumKernel", PipelineStageType.Reduction);
        var countStage = CreateStage("CountKernel", PipelineStageType.Reduction);
        var divideStage = CreateStage("DivideKernel", PipelineStageType.Transform);


        sumStage.SupportedBackends = ["CUDA", "CPU"];
        countStage.SupportedBackends = ["CUDA", "CPU"];
        divideStage.SupportedBackends = ["CUDA", "CPU"];

        // Mark these as parallel stages that can run simultaneously

        sumStage.CanRunInParallel = true;
        countStage.CanRunInParallel = true;


        AddStage(sumStage);
        AddStage(countStage);
        AddStage(divideStage);


        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitCountExpression(MethodCallExpression node)
    {
        var stage = CreateStage("CountKernel", PipelineStageType.Reduction);
        stage.KernelComplexity = KernelComplexity.Low;
        stage.SupportedBackends = ["CUDA", "CPU"];

        // If there's a predicate, it's Count(predicate)

        if (node.Arguments.Count > 1)
        {
            var predicate = ExtractLambdaExpression(node.Arguments[1]);
            stage.Parameters.Add("predicate", predicate);
            stage.KernelComplexity = AnalyzeLambdaComplexity(predicate);
        }


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitMinExpression(MethodCallExpression node)
    {
        var stage = CreateStage("MinKernel", PipelineStageType.Reduction);
        stage.KernelComplexity = KernelComplexity.Medium;
        stage.SupportedBackends = ["CUDA", "CPU"];


        if (node.Arguments.Count > 1)
        {
            var selector = ExtractLambdaExpression(node.Arguments[1]);
            stage.Parameters.Add("selector", selector);
        }


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitMaxExpression(MethodCallExpression node)
    {
        var stage = CreateStage("MaxKernel", PipelineStageType.Reduction);
        stage.KernelComplexity = KernelComplexity.Medium;
        stage.SupportedBackends = ["CUDA", "CPU"];


        if (node.Arguments.Count > 1)
        {
            var selector = ExtractLambdaExpression(node.Arguments[1]);
            stage.Parameters.Add("selector", selector);
        }


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitOrderByExpression(MethodCallExpression node, bool descending = false)
    {
        var stage = CreateStage("OrderByKernel", PipelineStageType.Sorting);
        var keySelector = ExtractLambdaExpression(node.Arguments[1]);


        stage.Parameters.Add("keySelector", keySelector);
        stage.Parameters.Add("descending", descending);
        stage.KernelComplexity = KernelComplexity.High; // Sorting is complex
        stage.SupportedBackends = ["CUDA"]; // GPU-optimized sorting
        stage.RequiredMemory = EstimateSortingMemory();


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitTakeExpression(MethodCallExpression node)
    {
        var stage = CreateStage("TakeKernel", PipelineStageType.Limiting);
        var count = ExtractConstantValue<int>(node.Arguments[1]);


        stage.Parameters.Add("count", count);
        stage.KernelComplexity = KernelComplexity.Low;
        stage.SupportedBackends = ["CUDA", "CPU"];


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitSkipExpression(MethodCallExpression node)
    {
        var stage = CreateStage("SkipKernel", PipelineStageType.Limiting);
        var count = ExtractConstantValue<int>(node.Arguments[1]);


        stage.Parameters.Add("count", count);
        stage.KernelComplexity = KernelComplexity.Low;
        stage.SupportedBackends = ["CUDA", "CPU"];


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitDistinctExpression(MethodCallExpression node)
    {
        var stage = CreateStage("DistinctKernel", PipelineStageType.Deduplication);
        stage.KernelComplexity = KernelComplexity.High;
        stage.SupportedBackends = ["CUDA"]; // Hash-based distinct on GPU
        stage.RequiredMemory = EstimateDistinctMemory();


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit source


        return node;
    }

    private Expression VisitUnionExpression(MethodCallExpression node)
    {
        var stage = CreateStage("UnionKernel", PipelineStageType.SetOperation);
        stage.KernelComplexity = KernelComplexity.Medium;
        stage.SupportedBackends = ["CUDA", "CPU"];


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit first source
        Visit(node.Arguments[1]); // Visit second source


        return node;
    }

    private Expression VisitIntersectExpression(MethodCallExpression node)
    {
        var stage = CreateStage("IntersectKernel", PipelineStageType.SetOperation);
        stage.KernelComplexity = KernelComplexity.High;
        stage.SupportedBackends = ["CUDA"]; // Hash-based intersection


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit first source
        Visit(node.Arguments[1]); // Visit second source


        return node;
    }

    private Expression VisitExceptExpression(MethodCallExpression node)
    {
        var stage = CreateStage("ExceptKernel", PipelineStageType.SetOperation);
        stage.KernelComplexity = KernelComplexity.High;
        stage.SupportedBackends = ["CUDA"]; // Hash-based except


        AddStage(stage);
        Visit(node.Arguments[0]); // Visit first source
        Visit(node.Arguments[1]); // Visit second source


        return node;
    }

    private Expression VisitGenericMethodCall(MethodCallExpression node)
    {
        _logger.LogWarning("Encountered unsupported method call: {Method}", node.Method.Name);


        var stage = CreateStage($"CustomKernel_{node.Method.Name}", PipelineStageType.Custom);
        stage.KernelComplexity = KernelComplexity.Unknown;
        stage.SupportedBackends = ["CPU"]; // Default to CPU for unknown operations
        stage.OptimizationHints.Add($"Custom implementation needed for {node.Method.Name}");


        AddStage(stage);

        // Visit all arguments

        foreach (var argument in node.Arguments)
        {
            Visit(argument);
        }


        return node;
    }

    #endregion

    #region Helper Methods

    private PipelineStageInfo CreateStage(string kernelName, PipelineStageType stageType)
    {
        return new PipelineStageInfo
        {
            StageId = ++_stageCounter,
            KernelName = kernelName,
            StageType = stageType,
            Parameters = new Dictionary<string, object>(),
            OptimizationHints = new List<string>(),
            Dependencies = new List<int>(),
            EstimatedExecutionTime = TimeSpan.Zero,
            RequiredMemory = 0,
            KernelComplexity = KernelComplexity.Unknown,
            SupportedBackends = Array.Empty<string>(),
            CanRunInParallel = false
        };
    }

    private void AddStage(PipelineStageInfo stage)
    {
        // Set dependency on previous stage (if exists)
        if (_stages.Count > 0 && !stage.CanRunInParallel)
        {
            stage.Dependencies.Add(_stages.Last().StageId);
        }


        _stages.Add(stage);
        _optimizationContext.AnalyzeStage(stage);
    }

    private LambdaExpression ExtractLambdaExpression(Expression expression)
    {
        return expression switch
        {
            LambdaExpression lambda => lambda,
            UnaryExpression { NodeType: ExpressionType.Quote } unary => (LambdaExpression)unary.Operand,
            _ => throw new ArgumentException($"Expected lambda expression, got {expression.GetType()}")
        };
    }

    private T ExtractConstantValue<T>(Expression expression)
    {
        if (expression is ConstantExpression constant && constant.Value is T value)
        {
            return value;
        }


        throw new ArgumentException($"Expected constant of type {typeof(T)}, got {expression.GetType()}");
    }

    private KernelComplexity AnalyzeLambdaComplexity(LambdaExpression lambda)
    {
        // Simple heuristic based on expression tree complexity
        var visitor = new ComplexityAnalysisVisitor();
        visitor.Visit(lambda.Body);


        return visitor.GetComplexity();
    }

    private string[] DetermineSelectorBackends(LambdaExpression selector)
    {
        // Analyze selector to determine which backends can handle it
        var analyzer = new BackendCompatibilityAnalyzer();
        return analyzer.AnalyzeSelector(selector);
    }

    private bool IsComplexProjection(LambdaExpression selector)
    {
        // Check if the projection involves complex operations
        return selector.Body.NodeType switch
        {
            ExpressionType.New => true,
            ExpressionType.MemberInit => true,
            ExpressionType.Call => true,
            _ => false
        };
    }

    private long EstimateGroupingMemory(LambdaExpression keySelector)
    {
        // Rough estimate based on key type and expected cardinality
        return 64 * 1024 * 1024; // 64MB default for grouping operations
    }

    private long EstimateSortingMemory()
    {
        return 32 * 1024 * 1024; // 32MB default for sorting operations
    }

    private long EstimateDistinctMemory()
    {
        return 16 * 1024 * 1024; // 16MB default for distinct operations
    }

    private int CalculateComplexity(List<PipelineStageInfo> stages)
    {
        return stages.Sum(stage => (int)stage.KernelComplexity);
    }

    private List<ParallelizationOpportunity> IdentifyParallelizationOpportunities(List<PipelineStageInfo> stages)
    {
        var opportunities = new List<ParallelizationOpportunity>();

        // Look for stages that can run in parallel

        for (int i = 0; i < stages.Count - 1; i++)
        {
            var current = stages[i];
            var next = stages[i + 1];


            if (current.CanRunInParallel || next.Dependencies.Count == 0)
            {
                opportunities.Add(new ParallelizationOpportunity
                {
                    StageIds = [current.StageId, next.StageId],
                    EstimatedSpeedup = 1.8, // Conservative estimate
                    Description = $"Parallel execution of {current.KernelName} and {next.KernelName}"
                });
            }
        }


        return opportunities;
    }

    #endregion
}

/// <summary>
/// Visitor for analyzing expression complexity.
/// </summary>
internal class ComplexityAnalysisVisitor : ExpressionVisitor
{
    private int _complexity = 0;

    public KernelComplexity GetComplexity()
    {
        return _complexity switch
        {
            <= 5 => KernelComplexity.Low,
            <= 15 => KernelComplexity.Medium,
            <= 30 => KernelComplexity.High,
            _ => KernelComplexity.VeryHigh
        };
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        _complexity += 3; // Method calls add complexity
        return base.VisitMethodCall(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _complexity += 1; // Binary operations are simple
        return base.VisitBinary(node);
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        _complexity += 1; // Unary operations are simple
        return base.VisitUnary(node);
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        _complexity += 2; // Conditionals add moderate complexity
        return base.VisitConditional(node);
    }
}

/// <summary>
/// Analyzer for determining backend compatibility.
/// </summary>
internal class BackendCompatibilityAnalyzer
{
    public string[] AnalyzeSelector(LambdaExpression selector)
    {
        // Analyze the selector to determine backend compatibility
        var supportsCuda = true;
        var supportsCpu = true;

        // Check for operations that might not be supported on GPU

        var visitor = new BackendCompatibilityVisitor();
        visitor.Visit(selector.Body);


        if (!visitor.SupportsGpu)
        {
            supportsCuda = false;
        }


        var backends = new List<string>();
        if (supportsCpu)
        {
            backends.Add("CPU");
        }


        if (supportsCuda)
        {
            backends.Add("CUDA");
        }


        return backends.ToArray();
    }
}

/// <summary>
/// Visitor for analyzing backend compatibility.
/// </summary>
internal class BackendCompatibilityVisitor : ExpressionVisitor
{
    public bool SupportsGpu { get; private set; } = true;

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Check for GPU-incompatible method calls
        var method = node.Method;
        if (method.DeclaringType == typeof(string) ||

            method.DeclaringType == typeof(DateTime) ||
            method.DeclaringType == typeof(TimeSpan))
        {
            SupportsGpu = false; // String and DateTime operations often not GPU-friendly
        }


        return base.VisitMethodCall(node);
    }
}