// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents an active profiling session for a kernel.
/// </summary>
public sealed class ProfilingSession
{
    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the kernel being profiled.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the accelerator type being used.
    /// </summary>
    public AcceleratorType AcceleratorType { get; init; }

    /// <summary>
    /// Gets the time when the session started.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets whether the session is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets the elapsed time since the session started.
    /// </summary>
    public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;

    /// <summary>
    /// Gets the input parameters for the kernel.
    /// </summary>
    public IReadOnlyList<object> Inputs { get; init; } = Array.Empty<object>();

    /// <summary>
    /// Gets the profiling data collected during this session.
    /// </summary>
    public IReadOnlyList<ProfilingData> Data { get; init; } = [];

    /// <summary>
    /// Gets the memory usage at the start of the session.
    /// </summary>
    public long StartMemory { get; init; }

    /// <summary>
    /// Gets the CPU time at the start of the session.
    /// </summary>
    public double StartCpuTime { get; init; }
}

/// <summary>
/// Represents performance data collected during kernel execution.
/// </summary>
public sealed class ProfilingData
{
    /// <summary>
    /// Gets the timestamp when this data was collected.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the memory usage in bytes.
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the CPU utilization percentage (0-100).
    /// </summary>
    public double CpuUtilization { get; init; }

    /// <summary>
    /// Gets the GPU utilization percentage (0-100).
    /// </summary>
    public double GpuUtilization { get; init; }

    /// <summary>
    /// Gets the throughput in operations per second.
    /// </summary>
    public double ThroughputOpsPerSecond { get; init; }

    /// <summary>
    /// Gets the accelerator type used for this execution.
    /// </summary>
    public AcceleratorType AcceleratorType { get; init; }

    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets additional metadata about the execution.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>
    /// Gets the session ID this data belongs to.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the kernel name.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the start time of execution.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the end time of execution.
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the execution duration.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets memory usage information.
    /// </summary>
    public MemoryProfilingData? MemoryUsage { get; init; }

    /// <summary>
    /// Gets CPU usage information.
    /// </summary>
    public CpuProfilingData? CpuUsage { get; init; }

    /// <summary>
    /// Gets the execution result data.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Gets the error information if execution failed.
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// Gets performance metrics collected during execution.
    /// </summary>
    public Dictionary<string, object> PerformanceMetrics { get; init; } = [];
}

/// <summary>
/// Represents memory profiling data for kernel execution.
/// </summary>
public sealed class MemoryProfilingData
{
    /// <summary>
    /// Gets the memory usage before execution.
    /// </summary>
    public long MemoryBefore { get; init; }

    /// <summary>
    /// Gets the memory usage after execution.
    /// </summary>
    public long MemoryAfter { get; init; }

    /// <summary>
    /// Gets the peak memory usage during execution.
    /// </summary>
    public long PeakMemory { get; init; }

    /// <summary>
    /// Gets the memory allocated during execution.
    /// </summary>
    public long AllocatedMemory { get; init; }

    /// <summary>
    /// Gets the memory usage at the start.
    /// </summary>
    public long StartMemory { get; init; }

    /// <summary>
    /// Gets the memory usage at the end.
    /// </summary>
    public long EndMemory { get; init; }

    /// <summary>
    /// Gets the number of garbage collections that occurred.
    /// </summary>
    public int GCCollections { get; init; }
}

/// <summary>
/// Represents CPU profiling data for kernel execution.
/// </summary>
public sealed class CpuProfilingData
{
    /// <summary>
    /// Gets the CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets the start CPU time.
    /// </summary>
    public double StartCpuTime { get; init; }

    /// <summary>
    /// Gets the end CPU time.
    /// </summary>
    public double EndCpuTime { get; init; }

    /// <summary>
    /// Gets the total CPU time.
    /// </summary>
    public TimeSpan CpuTime { get; init; }

    /// <summary>
    /// Gets the CPU utilization.
    /// </summary>
    public TimeSpan CpuUtilization { get; init; }

    /// <summary>
    /// Gets the user CPU time in milliseconds.
    /// </summary>
    public double UserTimeMs { get; init; }

    /// <summary>
    /// Gets the system CPU time in milliseconds.
    /// </summary>
    public double SystemTimeMs { get; init; }

    /// <summary>
    /// Gets the total CPU time in milliseconds.
    /// </summary>
    public double TotalTimeMs { get; init; }

    /// <summary>
    /// Gets the number of context switches.
    /// </summary>
    public long ContextSwitches { get; init; }

