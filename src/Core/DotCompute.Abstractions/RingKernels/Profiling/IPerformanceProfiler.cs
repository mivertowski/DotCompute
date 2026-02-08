// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.RingKernels.Profiling;

/// <summary>
/// Performance profiler for ring kernels with baseline tracking and regression detection.
/// </summary>
/// <remarks>
/// <para><b>Features</b>:</para>
/// <list type="bullet">
/// <item>Per-kernel latency histogram tracking (P50/P95/P99/P99.9)</item>
/// <item>Throughput measurement and trending</item>
/// <item>Memory allocation profiling</item>
/// <item>Baseline comparison for regression detection</item>
/// <item>Real-time anomaly detection</item>
/// </list>
/// </remarks>
public interface IPerformanceProfiler : IAsyncDisposable
{
    /// <summary>
    /// Starts profiling a specific kernel.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel.</param>
    /// <param name="options">Profiling options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StartProfilingAsync(string kernelId, ProfilingOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops profiling a kernel and returns the collected metrics.
    /// </summary>
    /// <param name="kernelId">Kernel to stop profiling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Performance report with all collected metrics.</returns>
    public Task<PerformanceReport> StopProfilingAsync(string kernelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a latency sample for a kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="latencyNanos">Latency in nanoseconds.</param>
    public void RecordLatency(string kernelId, long latencyNanos);

    /// <summary>
    /// Records a throughput measurement.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="messagesProcessed">Number of messages processed.</param>
    /// <param name="durationNanos">Duration of the measurement period in nanoseconds.</param>
    public void RecordThroughput(string kernelId, long messagesProcessed, long durationNanos);

    /// <summary>
    /// Records a memory allocation event.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="bytesAllocated">Number of bytes allocated.</param>
    /// <param name="allocationType">Type of allocation (e.g., "GPU", "Host", "Unified").</param>
    public void RecordAllocation(string kernelId, long bytesAllocated, string allocationType);

    /// <summary>
    /// Records a memory deallocation event (for leak detection).
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="bytesDeallocated">Number of bytes deallocated.</param>
    /// <param name="allocationType">Type of allocation (e.g., "GPU", "Host", "Unified").</param>
    public void RecordDeallocation(string kernelId, long bytesDeallocated, string allocationType);

    /// <summary>
    /// Gets memory statistics for a kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>Memory statistics snapshot, or null if not profiling.</returns>
    public MemoryStatistics? GetMemoryStatistics(string kernelId);

    /// <summary>
    /// Gets a real-time snapshot of performance metrics for a kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>Current performance snapshot, or null if not profiling.</returns>
    public PerformanceSnapshot? GetSnapshot(string kernelId);

    /// <summary>
    /// Gets latency percentiles for a kernel.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>Latency percentiles, or null if not profiling.</returns>
    public LatencyPercentiles? GetLatencyPercentiles(string kernelId);

    /// <summary>
    /// Sets a baseline for performance comparison.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <param name="baseline">Baseline metrics to compare against.</param>
    public void SetBaseline(string kernelId, PerformanceBaseline baseline);

    /// <summary>
    /// Compares current performance against the baseline.
    /// </summary>
    /// <param name="kernelId">Kernel identifier.</param>
    /// <returns>Comparison result with regression indicators.</returns>
    public BaselineComparison? CompareToBaseline(string kernelId);

    /// <summary>
    /// Gets all kernels currently being profiled.
    /// </summary>
    public IReadOnlyCollection<string> ProfiledKernels { get; }

    /// <summary>
    /// Occurs when a performance anomaly is detected.
    /// </summary>
    public event EventHandler<PerformanceAnomalyEventArgs>? AnomalyDetected;
}

/// <summary>
/// Options for configuring performance profiling.
/// </summary>
public sealed class ProfilingOptions
{
    /// <summary>
    /// Whether to track latency histogram (P50/P95/P99).
    /// Default: true.
    /// </summary>
    public bool TrackLatencyHistogram { get; init; } = true;

    /// <summary>
    /// Whether to track memory allocations.
    /// Default: true.
    /// </summary>
    public bool TrackAllocations { get; init; } = true;

    /// <summary>
    /// Whether to track throughput over time.
    /// Default: true.
    /// </summary>
    public bool TrackThroughput { get; init; } = true;

    /// <summary>
    /// Sampling rate for latency tracking (1.0 = all, 0.1 = 10%).
    /// Lower sampling reduces overhead but decreases accuracy.
    /// Default: 1.0 (100%).
    /// </summary>
    public double SamplingRate { get; init; } = 1.0;

    /// <summary>
    /// Interval for throughput snapshots.
    /// Default: 1 second.
    /// </summary>
    public TimeSpan ThroughputInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum number of throughput samples to retain.
    /// Default: 3600 (1 hour at 1-second intervals).
    /// </summary>
    public int MaxThroughputSamples { get; init; } = 3600;

    /// <summary>
    /// Whether to enable anomaly detection.
    /// Default: true.
    /// </summary>
    public bool EnableAnomalyDetection { get; init; } = true;

