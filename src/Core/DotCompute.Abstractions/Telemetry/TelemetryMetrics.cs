// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Telemetry;

/// <summary>
/// Immutable record containing telemetry metrics for kernel execution.
/// </summary>
/// <param name="KernelId">Unique identifier for the kernel instance.</param>
/// <param name="ExecutionCount">Total number of executions (successful + failed).</param>
/// <param name="SuccessfulExecutions">Number of successful executions.</param>
/// <param name="FailedExecutions">Number of failed executions.</param>
/// <param name="TotalExecutionTimeNanos">Total execution time in nanoseconds.</param>
/// <param name="AverageLatencyNanos">Average execution latency in nanoseconds.</param>
/// <param name="MinLatencyNanos">Minimum observed latency in nanoseconds.</param>
/// <param name="MaxLatencyNanos">Maximum observed latency in nanoseconds.</param>
/// <param name="ThroughputMessagesPerSecond">Throughput in messages per second (Ring Kernels only).</param>
/// <param name="TotalMemoryAllocatedBytes">Total memory allocated in bytes.</param>
/// <param name="TotalMemoryDeallocatedBytes">Total memory deallocated in bytes.</param>
/// <param name="CurrentMemoryUsageBytes">Current active memory usage in bytes.</param>
/// <param name="PeakMemoryUsageBytes">Peak memory usage observed in bytes.</param>
/// <param name="TotalMessagesProcessed">Total number of messages processed (Ring Kernels only).</param>
/// <param name="CollectionTimestamp">UTC timestamp when metrics were collected.</param>
public readonly record struct TelemetryMetrics(
    string KernelId,
    long ExecutionCount,
    long SuccessfulExecutions,
    long FailedExecutions,
    long TotalExecutionTimeNanos,
    long AverageLatencyNanos,
    long MinLatencyNanos,
    long MaxLatencyNanos,
    double ThroughputMessagesPerSecond,
    long TotalMemoryAllocatedBytes,
    long TotalMemoryDeallocatedBytes,
    long CurrentMemoryUsageBytes,
    long PeakMemoryUsageBytes,
    long TotalMessagesProcessed,
    DateTime CollectionTimestamp
)
{
    /// <summary>
    /// Gets the success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate => ExecutionCount > 0
        ? (SuccessfulExecutions * 100.0) / ExecutionCount
        : 0.0;

    /// <summary>
    /// Gets the failure rate as a percentage (0-100).
    /// </summary>
    public double FailureRate => ExecutionCount > 0
        ? (FailedExecutions * 100.0) / ExecutionCount
        : 0.0;

    /// <summary>
    /// Gets the average memory usage per execution in bytes.
    /// </summary>
    public long AverageMemoryPerExecution => ExecutionCount > 0
        ? TotalMemoryAllocatedBytes / ExecutionCount
        : 0;

    /// <summary>
    /// Gets the memory utilization efficiency (deallocated / allocated).
    /// </summary>
    public double MemoryUtilization => TotalMemoryAllocatedBytes > 0
        ? TotalMemoryDeallocatedBytes / (double)TotalMemoryAllocatedBytes
        : 0.0;

    /// <summary>
    /// Creates an empty metrics instance.
    /// </summary>
    public static TelemetryMetrics Empty(string kernelId) => new(
        KernelId: kernelId,
        ExecutionCount: 0,
        SuccessfulExecutions: 0,
        FailedExecutions: 0,
        TotalExecutionTimeNanos: 0,
        AverageLatencyNanos: 0,
        MinLatencyNanos: long.MaxValue,
        MaxLatencyNanos: 0,
        ThroughputMessagesPerSecond: 0.0,
        TotalMemoryAllocatedBytes: 0,
        TotalMemoryDeallocatedBytes: 0,
        CurrentMemoryUsageBytes: 0,
        PeakMemoryUsageBytes: 0,
        TotalMessagesProcessed: 0,
        CollectionTimestamp: DateTime.UtcNow
    );

    /// <summary>
    /// Formats the metrics as a human-readable string.
    /// </summary>
    public override string ToString()
    {
        return $"""
            Kernel Metrics: {KernelId}
            ========================================
            Execution Count:       {ExecutionCount:N0} ({SuccessRate:F2}% success)
            Average Latency:       {AverageLatencyNanos / 1_000_000.0:F3} ms
            Latency Range:         {MinLatencyNanos / 1_000_000.0:F3} ms - {MaxLatencyNanos / 1_000_000.0:F3} ms
            Throughput:            {ThroughputMessagesPerSecond:F2} msg/s
            Memory Usage:          {CurrentMemoryUsageBytes / 1_048_576.0:F2} MB (Peak: {PeakMemoryUsageBytes / 1_048_576.0:F2} MB)
            Memory Utilization:    {MemoryUtilization * 100:F2}%
            Messages Processed:    {TotalMessagesProcessed:N0}
            Collected:             {CollectionTimestamp:yyyy-MM-dd HH:mm:ss} UTC
            """;
    }
}
