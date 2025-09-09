// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Pipelines.Providers;

/// <summary>
/// LINQ query provider that converts LINQ expressions to optimized kernel pipelines.
/// Provides intelligent backend selection and performance optimization.
/// </summary>
public class PipelineOptimizedProvider : IQueryProvider
{
    private readonly IKernelPipelineBuilder _chainBuilder;
    private readonly IServiceProvider _serviceProvider;
    private readonly IPipelineExpressionAnalyzer _expressionAnalyzer;
    private readonly ILogger<PipelineOptimizedProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the PipelineOptimizedProvider class.
    /// </summary>
    /// <param name="chainBuilder">Kernel pipeline builder for creating execution pipelines</param>
    /// <param name="serviceProvider">Service provider for dependency injection</param>
    /// <param name="logger">Logger for diagnostics</param>
    public PipelineOptimizedProvider(
        IKernelPipelineBuilder chainBuilder,
        IServiceProvider serviceProvider,
        ILogger<PipelineOptimizedProvider> logger)
    {
        _chainBuilder = chainBuilder ?? throw new ArgumentNullException(nameof(chainBuilder));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _expressionAnalyzer = serviceProvider.GetService<IPipelineExpressionAnalyzer>() 
            ?? new PipelineExpressionAnalyzer(serviceProvider);
    }

    /// <inheritdoc />
    public IQueryable CreateQuery(Expression expression)
    {
        _logger.LogDebug("Creating query from expression: {Expression}", expression);
        
        var elementType = GetElementType(expression.Type);
        var queryableType = typeof(PipelineOptimizedQueryable<>).MakeGenericType(elementType);
        
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }

    /// <inheritdoc />
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        _logger.LogDebug("Creating typed query for {ElementType}: {Expression}", 
            typeof(TElement), expression);
            
