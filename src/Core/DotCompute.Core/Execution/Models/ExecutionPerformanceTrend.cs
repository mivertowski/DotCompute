// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models;

/// <summary>
/// Represents performance trends over time for execution monitoring.
/// </summary>
public class ExecutionPerformanceTrend
{
    /// <summary>
    /// Gets or sets the time range for this trend analysis.
    /// </summary>
    public TimeRange TimeRange { get; set; } = new();

    /// <summary>
    /// Gets or sets the throughput trend direction.
    /// </summary>
    public TrendDirection ThroughputTrend { get; set; }

    /// <summary>
    /// Gets or sets the efficiency trend direction.
    /// </summary>
    public TrendDirection EfficiencyTrend { get; set; }

    /// <summary>
    /// Gets or sets the execution time trend direction.
    /// </summary>
    public TrendDirection ExecutionTimeTrend { get; set; }

    /// <summary>
    /// Gets or sets the memory bandwidth trend direction.
    /// </summary>
    public TrendDirection MemoryBandwidthTrend { get; set; }

    /// <summary>
    /// Gets or sets additional trend data points.
    /// </summary>
    public Dictionary<string, TrendDirection> CustomTrends { get; } = [];
}

/// <summary>
/// Represents a time range for trend analysis.
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Gets or sets the start time of the range.
    /// </summary>
    public DateTimeOffset Start { get; set; }

    /// <summary>
    /// Gets or sets the end time of the range.
    /// </summary>
    public DateTimeOffset End { get; set; }

    /// <summary>
    /// Gets the duration of the time range.
    /// </summary>
    public TimeSpan Duration => End - Start;
}

/// <summary>
/// Defines the possible trend directions for performance metrics.
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// Performance is improving over time.
    /// </summary>
    Improving,

    /// <summary>
    /// Performance is stable with no significant change.
    /// </summary>
    Stable,

    /// <summary>
    /// Performance is degrading over time.
    /// </summary>
    Degrading
}