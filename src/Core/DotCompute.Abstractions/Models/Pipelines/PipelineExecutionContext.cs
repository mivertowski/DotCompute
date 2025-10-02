// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Interfaces.Pipelines.Profiling;

namespace DotCompute.Abstractions.Models.Pipelines;

/// <summary>
/// Abstract base execution context for pipeline operations.
/// </summary>
public abstract class PipelineExecutionContext
{
    /// <summary>
    /// Gets the unique identifier for this execution context.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Gets or sets the pipeline identifier.
    /// </summary>
    public abstract string? PipelineId { get; set; }

    /// <summary>
    /// Gets the execution start time.
    /// </summary>
    public abstract DateTimeOffset StartTime { get; }

    /// <summary>
    /// Gets the execution timeout.
    /// </summary>
    public abstract TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the cancellation token for the execution.
    /// </summary>
    public abstract CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the accelerator to use for execution.
    /// </summary>
    public abstract IAccelerator? Accelerator { get; }

    /// <summary>
    /// Gets the shared data dictionary for passing data between stages.
    /// </summary>
    public abstract IDictionary<string, object> SharedData { get; }

    /// <summary>
    /// Gets the input data for pipeline execution.
    /// </summary>
    public abstract IDictionary<string, object> Inputs { get; }

    /// <summary>
    /// Gets the memory manager for pipeline operations.
    /// </summary>
    public abstract IPipelineMemoryManager? MemoryManager { get; }

    /// <summary>
    /// Gets the compute device to use for execution.
    /// </summary>
    public abstract object? Device { get; }

    /// <summary>
    /// Gets or sets the profiler for pipeline performance analysis.
    /// </summary>
    public abstract IPipelineProfiler? Profiler { get; set; }

    /// <summary>
    /// Gets a value indicating whether the execution has been cancelled.
    /// </summary>
    public virtual bool IsCancelled => CancellationToken.IsCancellationRequested;

    /// <summary>
    /// Gets a value indicating whether the execution has timed out.
    /// </summary>
    public virtual bool IsTimedOut => DateTimeOffset.UtcNow - StartTime > Timeout;

    /// <summary>
    /// Gets or sets the execution options.
    /// </summary>
    public abstract object? Options { get; set; }

    /// <summary>
    /// Gets or sets the execution state dictionary.
    /// </summary>
    public abstract Dictionary<string, object> State { get; }

    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    public abstract string? SessionId { get; set; }
}