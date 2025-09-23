// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Simple builder interface for creating compute kernel pipelines
/// </summary>
public interface IKernelPipelineBuilder
{
    /// <summary>
    /// Adds a kernel stage to the pipeline
    /// </summary>
    /// <param name="kernelName">Name of the kernel to execute</param>
    /// <param name="parameters">Parameters for kernel execution</param>
    /// <returns>The pipeline builder for method chaining</returns>
    public IKernelPipelineBuilder AddStage(string kernelName, params object[] parameters);


    /// <summary>
    /// Adds a transformation stage to the pipeline
    /// </summary>
    /// <typeparam name="TInput">Input data type</typeparam>
    /// <typeparam name="TOutput">Output data type</typeparam>
    /// <param name="transform">Transformation function</param>
    /// <returns>The pipeline builder for method chaining</returns>
    public IKernelPipelineBuilder Transform<TInput, TOutput>(Func<TInput, TOutput> transform);


    /// <summary>
    /// Executes the pipeline with the given input data
    /// </summary>
    /// <typeparam name="T">Input/output data type</typeparam>
    /// <param name="input">Input data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline execution result</returns>
    public Task<T> ExecuteAsync<T>(T input, CancellationToken cancellationToken = default);


    /// <summary>
    /// Executes the pipeline and returns the specified output type
    /// </summary>
    /// <typeparam name="T">Output data type</typeparam>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline execution result</returns>
    public Task<T> ExecutePipelineAsync<T>(CancellationToken cancellationToken = default);


    /// <summary>
    /// Creates a pipeline instance from the current builder configuration
    /// </summary>
    /// <returns>A pipeline instance that can be executed</returns>
    public object Create();


    /// <summary>
    /// Creates a new pipeline starting with input data array (compatibility method)
    /// </summary>
    /// <typeparam name="T">The element type of the input data</typeparam>
    /// <param name="inputData">The input data array to process</param>
    /// <returns>A new pipeline initialized with the input data</returns>
    public IKernelPipelineBuilder FromData<T>(T[] inputData) where T : unmanaged;


    /// <summary>
    /// Creates a pipeline starting with an async data stream (compatibility method)
    /// </summary>
    /// <typeparam name="T">The element type of the input stream</typeparam>
    /// <param name="inputStream">The async enumerable input stream</param>
    /// <returns>A new streaming pipeline</returns>
    public IKernelPipelineBuilder FromStream<T>(IAsyncEnumerable<T> inputStream) where T : unmanaged;
}