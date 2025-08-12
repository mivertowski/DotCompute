// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Service for profiling performance across the DotCompute runtime
/// </summary>
public interface IPerformanceProfiler
{
    /// <summary>
    /// Starts profiling for a specific operation
    /// </summary>
    /// <param name="operationName">The name of the operation</param>
    /// <param name="metadata">Additional metadata about the operation</param>
    /// <returns>A profiling session token</returns>
    IProfilingSession StartProfiling(string operationName, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Gets performance metrics for a specific time period
    /// </summary>
    /// <param name="startTime">The start time</param>
    /// <param name="endTime">The end time</param>
    /// <returns>Performance metrics for the specified period</returns>
    Task<PerformanceMetrics> GetMetricsAsync(DateTime startTime, DateTime endTime);

    /// <summary>
    /// Gets real-time performance data
    /// </summary>
    /// <returns>Current performance data</returns>
    Task<RealTimePerformanceData> GetRealTimeDataAsync();

    /// <summary>
    /// Exports performance data to a file
    /// </summary>
    /// <param name="filePath">The output file path</param>
    /// <param name="format">The export format</param>
    /// <returns>A task representing the export operation</returns>
    Task ExportDataAsync(string filePath, PerformanceExportFormat format);

    /// <summary>
    /// Gets performance summary for all operations
    /// </summary>
    /// <returns>Performance summary</returns>
    Task<PerformanceSummary> GetSummaryAsync();
}

/// <summary>
/// Service for collecting device-specific metrics
/// </summary>
public interface IDeviceMetricsCollector
{
    /// <summary>
    /// Starts collecting metrics for a device
    /// </summary>
    /// <param name="acceleratorId">The accelerator ID</param>
    /// <returns>A task representing the start operation</returns>
    Task StartCollectionAsync(string acceleratorId);

    /// <summary>
    /// Stops collecting metrics for a device
    /// </summary>
    /// <param name="acceleratorId">The accelerator ID</param>
    /// <returns>A task representing the stop operation</returns>
    Task StopCollectionAsync(string acceleratorId);

    /// <summary>
    /// Gets current device metrics
    /// </summary>
    /// <param name="acceleratorId">The accelerator ID</param>
    /// <returns>Current device metrics</returns>
    Task<DeviceMetrics> GetCurrentMetricsAsync(string acceleratorId);

    /// <summary>
    /// Gets historical device metrics
    /// </summary>
    /// <param name="acceleratorId">The accelerator ID</param>
    /// <param name="startTime">The start time</param>
    /// <param name="endTime">The end time</param>
    /// <returns>Historical device metrics</returns>
    Task<IEnumerable<DeviceMetrics>> GetHistoricalMetricsAsync(
        string acceleratorId, DateTime startTime, DateTime endTime);

    /// <summary>
    /// Gets device utilization statistics
    /// </summary>
    /// <param name="acceleratorId">The accelerator ID</param>
    /// <returns>Device utilization statistics</returns>
    Task<DeviceUtilizationStats> GetUtilizationStatsAsync(string acceleratorId);
}

/// <summary>
/// Service for profiling individual kernel executions
/// </summary>
public interface IKernelProfiler
{
    /// <summary>
    /// Profiles a kernel execution
    /// </summary>
    /// <param name="kernel">The kernel to profile</param>
    /// <param name="arguments">The kernel arguments</param>
    /// <param name="accelerator">The target accelerator</param>
    /// <returns>Kernel profiling results</returns>
    Task<KernelProfilingResult> ProfileAsync(
        ICompiledKernel kernel,
        KernelArguments arguments,
        IAccelerator accelerator);

    /// <summary>
    /// Starts continuous profiling for all kernel executions
    /// </summary>
    /// <returns>A task representing the start operation</returns>
    Task StartContinuousProfilingAsync();

    /// <summary>
    /// Stops continuous profiling
    /// </summary>
    /// <returns>A task representing the stop operation</returns>
    Task StopContinuousProfilingAsync();

    /// <summary>
    /// Gets kernel execution history
    /// </summary>
    /// <param name="kernelName">The kernel name (optional)</param>
    /// <returns>Kernel execution history</returns>
    Task<IEnumerable<KernelExecutionRecord>> GetExecutionHistoryAsync(string? kernelName = null);
}

/// <summary>
/// Service for running performance benchmarks
/// </summary>
public interface IBenchmarkRunner
{
    /// <summary>
    /// Runs a standard benchmark suite
    /// </summary>
    /// <param name="accelerator">The accelerator to benchmark</param>
    /// <param name="suiteType">The benchmark suite type</param>
    /// <returns>Benchmark results</returns>
    Task<BenchmarkResults> RunBenchmarkAsync(IAccelerator accelerator, BenchmarkSuiteType suiteType);

    /// <summary>
    /// Runs a custom benchmark
    /// </summary>
    /// <param name="benchmarkDefinition">The benchmark definition</param>
    /// <param name="accelerator">The accelerator to benchmark</param>
    /// <returns>Benchmark results</returns>
    Task<BenchmarkResults> RunCustomBenchmarkAsync(
        BenchmarkDefinition benchmarkDefinition,
        IAccelerator accelerator);

