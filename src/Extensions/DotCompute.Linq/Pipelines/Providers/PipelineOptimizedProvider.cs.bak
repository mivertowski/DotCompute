// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Pipelines.Analysis;
using DotCompute.Linq.Pipelines.Models;
using DotCompute.Linq.Pipelines.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CorePipelineMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IPipelineMetrics;
using KernelPipelineBuilder = DotCompute.Abstractions.Pipelines.IKernelPipelineBuilder;
using IKernelPipeline = DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline;
using LinqKernelPipeline = DotCompute.Linq.Pipelines.Interfaces.IKernelPipeline;
namespace DotCompute.Linq.Pipelines.Providers;
/// <summary>
/// LINQ query provider that converts LINQ expressions to optimized kernel pipelines.
/// Provides intelligent backend selection and performance optimization.
/// </summary>
public class PipelineOptimizedProvider : IQueryProvider
{
    private readonly KernelPipelineBuilder _chainBuilder;
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
        KernelPipelineBuilder chainBuilder,
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
        _logger.LogDebug("Creating query from expression: {Expression}", expression);
        var elementType = GetElementType(expression.Type);
        var queryableType = typeof(PipelineOptimizedQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        _logger.LogDebug("Creating typed query for {ElementType}: {Expression}",
            typeof(TElement), expression);
        return new PipelineOptimizedQueryable<TElement>(this, expression);
    public object? Execute(Expression expression)
        _logger.LogDebug("Executing expression synchronously: {Expression}", expression);
        return ExecuteAsync<object>(expression).GetAwaiter().GetResult();
    public TResult Execute<TResult>(Expression expression)
        _logger.LogDebug("Executing typed expression synchronously for {ResultType}: {Expression}",
            typeof(TResult), expression);
        return ExecuteAsync<TResult>(expression).GetAwaiter().GetResult();
    /// Executes a LINQ expression asynchronously by converting it to an optimized kernel pipeline.
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="expression">The LINQ expression to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TResult> ExecuteAsync<TResult>(
        Expression expression,
        CancellationToken cancellationToken = default)
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
            _logger.LogError(ex, "Pipeline execution failed for expression: {Expression}", expression);
            throw new PipelineExecutionException($"Failed to execute pipeline: {ex.Message}", ex);
    /// Creates an optimized pipeline from a LINQ expression with intelligent backend selection.
    /// <param name="expression">The LINQ expression to convert</param>
    /// <param name="analysisResult">Results from expression analysis</param>
    /// <returns>An optimized kernel pipeline</returns>
    private async Task<IKernelPipeline> CreateOptimizedPipelineAsync(
        ExpressionAnalysisResult analysisResult)
        // Get the execution plan
        var executionPlan = await _expressionAnalyzer.ConvertToPipelinePlanAsync(expression);
        // Create pipeline with appropriate configuration
        var pipelineConfig = CreatePipelineConfiguration(executionPlan, analysisResult);
        var pipeline = _chainBuilder.Create();
        // Add stages to the pipeline
        pipeline = await AddStagesToPipelineAsync((DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline)pipeline, executionPlan.Stages);
        // Apply optimizations based on analysis results
        pipeline = await ApplyOptimizationsAsync((DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline)pipeline, analysisResult);
        return (DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline)pipeline;
    /// Creates pipeline configuration based on execution plan and analysis results.
    /// <param name="executionPlan">The pipeline execution plan</param>
    /// <param name="analysisResult">Expression analysis results</param>
    /// <returns>Pipeline configuration</returns>
    private IPipelineConfiguration CreatePipelineConfiguration(
        PipelineExecutionPlan executionPlan,
        return new DefaultPipelineConfiguration
            PreferredBackend = SelectOptimalBackend(analysisResult),
            EnableCaching = ShouldEnableCaching(executionPlan),
            EnableProfiling = _logger.IsEnabled(LogLevel.Debug),
            MaxMemoryUsage = CalculateMemoryLimit(analysisResult),
            TimeoutSeconds = CalculateTimeout(executionPlan),
            OptimizationLevel = SelectOptimizationLevel(analysisResult)
        };
    /// Adds stages to the pipeline based on the execution plan.
    /// <param name="pipeline">The pipeline to add stages to</param>
    /// <param name="stages">The stages to add</param>
    /// <returns>Pipeline with added stages</returns>
    private async Task<IKernelPipeline> AddStagesToPipelineAsync(
        IKernelPipeline pipeline,
        List<PipelineStageInfo> stages)
        var currentPipeline = pipeline;
        foreach (var stage in stages.OrderBy(s => s.StageId))
            currentPipeline = await AddStageAsync(currentPipeline, stage);
        return currentPipeline;
    /// Adds a single stage to the pipeline.
    /// <param name="pipeline">The pipeline to add the stage to</param>
    /// <param name="stage">The stage information</param>
    /// <returns>Pipeline with the added stage</returns>
    private async Task<IKernelPipeline> AddStageAsync(IKernelPipeline pipeline, PipelineStageInfo stage)
        var stageOptions = new PipelineStageOptions
            PreferredBackend = SelectBackendForStage(stage),
            EnableProfiling = true,
            EnableCaching = stage.KernelComplexity >= KernelComplexity.High,
            TimeoutMs = (int)stage.EstimatedExecutionTime.TotalMilliseconds + 1000 // Add 1s buffer
        // Convert stage parameters to objects array
        var parameters = ConvertStageParameters(stage);
        // Note: The Then() method is not implemented in the current IKernelPipeline interface
        // This is a simplified version that returns the original pipeline
        return await Task.FromResult(pipeline);
    /// Applies optimizations to the pipeline based on analysis results.
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="analysisResult">Analysis results guiding optimization</param>
    /// <returns>Optimized pipeline</returns>
    private async Task<IKernelPipeline> ApplyOptimizationsAsync(
        var optimizedPipeline = pipeline;
        // Note: Caching is not implemented in current IKernelPipeline interface
        // This would apply caching if the interface supported it
        // Apply optimization strategy based on complexity
        var optimizationStrategy = analysisResult.ComplexityScore switch
            <= 5 => OptimizationStrategy.Conservative,
            <= 15 => OptimizationStrategy.Balanced,
            <= 30 => OptimizationStrategy.Aggressive,
            _ => OptimizationStrategy.Adaptive
        // Note: The Optimize() method is not implemented in the current IKernelPipeline interface
        // Note: The Retry() method is not implemented in the current IKernelPipeline interface
        return await Task.FromResult(optimizedPipeline);
    /// Executes the pipeline with appropriate backend and error handling.
    /// <param name="pipeline">The pipeline to execute</param>
    /// <param name="analysisResult">Analysis results</param>
    /// <returns>Execution result</returns>
    private async Task<TResult> ExecutePipelineAsync<TResult>(
        ExpressionAnalysisResult analysisResult,
        CancellationToken cancellationToken)
        // Note: The Timeout() method is not implemented in the current IKernelPipeline interface
        var pipelineWithTimeout = pipeline;
        // Cast to LINQ pipeline interface if needed, or use Core interface directly
        if (pipelineWithTimeout is LinqKernelPipeline linqPipeline)
            var result = await linqPipeline.ExecuteAsync<TResult>(cancellationToken);
        // Fallback using Core pipeline interface with extension method
        // Convert pipeline to executable expression
        var pipelineExpression = ConvertPipelineToExpression(pipelineWithTimeout);
        var coreResult = await ExecuteAsync<TResult>(pipelineExpression, cancellationToken);
        return coreResult ?? throw new InvalidOperationException("Pipeline execution returned null result");
    #region Helper Methods
    private static Type GetElementType(Type type)
        if (type.IsGenericType)
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length > 0)
            {
                return genericArgs[0];
            }
        return typeof(object);
    private string SelectOptimalBackend(ExpressionAnalysisResult analysisResult)
        return analysisResult switch
            { IsGpuCompatible: true, ComplexityScore: > 10 } => "CUDA",
            { IsGpuCompatible: true, ParallelizationPotential: > 5 } => "CUDA",
            { IsCpuCompatible: true } => "CPU",
            _ => "CPU"
    private static bool ShouldEnableCaching(PipelineExecutionPlan executionPlan)
        return executionPlan.EstimatedComplexity > 15 ||
               executionPlan.EstimatedExecutionTime > TimeSpan.FromSeconds(1);
    private static long CalculateMemoryLimit(ExpressionAnalysisResult analysisResult)
        // Set memory limit based on estimated requirements
        return Math.Max(analysisResult.MemoryRequirement * 2, 64 * 1024 * 1024); // At least 64MB
    private static int CalculateTimeout(PipelineExecutionPlan executionPlan)
        // Calculate timeout with buffer
        var baseTimeout = (int)executionPlan.EstimatedExecutionTime.TotalSeconds;
        return Math.Max(baseTimeout * 3, 30); // At least 30 seconds
    private static OptimizationLevel SelectOptimizationLevel(ExpressionAnalysisResult analysisResult)
        return analysisResult.ComplexityScore switch
            <= 5 => OptimizationLevel.Conservative,
            <= 15 => OptimizationLevel.Balanced,
            <= 30 => OptimizationLevel.Aggressive,
            _ => OptimizationLevel.Adaptive
    private static string SelectBackendForStage(PipelineStageInfo stage)
        if (stage.SupportedBackends.Contains("CUDA") && stage.KernelComplexity >= KernelComplexity.Medium)
            return "CUDA";
        return stage.SupportedBackends.FirstOrDefault() ?? "CPU";
    private static object[] ConvertStageParameters(PipelineStageInfo stage)
        return stage.Parameters.Values.ToArray();
    private IPipelineExecutionContext CreateExecutionContext(ExpressionAnalysisResult analysisResult)
        return new DefaultPipelineExecutionContext
            EnableDetailedMetrics = true,
            TrackMemoryUsage = analysisResult.MemoryRequirement > 100 * 1024 * 1024, // Track if > 100MB
            CollectTimingData = true
    private void LogExecutionMetrics(CorePipelineMetrics metrics)
        _logger.LogInformation(
            "Pipeline executed - Duration: {Duration}ms, Memory: {Memory}MB, Stages: {Stages}",
            metrics.TotalExecutionTime.TotalMilliseconds,
            metrics.PeakMemoryUsage / (1024.0 * 1024.0),
            metrics.StageCount);
    /// Converts a pipeline to an executable expression for compatibility.
    private static Expression ConvertPipelineToExpression(DotCompute.Abstractions.Interfaces.Pipelines.IKernelPipeline pipeline)
        // Create a constant expression representing the pipeline
        // This is a simplified conversion - in practice, you might want more sophisticated conversion
        return Expression.Constant(pipeline);
    #endregion
}
/// Queryable implementation that uses the pipeline-optimized provider.
/// <typeparam name="T">Element type</typeparam>
public class PipelineOptimizedQueryable<T> : IQueryable<T>, IAsyncEnumerable<T>
    /// Initializes a new instance of the PipelineOptimizedQueryable class.
    /// <param name="provider">The query provider</param>
    /// <param name="expression">The query expression</param>
    public PipelineOptimizedQueryable(IQueryProvider provider, Expression expression)
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    public Type ElementType => typeof(T);
    public Expression Expression { get; }
    public IQueryProvider Provider { get; }
    public IEnumerator<T> GetEnumerator()
        var result = Provider.Execute<IEnumerable<T>>(Expression);
        return result.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        return GetEnumerator();
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        if (Provider is PipelineOptimizedProvider pipelineProvider)
            var results = await pipelineProvider.ExecuteAsync<IEnumerable<T>>(Expression, cancellationToken);
            foreach (var item in results)
                yield return item;
        else
            var results = Provider.Execute<IEnumerable<T>>(Expression);
    /// Executes the queryable asynchronously as a pipeline.
    /// <returns>The execution results</returns>
    public async Task<IEnumerable<T>> ExecuteAsync(CancellationToken cancellationToken = default)
            return await pipelineProvider.ExecuteAsync<IEnumerable<T>>(Expression, cancellationToken);
        return await Task.FromResult(Provider.Execute<IEnumerable<T>>(Expression));
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
