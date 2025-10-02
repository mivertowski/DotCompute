// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Core.Telemetry.Spans;

/// <summary>
/// Represents the context and metadata for a single span within a distributed trace.
/// Contains timing information, status, attributes, and hierarchical relationships.
/// </summary>
public sealed class SpanContext
{
    /// <summary>
    /// Gets or sets the unique span identifier.
    /// Used to identify this specific span within the trace.
    /// </summary>
    /// <value>The span identifier as a string.</value>
    public string SpanId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trace identifier this span belongs to.
    /// Links this span to the overall distributed trace.
    /// </summary>
    /// <value>The trace identifier as a string.</value>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation identifier for this span.
    /// Used for correlating related operations within the trace.
    /// </summary>
    /// <value>The correlation identifier as a string.</value>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the operation this span represents.
    /// Provides a human-readable description of what this span tracks.
    /// </summary>
    /// <value>The span name as a string.</value>
    public string SpanName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the device where this span executed.
    /// Used for tracking operations across different compute devices.
    /// </summary>
    /// <value>The device identifier as a string.</value>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind of span, indicating its role in the trace.
    /// Determines how the span relates to other spans and services.
    /// </summary>
    /// <value>The span kind from the SpanKind enumeration.</value>
    public SpanKind SpanKind { get; set; } = SpanKind.Internal;

    /// <summary>
    /// Gets or sets the timestamp when the span started.
    /// Used for calculating span duration and timeline analysis.
    /// </summary>
    /// <value>The start time as a DateTimeOffset.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the span ended.
    /// Null if the span is still active.
    /// </summary>
    /// <value>The end time as a DateTimeOffset or null if not ended.</value>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the duration of the span.
    /// Calculated as the difference between end time and start time.
    /// </summary>
    /// <value>The span duration as a TimeSpan.</value>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the status of the span execution.
    /// Indicates whether the operation completed successfully or encountered errors.
    /// </summary>
    /// <value>The span status from the SpanStatus enumeration.</value>
    public SpanStatus Status { get; set; } = SpanStatus.Ok;

    /// <summary>
    /// Gets or sets an optional message describing the span status.
    /// Typically used to provide error details when status is not Ok.
    /// </summary>
    /// <value>The status message as a string or null if not provided.</value>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Gets or sets the attributes associated with this span.
    /// Attributes are key-value pairs providing additional context about the operation.
    /// </summary>
    /// <value>A dictionary of attribute key-value pairs.</value>
    public Dictionary<string, object?> Attributes { get; set; } = [];

    /// <summary>
    /// Gets or sets the events that occurred during this span's lifetime.
    /// Events represent point-in-time occurrences within the span.
    /// </summary>
    /// <value>A list of span events.</value>
    public List<SpanEvent> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the parent span.
    /// Null if this is a root span with no parent.
    /// </summary>
    /// <value>The parent span identifier as a string or null if root span.</value>
    public string? ParentSpanId { get; set; }

    /// <summary>
    /// Gets or sets the Activity associated with this span.
    /// Provides integration with .NET's built-in activity tracing.
    /// </summary>
    /// <value>The Activity instance or null if not available.</value>
    public Activity? Activity { get; set; }
}