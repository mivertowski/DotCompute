// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Represents a telemetry event with structured data.
/// Supports both generic event structure and pipeline-specific properties.
/// </summary>
public sealed class TelemetryEvent
{
    // Generic event properties (existing)
    public required string Name { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required Dictionary<string, object?> Attributes { get; init; }
    public required string Source { get; init; }

    // Pipeline-specific properties (new)
    public TelemetryEventType? EventType { get; init; }
    public string? PipelineId { get; init; }
    public string? StageId { get; init; }
    public string? CorrelationId { get; init; }
    public TimeSpan? Duration { get; init; }
    public bool? Success { get; init; }
    public long? ItemsProcessed { get; init; }
    public Exception? Exception { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}