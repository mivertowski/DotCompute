// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Telemetry.Traces;

/// <summary>
/// Contains trace data and analysis results for distributed tracing.
/// </summary>
public sealed class TraceData
{
    /// <summary>
    /// Gets the unique trace identifier.
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Gets the correlation identifier.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the operation name.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Gets the start timestamp.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets the end timestamp.
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Gets the total duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Gets the trace tags.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Tags { get; init; }

    /// <summary>
    /// Gets the spans in the trace.
    /// </summary>
    public required IReadOnlyList<SpanData> Spans { get; init; }
}

/// <summary>
/// Represents span data within a trace.
/// </summary>
public sealed class SpanData
{
    /// <summary>
    /// Gets the span identifier.
    /// </summary>
    public required string SpanId { get; init; }

    /// <summary>
    /// Gets the operation name for this span.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Gets the start timestamp.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets the end timestamp.
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Gets the span duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Gets the span tags.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Tags { get; init; }
}