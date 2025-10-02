// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents a single profiling event with timestamp and associated data.
/// </summary>
public class ProfilingEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = [];
}