    /// <summary>
    /// Threshold multiplier for anomaly detection (e.g., 3.0 = 3x baseline).
    /// Default: 3.0.
    /// </summary>
    public double AnomalyThreshold { get; init; } = 3.0;

    /// <summary>
    /// Default profiling options.
    /// </summary>
    public static ProfilingOptions Default { get; } = new();
}

/// <summary>
/// Real-time performance snapshot.
/// </summary>
public readonly record struct PerformanceSnapshot
{
    /// <summary>Kernel identifier.</summary>
    public required string KernelId { get; init; }

    /// <summary>Timestamp when snapshot was taken.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Total messages processed.</summary>
    public required long TotalMessagesProcessed { get; init; }

    /// <summary>Current throughput in messages per second.</summary>
    public required double CurrentThroughputMps { get; init; }

    /// <summary>Average throughput over profiling period.</summary>
    public required double AverageThroughputMps { get; init; }

    /// <summary>Peak throughput observed.</summary>
    public required double PeakThroughputMps { get; init; }

    /// <summary>Current P50 latency in microseconds.</summary>
    public required double P50LatencyMicros { get; init; }

    /// <summary>Current P99 latency in microseconds.</summary>
    public required double P99LatencyMicros { get; init; }

    /// <summary>Total bytes allocated.</summary>
    public required long TotalBytesAllocated { get; init; }

    /// <summary>Profiling duration.</summary>
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Complete performance report after profiling session ends.
/// </summary>
public sealed class PerformanceReport
{
    /// <summary>Kernel identifier.</summary>
    public required string KernelId { get; init; }

    /// <summary>When profiling started.</summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>When profiling ended.</summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>Total profiling duration.</summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>Latency percentile statistics.</summary>
    public required LatencyPercentiles Latency { get; init; }

    /// <summary>Throughput statistics.</summary>
    public required ThroughputStatistics Throughput { get; init; }

    /// <summary>Memory allocation statistics.</summary>
    public required AllocationStatistics Allocations { get; init; }

    /// <summary>Baseline comparison if baseline was set.</summary>
    public BaselineComparison? BaselineComparison { get; init; }

    /// <summary>List of anomalies detected during profiling.</summary>
    public required IReadOnlyList<PerformanceAnomaly> Anomalies { get; init; }
}

/// <summary>
/// Throughput statistics over a profiling period.
/// </summary>
public readonly record struct ThroughputStatistics
{
    /// <summary>Total messages processed.</summary>
    public required long TotalMessages { get; init; }

    /// <summary>Average throughput in messages per second.</summary>
    public required double AverageMps { get; init; }

    /// <summary>Peak throughput in messages per second.</summary>
    public required double PeakMps { get; init; }

    /// <summary>Minimum throughput in messages per second.</summary>
    public required double MinMps { get; init; }

    /// <summary>Standard deviation of throughput.</summary>
    public required double StdDevMps { get; init; }

    /// <summary>Number of throughput samples collected.</summary>
    public required int SampleCount { get; init; }
}

/// <summary>
/// Memory allocation statistics.
/// </summary>
public readonly record struct AllocationStatistics
{
    /// <summary>Total bytes allocated.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>Total allocation count.</summary>
    public required long AllocationCount { get; init; }

    /// <summary>GPU memory allocated.</summary>
    public required long GpuBytes { get; init; }

    /// <summary>Host memory allocated.</summary>
    public required long HostBytes { get; init; }

    /// <summary>Unified memory allocated.</summary>
    public required long UnifiedBytes { get; init; }

    /// <summary>Average allocation size.</summary>
    public double AverageAllocationBytes => AllocationCount > 0 ? (double)TotalBytes / AllocationCount : 0;
}

/// <summary>
/// Detailed memory statistics for a kernel.
/// </summary>
public readonly record struct MemoryStatistics
{
    /// <summary>Kernel identifier.</summary>
    public required string KernelId { get; init; }

    /// <summary>Timestamp of the statistics snapshot.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Current GPU memory bytes (allocated - deallocated).</summary>
    public required long CurrentGpuBytes { get; init; }

    /// <summary>Current host memory bytes (allocated - deallocated).</summary>
    public required long CurrentHostBytes { get; init; }

    /// <summary>Current unified memory bytes (allocated - deallocated).</summary>
    public required long CurrentUnifiedBytes { get; init; }

    /// <summary>Peak GPU memory usage.</summary>
    public required long PeakGpuBytes { get; init; }

    /// <summary>Peak host memory usage.</summary>
    public required long PeakHostBytes { get; init; }

    /// <summary>Peak unified memory usage.</summary>
    public required long PeakUnifiedBytes { get; init; }

    /// <summary>Total allocation count.</summary>
    public required long TotalAllocations { get; init; }

    /// <summary>Total deallocation count.</summary>
    public required long TotalDeallocations { get; init; }

    /// <summary>Allocation rate per second (recent).</summary>
    public required double AllocationRatePerSecond { get; init; }

    /// <summary>Current total memory (GPU + Host + Unified).</summary>
    public long CurrentTotalBytes => CurrentGpuBytes + CurrentHostBytes + CurrentUnifiedBytes;

    /// <summary>Peak total memory (GPU + Host + Unified).</summary>
    public long PeakTotalBytes => PeakGpuBytes + PeakHostBytes + PeakUnifiedBytes;

    /// <summary>Indicates potential memory leak (allocations >> deallocations).</summary>
    public bool PotentialLeak => TotalAllocations > 0 &&
                                  TotalDeallocations < TotalAllocations * 0.9 &&
                                  CurrentTotalBytes > PeakTotalBytes * 0.8;

    /// <summary>Memory pressure level (0-1 based on peak usage).</summary>
    public double MemoryPressure => PeakTotalBytes > 0
        ? (double)CurrentTotalBytes / PeakTotalBytes
        : 0;
}

/// <summary>
/// Baseline performance metrics for comparison.
/// </summary>
public sealed class PerformanceBaseline
{
    /// <summary>Baseline identifier (e.g., git commit hash).</summary>
    public required string BaselineId { get; init; }

    /// <summary>When baseline was established.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Expected P50 latency in nanoseconds.</summary>
    public required long ExpectedP50Nanos { get; init; }

    /// <summary>Expected P99 latency in nanoseconds.</summary>
    public required long ExpectedP99Nanos { get; init; }

    /// <summary>Expected throughput in messages per second.</summary>
    public required double ExpectedThroughputMps { get; init; }

    /// <summary>Expected peak memory usage in bytes.</summary>
    public required long ExpectedPeakMemoryBytes { get; init; }

    /// <summary>Tolerance percentage for regression detection (0-1).</summary>
    public double Tolerance { get; init; } = 0.1; // 10% tolerance
}

/// <summary>
/// Result of comparing current performance to baseline.
/// </summary>
public sealed class BaselineComparison
{
    /// <summary>Baseline being compared against.</summary>
    public required PerformanceBaseline Baseline { get; init; }

    /// <summary>P50 latency change ratio (current/baseline).</summary>
    public required double P50LatencyRatio { get; init; }

    /// <summary>P99 latency change ratio (current/baseline).</summary>
    public required double P99LatencyRatio { get; init; }

    /// <summary>Throughput change ratio (current/baseline).</summary>
    public required double ThroughputRatio { get; init; }

    /// <summary>Memory change ratio (current/baseline).</summary>
    public required double MemoryRatio { get; init; }

    /// <summary>Whether P50 latency regressed beyond tolerance.</summary>
    public bool P50Regressed => P50LatencyRatio > 1 + Baseline.Tolerance;

    /// <summary>Whether P99 latency regressed beyond tolerance.</summary>
    public bool P99Regressed => P99LatencyRatio > 1 + Baseline.Tolerance;

    /// <summary>Whether throughput regressed (decreased) beyond tolerance.</summary>
    public bool ThroughputRegressed => ThroughputRatio < 1 - Baseline.Tolerance;

    /// <summary>Whether memory usage regressed (increased) beyond tolerance.</summary>
    public bool MemoryRegressed => MemoryRatio > 1 + Baseline.Tolerance;

    /// <summary>Whether any regression was detected.</summary>
    public bool HasRegression => P50Regressed || P99Regressed || ThroughputRegressed || MemoryRegressed;
}

/// <summary>
/// Performance anomaly detected during profiling.
/// </summary>
public sealed class PerformanceAnomaly
{
    /// <summary>When the anomaly was detected.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Type of anomaly.</summary>
    public required AnomalyType Type { get; init; }

    /// <summary>Severity level.</summary>
    public required AnomalySeverity Severity { get; init; }

    /// <summary>Description of the anomaly.</summary>
    public required string Description { get; init; }

    /// <summary>Observed value that triggered the anomaly.</summary>
    public required double ObservedValue { get; init; }

    /// <summary>Expected value or threshold.</summary>
    public required double ExpectedValue { get; init; }
}

/// <summary>
/// Type of performance anomaly.
/// </summary>
public enum AnomalyType
{
    /// <summary>Latency spike detected.</summary>
    LatencySpike,

    /// <summary>Throughput dropped significantly.</summary>
    ThroughputDrop,

    /// <summary>Memory usage spike.</summary>
    MemorySpike,

    /// <summary>Kernel appears stuck (no progress).</summary>
    StuckKernel,

    /// <summary>Error rate increased.</summary>
    ErrorRateIncrease
}

/// <summary>
/// Severity of performance anomaly.
/// </summary>
public enum AnomalySeverity
{
    /// <summary>Minor anomaly, informational only.</summary>
    Info,

    /// <summary>Warning level, may need attention.</summary>
    Warning,

    /// <summary>Critical anomaly requiring immediate attention.</summary>
    Critical
}

/// <summary>
/// Event args for anomaly detection.
/// </summary>
public sealed class PerformanceAnomalyEventArgs : EventArgs
{
    /// <summary>Kernel where anomaly occurred.</summary>
    public required string KernelId { get; init; }

    /// <summary>The detected anomaly.</summary>
    public required PerformanceAnomaly Anomaly { get; init; }
}
