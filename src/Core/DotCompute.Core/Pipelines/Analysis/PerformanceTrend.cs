// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Enums;

namespace DotCompute.Core.Pipelines.Analysis;

/// <summary>
/// Contains information about performance trends observed over time for a specific metric.
/// Provides insights into whether performance is improving, stable, or degrading.
/// </summary>
public sealed class PerformanceTrend
{
    /// <summary>
    /// Gets the name of the metric this trend analysis covers.
    /// Identifies which performance characteristic is being analyzed.
    /// </summary>
    /// <value>The metric name as a string.</value>
    public required string MetricName { get; init; }

    /// <summary>
    /// Gets the direction of the performance trend.
    /// Indicates whether the metric is improving, remaining stable, or degrading over time.
    /// </summary>
    /// <value>The trend direction from the TrendDirection enumeration.</value>
    public required TrendDirection Direction { get; init; }

    /// <summary>
    /// Gets the magnitude of the trend as a rate of change.
    /// Represents how quickly the metric is changing over time.
    /// </summary>
    /// <value>The trend magnitude as a double (units depend on the specific metric).</value>
    public required double Magnitude { get; init; }

    /// <summary>
    /// Gets the confidence level in this trend analysis.
    /// Higher values indicate more reliable trend detection based on data quality and quantity.
    /// </summary>
    /// <value>The confidence level as a double between 0.0 and 1.0.</value>
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets the time period over which this trend was observed.
    /// Provides context for the temporal scope of the trend analysis.
    /// </summary>
    /// <value>The analysis period as a TimeSpan.</value>
    public required TimeSpan Period { get; init; }
}