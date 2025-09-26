// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Results from performance analysis of kernel execution.
/// </summary>
public class PerformanceAnalysisResult
{
    /// <summary>
    /// Name of the analyzed kernel.
    /// </summary>
    public required string KernelName { get; set; }

    /// <summary>
    /// Backend type where analysis was performed.
    /// </summary>
    public required string BackendType { get; set; }

    /// <summary>
    /// Total execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Memory usage during execution.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Throughput in operations per second.
    /// </summary>
    public double ThroughputOpsPerSecond { get; set; }

    /// <summary>
    /// Bottlenecks identified during analysis.
    /// </summary>
    public List<string> Bottlenecks { get; set; } = [];

    /// <summary>
    /// Performance recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = [];

    /// <summary>
    /// Detailed performance metrics.
    /// </summary>
    public Dictionary<string, object> DetailedMetrics { get; set; } = [];

    /// <summary>
    /// Execution statistics for the analysis.
    /// </summary>
    public ExecutionStatistics ExecutionStatistics { get; set; } = new();

    /// <summary>
    /// Bottleneck analysis results.
    /// </summary>
    public BottleneckAnalysis BottleneckAnalysis { get; set; } = new();

    /// <summary>
    /// Memory analysis results.
    /// </summary>
    public MemoryAnalysis MemoryAnalysis { get; set; } = new();
}

/// <summary>
/// Statistics about execution performance.
/// </summary>
public class ExecutionStatistics
{
    /// <summary>
    /// Name of the kernel.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Total number of executions performed.
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Number of successful executions.
    /// </summary>
    public int SuccessfulExecutions { get; set; }

    /// <summary>
    /// Number of failed executions.
    /// </summary>
    public int FailedExecutions { get; set; }

    /// <summary>
    /// Execution count (alias for TotalExecutions for compatibility).
    /// </summary>
    public long ExecutionCount
    {
        get => TotalExecutions;
        set => TotalExecutions = (int)value;
    }

    /// <summary>
    /// Success rate of executions (0-1).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Average execution time across all runs.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public double TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// Average execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Last execution timestamp.
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// Whether vectorization is used (CPU-specific).
    /// </summary>
    public bool UseVectorization { get; set; }

    /// <summary>
    /// Vectorization factor (CPU-specific).
    /// </summary>
    public int VectorizationFactor { get; set; }

    /// <summary>
    /// Vector width (CPU-specific).
    /// </summary>
    public int VectorWidth { get; set; }
}

/// <summary>
/// Analysis of performance bottlenecks.
/// </summary>
public class BottleneckAnalysis
{
    /// <summary>
    /// Identified bottlenecks.
    /// </summary>
    public List<PerformanceBottleneck> Bottlenecks { get; set; } = [];

    /// <summary>
    /// Overall performance score (0-100).
    /// </summary>
    public double OverallPerformanceScore { get; set; }
}

/// <summary>
/// Memory usage analysis.
/// </summary>
public class MemoryAnalysis
{
    /// <summary>
    /// Memory efficiency score (0-1).
    /// </summary>
    public double MemoryEfficiencyScore { get; set; }

    /// <summary>
    /// Peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsage { get; set; }
}

/// <summary>
/// Represents a performance bottleneck.
/// </summary>
public class PerformanceBottleneck
{
    /// <summary>
    /// Description of the bottleneck.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Severity of the bottleneck.
    /// </summary>
    public BottleneckSeverity Severity { get; set; }

    /// <summary>
    /// Component affected by the bottleneck.
    /// </summary>
    public string? Component { get; set; }
}

/// <summary>
/// Severity levels for performance bottlenecks.
/// </summary>
public enum BottleneckSeverity
{
    /// <summary>
    /// Low severity bottleneck.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity bottleneck.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity bottleneck.
    /// </summary>
    High,

    /// <summary>
    /// Critical severity bottleneck.
    /// </summary>
    Critical
}