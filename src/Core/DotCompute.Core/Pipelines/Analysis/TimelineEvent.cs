// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Analysis;

/// <summary>
/// Represents a specific event that occurred during pipeline execution.
/// Provides chronological insight into the execution flow and can help identify bottlenecks.
/// </summary>
public sealed class TimelineEvent
{
    /// <summary>
    /// Gets the timestamp when this event occurred.
    /// Provides the exact moment in the execution timeline when this event was recorded.
    /// </summary>
    /// <value>The event timestamp as a DateTime.</value>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the type or category of this event.
    /// Classifies the event for analysis and visualization purposes.
    /// </summary>
    /// <value>The event type as a string (e.g., "StageStart", "KernelLaunch", "MemoryTransfer").</value>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the human-readable description of this event.
    /// Provides detailed information about what occurred at this point in the timeline.
    /// </summary>
    /// <value>The event description as a string.</value>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the duration of this event if it represents a time-spanning operation.
    /// Null for instantaneous events that occur at a specific point in time.
    /// </summary>
    /// <value>The event duration as a TimeSpan, or null for point-in-time events.</value>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets the identifier of the pipeline stage associated with this event.
    /// Null for events that are not specific to a particular stage.
    /// </summary>
    /// <value>The stage identifier as a string, or null if not stage-specific.</value>
    public string? StageId { get; init; }

    /// <summary>
    /// Gets additional metadata associated with this event.
    /// Provides extensible context-specific information for detailed analysis.
    /// </summary>
    /// <value>A read-only dictionary of metadata key-value pairs, or null if no metadata is present.</value>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
