// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Profiling;

/// <summary>
/// Represents a comprehensive profiling snapshot of accelerator performance at a point in time.
/// </summary>
/// <remarks>
/// <para>
/// This snapshot provides detailed performance metrics for kernel execution, memory operations,
/// and overall device utilization. It is designed for performance analysis, optimization,
/// and runtime decision-making.
/// </para>
///
/// <para>
/// Unlike health monitoring which focuses on device availability and hardware status,
/// profiling focuses on execution performance and resource utilization patterns.
/// </para>
///
/// <para>
/// <b>Use Cases:</b>
/// - Performance benchmarking and comparison
/// - Identifying bottlenecks in kernel execution
/// - Memory transfer optimization
/// - Backend selection based on workload characteristics
/// - Real-time performance monitoring dashboards
/// </para>
/// </remarks>
public sealed class ProfilingSnapshot
{
    /// <summary>
    /// Gets the unique identifier for the device being profiled.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets the friendly name of the device being profiled.
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Gets the backend type (e.g., "CUDA", "Metal", "OpenCL", "CPU").
    /// </summary>
    public required string BackendType { get; init; }

    /// <summary>
    /// Gets the timestamp when this snapshot was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets whether profiling data is available for this device.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Gets the collection of profiling metrics for this snapshot.
    /// </summary>
    public IReadOnlyList<ProfilingMetric> Metrics { get; init; } = Array.Empty<ProfilingMetric>();

    /// <summary>
    /// Gets the current kernel execution statistics.
    /// </summary>
    public KernelProfilingStats? KernelStats { get; init; }

    /// <summary>
    /// Gets the current memory operation statistics.
    /// </summary>
    public MemoryProfilingStats? MemoryStats { get; init; }

    /// <summary>
    /// Gets the overall device utilization percentage (0-100).
    /// </summary>
    /// <remarks>
    /// Represents the fraction of time the device was actively executing work.
    /// Values near 100% indicate high utilization, values near 0% indicate idle time.
    /// </remarks>
    public double DeviceUtilizationPercent { get; init; }

    /// <summary>
    /// Gets the total number of operations profiled since last reset.
    /// </summary>
    public long TotalOperations { get; init; }

    /// <summary>
    /// Gets the average operation latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; init; }

    /// <summary>
    /// Gets the throughput in operations per second.
    /// </summary>
    public double ThroughputOpsPerSecond { get; init; }

    /// <summary>
    /// Gets the profiling status message providing context about the current state.
    /// </summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets performance trends identified in recent profiling data.
    /// </summary>
    public IReadOnlyList<string> PerformanceTrends { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets bottlenecks identified in the profiling data.
    /// </summary>
    public IReadOnlyList<string> IdentifiedBottlenecks { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets optimization recommendations based on profiling data.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates an unavailable profiling snapshot when profiling is not supported or enabled.
    /// </summary>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="deviceName">Device name.</param>
    /// <param name="backendType">Backend type.</param>
    /// <param name="reason">Reason why profiling is unavailable.</param>
    /// <returns>A profiling snapshot indicating unavailability.</returns>
    public static ProfilingSnapshot CreateUnavailable(
        string deviceId,
        string deviceName,
        string backendType,
        string reason)
    {
        return new ProfilingSnapshot
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            BackendType = backendType,
            IsAvailable = false,
            StatusMessage = $"Profiling unavailable: {reason}"
        };
    }
}

/// <summary>
/// Represents a single profiling metric with metadata.
/// </summary>
public sealed class ProfilingMetric
{
    /// <summary>
    /// Gets the metric type.
    /// </summary>
    public required ProfilingMetricType Type { get; init; }

    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the metric value.
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// Gets the metric unit (e.g., "ms", "MB", "ops/sec").
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Gets the minimum expected value for this metric (if applicable).
    /// </summary>
    public double? MinValue { get; init; }

    /// <summary>
    /// Gets the maximum expected value for this metric (if applicable).
    /// </summary>
    public double? MaxValue { get; init; }

