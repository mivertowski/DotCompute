// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Analytics.Types;

/// <summary>
/// Comprehensive advanced performance analysis result.
/// </summary>
/// <remarks>
/// <para>
/// Aggregates multiple analysis dimensions including trend analysis, regression analysis,
/// optimization opportunities, scaling predictions, and efficiency scoring.
/// </para>
/// <para>
/// Used for deep performance profiling and generating actionable optimization recommendations.
/// </para>
/// </remarks>
public record AdvancedPerformanceAnalysis
{
    /// <summary>Gets the kernel name being analyzed.</summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>Gets the trend analysis showing performance evolution over time.</summary>
    public TrendAnalysis TrendAnalysis { get; init; } = new();

    /// <summary>Gets the regression analysis for predictive modeling.</summary>
    public RegressionAnalysis RegressionAnalysis { get; init; } = new();

    /// <summary>Gets the identified optimization opportunities.</summary>
    public IReadOnlyList<OptimizationOpportunity> OptimizationOpportunities { get; init; } = [];

    /// <summary>Gets the scaling behavior predictions.</summary>
    public ScalingPredictions ScalingPredictions { get; init; } = new();

    /// <summary>Gets the efficiency scores for different backends.</summary>
    public Dictionary<string, double> EfficiencyScores { get; init; } = [];

    /// <summary>Gets the quality rating of this analysis.</summary>
    public AnalysisQuality AnalysisQuality { get; init; }

    /// <summary>Gets when this analysis was generated.</summary>
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Result of determinism validation analysis.
/// </summary>
/// <remarks>
/// <para>
/// Assesses whether kernel execution produces consistent results across multiple runs,
/// identifying sources of non-determinism and providing remediation recommendations.
/// </para>
/// <para>
/// Critical for ensuring reproducibility in scientific computing and debugging.
/// </para>
/// </remarks>
public record DeterminismAnalysisResult
{
    /// <summary>Gets the kernel name being analyzed.</summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>Gets the number of validation runs performed.</summary>
    public int RunCount { get; init; }

    /// <summary>Gets whether the kernel execution is deterministic.</summary>
    public bool IsDeterministic { get; init; }

    /// <summary>Gets the variability score (0.0 = perfectly deterministic, 1.0 = highly variable).</summary>
    public double VariabilityScore { get; init; }

    /// <summary>Gets the identified sources of non-determinism.</summary>
    public IReadOnlyList<string> NonDeterministicComponents { get; init; } = [];

    /// <summary>Gets the recommendations for improving determinism.</summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    /// <summary>Gets when this analysis was performed.</summary>
    public DateTime AnalysisTimestamp { get; init; }
}

/// <summary>
/// Performance insights from cross-backend validation.
/// </summary>
/// <remarks>
/// <para>
/// Compares performance across different compute backends to identify optimal
/// execution strategy and provide confidence scoring for backend selection.
/// </para>
/// </remarks>
public record ValidationPerformanceInsights
{
    /// <summary>Gets the performance distribution across tested backends.</summary>
    public Dictionary<string, double> BackendPerformanceDistribution { get; init; } = [];

    /// <summary>Gets the identified optimal backend for this workload.</summary>
    public string OptimalBackend { get; init; } = string.Empty;

    /// <summary>Gets the confidence scores for each backend.</summary>
    public Dictionary<string, double> ConfidenceScores { get; init; } = [];

    /// <summary>Gets the analyzed input characteristics.</summary>
    public Dictionary<string, object> InputCharacteristics { get; init; } = [];

    /// <summary>Gets the recommended validation strategy.</summary>
    public ValidationStrategy RecommendedValidationStrategy { get; init; }
}

/// <summary>
/// Statistical analysis of performance variation.
/// </summary>
/// <remarks>
/// <para>
/// Quantifies execution time and memory usage variability using statistical measures.
/// High variation indicates unstable performance requiring investigation.
/// </para>
/// </remarks>
public record PerformanceVariationAnalysis
{
    /// <summary>Gets the variance in execution time across runs.</summary>
    public double ExecutionTimeVariance { get; init; }

    /// <summary>Gets the standard deviation of execution time.</summary>
    public double ExecutionTimeStandardDeviation { get; init; }

    /// <summary>Gets the variance in memory usage across runs.</summary>
    public double MemoryUsageVariance { get; init; }

    /// <summary>Gets the coefficient of variation (CV = σ/μ) indicating relative variability.</summary>
    public double CoefficientOfVariation { get; init; }
}

/// <summary>
/// Analysis of execution trace point patterns.
/// </summary>
/// <remarks>
/// <para>
/// Identifies temporal patterns in kernel execution to characterize behavior
/// and detect anomalies or optimization opportunities.
/// </para>
/// </remarks>
public record TracePointPatternAnalysis
{
    /// <summary>Gets the detected execution pattern type.</summary>
    public ExecutionPattern PatternType { get; init; }

    /// <summary>Gets the regularity score (0.0 = irregular, 1.0 = perfectly regular).</summary>
    public double Regularity { get; init; }

    /// <summary>Gets the identified execution characteristics.</summary>
    public IReadOnlyList<string> Characteristics { get; init; } = [];
}

/// <summary>
/// Analysis of memory usage patterns over time.
/// </summary>
/// <remarks>
/// <para>
/// Tracks memory growth patterns to identify leaks, inefficient allocations,
/// or opportunities for memory optimization.
/// </para>
/// </remarks>
public record MemoryPatternAnalysis
{
    /// <summary>Gets the detected memory growth pattern.</summary>
    public MemoryGrowthPattern GrowthPattern { get; init; }

