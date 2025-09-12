// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Pipelines.Models;

namespace DotCompute.Linq.Pipelines.Interfaces;

/// <summary>
/// Interface for kernel execution pipelines that manage GPU/CPU execution flow.
/// </summary>
public interface IKernelPipeline : IAsyncEnumerable<object>
{
    /// <summary>
    /// Gets the pipeline ID.
    /// </summary>
    string PipelineId { get; }

    /// <summary>
    /// Gets the pipeline configuration.
    /// </summary>
    PipelineConfiguration Configuration { get; }

    /// <summary>
    /// Executes the pipeline asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the pipeline execution.</returns>
    Task<PipelineExecutionResult> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pipeline asynchronously with typed result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the pipeline execution with typed result.</returns>
    Task<TResult> ExecuteAsync<TResult>(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a stage to the pipeline.
    /// </summary>
    /// <param name="stage">The stage to add.</param>
    /// <returns>The pipeline for fluent chaining.</returns>
    IKernelPipeline AddStage(IPipelineStage stage);

    /// <summary>
    /// Optimizes the pipeline for execution.
    /// </summary>
    /// <returns>The optimized pipeline.</returns>
    IKernelPipeline Optimize();

    /// <summary>
    /// Gets the pipeline execution plan.
    /// </summary>
    /// <returns>The execution plan.</returns>
    PipelineExecutionPlan GetExecutionPlan();

    /// <summary>
    /// Sets the input data for the pipeline.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <returns>The pipeline for fluent chaining.</returns>
    IKernelPipeline WithInput<T>(IEnumerable<T> data);

    /// <summary>
    /// Configures pipeline options.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The pipeline for fluent chaining.</returns>
    IKernelPipeline WithOptions(PipelineOptions options);

    /// <summary>
    /// Gets the current pipeline status.
    /// </summary>
    PipelineStatus Status { get; }

    /// <summary>
    /// Event raised when pipeline execution starts.
    /// </summary>
    event EventHandler<PipelineEventArgs>? ExecutionStarted;

    /// <summary>
    /// Event raised when pipeline execution completes.
    /// </summary>
    event EventHandler<PipelineEventArgs>? ExecutionCompleted;

    /// <summary>
    /// Event raised when a pipeline stage completes.
    /// </summary>
    event EventHandler<StageCompletedEventArgs>? StageCompleted;
}

/// <summary>
/// Interface for pipeline stages.
/// </summary>
public interface IPipelineStage
{
    /// <summary>
    /// Gets the stage name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the stage.
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stage output.</returns>
    Task<object> ExecuteAsync(object input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the stage can be parallelized.
    /// </summary>
    bool CanParallelize { get; }

    /// <summary>
    /// Gets the stage configuration.
    /// </summary>
    StageConfiguration Configuration { get; }
}

/// <summary>
/// Pipeline configuration settings.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Gets or sets the maximum degree of parallelism.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether to enable GPU acceleration.
    /// </summary>
    public bool EnableGpuAcceleration { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory limit in bytes.
    /// </summary>
    public long MemoryLimit { get; set; } = long.MaxValue;

    /// <summary>
    /// Gets or sets whether to enable optimization.
    /// </summary>
    public bool EnableOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets the execution timeout.
    /// </summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Pipeline execution result.
/// </summary>
public class PipelineExecutionResult
{
    /// <summary>
    /// Gets or sets whether the execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the output data.
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the stages executed.
    /// </summary>
    public List<StageExecutionInfo> StagesExecuted { get; set; } = new();

    /// <summary>
    /// Gets or sets any errors that occurred.
    /// </summary>
    public List<Exception> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets execution metrics.
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}

/// <summary>
/// Pipeline options.
/// </summary>
public class PipelineOptions
{
    /// <summary>
    /// Gets or sets whether to use caching.
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable profiling.
    /// </summary>
    public bool EnableProfiling { get; set; } = false;

    /// <summary>
    /// Gets or sets the batch size.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets custom options.
    /// </summary>
    public Dictionary<string, object> CustomOptions { get; set; } = new();
}

/// <summary>
/// Pipeline execution status.
/// </summary>
public enum PipelineStatus
{
    /// <summary>
    /// Pipeline is idle.
    /// </summary>
    Idle,

    /// <summary>
    /// Pipeline is being configured.
    /// </summary>
    Configuring,

    /// <summary>
    /// Pipeline is being optimized.
    /// </summary>
    Optimizing,

    /// <summary>
    /// Pipeline is executing.
    /// </summary>
    Executing,

    /// <summary>
    /// Pipeline execution completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Pipeline execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Pipeline execution was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Pipeline event arguments.
/// </summary>
public class PipelineEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the pipeline ID.
    /// </summary>
    public string PipelineId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional data.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Stage completed event arguments.
/// </summary>
public class StageCompletedEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets whether the stage succeeded.
    /// </summary>
    public bool Success { get; set; }
}

/// <summary>
/// Stage configuration.
/// </summary>
public class StageConfiguration
{
    /// <summary>
    /// Gets or sets the stage timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the retry count.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to skip on error.
    /// </summary>
    public bool SkipOnError { get; set; } = false;

    /// <summary>
    /// Gets or sets custom configuration.
    /// </summary>
    public Dictionary<string, object> CustomConfig { get; set; } = new();
}

/// <summary>
/// Stage execution information.
/// </summary>
public class StageExecutionInfo
{
    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets whether the stage succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the input size.
    /// </summary>
    public long InputSize { get; set; }

    /// <summary>
    /// Gets or sets the output size.
    /// </summary>
    public long OutputSize { get; set; }

    /// <summary>
    /// Gets or sets stage metrics.
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();
}