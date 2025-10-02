// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Represents a comprehensive performance trend analysis for kernels and operations.
/// Combines trend detection, statistical analysis, and performance recommendations
/// to provide actionable insights into performance patterns over time.
/// </summary>
public sealed class PerformanceTrend
{
    #region Core Identification Properties

    /// <summary>
    /// Gets the name of the kernel being analyzed.
    /// </summary>
    /// <value>The kernel name as a string.</value>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the specific metric being analyzed.
    /// Examples: "ExecutionTime", "MemoryUsage", "Throughput", "CPUUtilization"
    /// </summary>
    /// <value>The metric name as a string.</value>
    public string MetricName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the accelerator type this trend analysis applies to.
    /// </summary>
    /// <value>The accelerator type from the AcceleratorType enumeration.</value>
    public AcceleratorType AcceleratorType { get; init; }

    /// <summary>
    /// Gets the session ID this trend data belongs to, if applicable.
    /// </summary>
    /// <value>The session identifier as a string.</value>
    public string SessionId { get; init; } = string.Empty;

    #endregion

    #region Trend Analysis Properties

    /// <summary>
    /// Gets the overall direction of the performance trend.
    /// Indicates whether the metric is improving, remaining stable, or degrading over time.
    /// </summary>
    /// <value>The trend direction from the TrendDirection enumeration.</value>
    public TrendDirection TrendDirection { get; init; }

    /// <summary>
    /// Gets the direction of the trend (alias for TrendDirection for compatibility).
    /// </summary>
    /// <value>The trend direction from the TrendDirection enumeration.</value>
    public TrendDirection Direction => TrendDirection;

    /// <summary>
    /// Gets the magnitude of the trend as a rate of change per time unit.
    /// Represents how quickly the metric is changing over time.
    /// Positive values indicate improvement, negative values indicate degradation.
    /// </summary>
    /// <value>The trend magnitude as a double (units depend on the specific metric).</value>
    public double Magnitude { get; init; }

    /// <summary>
    /// Gets the rate of change per time period for the metric.
    /// Similar to Magnitude but may use different calculation methods.
    /// </summary>
    /// <value>The rate of change as a double.</value>
    public double RateOfChange { get; init; }

    /// <summary>
    /// Gets the percentage change in the metric over the analysis period.
    /// Expressed as a decimal (0.1 = 10% change).
    /// </summary>
    /// <value>The percentage change as a double.</value>
    public double PercentChange { get; init; }

    #endregion

    #region Statistical Properties

    /// <summary>
    /// Gets the confidence level in this trend analysis.
    /// Higher values indicate more reliable trend detection based on data quality and quantity.
    /// </summary>
    /// <value>The confidence level as a double between 0.0 and 1.0.</value>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the number of data points used in this trend analysis.
    /// More data points generally lead to higher confidence in the trend.
    /// </summary>
    /// <value>The number of data points as an integer.</value>
    public int DataPoints { get; init; }

    /// <summary>
    /// Gets the number of samples used in the analysis.
    /// Similar to DataPoints but may refer to aggregated samples.
    /// </summary>
    /// <value>The sample size as an integer.</value>
    public int SampleSize { get; init; }

    #endregion

    #region Time-based Properties

    /// <summary>
    /// Gets the time period over which this trend was observed.
    /// Provides context for the temporal scope of the trend analysis.
    /// </summary>
    /// <value>The analysis period as a TimeSpan.</value>
    public TimeSpan Period { get; init; }

    /// <summary>
    /// Gets the time range for the trend analysis.
    /// Similar to Period but may use different measurement approach.
    /// </summary>
    /// <value>The time range as a TimeSpan.</value>
    public TimeSpan TimeRange { get; init; }

    /// <summary>
    /// Gets the timestamp when this analysis was performed.
    /// </summary>
    /// <value>The analysis time as a DateTime.</value>
    public DateTime AnalysisTime { get; init; } = DateTime.UtcNow;

    #endregion

    #region Strategy-specific Properties

    /// <summary>
    /// Gets the execution strategy associated with this trend, if applicable.
    /// Useful for backend-specific trend analysis (e.g., SIMD strategies).
    /// </summary>
    /// <value>The strategy name as a string.</value>
    public string? Strategy { get; init; }

    /// <summary>
    /// Gets whether this trend analysis is considered valid.
    /// False may indicate insufficient data or analysis errors.
    /// </summary>
    /// <value>True if the trend analysis is valid; otherwise, false.</value>
    public bool IsValid { get; init; } = true;

    #endregion

    #region Specialized Trend Metrics

    /// <summary>
    /// Gets the throughput trend for performance analysis.
    /// Specific to operations per second or similar throughput metrics.
    /// </summary>
    /// <value>The throughput trend value as a double.</value>
    public double ThroughputTrend { get; init; }

