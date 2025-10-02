// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Represents a telemetry event with structured data.
/// Supports both generic event structure and pipeline-specific properties.
/// </summary>
public sealed class TelemetryEvent
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    // Generic event properties (existing)
    public required string Name { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public required DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    /// <value>The attributes.</value>
    public required Dictionary<string, object?> Attributes { get; init; }
    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    /// <value>The source.</value>
    public required string Source { get; init; }
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    /// <value>The event type.</value>

    // Pipeline-specific properties (new)
    public TelemetryEventType? EventType { get; init; }
    /// <summary>
    /// Gets or sets the pipeline identifier.
    /// </summary>
    /// <value>The pipeline id.</value>
    public string? PipelineId { get; init; }
    /// <summary>
    /// Gets or sets the stage identifier.
    /// </summary>
    /// <value>The stage id.</value>
    public string? StageId { get; init; }
    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    /// <value>The correlation id.</value>
    public string? CorrelationId { get; init; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan? Duration { get; init; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool? Success { get; init; }
    /// <summary>
    /// Gets or sets the items processed.
    /// </summary>
    /// <value>The items processed.</value>
    public long? ItemsProcessed { get; init; }
    /// <summary>
    /// Gets or sets the exception.
    /// </summary>
    /// <value>The exception.</value>
    public Exception? Exception { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public Dictionary<string, object>? Metadata { get; init; }
}