    /// <summary>Gets the estimated probability of memory leak (0.0 = no leak, 1.0 = definite leak).</summary>
    public double LeakProbability { get; init; }

    /// <summary>Gets the allocation efficiency score (0.0 = poor, 1.0 = excellent).</summary>
    public double AllocationEfficiency { get; init; }
}

/// <summary>
/// Detected performance anomaly with classification and metadata.
/// </summary>
/// <remarks>
/// Represents a single detected outlier or abnormal behavior in execution metrics.
/// </remarks>
public record PerformanceAnomaly
{
    /// <summary>Gets the type of anomaly detected.</summary>
    public AnomalyType Type { get; init; }

    /// <summary>Gets the severity classification.</summary>
    public AnomalySeverity Severity { get; init; }

    /// <summary>Gets the human-readable description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the trace point index where anomaly was detected.</summary>
    public int TracePointIndex { get; init; }

    /// <summary>Gets the anomaly value (interpretation depends on Type).</summary>
    public double Value { get; init; }
}

/// <summary>
/// Historical analysis profile for a kernel across multiple executions.
/// </summary>
/// <remarks>
/// <para>
/// Maintains longitudinal data about kernel behavior to enable trend detection,
/// regression analysis, and performance baselining.
/// </para>
/// </remarks>
public record KernelAnalysisProfile
{
    /// <summary>Gets the kernel name.</summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>Gets when this kernel was first analyzed.</summary>
    public DateTime FirstAnalyzed { get; init; }

    /// <summary>Gets when this kernel was last analyzed.</summary>
    public DateTime LastAnalyzed { get; set; }

    /// <summary>Gets the total number of executions analyzed.</summary>
    public int ExecutionCount { get; set; }

    /// <summary>Gets the number of successful executions.</summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>Gets the most recently detected execution pattern.</summary>
    public ExecutionPattern LastExecutionPattern { get; set; }

    /// <summary>Gets the average regularity score across all executions.</summary>
    public double AverageRegularity { get; set; }

    /// <summary>Gets the average execution time across all executions.</summary>
    public double AverageExecutionTime { get; set; }
}

/// <summary>
/// Statistical trend analysis of performance over time.
/// </summary>
/// <remarks>
/// <para>
/// Uses time-series analysis to characterize performance trends and predict
/// future behavior with associated confidence metrics.
/// </para>
/// </remarks>
public record TrendAnalysis
{
    /// <summary>Gets the detected performance trend.</summary>
    public PerformanceTrend Trend { get; init; }

    /// <summary>Gets the confidence in trend detection (0.0 = low, 1.0 = high).</summary>
    public double Confidence { get; init; }

    /// <summary>Gets the strength of the trend (0.0 = weak, 1.0 = strong).</summary>
    public double TrendStrength { get; init; }

    /// <summary>Gets the predicted future trend direction.</summary>
    public TrendDirection PredictedDirection { get; init; }
}

/// <summary>
/// Linear regression analysis for performance prediction.
/// </summary>
/// <remarks>
/// <para>
/// Fits a linear model to historical performance data to enable prediction
/// and quantify relationship strength.
/// </para>
/// </remarks>
public record RegressionAnalysis
{
    /// <summary>Gets the Pearson correlation coefficient (-1.0 to 1.0).</summary>
    public double CorrelationCoefficient { get; init; }

    /// <summary>Gets the R² coefficient of determination (0.0 to 1.0).</summary>
    public double RSquared { get; init; }

    /// <summary>Gets the slope of the regression line.</summary>
    public double Slope { get; init; }

    /// <summary>Gets the y-intercept of the regression line.</summary>
    public double Intercept { get; init; }

    /// <summary>Gets the predictive accuracy score (0.0 = poor, 1.0 = excellent).</summary>
    public double PredictiveAccuracy { get; init; }
}

/// <summary>
/// Identified optimization opportunity with impact assessment.
/// </summary>
/// <remarks>
/// <para>
/// Represents a specific actionable recommendation for improving performance,
/// with estimated impact and implementation guidance.
/// </para>
/// </remarks>
public record OptimizationOpportunity
{
    /// <summary>Gets the type of optimization.</summary>
    public OptimizationType Type { get; init; }

    /// <summary>Gets the expected impact level.</summary>
    public OptimizationImpact Impact { get; init; }

    /// <summary>Gets the detailed description of the opportunity.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the implementation recommendation.</summary>
    public string Recommendation { get; init; } = string.Empty;

    /// <summary>Gets the estimated performance improvement percentage.</summary>
    public double EstimatedImprovement { get; init; }
}

/// <summary>
/// Predictions for how performance scales with workload size.
/// </summary>
/// <remarks>
/// <para>
/// Models scaling behavior to predict performance at different workload sizes
/// and identify optimal operating points.
/// </para>
/// </remarks>
public record ScalingPredictions
{
    /// <summary>Gets the linear scaling factor (1.0 = perfect linear scaling).</summary>
    public double LinearScalingFactor { get; init; }

    /// <summary>Gets the predicted optimal workload size for best efficiency.</summary>
    public int OptimalWorkloadSize { get; init; }

    /// <summary>Gets the overall scaling efficiency (0.0 = poor, 1.0 = perfect).</summary>
    public double ScalingEfficiency { get; init; }

    /// <summary>Gets the recommended backend for this scaling profile.</summary>
    public string RecommendedBackend { get; init; } = string.Empty;
}
