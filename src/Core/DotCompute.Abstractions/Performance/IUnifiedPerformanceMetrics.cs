// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Performance;

/// <summary>
/// Unified performance metrics interface that replaces all duplicate metrics implementations.
/// This is the ONLY performance metrics interface in the entire solution.
/// </summary>
public interface IUnifiedPerformanceMetrics
{
    /// <summary>
    /// Gets the name of the component being measured.
    /// </summary>
    string ComponentName { get; }
    
    /// <summary>
    /// Records a kernel execution.
    /// </summary>
    void RecordKernelExecution(KernelExecutionMetrics metrics);
    
    /// <summary>
    /// Records a memory operation.
    /// </summary>
    void RecordMemoryOperation(MemoryOperationMetrics metrics);
    
    /// <summary>
    /// Records a data transfer.
    /// </summary>
    void RecordDataTransfer(DataTransferMetrics metrics);
    
    /// <summary>
    /// Gets a snapshot of current performance metrics.
    /// </summary>
    PerformanceSnapshot GetSnapshot();
    
    /// <summary>
    /// Resets all metrics.
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Exports metrics in the specified format.
    /// </summary>
    string Export(MetricsExportFormat format);
}

/// <summary>
/// Metrics for kernel execution.
/// </summary>
public sealed class KernelExecutionMetrics
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    public required string KernelName { get; init; }
    
    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; init; }
    
    /// <summary>
    /// Gets or sets the queue wait time in milliseconds.
    /// </summary>
    public double QueueWaitTimeMs { get; init; }
    
    /// <summary>
    /// Gets or sets the number of threads used.
    /// </summary>
    public int ThreadCount { get; init; }
    
    /// <summary>
    /// Gets or sets the number of blocks/workgroups.
    /// </summary>
    public int BlockCount { get; init; }
    
    /// <summary>
    /// Gets or sets the shared memory used in bytes.
    /// </summary>
    public long SharedMemoryBytes { get; init; }
    
    /// <summary>
    /// Gets or sets the register count per thread.
    /// </summary>
    public int RegistersPerThread { get; init; }
    
    /// <summary>
    /// Gets or sets the achieved occupancy (0.0 to 1.0).
    /// </summary>
    public double Occupancy { get; init; }
    
    /// <summary>
    /// Gets or sets the FLOPS achieved.
    /// </summary>
    public double GigaFlops { get; init; }
    
    /// <summary>
    /// Gets or sets the memory bandwidth achieved in GB/s.
    /// </summary>
    public double MemoryBandwidthGBps { get; init; }
    
    /// <summary>
    /// Gets or sets the timestamp of execution.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Metrics for memory operations.
/// </summary>
public sealed class MemoryOperationMetrics
{
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public required MemoryOperationType OperationType { get; init; }
    
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long SizeInBytes { get; init; }
    
    /// <summary>
    /// Gets or sets the duration in milliseconds.
    /// </summary>
    public double DurationMs { get; init; }
    
    /// <summary>
    /// Gets or sets whether the operation was pooled.
    /// </summary>
    public bool WasPooled { get; init; }
    
    /// <summary>
    /// Gets or sets the memory type.
    /// </summary>
    public MemoryType MemoryType { get; init; }
    
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Metrics for data transfers.
/// </summary>
public sealed class DataTransferMetrics
{
    /// <summary>
    /// Gets or sets the transfer direction.
    /// </summary>
    public required TransferDirection Direction { get; init; }
    
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long SizeInBytes { get; init; }
    
    /// <summary>
    /// Gets or sets the transfer time in milliseconds.
    /// </summary>
    public double TransferTimeMs { get; init; }
    
    /// <summary>
    /// Gets or sets the bandwidth achieved in GB/s.
    /// </summary>
    public double BandwidthGBps => SizeInBytes / (TransferTimeMs * 1_000_000.0);
    
    /// <summary>
    /// Gets or sets whether the transfer was asynchronous.
    /// </summary>
    public bool IsAsync { get; init; }
    
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Snapshot of performance metrics at a point in time.
/// </summary>
public sealed class PerformanceSnapshot
{
    /// <summary>
    /// Gets or sets the snapshot timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Gets or sets the duration covered by this snapshot.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    // Kernel Metrics
    public long TotalKernelExecutions { get; init; }
    public double AverageKernelTimeMs { get; init; }
    public double MinKernelTimeMs { get; init; }
    public double MaxKernelTimeMs { get; init; }
    public double TotalKernelTimeMs { get; init; }
    public double AverageOccupancy { get; init; }
    public double PeakGigaFlops { get; init; }
    
    // Memory Metrics
    public long TotalMemoryAllocations { get; init; }
    public long TotalMemoryDeallocations { get; init; }
    public long CurrentMemoryUsageBytes { get; init; }
    public long PeakMemoryUsageBytes { get; init; }
    public double MemoryPoolHitRate { get; init; }
    
    // Transfer Metrics
    public long TotalDataTransfers { get; init; }
    public long TotalBytesTransferred { get; init; }
    public double AverageTransferBandwidthGBps { get; init; }
    public double PeakTransferBandwidthGBps { get; init; }
    
    // Throughput Metrics
    public double OperationsPerSecond { get; init; }
    public double BytesPerSecond { get; init; }
    
    // Error Metrics
    public long ErrorCount { get; init; }
    public long RecoveredErrorCount { get; init; }
    
    /// <summary>
    /// Gets detailed kernel statistics by name.
    /// </summary>
    public IReadOnlyDictionary<string, KernelStatistics> KernelStatistics { get; init; } = 
        new Dictionary<string, KernelStatistics>();
}

/// <summary>
/// Statistics for a specific kernel.
/// </summary>
public sealed class KernelStatistics
{
    public string KernelName { get; init; } = string.Empty;
    public long ExecutionCount { get; init; }
    public double TotalTimeMs { get; init; }
    public double AverageTimeMs { get; init; }
    public double MinTimeMs { get; init; }
    public double MaxTimeMs { get; init; }
    public double StandardDeviationMs { get; init; }
    public double AverageOccupancy { get; init; }
}

/// <summary>
/// Memory operation types.
/// </summary>
public enum MemoryOperationType
{
    Allocate,
    Deallocate,
    Resize,
    Defragment,
    Pool
}

/// <summary>
/// Data transfer directions.
/// </summary>
public enum TransferDirection
{
    HostToDevice,
    DeviceToHost,
    DeviceToDevice,
    HostToHost,
    PeerToPeer
}

/// <summary>
/// Metrics export formats.
/// </summary>
public enum MetricsExportFormat
{
    Json,
    Csv,
    Prometheus,
    StatsD,
    OpenTelemetry
}

/// <summary>
/// High-resolution timer for performance measurements.
/// </summary>
public sealed class PerformanceTimer : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly IUnifiedPerformanceMetrics? _metrics;
    private readonly string _operationName;
    
    public PerformanceTimer(string operationName, IUnifiedPerformanceMetrics? metrics = null)
    {
        _operationName = operationName;
        _metrics = metrics;
        _stopwatch = Stopwatch.StartNew();
    }
    
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    
    public double ElapsedMilliseconds => _stopwatch.Elapsed.TotalMilliseconds;
    
    public void Dispose()
    {
        _stopwatch.Stop();
        // Optionally record to metrics
        if (_metrics != null)
        {
            // Record based on operation type
        }
    }
}