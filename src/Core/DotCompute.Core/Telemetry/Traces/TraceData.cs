// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.Analysis;
using DotCompute.Core.Telemetry.Enums;
using DotCompute.Core.Telemetry.Spans;

namespace DotCompute.Core.Telemetry.Traces;

/// <summary>
/// Represents complete trace data including all spans, device operations, and analysis results.
/// Contains the comprehensive information captured during a distributed trace execution.
/// </summary>
public sealed class TraceData
{
    /// <summary>
    /// Gets or sets the unique trace identifier.
    /// Used to identify this specific trace across service boundaries.
    /// </summary>
    /// <value>The trace identifier as a string.</value>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation identifier for this trace.
    /// Used for correlating related operations within the same trace.
    /// </summary>
    /// <value>The correlation identifier as a string.</value>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the root operation for this trace.
    /// Typically represents the high-level operation being traced.
    /// </summary>
    /// <value>The operation name as a string.</value>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the trace started.
    /// Represents the beginning of the root operation.
    /// </summary>
    /// <value>The start time as a DateTimeOffset.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the trace ended.
    /// Represents the completion of all operations within the trace.
    /// </summary>
    /// <value>The end time as a DateTimeOffset.</value>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the trace.
    /// Calculated as the difference between end time and start time.
    /// </summary>
    /// <value>The total duration as a TimeSpan.</value>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the overall status of the trace execution.
    /// Indicates whether the trace completed successfully or encountered issues.
    /// </summary>
    /// <value>The trace status from the TraceStatus enumeration.</value>
    public TraceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets all spans that belong to this trace.
    /// Each span represents an individual operation within the trace.
    /// </summary>
    /// <value>A list of span data objects.</value>
    public IList<SpanData> Spans { get; init; } = [];

    /// <summary>
    /// Gets or sets device operations tracked within this trace.
    /// Maps device IDs to their corresponding operation summaries.
    /// </summary>
    /// <value>A dictionary mapping device IDs to operation traces.</value>
    public Dictionary<string, DeviceOperationTrace> DeviceOperations { get; init; } = [];

    /// <summary>
    /// Gets or sets the tags associated with this trace.
    /// Tags are key-value pairs that provide additional metadata about the trace.
    /// </summary>
    /// <value>A dictionary of tag key-value pairs.</value>
    public Dictionary<string, object?> Tags { get; init; } = [];

    /// <summary>
    /// Gets or sets the analysis results for this trace.
    /// Contains performance insights and optimization recommendations.
    /// </summary>
    /// <value>The trace analysis results or null if analysis hasn't been performed.</value>
    public TraceAnalysis? Analysis { get; set; }
}
