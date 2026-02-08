// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Analytics.Types;

/// <summary>
/// Execution pattern classification for kernel behavior analysis.
/// </summary>
/// <remarks>
/// Categorizes the temporal characteristics of kernel execution for
/// performance profiling and optimization recommendations.
/// </remarks>
public enum ExecutionPattern
{
    /// <summary>Unknown execution pattern.</summary>
    Unknown,

    /// <summary>Regular execution pattern with consistent timing.</summary>
    Regular,

    /// <summary>Irregular execution pattern with variable timing.</summary>
    Irregular,

    /// <summary>Burst execution pattern with periodic spikes.</summary>
    Burst,

    /// <summary>Adaptive execution pattern that adjusts over time.</summary>
    Adaptive
}

/// <summary>
/// Memory growth pattern classification for leak detection and optimization.
/// </summary>
/// <remarks>
/// Tracks how kernel memory usage evolves over time to identify potential
/// memory leaks, inefficient allocation patterns, or optimization opportunities.
/// </remarks>
public enum MemoryGrowthPattern
{
    /// <summary>Unknown memory growth pattern.</summary>
    Unknown,

    /// <summary>Stable memory usage with no significant growth.</summary>
    Stable,

    /// <summary>Increasing memory usage over time.</summary>
    /// <remarks>May indicate memory leak or unbounded accumulation.</remarks>
    Increasing,

    /// <summary>Decreasing memory usage over time.</summary>
    /// <remarks>May indicate progressive memory optimization or cleanup.</remarks>
    Decreasing,

    /// <summary>Oscillating memory usage pattern.</summary>
    /// <remarks>Indicates periodic allocation/deallocation cycles.</remarks>
    Oscillating
}

/// <summary>
/// Type of performance anomaly detected during analysis.
/// </summary>
/// <remarks>
/// Classifies detected anomalies to enable targeted investigation and remediation.
/// </remarks>
public enum AnomalyType
{
    /// <summary>Timing-related performance anomaly.</summary>
    /// <remarks>Unexpected execution time variance or spikes.</remarks>
    Timing,

    /// <summary>Memory-related anomaly.</summary>
    /// <remarks>Abnormal memory usage, leaks, or allocation patterns.</remarks>
    Memory,

    /// <summary>Throughput-related anomaly.</summary>
    /// <remarks>Unexpected changes in processing rate.</remarks>
    Throughput,

    /// <summary>Resource utilization anomaly.</summary>
    /// <remarks>CPU, GPU, or I/O resource usage outliers.</remarks>
    Resource
}

/// <summary>
/// Severity classification for detected anomalies.
/// </summary>
/// <remarks>
/// Helps prioritize investigation and remediation efforts based on
/// potential impact on system performance and reliability.
/// </remarks>
public enum AnomalySeverity
{
    /// <summary>Low severity anomaly.</summary>
    /// <remarks>Minor deviation with minimal performance impact.</remarks>
    Low,

    /// <summary>Medium severity anomaly.</summary>
    /// <remarks>Noticeable deviation warranting monitoring.</remarks>
    Medium,

    /// <summary>High severity anomaly.</summary>
    /// <remarks>Significant deviation with measurable performance degradation.</remarks>
    High,

    /// <summary>Critical severity anomaly requiring immediate attention.</summary>
    /// <remarks>Severe deviation indicating potential system failure or major performance issue.</remarks>
    Critical
}

/// <summary>
/// Performance trend classification for historical analysis.
/// </summary>
/// <remarks>
/// Characterizes how kernel performance evolves over time to identify
/// regressions, improvements, or instability.
/// </remarks>
public enum PerformanceTrend
{
    /// <summary>Unknown performance trend.</summary>
    Unknown,

    /// <summary>Improving performance over time.</summary>
    /// <remarks>Indicates successful optimizations or cache warmup effects.</remarks>
    Improving,

    /// <summary>Degrading performance over time.</summary>
    /// <remarks>May indicate regressions, memory leaks, or resource exhaustion.</remarks>
    Degrading,

    /// <summary>Stable performance with minimal variation.</summary>
    /// <remarks>Desired state for production workloads.</remarks>
    Stable,

    /// <summary>Volatile performance with significant fluctuations.</summary>
    /// <remarks>Indicates instability or external interference.</remarks>
    Volatile
}

