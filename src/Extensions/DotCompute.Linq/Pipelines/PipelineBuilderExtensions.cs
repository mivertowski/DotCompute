// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines;
using DotCompute.Linq.Pipelines.Models;
namespace DotCompute.Linq.Pipelines;
/// <summary>
/// Extension methods for IKernelPipelineBuilder to support missing functionality needed by LINQ extensions.
/// These methods provide a bridge between the expected fluent API and the actual implementation.
/// </summary>
public static class PipelineBuilderExtensions
{
    /// <summary>
    /// Creates a new pipeline starting with input data array.
    /// </summary>
    /// <typeparam name="T">The element type of the input data</typeparam>
    /// <param name="builder">The pipeline builder</param>
    /// <param name="inputData">The input data array to process</param>
    /// <param name="options">Optional configuration for data handling</param>
    /// <returns>A new pipeline initialized with the input data</returns>
    /// <exception cref="ArgumentNullException">Thrown when inputData is null</exception>
    public static IKernelPipelineBuilder FromData<T>(this IKernelPipelineBuilder builder, T[] inputData, DataPipelineOptions? options = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(inputData);
        ArgumentNullException.ThrowIfNull(builder);
        // For now, add the data as a transformation stage
        // In a full implementation, this would create proper data initialization
        return builder.AddStage("DataInitialization", inputData, options ?? new DataPipelineOptions());
    }
    /// Creates a pipeline starting with an async data stream for large dataset processing.
    /// <typeparam name="T">The element type of the input stream</typeparam>
    /// <param name="inputStream">The async enumerable input stream</param>
    /// <param name="options">Optional streaming configuration</param>
    /// <returns>A new streaming pipeline builder</returns>
    /// <exception cref="ArgumentNullException">Thrown when inputStream is null</exception>
    public static IKernelPipelineBuilder FromStream<T>(this IKernelPipelineBuilder builder, IAsyncEnumerable<T> inputStream, StreamPipelineOptions? options = null) where T : unmanaged
        ArgumentNullException.ThrowIfNull(inputStream);
        // For now, add the stream as a stage
        // In a full implementation, this would create proper stream processing
        return builder.AddStage("StreamInitialization", inputStream, options ?? new StreamPipelineOptions());
    /// Creates a pipeline instance from the current builder configuration.
    /// This method provides compatibility for the Create() method calls in LINQ extensions.
    /// <returns>A pipeline-like object that supports method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when builder is null</exception>
    public static PipelineChain Create(this IKernelPipelineBuilder builder)
        // Return a wrapper that provides the expected fluent API
        return new PipelineChain(builder);
}
/// A wrapper class that provides the expected fluent API for pipeline operations.
/// This bridges the gap between the builder pattern and the execution pattern.
public sealed class PipelineChain
    private readonly IKernelPipelineBuilder _builder;
    /// Initializes a new instance of the PipelineChain class.
    /// <param name="builder">The underlying pipeline builder</param>
    public PipelineChain(IKernelPipelineBuilder builder)
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    /// Adds a transformation stage to the pipeline chain.
    /// This method provides compatibility for Then() method calls in LINQ extensions.
    /// <typeparam name="TInput">The input type for the transformation</typeparam>
    /// <typeparam name="TOutput">The output type for the transformation</typeparam>
    /// <param name="stageName">The name of the transformation stage</param>
    /// <param name="parametersProvider">Function that provides parameters for the stage</param>
    /// <param name="options">Optional stage configuration</param>
    /// <returns>The pipeline chain for method chaining</returns>
    public PipelineChain Then<TInput, TOutput>(
        string stageName,
        Func<TInput, object[]> parametersProvider,
        PipelineStageOptions? options = null)
        // Add the stage to the underlying builder
        ArgumentNullException.ThrowIfNull(parametersProvider);
        _builder.AddStage(stageName, (object)parametersProvider);
        // Return this for method chaining
        return this;
    /// Creates a new pipeline chain from this configuration.
    /// This provides compatibility for Create() method calls on chain objects.
    /// <returns>A new pipeline chain with the same configuration</returns>
    public PipelineChain Create()
        return new PipelineChain(_builder);
    /// Executes the pipeline chain asynchronously.
    /// <typeparam name="TResult">The result type</typeparam>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The execution result</returns>
    public async Task<TResult> ExecuteAsync<TResult>(CancellationToken cancellationToken = default)
        // Delegate to the underlying builder's execution method
        return await _builder.ExecutePipelineAsync<TResult>(cancellationToken);
    /// Applies optimization to the pipeline chain.
    /// <param name="strategy">The optimization strategy to apply</param>
    /// <returns>An optimized pipeline chain</returns>
    public PipelineChain Optimize(OptimizationStrategy strategy)
        // For now, return the same chain
        // In a full implementation, this would apply optimizations
    /// Adds caching to the pipeline chain.
    /// <param name="options">Caching options</param>
    /// <returns>The pipeline chain with caching enabled</returns>
    public PipelineChain AdaptiveCache(AdaptiveCacheOptions options)
        // In a full implementation, this would configure caching
    /// Executes the pipeline with specific input and output types.
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    /// <param name="input">The input data</param>
    /// <returns>The processed output</returns>
    public async Task<TOutput> ExecuteAsync<TInput, TOutput>(TInput input)
        // Delegate to the underlying builder
        return await _builder.ExecuteAsync<TOutput>((dynamic)input!);
/// Optimization strategies for pipeline execution.
public enum OptimizationStrategy
    /// <summary>Conservative optimization with safety focus.</summary>
    Conservative,
    /// <summary>Balanced optimization approach.</summary>
    Balanced,
    /// <summary>Aggressive optimization for maximum performance.</summary>
    Aggressive,
    /// <summary>Adaptive optimization based on runtime characteristics.</summary>
    Adaptive
/// Configuration options for adaptive caching.
public sealed class AdaptiveCacheOptions
    /// Gets or sets whether automatic cache key generation is enabled.
    public bool AutoKeyGeneration { get; set; } = true;
    /// Gets or sets whether policy adaptation is enabled.
    public bool PolicyAdaptation { get; set; } = true;
    /// Gets or sets the performance threshold for caching decisions.
    public double PerformanceThreshold { get; set; } = 0.1;
    /// Gets or sets the maximum cache size in bytes.
    public long MaxCacheSize { get; set; } = 100 * 1024 * 1024; // 100MB