    /// <summary>
    /// Compares performance across multiple accelerators
    /// </summary>
    /// <param name="accelerators">The accelerators to compare</param>
    /// <param name="benchmarkDefinition">The benchmark definition</param>
    /// <returns>Comparison results</returns>
    Task<AcceleratorComparisonResults> CompareAcceleratorsAsync(
        IEnumerable<IAccelerator> accelerators,
        BenchmarkDefinition benchmarkDefinition);

    /// <summary>
    /// Gets historical benchmark results
    /// </summary>
    /// <param name="acceleratorId">The accelerator ID (optional)</param>
    /// <returns>Historical benchmark results</returns>
    Task<IEnumerable<BenchmarkResults>> GetHistoricalResultsAsync(string? acceleratorId = null);
}

/// <summary>
/// Profiling session for tracking operation performance
/// </summary>
public interface IProfilingSession : IDisposable
{
    /// <summary>
    /// Gets the session ID
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Gets the operation name
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// Gets the start time
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Records a custom metric for this session
    /// </summary>
    /// <param name="name">The metric name</param>
    /// <param name="value">The metric value</param>
    void RecordMetric(string name, double value);

    /// <summary>
    /// Adds a tag to this session
    /// </summary>
    /// <param name="key">The tag key</param>
    /// <param name="value">The tag value</param>
    void AddTag(string key, string value);

    /// <summary>
    /// Gets the current session metrics
    /// </summary>
    /// <returns>Current session metrics</returns>
    SessionMetrics GetMetrics();

    /// <summary>
    /// Ends the profiling session
    /// </summary>
    /// <returns>Final session results</returns>
    ProfilingSessionResult End();
}

/// <summary>
/// Performance metrics collection
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Gets the time period for these metrics
    /// </summary>
    public required TimeRange Period { get; init; }

    /// <summary>
    /// Gets operation-specific metrics
    /// </summary>
    public Dictionary<string, OperationMetrics> Operations { get; init; } = new();

    /// <summary>
    /// Gets system-wide metrics
    /// </summary>
    public SystemMetrics System { get; init; } = new();

    /// <summary>
    /// Gets memory metrics
    /// </summary>
    public MemoryMetrics Memory { get; init; } = new();

    /// <summary>
    /// Gets throughput metrics
    /// </summary>
    public ThroughputMetrics Throughput { get; init; } = new();
}

/// <summary>
/// Real-time performance data
/// </summary>
public class RealTimePerformanceData
{
    /// <summary>
    /// Gets the timestamp of this data
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets current CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets current memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets current GPU usage percentage (if applicable)
    /// </summary>
    public double GpuUsagePercent { get; init; }

    /// <summary>
    /// Gets current operations per second
    /// </summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>
    /// Gets per-accelerator usage data
    /// </summary>
    public Dictionary<string, AcceleratorUsageData> AcceleratorUsage { get; init; } = new();

    /// <summary>
    /// Gets active operation count
    /// </summary>
    public int ActiveOperationCount { get; init; }
}

/// <summary>
/// Performance export formats
/// </summary>
public enum PerformanceExportFormat
{
    /// <summary>
    /// JSON format
    /// </summary>
    Json,

    /// <summary>
    /// CSV format
    /// </summary>
    Csv,

    /// <summary>
    /// XML format
    /// </summary>
    Xml,

    /// <summary>
    /// Binary format
    /// </summary>
    Binary
}

/// <summary>
/// Benchmark suite types
/// </summary>
public enum BenchmarkSuiteType
{
    /// <summary>
    /// Basic computational benchmarks
    /// </summary>
    Basic,

    /// <summary>
    /// Memory bandwidth benchmarks
    /// </summary>
    Memory,

    /// <summary>
    /// Linear algebra benchmarks
    /// </summary>
    LinearAlgebra,

    /// <summary>
    /// FFT benchmarks
    /// </summary>
    FFT,

    /// <summary>
    /// Comprehensive benchmark suite
    /// </summary>
    Comprehensive,

    /// <summary>
    /// Stress test benchmarks
    /// </summary>
    StressTest
}

/// <summary>
/// Time range specification
/// </summary>
public record TimeRange(DateTime Start, DateTime End)
{
    /// <summary>
    /// Gets the duration of this time range
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Checks if a timestamp falls within this range
    /// </summary>
    /// <param name="timestamp">The timestamp to check</param>
    /// <returns>True if the timestamp is within this range</returns>
    public bool Contains(DateTime timestamp) => timestamp >= Start && timestamp <= End;
}

/// <summary>
/// Operation-specific metrics
/// </summary>
public class OperationMetrics
{
    /// <summary>
    /// Gets the operation name
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Gets the total execution count
    /// </summary>
    public long ExecutionCount { get; init; }

    /// <summary>
    /// Gets the average execution time
    /// </summary>
    public TimeSpan AverageExecutionTime { get; init; }

