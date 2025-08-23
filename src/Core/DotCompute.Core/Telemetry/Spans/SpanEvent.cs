// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Spans;

/// <summary>
/// Represents a point-in-time event that occurred during a span's execution.
/// Events capture specific moments or state changes within the span lifecycle.
/// </summary>
public sealed class SpanEvent
{
    /// <summary>
    /// Gets or sets the name of the event.
    /// Provides a human-readable identifier for the type of event that occurred.
    /// </summary>
    /// <value>The event name as a string.</value>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the event occurred.
    /// Represents the exact moment in time when this event was recorded.
    /// </summary>
    /// <value>The event timestamp as a DateTimeOffset.</value>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the attributes associated with this event.
    /// Attributes provide additional context and details about the event.
    /// </summary>
    /// <value>A dictionary of attribute key-value pairs.</value>
    public Dictionary<string, object?> Attributes { get; set; } = [];
}