    /// <summary>
    /// Gets the number of page faults.
    /// </summary>
    public long PageFaults { get; init; }
}

/// <summary>
/// Represents the result of performance analysis for a kernel.
/// </summary>
public sealed class PerformanceAnalysis
{
    /// <summary>
    /// Gets the name of the kernel that was analyzed.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the minimum execution time in milliseconds.
    /// </summary>
    public double MinExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the standard deviation of execution times.
    /// </summary>
    public double ExecutionTimeStdDev { get; init; }

    /// <summary>
    /// Gets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; init; }

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; init; }

    /// <summary>
    /// Gets the average throughput in operations per second.
    /// </summary>
    public double AverageThroughput { get; init; }

    /// <summary>
    /// Gets the number of data points used in the analysis.
    /// </summary>
    public int DataPointCount { get; init; }

    /// <summary>
    /// Gets the time range of the data analyzed.
    /// </summary>
    public TimeSpan AnalysisTimeRange { get; init; }

    /// <summary>
    /// Gets performance trends identified in the data.
    /// </summary>
    public IReadOnlyList<PerformanceTrend> Trends { get; init; } = [];

    /// <summary>
    /// Gets performance anomalies detected in the data.
    /// </summary>
    public IReadOnlyList<PerformanceAnomaly> Anomalies { get; init; } = [];

    /// <summary>
    /// Gets the accelerator type used for the analysis.
    /// </summary>
    public AcceleratorType AcceleratorType { get; init; }

    /// <summary>
    /// Gets the number of data points analyzed.
    /// </summary>
    public int DataPoints { get; init; }

    /// <summary>
    /// Gets the time when the analysis was performed.
    /// </summary>
    public DateTime AnalysisTime { get; init; }
}

/// <summary>
/// Represents a performance trend identified in the data.
/// </summary>
public sealed class PerformanceTrend
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time range for the trend.
    /// </summary>
    public TimeSpan TimeRange { get; set; }

    /// <summary>
    /// Gets or sets the number of data points.
    /// </summary>
    public int DataPoints { get; set; }

    /// <summary>
    /// Gets or sets the trend direction.
    /// </summary>
    public TrendDirection TrendDirection { get; set; }

    /// <summary>
    /// Gets or sets the time of analysis.
    /// </summary>
    public DateTime AnalysisTime { get; set; }

    /// <summary>
    /// Gets the metric that is trending.
    /// </summary>
    public string Metric { get; init; } = string.Empty;

    /// <summary>
    /// Gets the direction of the trend.
    /// </summary>
    public TrendDirection Direction { get; init; }

    /// <summary>
    /// Gets the confidence level of the trend (0-1).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the rate of change per time period.
    /// </summary>
    public double RateOfChange { get; init; }

    /// <summary>
    /// Gets the description of the trend.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the slope of the trend line.
    /// </summary>
    public double Slope { get; set; }

    /// <summary>
    /// Gets or sets the first data point in the trend.
    /// </summary>
    public TimeSpan FirstDataPoint { get; set; }

    /// <summary>
    /// Gets or sets the last data point in the trend.
    /// </summary>
    public TimeSpan LastDataPoint { get; set; }

    /// <summary>
    /// Gets or sets the average change per time period.
    /// </summary>
    public double AverageChange { get; set; }
}

/// <summary>
/// Represents a performance anomaly detected in the data.
/// </summary>
public sealed class PerformanceAnomaly
{
    /// <summary>
    /// Gets or sets the session ID where the anomaly was detected.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the anomaly was detected.
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the anomaly occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the type of anomaly.
    /// </summary>
    public AnomalyType Type { get; set; }

    /// <summary>
    /// Gets or sets the severity of the anomaly.
    /// </summary>
    public AnomalySeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the metric that showed anomalous behavior.
    /// </summary>
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actual value that was anomalous.
    /// </summary>
    public double ActualValue { get; set; }

    /// <summary>
    /// Gets or sets the expected value based on historical data.
    /// </summary>
    public double ExpectedValue { get; set; }

    /// <summary>
    /// Gets or sets the deviation from the expected value.
    /// </summary>
    public double Deviation { get; set; }

    /// <summary>
    /// Gets or sets a description of the anomaly.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents the result of comparing accelerator performance.
/// </summary>
public sealed class AcceleratorComparisonResult
{
    /// <summary>
    /// Gets the name of the kernel that was compared.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the accelerator performance summaries.
    /// </summary>
    public IReadOnlyList<AcceleratorPerformanceSummary> AcceleratorSummaries { get; init; } = [];