    /// <summary>
    /// Gets the vectorization trend for SIMD operations.
    /// Indicates how vectorization efficiency is changing over time.
    /// </summary>
    /// <value>The vectorization trend value as a double.</value>
    public double VectorizationTrend { get; init; }

    /// <summary>
    /// Gets the efficiency trend for resource utilization.
    /// Measures how efficiently resources are being used over time.
    /// </summary>
    /// <value>The efficiency trend value as a double.</value>
    public double EfficiencyTrend { get; init; }

    /// <summary>
    /// Gets the performance change magnitude for comparison analysis.
    /// Used in telemetry and monitoring systems.
    /// </summary>
    /// <value>The performance change as a double.</value>
    public double PerformanceChange { get; init; }

    #endregion

    #region Descriptive Properties

    /// <summary>
    /// Gets a human-readable description of the trend.
    /// Provides context and interpretation of the numerical data.
    /// </summary>
    /// <value>The trend description as a string.</value>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reason for the trend or any analysis limitations.
    /// May include explanations for low confidence or invalid trends.
    /// </summary>
    /// <value>The reason as a string.</value>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets performance recommendations based on this trend analysis.
    /// Actionable insights for optimizing performance.
    /// </summary>
    /// <value>A list of recommendation strings.</value>
    public List<string> Recommendations { get; init; } = [];

    #endregion

    #region Plugin-specific Properties

    /// <summary>
    /// Gets the plugin ID if this trend is specific to a plugin.
    /// Used in extension and algorithm plugin systems.
    /// </summary>
    /// <value>The plugin identifier as a string.</value>
    public string? PluginId { get; init; }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets a summary of this performance trend for logging and reporting.
    /// </summary>
    /// <returns>A formatted string summarizing the trend analysis.</returns>
    public string GetSummary()
    {
        var direction = TrendDirection switch
        {
            Types.TrendDirection.Improving => "improving",
            Types.TrendDirection.Degrading => "degrading",
            Types.TrendDirection.Stable => "stable",
            _ => "unknown"
        };

        return $"{MetricName} for {KernelName}: {direction} trend over {Period.TotalSeconds:F1}s " +
               $"({DataPoints} data points, {Confidence:P1} confidence)";
    }

    /// <summary>
    /// Determines if this trend indicates a significant performance change.
    /// </summary>
    /// <param name="threshold">The threshold for significance (default 0.1 for 10%)</param>
    /// <returns>True if the change is significant; otherwise, false.</returns>
    public bool IsSignificantChange(double threshold = 0.1)
    {
        return Math.Abs(PercentChange) > threshold && Confidence > 0.7;
    }

    /// <summary>
    /// Compares this trend with another trend for the same metric.
    /// </summary>
    /// <param name="other">The other performance trend to compare with.</param>
    /// <returns>A positive value if this trend is better, negative if worse, zero if similar.</returns>
    public double CompareTo(PerformanceTrend other)
    {
        if (other.MetricName != MetricName)
        {
            throw new ArgumentException("Cannot compare trends for different metrics", nameof(other));
        }

        // For most metrics, improving is better than degrading
        var directionScore = TrendDirection switch
        {
            Types.TrendDirection.Improving => 1.0,
            Types.TrendDirection.Stable => 0.0,
            Types.TrendDirection.Degrading => -1.0,
            _ => 0.0
        };

        var otherDirectionScore = other.TrendDirection switch
        {
            Types.TrendDirection.Improving => 1.0,
            Types.TrendDirection.Stable => 0.0,
            Types.TrendDirection.Degrading => -1.0,
            _ => 0.0
        };

        return (directionScore - otherDirectionScore) * Math.Max(Confidence, other.Confidence);
    }

    #endregion
}

/// <summary>
/// Defines the possible directions for performance trends observed over time.
/// Indicates whether a performance metric is getting better, worse, or staying consistent.
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// Unknown or indeterminate trend direction.
    /// May indicate insufficient data or analysis errors.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No clear trend direction detected.
    /// Performance shows random variation without clear pattern.
    /// </summary>
    None = 1,

    /// <summary>
    /// Performance is stable over time, showing consistent values for the metric.
    /// Indicates predictable performance with minimal variation.
    /// </summary>
    Stable = 2,

    /// <summary>
    /// Performance is improving over time, showing better values for the metric.
    /// Indicates positive trends such as faster execution times or higher throughput.
    /// </summary>
    Improving = 3,

    /// <summary>
    /// Performance is degrading over time, showing worse values for the metric.
    /// Indicates negative trends such as slower execution times or lower throughput.
    /// </summary>
    Degrading = 4
}