/// <summary>
/// Direction of detected performance trend.
/// </summary>
/// <remarks>
/// Simplified directional indicator for trend forecasting and alerting.
/// </remarks>
public enum TrendDirection
{
    /// <summary>Upward trend direction.</summary>
    /// <remarks>Performance or resource usage increasing over time.</remarks>
    Up,

    /// <summary>Downward trend direction.</summary>
    /// <remarks>Performance or resource usage decreasing over time.</remarks>
    Down,

    /// <summary>Neutral trend with no significant direction.</summary>
    /// <remarks>Stable or random walk behavior.</remarks>
    Neutral
}

/// <summary>
/// Type of optimization opportunity identified by analysis.
/// </summary>
/// <remarks>
/// Categories optimization recommendations to enable targeted improvements.
/// </remarks>
public enum OptimizationType
{
    /// <summary>Memory-related optimization.</summary>
    /// <remarks>Reduce allocations, improve memory access patterns, or fix leaks.</remarks>
    Memory,

    /// <summary>Performance-related optimization.</summary>
    /// <remarks>Improve execution speed, throughput, or latency.</remarks>
    Performance,

    /// <summary>Resource utilization optimization.</summary>
    /// <remarks>Better use of CPU, GPU, or I/O resources.</remarks>
    Resource,

    /// <summary>Algorithm-related optimization.</summary>
    /// <remarks>Algorithmic improvements or data structure changes.</remarks>
    Algorithm
}

/// <summary>
/// Expected impact of implementing an optimization.
/// </summary>
/// <remarks>
/// Helps prioritize optimization efforts based on potential return on investment.
/// </remarks>
public enum OptimizationImpact
{
    /// <summary>Low impact optimization.</summary>
    /// <remarks>Minor improvements (&lt;10% expected gain).</remarks>
    Low,

    /// <summary>Medium impact optimization.</summary>
    /// <remarks>Moderate improvements (10-30% expected gain).</remarks>
    Medium,

    /// <summary>High impact optimization.</summary>
    /// <remarks>Substantial improvements (30-100% expected gain).</remarks>
    High,

    /// <summary>Critical impact optimization with significant performance improvement.</summary>
    /// <remarks>Major improvements (&gt;100% expected gain or critical stability fix).</remarks>
    Critical
}

/// <summary>
/// Quality assessment for analysis results based on data availability.
/// </summary>
/// <remarks>
/// <para>
/// Indicates confidence level in analysis conclusions based on sample size
/// and data quality.
/// </para>
/// <para>
/// Higher quality requires more execution samples and stable measurements.
/// </para>
/// </remarks>
public enum AnalysisQuality
{
    /// <summary>Insufficient data for quality analysis.</summary>
    /// <remarks>Less than 5 samples - results unreliable.</remarks>
    Insufficient,

    /// <summary>Poor quality analysis with limited accuracy.</summary>
    /// <remarks>5-19 samples - preliminary insights only.</remarks>
    Poor,

    /// <summary>Fair quality analysis with moderate accuracy.</summary>
    /// <remarks>20-49 samples - reasonable confidence.</remarks>
    Fair,

    /// <summary>Good quality analysis with high accuracy.</summary>
    /// <remarks>50-99 samples - high confidence.</remarks>
    Good,

    /// <summary>Excellent quality analysis with very high accuracy.</summary>
    /// <remarks>100+ samples - very high confidence, statistical significance.</remarks>
    Excellent
}

/// <summary>
/// Validation strategy recommendation based on workload characteristics.
/// </summary>
/// <remarks>
/// Selects appropriate validation depth based on data size, error sensitivity,
/// and performance requirements.
/// </remarks>
public enum ValidationStrategy
{
    /// <summary>Standard validation strategy with basic checks.</summary>
    /// <remarks>Fast validation for low-risk scenarios.</remarks>
    Standard,

    /// <summary>Comprehensive validation strategy with extensive checks.</summary>
    /// <remarks>Thorough validation for high-risk or critical workloads.</remarks>
    Comprehensive,

    /// <summary>Sampling-based validation strategy for large datasets.</summary>
    /// <remarks>Statistical sampling when full validation is impractical.</remarks>
    Sampling,

    /// <summary>Adaptive validation strategy that adjusts based on context.</summary>
    /// <remarks>Dynamically adjusts validation depth based on runtime conditions.</remarks>
    Adaptive
}