        return new PipelineOptimizedQueryable<TElement>(this, expression);
    }

    /// <inheritdoc />
    public object? Execute(Expression expression)
    {
        _logger.LogDebug("Executing expression synchronously: {Expression}", expression);
        
        return ExecuteAsync<object>(expression).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public TResult Execute<TResult>(Expression expression)
    {
        _logger.LogDebug("Executing typed expression synchronously for {ResultType}: {Expression}", 
            typeof(TResult), expression);
            
        return ExecuteAsync<TResult>(expression).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes a LINQ expression asynchronously by converting it to an optimized kernel pipeline.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="expression">The LINQ expression to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TResult> ExecuteAsync<TResult>(
        Expression expression, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Converting LINQ expression to pipeline for async execution");

            // Analyze the expression for optimization opportunities
            var analysisResult = await _expressionAnalyzer.AnalyzeCompatibilityAsync(expression);
            
            // Create optimized pipeline from the expression
            var pipeline = await CreateOptimizedPipelineAsync(expression, analysisResult);
            
            // Execute the pipeline with appropriate backend selection
            var result = await ExecutePipelineAsync<TResult>(pipeline, analysisResult, cancellationToken);
            
            _logger.LogInformation("Pipeline execution completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline execution failed for expression: {Expression}", expression);
            throw new PipelineExecutionException($"Failed to execute pipeline: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates an optimized pipeline from a LINQ expression with intelligent backend selection.
    /// </summary>
    /// <param name="expression">The LINQ expression to convert</param>
    /// <param name="analysisResult">Results from expression analysis</param>
    /// <returns>An optimized kernel pipeline</returns>
    private async Task<IKernelPipeline> CreateOptimizedPipelineAsync(
        Expression expression, 
        ExpressionAnalysisResult analysisResult)
    {
        // Get the execution plan
        var executionPlan = await _expressionAnalyzer.ConvertToPipelinePlanAsync(expression);
        
        // Create pipeline with appropriate configuration
        var pipelineConfig = CreatePipelineConfiguration(executionPlan, analysisResult);
        var pipeline = _chainBuilder.Create(pipelineConfig);
        
        // Add stages to the pipeline
        pipeline = await AddStagesToPipelineAsync(pipeline, executionPlan.Stages);
        
        // Apply optimizations based on analysis results
        pipeline = await ApplyOptimizationsAsync(pipeline, analysisResult);
        
        return pipeline;
    }

    /// <summary>
    /// Creates pipeline configuration based on execution plan and analysis results.
    /// </summary>
    /// <param name="executionPlan">The pipeline execution plan</param>
    /// <param name="analysisResult">Expression analysis results</param>
    /// <returns>Pipeline configuration</returns>
    private IPipelineConfiguration CreatePipelineConfiguration(
        PipelineExecutionPlan executionPlan, 
        ExpressionAnalysisResult analysisResult)
    {
        return new DefaultPipelineConfiguration
        {
            PreferredBackend = SelectOptimalBackend(analysisResult),
            EnableCaching = ShouldEnableCaching(executionPlan),
            EnableProfiling = _logger.IsEnabled(LogLevel.Debug),
            MaxMemoryUsage = CalculateMemoryLimit(analysisResult),
            TimeoutSeconds = CalculateTimeout(executionPlan),
            OptimizationLevel = SelectOptimizationLevel(analysisResult)
        };
    }

    /// <summary>
    /// Adds stages to the pipeline based on the execution plan.
    /// </summary>
    /// <param name="pipeline">The pipeline to add stages to</param>
    /// <param name="stages">The stages to add</param>
    /// <returns>Pipeline with added stages</returns>
    private async Task<IKernelPipeline> AddStagesToPipelineAsync(
        IKernelPipeline pipeline, 
        List<PipelineStageInfo> stages)
    {
        var currentPipeline = pipeline;
        
        foreach (var stage in stages.OrderBy(s => s.StageId))
        {
            currentPipeline = await AddStageAsync(currentPipeline, stage);
        }
        
        return currentPipeline;
    }

    /// <summary>
    /// Adds a single stage to the pipeline.
    /// </summary>
    /// <param name="pipeline">The pipeline to add the stage to</param>
    /// <param name="stage">The stage information</param>
    /// <returns>Pipeline with the added stage</returns>
    private async Task<IKernelPipeline> AddStageAsync(IKernelPipeline pipeline, PipelineStageInfo stage)
    {
        var stageOptions = new PipelineStageOptions
        {
            PreferredBackend = SelectBackendForStage(stage),
            EnableProfiling = true,
            EnableCaching = stage.KernelComplexity >= KernelComplexity.High,
            TimeoutMs = (int)stage.EstimatedExecutionTime.TotalMilliseconds + 1000 // Add 1s buffer
        };

        // Convert stage parameters to objects array
        var parameters = ConvertStageParameters(stage);

        return await Task.FromResult(pipeline.Then<object, object>(
            stage.KernelName, 
            input => parameters,
            stageOptions));
    }

    /// <summary>
    /// Applies optimizations to the pipeline based on analysis results.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="analysisResult">Analysis results guiding optimization</param>
    /// <returns>Optimized pipeline</returns>
    private async Task<IKernelPipeline> ApplyOptimizationsAsync(
        IKernelPipeline pipeline, 
        ExpressionAnalysisResult analysisResult)
    {
        var optimizedPipeline = pipeline;

        // Apply caching if beneficial
        if (analysisResult.ComplexityScore > 10)
        {
            optimizedPipeline = optimizedPipeline.AdaptiveCache(new AdaptiveCacheOptions
            {
                AutoKeyGeneration = true,
                PolicyAdaptation = true,
                PerformanceThreshold = 0.15 // Cache if 15% improvement
            });
        }

        // Apply optimization strategy based on complexity
        var optimizationStrategy = analysisResult.ComplexityScore switch
        {
            <= 5 => OptimizationStrategy.Conservative,
            <= 15 => OptimizationStrategy.Balanced,
            <= 30 => OptimizationStrategy.Aggressive,
            _ => OptimizationStrategy.Adaptive
        };

        optimizedPipeline = optimizedPipeline.Optimize(optimizationStrategy);

        // Add error handling for complex pipelines
        if (analysisResult.ComplexityScore > 20)
        {
            optimizedPipeline = optimizedPipeline.Retry(
                maxAttempts: 3,
                delay: TimeSpan.FromMilliseconds(100));
        }

        return await Task.FromResult(optimizedPipeline);
    }

    /// <summary>
    /// Executes the pipeline with appropriate backend and error handling.
    /// </summary>
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="pipeline">The pipeline to execute</param>
    /// <param name="analysisResult">Analysis results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result</returns>
    private async Task<TResult> ExecutePipelineAsync<TResult>(
        IKernelPipeline pipeline, 
        ExpressionAnalysisResult analysisResult,
        CancellationToken cancellationToken)
    {
        // Add timeout based on complexity
        var timeout = TimeSpan.FromSeconds(Math.Max(30, analysisResult.ComplexityScore));
        var pipelineWithTimeout = pipeline.Timeout(timeout);

        // Execute with context for detailed monitoring
        var executionContext = CreateExecutionContext(analysisResult);
        var detailedResult = await pipelineWithTimeout.ExecuteWithContextAsync<TResult>(
            executionContext, 
            cancellationToken);

        // Log execution metrics
        LogExecutionMetrics(detailedResult.Metrics);

        return detailedResult.Result;
    }

    #region Helper Methods

    private static Type GetElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return genericArgs[0];
            }
        }

        return typeof(object);
    }

    private string SelectOptimalBackend(ExpressionAnalysisResult analysisResult)
    {
        return analysisResult switch
        {
            { IsGpuCompatible: true, ComplexityScore: > 10 } => "CUDA",
            { IsGpuCompatible: true, ParallelizationPotential: > 5 } => "CUDA",
            { IsCpuCompatible: true } => "CPU",
            _ => "CPU"
        };
    }

    private static bool ShouldEnableCaching(PipelineExecutionPlan executionPlan)
    {
        return executionPlan.EstimatedComplexity > 15 || 
               executionPlan.EstimatedExecutionTime > TimeSpan.FromSeconds(1);
    }

    private static long CalculateMemoryLimit(ExpressionAnalysisResult analysisResult)
    {
        // Set memory limit based on estimated requirements
        return Math.Max(analysisResult.MemoryRequirement * 2, 64 * 1024 * 1024); // At least 64MB
    }

    private static int CalculateTimeout(PipelineExecutionPlan executionPlan)
    {
        // Calculate timeout with buffer
        var baseTimeout = (int)executionPlan.EstimatedExecutionTime.TotalSeconds;
        return Math.Max(baseTimeout * 3, 30); // At least 30 seconds
    }

    private static OptimizationLevel SelectOptimizationLevel(ExpressionAnalysisResult analysisResult)
    {
        return analysisResult.ComplexityScore switch
        {
            <= 5 => OptimizationLevel.Conservative,
            <= 15 => OptimizationLevel.Balanced,
            <= 30 => OptimizationLevel.Aggressive,
            _ => OptimizationLevel.Adaptive
        };
    }

    private static string SelectBackendForStage(PipelineStageInfo stage)
    {
        if (stage.SupportedBackends.Contains("CUDA") && stage.KernelComplexity >= KernelComplexity.Medium)
        {
            return "CUDA";
        }

        return stage.SupportedBackends.FirstOrDefault() ?? "CPU";
    }

    private static object[] ConvertStageParameters(PipelineStageInfo stage)
    {
        return stage.Parameters.Values.ToArray();
    }

    private IPipelineExecutionContext CreateExecutionContext(ExpressionAnalysisResult analysisResult)
    {
        return new DefaultPipelineExecutionContext
        {
            EnableDetailedMetrics = true,
            EnableProfiling = _logger.IsEnabled(LogLevel.Debug),
            TrackMemoryUsage = analysisResult.MemoryRequirement > 100 * 1024 * 1024, // Track if > 100MB
            CollectTimingData = true
        };
    }

    private void LogExecutionMetrics(IPipelineMetrics metrics)
    {
        _logger.LogInformation(
            "Pipeline executed - Duration: {Duration}ms, Memory: {Memory}MB, Stages: {Stages}",
            metrics.TotalExecutionTime.TotalMilliseconds,
            metrics.PeakMemoryUsage / (1024.0 * 1024.0),
            metrics.StageCount);
    }

    #endregion
}

