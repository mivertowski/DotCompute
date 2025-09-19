// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Linq.Pipelines.Interfaces;
using LinqPipeline = DotCompute.Linq.Pipelines.Interfaces.IKernelPipeline;

namespace DotCompute.Linq.Pipelines.Extensions;

/// <summary>
/// Extension methods for IKernelPipeline that provide cancellation support and other utilities.
/// Provides compatibility layer between different IKernelPipeline interface definitions.
/// </summary>
public static class IKernelPipelineExtensions
{
    #region WithCancellation Extensions for LINQ IKernelPipeline

    /// <summary>
    /// Configures a LINQ IKernelPipeline to observe the provided cancellation token.
    /// </summary>
    /// <param name="pipeline">The pipeline to configure</param>
    /// <param name="cancellationToken">The cancellation token to observe</param>
    /// <returns>A configured async enumerable that observes the cancellation token</returns>
    public static ConfiguredCancelableAsyncEnumerable<object> WithCancellation(
        this LinqPipeline pipeline, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.ConfigureAwait(false).WithCancellation(cancellationToken);
    }

    /// <summary>
    /// Gets an async enumerator for the LINQ IKernelPipeline with cancellation support.
    /// </summary>
    /// <param name="pipeline">The pipeline to enumerate</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An async enumerator</returns>
    public static IAsyncEnumerator<object> GetAsyncEnumerator(
        this LinqPipeline pipeline, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.GetAsyncEnumerator(cancellationToken);
    }

    #endregion

    #region Type Conversion Extensions

    /// <summary>
    /// Safely casts an object to a LINQ IKernelPipeline.
    /// </summary>
    /// <param name="obj">The object to cast</param>
    /// <returns>The cast pipeline</returns>
    /// <exception cref="InvalidCastException">When the object cannot be cast to IKernelPipeline</exception>
    public static LinqPipeline AsLinqPipeline(this object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        
        return obj switch
        {
            LinqPipeline linqPipeline => linqPipeline,
            _ => throw new InvalidCastException(
                $"Cannot convert object of type {obj.GetType()} to {typeof(LinqPipeline)}")
        };
    }

    /// <summary>
    /// Attempts to cast an object to a LINQ IKernelPipeline.
    /// </summary>
    /// <param name="obj">The object to cast</param>
    /// <param name="pipeline">The resulting pipeline if successful</param>
    /// <returns>True if the cast was successful; otherwise false</returns>
    public static bool TryAsLinqPipeline(this object obj, out LinqPipeline? pipeline)
    {
        pipeline = obj as LinqPipeline;
        return pipeline != null;
    }

    #endregion

    #region Configuration Extensions

    /// <summary>
    /// Configures a LINQ IKernelPipeline with specific options.
    /// </summary>
    /// <param name="pipeline">The pipeline to configure</param>
    /// <param name="options">The pipeline options</param>
    /// <returns>The configured pipeline</returns>
    public static LinqPipeline WithOptions(
        this LinqPipeline pipeline, 
        PipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(options);
        
        return pipeline.WithOptions(options);
    }

    /// <summary>
    /// Configures a LINQ IKernelPipeline with input data.
    /// </summary>
    /// <typeparam name="T">The input data type</typeparam>
    /// <param name="pipeline">The pipeline to configure</param>
    /// <param name="data">The input data</param>
    /// <returns>The configured pipeline</returns>
    public static LinqPipeline WithInput<T>(
        this LinqPipeline pipeline, 
        IEnumerable<T> data)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(data);
        
        return pipeline.WithInput(data);
    }

    #endregion

    #region Execution Extensions

    /// <summary>
    /// Executes a LINQ IKernelPipeline with cancellation support.
    /// </summary>
    /// <typeparam name="TResult">The expected result type</typeparam>
    /// <param name="pipeline">The pipeline to execute</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The execution result</returns>
    public static Task<TResult> ExecuteAsync<TResult>(
        this LinqPipeline pipeline, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.ExecuteAsync<TResult>(cancellationToken);
    }

    /// <summary>
    /// Executes a LINQ IKernelPipeline and returns untyped result.
    /// </summary>
    /// <param name="pipeline">The pipeline to execute</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The execution result</returns>
    public static Task<PipelineExecutionResult> ExecuteAsync(
        this LinqPipeline pipeline, 
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.ExecuteAsync(cancellationToken);
    }

    #endregion

    #region Bridge Extensions

    /// <summary>
    /// Creates a bridge adapter between different IKernelPipeline interfaces.
    /// </summary>
    /// <param name="linqPipeline">The LINQ pipeline to bridge</param>
    /// <returns>A bridge adapter</returns>
    public static Bridge.FluentPipelineAdapter ToBridge(this LinqPipeline linqPipeline)
    {
        ArgumentNullException.ThrowIfNull(linqPipeline);
        
        // For now, we need to convert through the Core pipeline
        // This would need proper implementation based on the actual bridge architecture
        throw new NotImplementedException(
            "Pipeline bridge conversion is not yet implemented. " +
            "This requires coordination between Core and LINQ pipeline implementations.");
    }

    #endregion

    #region Utility Extensions

    /// <summary>
    /// Gets the pipeline ID safely.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <returns>The pipeline ID or empty string if not available</returns>
    public static string GetPipelineId(this LinqPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.PipelineId ?? string.Empty;
    }

    /// <summary>
    /// Gets the pipeline configuration safely.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <returns>The pipeline configuration</returns>
    public static PipelineConfiguration GetConfiguration(this LinqPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.Configuration;
    }

    /// <summary>
    /// Gets the pipeline status safely.
    /// </summary>
    /// <param name="pipeline">The pipeline</param>
    /// <returns>The pipeline status</returns>
    public static PipelineStatus GetStatus(this LinqPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.Status;
    }

    #endregion
}