    /// <summary>
    /// Creates a profiling metric.
    /// </summary>
    public static ProfilingMetric Create(
        ProfilingMetricType type,
        double value,
        string name,
        string? unit = null,
        double? minValue = null,
        double? maxValue = null)
    {
        return new ProfilingMetric
        {
            Type = type,
            Name = name,
            Value = value,
            Unit = unit,
            MinValue = minValue,
            MaxValue = maxValue
        };
    }
}

/// <summary>
/// Type of profiling metric.
/// </summary>
public enum ProfilingMetricType
{
    /// <summary>Kernel execution time metric.</summary>
    KernelExecutionTime,

    /// <summary>Memory transfer time metric.</summary>
    MemoryTransferTime,

    /// <summary>Compilation time metric.</summary>
    CompilationTime,

    /// <summary>Queue wait time metric.</summary>
    QueueWaitTime,

    /// <summary>Device utilization metric.</summary>
    DeviceUtilization,

    /// <summary>Memory bandwidth metric.</summary>
    MemoryBandwidth,

    /// <summary>Throughput metric.</summary>
    Throughput,

    /// <summary>Latency metric.</summary>
    Latency,

    /// <summary>Custom metric.</summary>
    Custom
}

/// <summary>
/// Kernel execution profiling statistics.
/// </summary>
public sealed class KernelProfilingStats
{
    /// <summary>
    /// Gets the total number of kernel executions.
    /// </summary>
    public long TotalExecutions { get; init; }

    /// <summary>
    /// Gets the average kernel execution time in milliseconds.
    /// </summary>
    public double AverageExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the minimum kernel execution time in milliseconds.
    /// </summary>
    public double MinExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the maximum kernel execution time in milliseconds.
    /// </summary>
    public double MaxExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the median kernel execution time in milliseconds.
    /// </summary>
    public double MedianExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the 95th percentile execution time in milliseconds.
    /// </summary>
    public double P95ExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the 99th percentile execution time in milliseconds.
    /// </summary>
    public double P99ExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the standard deviation of execution times.
    /// </summary>
    public double StandardDeviationMs { get; init; }

    /// <summary>
    /// Gets the total kernel execution time in milliseconds.
    /// </summary>
    public double TotalExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the number of failed kernel executions.
    /// </summary>
    public long FailedExecutions { get; init; }

    /// <summary>
    /// Gets the success rate (0.0-1.0).
    /// </summary>
    public double SuccessRate => TotalExecutions > 0
        ? (double)(TotalExecutions - FailedExecutions) / TotalExecutions
        : 1.0;
}

/// <summary>
/// Memory operation profiling statistics.
/// </summary>
public sealed class MemoryProfilingStats
{
    /// <summary>
    /// Gets the total number of memory allocations.
    /// </summary>
    public long TotalAllocations { get; init; }

    /// <summary>
    /// Gets the total bytes allocated.
    /// </summary>
    public long TotalBytesAllocated { get; init; }

    /// <summary>
    /// Gets the number of memory transfers (host-to-device).
    /// </summary>
    public long HostToDeviceTransfers { get; init; }

    /// <summary>
    /// Gets the bytes transferred (host-to-device).
    /// </summary>
    public long HostToDeviceBytes { get; init; }

    /// <summary>
    /// Gets the number of memory transfers (device-to-host).
    /// </summary>
    public long DeviceToHostTransfers { get; init; }

    /// <summary>
    /// Gets the bytes transferred (device-to-host).
    /// </summary>
    public long DeviceToHostBytes { get; init; }

    /// <summary>
    /// Gets the average memory transfer time in milliseconds.
    /// </summary>
    public double AverageTransferTimeMs { get; init; }

    /// <summary>
    /// Gets the memory bandwidth in MB/s.
    /// </summary>
    public double BandwidthMBps { get; init; }

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public long PeakMemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    public long CurrentMemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets the memory utilization percentage (0-100).
    /// </summary>
    public double MemoryUtilizationPercent { get; init; }
}