/// <summary>
/// Queryable implementation that uses the pipeline-optimized provider.
/// </summary>
/// <typeparam name="T">Element type</typeparam>
public class PipelineOptimizedQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>
{
    /// <summary>
    /// Initializes a new instance of the PipelineOptimizedQueryable class.
    /// </summary>
    /// <param name="provider">The query provider</param>
    /// <param name="expression">The query expression</param>
    public PipelineOptimizedQueryable(IQueryProvider provider, Expression expression)
    {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    /// <inheritdoc />
    public Type ElementType => typeof(T);

    /// <inheritdoc />
    public Expression Expression { get; }

    /// <inheritdoc />
    public IQueryProvider Provider { get; }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        var result = Provider.Execute<IEnumerable<T>>(Expression);
        return result.GetEnumerator();
    }

    /// <inheritdoc />
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <inheritdoc />
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Provider is PipelineOptimizedProvider pipelineProvider)
        {
            var results = await pipelineProvider.ExecuteAsync<IEnumerable<T>>(Expression, cancellationToken);
            foreach (var item in results)
            {
                yield return item;
            }
        }
        else
        {
            var results = Provider.Execute<IEnumerable<T>>(Expression);
            foreach (var item in results)
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Executes the queryable asynchronously as a pipeline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution results</returns>
    public async Task<IEnumerable<T>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (Provider is PipelineOptimizedProvider pipelineProvider)
        {
            return await pipelineProvider.ExecuteAsync<IEnumerable<T>>(Expression, cancellationToken);
        }

        return await Task.FromResult(Provider.Execute<IEnumerable<T>>(Expression));
    }
}

