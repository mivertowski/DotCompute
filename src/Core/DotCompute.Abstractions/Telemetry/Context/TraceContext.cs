// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;

namespace DotCompute.Abstractions.Telemetry.Context;

/// <summary>
/// Represents the context for a distributed trace, containing trace metadata,
/// spans, and device operation information.
/// </summary>
public sealed class TraceContext
{
    /// <summary>
    /// Gets or sets the unique trace identifier.
    /// This ID is used to correlate spans across service boundaries.
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
    /// Used for calculating total trace duration and timeline analysis.
    /// </summary>
    /// <value>The start time as a DateTimeOffset.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the Activity associated with this trace context.
    /// Provides integration with .NET's built-in activity tracing.
    /// </summary>
    /// <value>The Activity instance or null if not available.</value>
    public Activity? Activity { get; set; }

    /// <summary>
    /// Gets or sets the tags associated with this trace.
    /// Tags are key-value pairs that provide additional metadata about the trace.
    /// </summary>
    /// <value>A dictionary of tag key-value pairs.</value>
    public Dictionary<string, object?> Tags { get; } = [];
}