    /// <summary>
    /// Gets the fastest accelerator for this kernel.
    /// </summary>
    public AcceleratorType FastestAccelerator { get; init; }

    /// <summary>
    /// Gets the most memory-efficient accelerator.
    /// </summary>
    public AcceleratorType MostMemoryEfficient { get; init; }

    /// <summary>
    /// Gets the recommended accelerator based on overall performance.
    /// </summary>
    public AcceleratorType RecommendedAccelerator { get; init; }

    /// <summary>
    /// Gets the best performing accelerator.
    /// </summary>
    public AcceleratorType BestPerformingAccelerator { get; init; }

    /// <summary>
    /// Gets the most reliable accelerator.
    /// </summary>
    public AcceleratorType MostReliableAccelerator { get; init; }

    /// <summary>
    /// Gets performance recommendations.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets performance comparisons between accelerators.
    /// </summary>
    public Dictionary<string, double> PerformanceRatios { get; init; } = [];

    /// <summary>
    /// Gets the time when the comparison was performed.
    /// </summary>
    public DateTime ComparisonTime { get; init; }

    /// <summary>
    /// Gets whether there is sufficient data for comparison.
    /// </summary>
    public bool HasSufficientData { get; init; }
}

/// <summary>
/// Represents a performance summary for a specific accelerator.
/// </summary>
public sealed class AcceleratorPerformanceSummary
{
    /// <summary>
    /// Gets the accelerator type.
    /// </summary>
    public AcceleratorType AcceleratorType { get; init; }

    /// <summary>
    /// Gets the average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the average execution time in milliseconds (alias for AverageExecutionTimeMs).
    /// </summary>
    public double AverageExecutionTime => AverageExecutionTimeMs;

    /// <summary>
    /// Gets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; init; }

    /// <summary>
    /// Gets the average throughput in operations per second.
    /// </summary>
    public double AverageThroughput { get; init; }

    /// <summary>
    /// Gets the number of executions measured.
    /// </summary>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Gets the efficiency score (0-100).
    /// </summary>
    public double EfficiencyScore { get; init; }

    /// <summary>
    /// Gets the number of data points used in this summary.
    /// </summary>
    public int DataPoints { get; init; }

    /// <summary>
    /// Gets the median execution time in milliseconds.
    /// </summary>
    public double MedianExecutionTime { get; init; }

    /// <summary>
    /// Gets the minimum execution time in milliseconds.
    /// </summary>
    public double MinExecutionTime { get; init; }

    /// <summary>
    /// Gets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxExecutionTime { get; init; }

    /// <summary>
    /// Gets the standard deviation of execution times.
    /// </summary>
    public double StandardDeviation { get; init; }

    /// <summary>
    /// Gets the success rate (0-1).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Gets the throughput score for performance comparison.
    /// </summary>
    public double ThroughputScore { get; init; }
}

/// <summary>
/// Direction of a performance trend.
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// Unknown trend direction.
    /// </summary>
    Unknown,

    /// <summary>
    /// No clear trend direction.
    /// </summary>
    None,

    /// <summary>
    /// Performance is improving over time.
    /// </summary>
    Improving,

    /// <summary>
    /// Performance is degrading over time.
    /// </summary>
    Degrading,

    /// <summary>
    /// Performance is stable over time.
    /// </summary>
    Stable
}

/// <summary>
/// Type of performance anomaly.
/// </summary>
public enum AnomalyType
{
    /// <summary>
    /// Execution time spike.
    /// </summary>
    PerformanceSpike,

    /// <summary>
    /// Memory usage spike.
    /// </summary>
    MemorySpike,

    /// <summary>
    /// Execution time anomaly.
    /// </summary>
    ExecutionTime,

    /// <summary>
    /// Memory usage anomaly.
    /// </summary>
    MemoryUsage,

    /// <summary>
    /// Throughput drop.
    /// </summary>
    ThroughputDrop,

    /// <summary>
    /// Execution failure.
    /// </summary>
    ExecutionFailure,

    /// <summary>
    /// Resource contention detected.
    /// </summary>
    ResourceContention,

    /// <summary>
    /// Other anomaly type.
    /// </summary>
    Other
}

/// <summary>
/// Severity of a performance anomaly.
/// </summary>
public enum AnomalySeverity
{
    /// <summary>
    /// Low severity - minor performance deviation.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity - noticeable performance issue.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity - significant performance problem.
    /// </summary>
    High,

    /// <summary>
    /// Critical severity - system may be unusable.
    /// </summary>
    Critical
}

// CrossValidationResult is defined elsewhere to avoid duplicates
