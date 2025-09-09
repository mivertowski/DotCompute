// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;
using System.ComponentModel;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Represents a pipeline of interconnected kernel operations with advanced execution capabilities.
/// Provides fluent API for chaining kernels with sequential, parallel, conditional, and hybrid execution patterns.
/// </summary>
public interface IKernelPipeline : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this pipeline instance for monitoring and debugging.
    /// </summary>
    Guid PipelineId { get; }
    
    /// <summary>
    /// Gets the current execution state of the pipeline.
    /// </summary>
    PipelineState State { get; }
    
    /// <summary>
    /// Gets real-time performance statistics for this pipeline.
    /// </summary>
    IPipelineMetrics Metrics { get; }
    
    /// <summary>
    /// Gets the pipeline configuration and metadata.
    /// </summary>
    IPipelineConfiguration Configuration { get; }

    #region Sequential Chaining
    
    /// <summary>
    /// Chains a kernel to execute after the current pipeline stage with typed input/output.
    /// </summary>
    /// <typeparam name="TInput">The input type for the kernel</typeparam>
    /// <typeparam name="TOutput">The output type for the kernel</typeparam>
    /// <param name="kernelName">The name of the kernel to execute</param>
    /// <param name="parameterBuilder">Function to build kernel parameters from input</param>
    /// <param name="options">Optional stage-specific execution options</param>
    /// <returns>A new pipeline with the additional stage</returns>
    IKernelPipeline Then<TInput, TOutput>(
        string kernelName, 
        Func<TInput, object[]>? parameterBuilder = null,
        PipelineStageOptions? options = null);
        
    /// <summary>
    /// Chains a pre-configured kernel stage to execute after the current pipeline stage.
    /// </summary>
    /// <typeparam name="TOutput">The output type for the stage</typeparam>
    /// <param name="stage">The kernel stage to add</param>
    /// <returns>A new pipeline with the additional stage</returns>
    IKernelPipeline Then<TOutput>(IKernelStage<TOutput> stage);
    
    /// <summary>
    /// Chains multiple kernel stages to execute sequentially.
    /// </summary>
    /// <param name="stages">The stages to execute in sequence</param>
    /// <returns>A new pipeline with the additional stages</returns>
    IKernelPipeline ThenSequence(params IKernelStage[] stages);
    
    #endregion

    #region Parallel Execution
    
    /// <summary>
    /// Executes multiple kernel stages in parallel, each receiving the same input.
    /// </summary>
    /// <typeparam name="TInput">The input type for all parallel stages</typeparam>
    /// <typeparam name="TOutput">The output type for all parallel stages</typeparam>
    /// <param name="stages">The parallel stages with their parameter builders</param>
    /// <returns>A new pipeline that aggregates parallel results</returns>
    IKernelPipeline Parallel<TInput, TOutput>(
        params (string kernelName, Func<TInput, object[]>? parameterBuilder)[] stages);
        
    /// <summary>
    /// Executes multiple pre-configured kernel stages in parallel.
    /// </summary>
    /// <typeparam name="TOutput">The output type for all parallel stages</typeparam>
    /// <param name="stages">The parallel stages to execute</param>
    /// <returns>A new pipeline that aggregates parallel results</returns>
    IKernelPipeline Parallel<TOutput>(params IKernelStage<TOutput>[] stages);
    
    /// <summary>
    /// Executes stages in parallel with different input partitioning strategies.
    /// </summary>
    /// <typeparam name="TInput">The input type to partition</typeparam>
    /// <typeparam name="TOutput">The aggregated output type</typeparam>
    /// <param name="partitionStrategy">How to partition the input data</param>
    /// <param name="stages">The stages to execute on partitioned data</param>
    /// <returns>A new pipeline with partitioned parallel execution</returns>
    IKernelPipeline ParallelPartition<TInput, TOutput>(
        IPartitionStrategy<TInput> partitionStrategy,
        params IKernelStage<TOutput>[] stages);
    
    #endregion

    #region Conditional Branching
    
    /// <summary>
    /// Creates a conditional branch in the pipeline execution.
    /// </summary>
    /// <typeparam name="TInput">The input type for the condition</typeparam>
    /// <typeparam name="TOutput">The output type from both branches</typeparam>
    /// <param name="condition">The condition to evaluate for branching</param>
    /// <param name="truePath">Pipeline to execute if condition is true</param>
    /// <param name="falsePath">Pipeline to execute if condition is false</param>
    /// <returns>A new pipeline with conditional branching</returns>
    IKernelPipeline Branch<TInput, TOutput>(
        Func<TInput, bool> condition,
        IKernelPipeline truePath,
        IKernelPipeline falsePath);
        
    /// <summary>
    /// Creates a multi-way switch for pipeline execution based on a selector.
    /// </summary>
    /// <typeparam name="TInput">The input type for the selector</typeparam>
    /// <typeparam name="TOutput">The output type from all branches</typeparam>
    /// <param name="selector">Function to select the branch key</param>
    /// <param name="branches">Dictionary of branch keys to pipelines</param>
    /// <param name="defaultBranch">Default pipeline if no branch matches</param>
    /// <returns>A new pipeline with multi-way branching</returns>
    IKernelPipeline Switch<TInput, TOutput>(
        Func<TInput, string> selector,
        Dictionary<string, IKernelPipeline> branches,
        IKernelPipeline? defaultBranch = null);
    
    /// <summary>
    /// Creates a dynamic branch based on runtime analysis of the data.
    /// </summary>
    /// <typeparam name="TInput">The input type to analyze</typeparam>
    /// <typeparam name="TOutput">The output type from all branches</typeparam>
    /// <param name="analyzer">Analyzes input to select optimal execution path</param>
    /// <param name="pathSelector">Selects pipeline based on analysis results</param>
    /// <returns>A new pipeline with dynamic branching</returns>
    IKernelPipeline DynamicBranch<TInput, TOutput>(
        IDataAnalyzer<TInput> analyzer,
        Func<IAnalysisResult, IKernelPipeline> pathSelector);
    
    #endregion

    #region Merging and Aggregation
    
    /// <summary>
    /// Merges this pipeline with another pipeline, combining their outputs.
    /// </summary>
    /// <typeparam name="TInput">The input type for both pipelines</typeparam>
    /// <typeparam name="TOutput">The merged output type</typeparam>
    /// <param name="other">The other pipeline to merge with</param>
    /// <param name="mergeFunction">Function to combine outputs from both pipelines</param>
    /// <returns>A new pipeline that merges the two pipelines</returns>
    IKernelPipeline Merge<TInput, TOutput>(
        IKernelPipeline other,
        Func<TInput, TInput, TOutput> mergeFunction);
        
    /// <summary>
    /// Aggregates multiple pipeline outputs using a custom aggregation function.
    /// </summary>
    /// <typeparam name="TInput">The input element type to aggregate</typeparam>
    /// <typeparam name="TOutput">The aggregated output type</typeparam>
    /// <param name="aggregateFunction">Function to aggregate multiple inputs</param>
    /// <returns>A new pipeline with aggregation</returns>
    IKernelPipeline Aggregate<TInput, TOutput>(
        Func<IEnumerable<TInput>, TOutput> aggregateFunction);
    
    /// <summary>
    /// Reduces pipeline outputs using a binary reduction function.
    /// </summary>
    /// <typeparam name="T">The type to reduce</typeparam>
    /// <param name="reductionFunction">Binary function for reduction</param>
    /// <param name="seed">Initial value for reduction</param>
    /// <returns>A new pipeline with reduction</returns>
    IKernelPipeline Reduce<T>(
        Func<T, T, T> reductionFunction,
        T? seed = default);
    
    #endregion

    #region Caching and Optimization
    
    /// <summary>
    /// Adds result caching to the pipeline with configurable cache policy.
    /// </summary>
    /// <typeparam name="T">The type to cache</typeparam>
    /// <param name="cacheKey">Unique key for the cached results</param>
    /// <param name="policy">Cache policy for expiration and eviction</param>
    /// <returns>A new pipeline with caching enabled</returns>
    IKernelPipeline Cache<T>(
        string cacheKey,
        CachePolicy policy = CachePolicy.Default);
        
    /// <summary>
    /// Enables adaptive caching with automatic key generation and policy selection.
    /// </summary>
    /// <param name="options">Adaptive caching configuration options</param>
    /// <returns>A new pipeline with adaptive caching</returns>
    IKernelPipeline AdaptiveCache(AdaptiveCacheOptions? options = null);
    
    /// <summary>
    /// Applies optimization strategies to improve pipeline performance.
    /// </summary>
    /// <param name="strategy">The optimization strategy to apply</param>
    /// <returns>A new pipeline with optimizations applied</returns>
    IKernelPipeline Optimize(OptimizationStrategy strategy = OptimizationStrategy.Adaptive);
    
    /// <summary>
    /// Applies custom optimization using a pipeline optimizer.
    /// </summary>
    /// <param name="optimizer">Custom pipeline optimizer</param>
    /// <returns>A new pipeline with custom optimizations</returns>
    IKernelPipeline OptimizeWith(IPipelineOptimizer optimizer);
    
    #endregion

    #region Error Handling and Resilience
    
    /// <summary>
    /// Adds error handling with custom error recovery logic.
    /// </summary>
    /// <typeparam name="T">The type being processed when error occurs</typeparam>
    /// <param name="errorHandler">Function to handle errors and provide recovery</param>
    /// <returns>A new pipeline with error handling</returns>
    IKernelPipeline OnError<T>(Func<Exception, T, Task<T>> errorHandler);
        
    /// <summary>
    /// Adds retry logic with configurable retry policy.
    /// </summary>
    /// <param name="maxAttempts">Maximum number of retry attempts</param>
    /// <param name="delay">Delay between retry attempts</param>
    /// <param name="retryCondition">Condition to determine if retry should occur</param>
    /// <returns>A new pipeline with retry logic</returns>
    IKernelPipeline Retry(
        int maxAttempts = 3,
        TimeSpan? delay = null,
        Func<Exception, bool>? retryCondition = null);
    
    /// <summary>
    /// Adds circuit breaker pattern for fault tolerance.
    /// </summary>
    /// <param name="options">Circuit breaker configuration options</param>
    /// <returns>A new pipeline with circuit breaker protection</returns>
    IKernelPipeline CircuitBreaker(CircuitBreakerOptions options);
    
    /// <summary>
    /// Adds timeout handling for long-running operations.
    /// </summary>
    /// <param name="timeout">Maximum execution time before timeout</param>
    /// <param name="timeoutHandler">Optional custom timeout handling logic</param>
    /// <returns>A new pipeline with timeout protection</returns>
    IKernelPipeline Timeout(
        TimeSpan timeout,
        Func<TimeoutException, Task>? timeoutHandler = null);
    
    #endregion

    #region Execution
    
    /// <summary>
    /// Executes the pipeline and returns the final result.
    /// </summary>
    /// <typeparam name="TOutput">The expected output type</typeparam>
    /// <param name="cancellationToken">Cancellation token for the execution</param>
    /// <returns>The pipeline execution result</returns>
    Task<TOutput> ExecuteAsync<TOutput>(CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Executes the pipeline with explicit input data.
    /// </summary>
    /// <typeparam name="TInput">The input data type</typeparam>
    /// <typeparam name="TOutput">The expected output type</typeparam>
    /// <param name="input">The input data for the pipeline</param>
    /// <param name="cancellationToken">Cancellation token for the execution</param>
    /// <returns>The pipeline execution result</returns>
    Task<TOutput> ExecuteAsync<TInput, TOutput>(
        TInput input,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Executes the pipeline in streaming mode for processing large datasets.
    /// </summary>
    /// <typeparam name="TInput">The input stream element type</typeparam>
    /// <typeparam name="TOutput">The output stream element type</typeparam>
    /// <param name="inputStream">The input data stream</param>
    /// <param name="cancellationToken">Cancellation token for the stream processing</param>
    /// <returns>An async enumerable of processed results</returns>
    IAsyncEnumerable<TOutput> ExecuteStreamAsync<TInput, TOutput>(
        IAsyncEnumerable<TInput> inputStream,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes the pipeline with detailed execution context and monitoring.
    /// </summary>
    /// <typeparam name="TOutput">The expected output type</typeparam>
    /// <param name="context">Execution context with configuration and monitoring</param>
    /// <param name="cancellationToken">Cancellation token for the execution</param>
    /// <returns>Detailed execution result with metrics and diagnostics</returns>
    Task<IPipelineExecutionResult<TOutput>> ExecuteWithContextAsync<TOutput>(
        IPipelineExecutionContext context,
        CancellationToken cancellationToken = default);
    
    #endregion

    #region Pipeline Composition and Transformation
    
    /// <summary>
    /// Composes this pipeline with another pipeline in sequence.
    /// </summary>
    /// <param name="other">The pipeline to compose with</param>
    /// <returns>A new composed pipeline</returns>
    IKernelPipeline Compose(IKernelPipeline other);
    
    /// <summary>
    /// Creates a deep copy of this pipeline for independent execution.
    /// </summary>
    /// <returns>A cloned pipeline instance</returns>
    IKernelPipeline Clone();
    
    /// <summary>
    /// Transforms the pipeline structure using a pipeline transformer.
    /// </summary>
    /// <param name="transformer">The transformation logic</param>
    /// <returns>A new transformed pipeline</returns>
    IKernelPipeline Transform(IPipelineTransformer transformer);
    
    /// <summary>
    /// Creates a sub-pipeline from a portion of this pipeline.
    /// </summary>
    /// <param name="startStage">The starting stage index or name</param>
    /// <param name="endStage">The ending stage index or name</param>
    /// <returns>A new sub-pipeline</returns>
    IKernelPipeline Slice(object startStage, object endStage);
    
    #endregion

    #region Monitoring and Diagnostics
    
    /// <summary>
    /// Gets comprehensive diagnostics information about the pipeline.
    /// </summary>
    /// <returns>Detailed pipeline diagnostics</returns>
    Task<IPipelineDiagnostics> GetDiagnosticsAsync();
    
    /// <summary>
    /// Gets the execution graph representation of the pipeline.
    /// </summary>
    /// <returns>The pipeline execution graph</returns>
    Task<IPipelineExecutionGraph> GetExecutionGraphAsync();
    
    /// <summary>
    /// Gets an observable stream of pipeline events for real-time monitoring.
    /// </summary>
    IObservable<PipelineEvent> Events { get; }
    
    /// <summary>
    /// Enables or disables detailed performance profiling.
    /// </summary>
    /// <param name="enabled">Whether profiling should be enabled</param>
    /// <param name="options">Profiling configuration options</param>
    /// <returns>A new pipeline with profiling configuration</returns>
    IKernelPipeline EnableProfiling(bool enabled = true, ProfilingOptions? options = null);
    
    /// <summary>
    /// Adds custom telemetry collection to the pipeline.
    /// </summary>
    /// <param name="telemetryCollector">Custom telemetry collector</param>
    /// <returns>A new pipeline with custom telemetry</returns>
    IKernelPipeline WithTelemetry(ITelemetryCollector telemetryCollector);
    
    #endregion

    #region Validation and Analysis
    
    /// <summary>
    /// Validates the pipeline structure and configuration for correctness.
    /// </summary>
    /// <returns>Validation result with any errors or warnings</returns>
    Task<IPipelineValidationResult> ValidateAsync();
    
    /// <summary>
    /// Analyzes the pipeline for performance characteristics and optimization opportunities.
    /// </summary>
    /// <returns>Pipeline analysis results</returns>
    Task<IPipelineAnalysis> AnalyzeAsync();
    
    /// <summary>
    /// Estimates resource requirements for pipeline execution.
    /// </summary>
    /// <param name="inputCharacteristics">Characteristics of expected input data</param>
    /// <returns>Estimated resource requirements</returns>
    Task<IResourceEstimate> EstimateResourcesAsync(IInputCharacteristics inputCharacteristics);
    
    #endregion
}