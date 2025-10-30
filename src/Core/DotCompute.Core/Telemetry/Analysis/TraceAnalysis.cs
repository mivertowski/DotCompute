// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.Spans;

namespace DotCompute.Core.Telemetry.Analysis;

/// <summary>
/// Contains comprehensive analysis results for a distributed trace.
/// Provides performance insights, bottleneck identification, and optimization recommendations.
/// </summary>
public sealed class TraceAnalysis
{
    /// <summary>
    /// Gets or sets the trace identifier this analysis belongs to.
    /// Links the analysis results to the specific trace that was analyzed.
    /// </summary>
    /// <value>The trace identifier as a string.</value>
    public string TraceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this analysis was performed.
    /// Used for tracking when the analysis was generated and its freshness.
    /// </summary>
    /// <value>The analysis timestamp as a DateTimeOffset.</value>
    public DateTimeOffset AnalysisTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the total time spent on operations within the trace.
    /// Represents the sum of all operation durations, including parallel operations.
    /// </summary>
    /// <value>The total operation time as a TimeSpan.</value>
    public TimeSpan TotalOperationTime { get; set; }

    /// <summary>
    /// Gets or sets the total number of spans in the trace.
    /// Provides an indication of the trace complexity and granularity.
    /// </summary>
    /// <value>The span count as an integer.</value>
    public int SpanCount { get; set; }

    /// <summary>
    /// Gets or sets the number of unique devices involved in the trace.
    /// Indicates the level of distributed computing utilization.
    /// </summary>
    /// <value>The device count as an integer.</value>
    public int DeviceCount { get; set; }

    /// <summary>
    /// Gets or sets the critical path spans that determine the minimum execution time.
    /// These spans represent the longest chain of dependencies in the trace.
    /// </summary>
    /// <value>A list of spans representing the critical path.</value>
    public IList<SpanData> CriticalPath { get; } = [];

    /// <summary>
    /// Gets or sets the duration of the critical path in milliseconds.
    /// Represents the theoretical minimum time needed to complete the trace.
    /// </summary>
    /// <value>The critical path duration in milliseconds.</value>
    public double CriticalPathDuration { get; set; }

    /// <summary>
    /// Gets or sets the utilization percentage for each device involved in the trace.
    /// Maps device IDs to their utilization percentages (0.0 to 1.0).
    /// </summary>
    /// <value>A dictionary mapping device IDs to utilization percentages.</value>
    public Dictionary<string, double> DeviceUtilization { get; } = [];

    /// <summary>
    /// Gets or sets the identified performance bottlenecks in the trace.
    /// Each bottleneck includes location, severity, and impact information.
    /// </summary>
    /// <value>A list of performance bottleneck objects.</value>
    public IList<PerformanceBottleneck> Bottlenecks { get; } = [];

    /// <summary>
    /// Gets or sets the memory access patterns observed during the trace.
    /// Provides insights into memory usage efficiency and potential optimizations.
    /// </summary>
    /// <value>A dictionary of memory access pattern data.</value>
    public Dictionary<string, object> MemoryAccessPatterns { get; } = [];

    /// <summary>
    /// Gets or sets the parallelism efficiency score (0.0 to 1.0).
    /// Indicates how effectively the trace utilized parallel computing capabilities.
    /// </summary>
    /// <value>The parallelism efficiency as a decimal between 0.0 and 1.0.</value>
    public double ParallelismEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the device efficiency score (0.0 to 1.0).
    /// Measures how effectively the available device resources were utilized.
    /// </summary>
    /// <value>The device efficiency as a decimal between 0.0 and 1.0.</value>
    public double DeviceEfficiency { get; set; }

    /// <summary>
    /// Gets or sets the list of optimization recommendations based on the analysis.
    /// Provides actionable suggestions for improving trace performance.
    /// </summary>
    /// <value>A list of optimization recommendation strings.</value>
    public IList<string> OptimizationRecommendations { get; } = [];
}
