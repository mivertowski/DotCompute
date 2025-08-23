// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Metrics;

/// <summary>
/// Represents a single data point in a time series of performance metrics.
/// Used for tracking metric values over time to enable trend analysis and monitoring.
/// </summary>
public sealed class TimeSeriesMetric
{
    /// <summary>
    /// Gets the timestamp when this metric value was captured.
    /// Provides the temporal context for the metric data point.
    /// </summary>
    /// <value>The timestamp as a DateTime.</value>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the numeric value of the metric at this timestamp.
    /// Represents the measured performance characteristic at this point in time.
    /// </summary>
    /// <value>The metric value as a double.</value>
    public required double Value { get; init; }

    /// <summary>
    /// Gets the name of the metric being measured.
    /// Identifies what performance characteristic this data point represents.
    /// </summary>
    /// <value>The metric name as a string.</value>
    public required string MetricName { get; init; }

    /// <summary>
    /// Gets additional labels that provide context for this metric data point.
    /// Enables multi-dimensional analysis and filtering of time series data.
    /// </summary>
    /// <value>A read-only dictionary of label key-value pairs, or null if no labels are present.</value>
    public IReadOnlyDictionary<string, string>? Labels { get; init; }
}