/// <summary>
/// Exception thrown when pipeline execution fails.
/// </summary>
public class PipelineExecutionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the PipelineExecutionException class.
    /// </summary>
    /// <param name="message">Exception message</param>
    public PipelineExecutionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the PipelineExecutionException class.
    /// </summary>
    /// <param name="message">Exception message</param>
    /// <param name="innerException">Inner exception</param>
    public PipelineExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

// Placeholder implementations for missing interfaces
internal class DefaultPipelineConfiguration : IPipelineConfiguration
{
    public string PreferredBackend { get; set; } = "CPU";
    public bool EnableCaching { get; set; }
    public bool EnableProfiling { get; set; }
    public long MaxMemoryUsage { get; set; }
    public int TimeoutSeconds { get; set; }
    public OptimizationLevel OptimizationLevel { get; set; }
}

internal class DefaultPipelineExecutionContext : IPipelineExecutionContext
{
    public bool EnableDetailedMetrics { get; set; }
    public bool EnableProfiling { get; set; }
    public bool TrackMemoryUsage { get; set; }
    public bool CollectTimingData { get; set; }
}

// Extension to buffer async enumerable
public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T[]> Buffer<T>(this IAsyncEnumerable<T> source, int count)
    {
        var buffer = new List<T>(count);
        
        await foreach (var item in source)
        {
            buffer.Add(item);
            
            if (buffer.Count >= count)
            {
                yield return buffer.ToArray();
                buffer.Clear();
            }
        }
        
        if (buffer.Count > 0)
        {
            yield return buffer.ToArray();
        }
    }
}