// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models;

/// <summary>
/// Represents a performance metric for execution monitoring.
/// </summary>
public class ExecutionPerformanceMetric
{
    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the metric value.
    /// </summary>
    public required double Value { get; set; }

    /// <summary>
    /// Gets or sets the metric unit.
    /// </summary>
    public required string Unit { get; set; }

    /// <summary>
    /// Gets or sets when this metric was recorded.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets additional metadata for the metric.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}