    /// <summary>
    /// Gets the minimum execution time
    /// </summary>
    public TimeSpan MinExecutionTime { get; init; }

    /// <summary>
    /// Gets the maximum execution time
    /// </summary>
    public TimeSpan MaxExecutionTime { get; init; }

    /// <summary>
    /// Gets the success rate
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Gets custom metrics for this operation
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; init; } = new();
}

/// <summary>
/// System-wide metrics
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Gets the average CPU usage percentage
    /// </summary>
    public double AverageCpuUsage { get; init; }

    /// <summary>
    /// Gets the peak CPU usage percentage
    /// </summary>
    public double PeakCpuUsage { get; init; }

    /// <summary>
    /// Gets the system uptime
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// Gets the number of active threads
    /// </summary>
    public int ActiveThreadCount { get; init; }

    /// <summary>
    /// Gets garbage collection metrics
    /// </summary>
    public GCMetrics GarbageCollection { get; init; } = new();
}

/// <summary>
/// Memory metrics
/// </summary>
public class MemoryMetrics
{
    /// <summary>
    /// Gets the total allocated memory
    /// </summary>
    public long TotalAllocatedBytes { get; init; }

    /// <summary>
    /// Gets the peak memory usage
    /// </summary>
    public long PeakMemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the average memory usage
    /// </summary>
    public long AverageMemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the number of allocations
    /// </summary>
    public long AllocationCount { get; init; }

    /// <summary>
    /// Gets the number of deallocations
    /// </summary>
    public long DeallocationCount { get; init; }

    /// <summary>
    /// Gets fragmentation metrics
    /// </summary>
    public FragmentationMetrics Fragmentation { get; init; } = new();
}

/// <summary>
/// Throughput metrics
/// </summary>
public class ThroughputMetrics
{
    /// <summary>
    /// Gets operations per second
    /// </summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>
    /// Gets bytes processed per second
    /// </summary>
    public double BytesPerSecond { get; init; }

    /// <summary>
    /// Gets the peak throughput
    /// </summary>
    public double PeakThroughput { get; init; }

    /// <summary>
    /// Gets throughput by operation type
    /// </summary>
    public Dictionary<string, double> ThroughputByOperation { get; init; } = new();
}

/// <summary>
/// Additional supporting classes would be defined here...
/// </summary>
public class AcceleratorUsageData
{
    public double UsagePercent { get; init; }
    public long MemoryUsageBytes { get; init; }
    public double TemperatureCelsius { get; init; }
    public double PowerUsageWatts { get; init; }
}

public class GCMetrics
{
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public long TotalMemoryBytes { get; init; }
}

public class FragmentationMetrics
{
    public double FragmentationRatio { get; init; }
    public int FragmentedBlocks { get; init; }
    public long LargestFreeBlock { get; init; }
}

/// <summary>
/// Additional classes for benchmarking and profiling results
/// </summary>
public class BenchmarkResults
{
    public required string BenchmarkName { get; init; }
    public required string AcceleratorId { get; init; }
    public DateTime ExecutionTime { get; init; }
    public Dictionary<string, double> Results { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public class BenchmarkDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
}

public class AcceleratorComparisonResults
{
    public required IReadOnlyList<string> AcceleratorIds { get; init; }
    public Dictionary<string, BenchmarkResults> Results { get; init; } = new();
    public Dictionary<string, int> Rankings { get; init; } = new();
}

public class KernelProfilingResult
{
    public required string KernelName { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public long MemoryUsed { get; init; }
    public Dictionary<string, double> Metrics { get; init; } = new();
}

public class KernelExecutionRecord
{
    public required string KernelName { get; init; }
    public DateTime ExecutionTime { get; init; }
    public TimeSpan Duration { get; init; }
    public bool Success { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();
}

public class DeviceMetrics
{
    public required string AcceleratorId { get; init; }
    public DateTime Timestamp { get; init; }
    public double UsagePercent { get; init; }
    public long MemoryUsageBytes { get; init; }
    public double TemperatureCelsius { get; init; }
    public double PowerUsageWatts { get; init; }
    public Dictionary<string, double> CustomMetrics { get; init; } = new();
}

public class DeviceUtilizationStats
{
    public required string AcceleratorId { get; init; }
    public double AverageUsagePercent { get; init; }
    public double PeakUsagePercent { get; init; }
    public TimeSpan TotalActiveTime { get; init; }
    public TimeSpan TotalIdleTime { get; init; }
}

public class PerformanceSummary
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public TimeRange Period { get; init; } = default!;
    public Dictionary<string, double> KeyMetrics { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
}

public class SessionMetrics
{
    public TimeSpan ElapsedTime { get; init; }
    public Dictionary<string, double> Metrics { get; init; } = new();
    public Dictionary<string, string> Tags { get; init; } = new();
}

public class ProfilingSessionResult
{
    public required string SessionId { get; init; }
    public required string OperationName { get; init; }
    public TimeSpan TotalTime { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public Dictionary<string, double> FinalMetrics { get; init; } = new();
    public Dictionary<string, string> Tags { get; init; } = new();
    public bool Success { get; init; }
}