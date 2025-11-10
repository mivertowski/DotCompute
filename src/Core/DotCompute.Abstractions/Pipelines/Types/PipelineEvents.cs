// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Configuration;

namespace DotCompute.Abstractions.Pipelines.Types;

/// <summary>
/// Base class for all pipeline-related events.
/// </summary>
/// <remarks>
/// <para>
/// Provides a common structure for pipeline events including identification,
/// timing, severity classification, and extensible metadata.
/// </para>
/// <para>
/// All pipeline events inherit from this base class to ensure consistent
/// event handling and telemetry collection across the pipeline infrastructure.
/// </para>
/// </remarks>
public class PipelineEvent
{
    /// <summary>Unique identifier for the event.</summary>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>Timestamp when the event occurred.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Pipeline identifier associated with the event.</summary>
    public Guid PipelineId { get; set; }

    /// <summary>Event severity level.</summary>
    public EventSeverity Severity { get; set; } = EventSeverity.Information;

    /// <summary>Human-readable description of the event.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Event type identifier.</summary>
    public PipelineEventType Type { get; set; }

    /// <summary>Event message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Additional event data.</summary>
    public Dictionary<string, object> Data { get; } = [];

    /// <summary>Stage identifier associated with the event (if applicable).</summary>
    public string? StageId { get; set; }

    /// <summary>Additional event metadata.</summary>
    public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Event fired when pipeline execution starts.
/// </summary>
/// <remarks>
/// This event signals the beginning of pipeline execution and provides access
/// to the execution context and configuration being used.
/// </remarks>
public class PipelineExecutionStartedEvent : PipelineEvent
{
    /// <summary>Execution context for the started pipeline.</summary>
    public IPipelineExecutionContext ExecutionContext { get; set; } = null!;

    /// <summary>Configuration used for the execution.</summary>
    public IPipelineConfiguration Configuration { get; set; } = null!;
}

/// <summary>
/// Event fired when pipeline execution completes.
/// </summary>
/// <remarks>
/// <para>
/// This event contains the final execution status, timing information,
/// and any exceptions that occurred during execution.
/// </para>
/// <para>
/// A completed pipeline may still have IsSuccess=false if errors occurred
/// but execution reached a terminal state.
/// </para>
/// </remarks>
public class PipelineExecutionCompletedEvent : PipelineEvent
{
    /// <summary>Whether the execution completed successfully.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Total execution time.</summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>Summary of execution results.</summary>
    public string? ResultSummary { get; set; }

    /// <summary>Any exceptions that occurred.</summary>
    public IList<Exception> Exceptions { get; } = [];
}

/// <summary>
/// Event fired when a pipeline stage starts execution.
/// </summary>
/// <remarks>
/// Provides detailed information about the stage being executed, including
/// the kernel name, backend selection, and stage-specific configuration.
/// </remarks>
public class StageExecutionStartedEvent : PipelineEvent
{
    /// <summary>Name of the stage that started.</summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>Kernel name being executed in the stage.</summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>Backend selected for stage execution.</summary>
    public string? Backend { get; set; }

    /// <summary>Stage configuration options.</summary>
    public PipelineStageOptions? StageOptions { get; set; }
}

/// <summary>
/// Event fired when a pipeline stage completes execution.
/// </summary>
/// <remarks>
/// Contains stage execution results including success status, timing,
/// and any exceptions that occurred during stage processing.
/// </remarks>
public class StageExecutionCompletedEvent : PipelineEvent
{
    /// <summary>Name of the stage that completed.</summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>Whether the stage completed successfully.</summary>
    public bool IsSuccess { get; set; }

    /// <summary>Stage execution time.</summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>Any exceptions from the stage.</summary>
    public Exception? Exception { get; set; }

    // TODO: Define missing type
    /* <summary>Performance metrics for the stage.</summary>
    public IStageExecutionMetrics? Metrics { get; set; } */
}
