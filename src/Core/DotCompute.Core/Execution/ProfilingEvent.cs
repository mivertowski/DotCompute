// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents a single profiling event with timestamp and associated data.
/// </summary>
public class ProfilingEvent
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    /// <value>The event type.</value>
    public string EventType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>The description.</value>
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the properties.
    /// </summary>
    /// <value>The properties.</value>
    public Dictionary<string, object> Properties { get; init; } = [];
}
