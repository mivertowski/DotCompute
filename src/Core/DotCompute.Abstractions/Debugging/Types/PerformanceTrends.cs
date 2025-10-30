// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Aggregated performance trends analysis over a time window.
/// Provides statistical analysis of performance patterns and trajectory.
/// </summary>
public sealed class PerformanceTrends
{
    /// <summary>
    /// Gets or sets the kernel name being analyzed.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the analysis time window.
    /// </summary>
    public TimeSpan TimeWindow { get; set; }

    /// <summary>
    /// Gets or sets the number of data points analyzed.
    /// </summary>
    public int DataPoints { get; set; }

    /// <summary>
    /// Gets or sets when this analysis was performed.
    /// </summary>
    public DateTime AnalysisTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the execution time trend.
    /// </summary>
    public TrendAnalysis ExecutionTimeTrend { get; set; } = new();

    /// <summary>
    /// Gets or sets the memory usage trend.
    /// </summary>
    public TrendAnalysis MemoryUsageTrend { get; set; } = new();

    /// <summary>
    /// Gets or sets the throughput trend.
    /// </summary>
    public TrendAnalysis ThroughputTrend { get; set; } = new();

    /// <summary>
    /// Gets or sets the success rate trend.
    /// </summary>
    public TrendAnalysis SuccessRateTrend { get; set; } = new();

    /// <summary>
    /// Gets or sets the overall performance trend direction.
    /// </summary>
    public TrendDirection OverallTrend { get; set; }

    /// <summary>
    /// Gets or sets the trend confidence score (0-1).
    /// </summary>
    public double TrendConfidence { get; set; }

    /// <summary>
    /// Gets or sets detected performance anomalies during the analysis window.
    /// </summary>
    public IList<PerformanceAnomaly> DetectedAnomalies { get; init; } = [];

    /// <summary>
    /// Gets or sets trend-based recommendations.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets or sets predicted future performance metrics.
    /// </summary>
    public PerformancePrediction? Prediction { get; set; }
}

/// <summary>
/// Detailed trend analysis for a specific metric.
/// </summary>
public sealed class TrendAnalysis
{
    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trend direction.
    /// </summary>
    public TrendDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the rate of change per time unit.
    /// </summary>
    public double RateOfChange { get; set; }

    /// <summary>
    /// Gets or sets the trend slope (linear regression coefficient).
    /// </summary>
    public double Slope { get; set; }

    /// <summary>
    /// Gets or sets the R-squared value indicating trend fit quality (0-1).
    /// </summary>
    public double RSquared { get; set; }

    /// <summary>
    /// Gets or sets the statistical significance (p-value).
    /// </summary>
    public double PValue { get; set; }

    /// <summary>
    /// Gets or sets the current value of the metric.
    /// </summary>
    public double CurrentValue { get; set; }

    /// <summary>
    /// Gets or sets the average value over the trend window.
    /// </summary>
    public double AverageValue { get; set; }

    /// <summary>
    /// Gets or sets the minimum value observed.
    /// </summary>
    public double MinValue { get; set; }

    /// <summary>
    /// Gets or sets the maximum value observed.
    /// </summary>
    public double MaxValue { get; set; }

    /// <summary>
    /// Gets or sets the standard deviation.
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// Gets or sets whether the trend is statistically significant.
    /// </summary>
    public bool IsSignificant { get; set; }
}

/// <summary>
/// Predicted future performance metrics based on trend analysis.
/// </summary>
public sealed class PerformancePrediction
{
    /// <summary>
    /// Gets or sets the prediction horizon (how far into the future).
    /// </summary>
    public TimeSpan PredictionHorizon { get; set; }

    /// <summary>
    /// Gets or sets the predicted execution time.
    /// </summary>
    public TimeSpan PredictedExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the predicted memory usage in bytes.
    /// </summary>
    public long PredictedMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the predicted throughput.
    /// </summary>
    public double PredictedThroughput { get; set; }

    /// <summary>
    /// Gets or sets the prediction confidence interval lower bound.
    /// </summary>
    public double ConfidenceIntervalLower { get; set; }

    /// <summary>
    /// Gets or sets the prediction confidence interval upper bound.
    /// </summary>
    public double ConfidenceIntervalUpper { get; set; }

    /// <summary>
    /// Gets or sets the prediction confidence level (typically 0.95 for 95% confidence).
    /// </summary>
    public double ConfidenceLevel { get; set; }

    /// <summary>
    /// Gets or sets potential risks identified in the prediction.
    /// </summary>
    public IList<string> IdentifiedRisks { get; init; } = [];
}
