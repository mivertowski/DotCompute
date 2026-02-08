// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Types;

/// <summary>
/// Represents the current execution state of a pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This enumeration defines all possible states a pipeline can be in during its lifecycle,
/// from creation through disposal. State transitions should follow the natural progression:
/// Created → Ready → Executing → (Completed|Failed|Cancelled) → Disposed.
/// </para>
/// <para>
/// The Paused and Optimizing states allow for intermediate processing and can transition
/// back to Executing when resumed.
/// </para>
/// </remarks>
public enum PipelineState
{
    /// <summary>Pipeline has been created but not yet configured.</summary>
    Created,

    /// <summary>Pipeline is configured and ready for execution.</summary>
    Ready,

    /// <summary>Pipeline is currently executing.</summary>
    Executing,

    /// <summary>Pipeline execution has been paused.</summary>
    Paused,

    /// <summary>Pipeline execution completed successfully.</summary>
    Completed,

    /// <summary>Pipeline execution failed with errors.</summary>
    Failed,

    /// <summary>Pipeline execution was cancelled.</summary>
    Cancelled,

    /// <summary>Pipeline is being optimized.</summary>
    Optimizing,

    /// <summary>Pipeline has been disposed.</summary>
    Disposed
}

/// <summary>
/// Event severity levels for pipeline events.
/// </summary>
/// <remarks>
/// Used to categorize the importance and urgency of pipeline events.
/// Severity levels help in filtering, routing, and responding to events appropriately.
/// </remarks>
public enum EventSeverity
{
    /// <summary>Verbose debugging information.</summary>
    Verbose,

    /// <summary>General informational messages.</summary>
    Information,

    /// <summary>Warning conditions that don't prevent execution.</summary>
    Warning,

    /// <summary>Error conditions that may affect execution.</summary>
    Error,

    /// <summary>Critical errors that prevent execution.</summary>
    Critical
}

/// <summary>
/// Types of pipeline events.
/// </summary>
/// <remarks>
/// Defines the taxonomy of events that can occur during pipeline execution.
/// These event types enable fine-grained monitoring and diagnostics of pipeline behavior.
/// </remarks>
public enum PipelineEventType
{
    /// <summary>Pipeline execution started.</summary>
    Started,

    /// <summary>Pipeline execution completed.</summary>
    Completed,

    /// <summary>Pipeline execution failed.</summary>
    Failed,

    /// <summary>Pipeline stage started.</summary>
    StageStarted,

    /// <summary>Pipeline stage completed.</summary>
    StageCompleted,

    /// <summary>Pipeline stage failed.</summary>
    StageFailed,

    /// <summary>Pipeline was optimized.</summary>
    Optimized,

    /// <summary>Pipeline validation occurred.</summary>
    Validated
}
