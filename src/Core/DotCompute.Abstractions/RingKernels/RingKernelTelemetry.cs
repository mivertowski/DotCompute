// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Abstractions.RingKernels;

/// <summary>
/// Ring kernel telemetry data collected on the GPU and polled by the CPU.
/// This struct is cache-line aligned (64 bytes) for optimal GPU memory access.
/// All fields use atomic operations for thread-safe GPU updates.
/// </summary>
/// <remarks>
/// Ring kernels run indefinitely in infinite loops, making traditional debugging impossible.
/// Telemetry enables real-time monitoring of kernel health, message throughput, and latency.
///
/// <para><b>Usage Pattern</b>:</para>
/// <code>
/// // GPU side (auto-injected by source generator):
/// telemetry[0].MessagesProcessed++;
/// telemetry[0].LastProcessedTimestamp = GetGpuTimestamp();
///
/// // CPU side (polling):
/// var telemetry = await runtime.GetTelemetryAsync(kernelId);
/// Console.WriteLine($"Throughput: {telemetry.MessagesProcessed / uptime} msg/s");
/// </code>
///
/// <para><b>Performance</b>:</para>
/// <list type="bullet">
/// <item>GPU update overhead: &lt;50ns per message (atomic increment)</item>
/// <item>CPU polling latency: &lt;1μs (zero-copy pinned host memory)</item>
/// <item>Memory footprint: 64 bytes per kernel</item>
/// </list>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 64)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1815:Override equals and operator equals on value types",
    Justification = "Telemetry is mutable state container, not a value type for comparison")]
public struct RingKernelTelemetry
{
    /// <summary>
    /// Total number of messages successfully processed since kernel launch.
    /// Updated atomically on GPU via atomic_add or Interlocked.Increment.
    /// </summary>
    /// <remarks>
    /// Use this field to calculate throughput: MessagesProcessed / uptime.
    /// For stuck kernel detection: if this value doesn't change for N seconds,
    /// the kernel may be deadlocked or idle.
    /// </remarks>
    public ulong MessagesProcessed;

    /// <summary>
    /// Total number of messages dropped due to backpressure or validation failures.
    /// Incremented when queue is full (BackpressureStrategy.DropOldest/DropNew)
    /// or when messages fail validation before enqueuing to dead letter queue.
    /// </summary>
    public ulong MessagesDropped;

    /// <summary>
    /// GPU timestamp (nanoseconds) of the last successfully processed message.
    /// Obtained from ITimingProvider.GetTimestampAsync() (Phase 1 timing API).
    /// </summary>
    /// <remarks>
    /// On CUDA Compute Capability 6.0+: 1ns resolution via globaltimer.
    /// On CUDA CC 5.0: 1μs resolution via CUDA events.
    /// On OpenCL/Metal: implementation-dependent resolution.
    ///
    /// Use for stuck kernel detection:
    /// if (currentTime - LastProcessedTimestamp > timeout) { /* kernel stuck */ }
    /// </remarks>
    public long LastProcessedTimestamp;

    /// <summary>
    /// Current depth of the input message queue (number of pending messages).
    /// Updated on each kernel iteration to reflect queue size.
    /// </summary>
    /// <remarks>
    /// Use for backpressure monitoring:
    /// - Low values (&lt;10% capacity): Kernel is keeping up with message rate
    /// - High values (&gt;80% capacity): Risk of queue overflow, consider scaling
    /// - Full capacity: Backpressure strategy is actively dropping/rejecting messages
    /// </remarks>
    public int QueueDepth;

    /// <summary>
    /// Cumulative processing latency in nanoseconds across all processed messages.
    /// Sum of (dequeue timestamp - enqueue timestamp) for each message.
    /// </summary>
    /// <remarks>
    /// Calculate average latency: TotalLatencyNanos / MessagesProcessed.
    /// For detailed P50/P99 metrics, enable TrackLatency attribute (Phase 2.2).
    /// </remarks>
    public ulong TotalLatencyNanos;

    /// <summary>
    /// Peak message processing latency observed in nanoseconds.
    /// Updated when a message's latency exceeds current max.
    /// </summary>
    /// <remarks>
    /// Useful for detecting outliers and tail latency issues.
    /// High MaxLatencyNanos may indicate:
    /// - GPU memory contention
    /// - Complex message processing
    /// - Context switches (if sharing GPU with other kernels)
    /// </remarks>
    public ulong MaxLatencyNanos;

    /// <summary>
    /// Minimum message processing latency observed in nanoseconds.
    /// Represents best-case performance under ideal conditions.
    /// </summary>
    /// <remarks>
    /// Compare with MaxLatencyNanos to understand latency variance.
    /// Large variance (MaxLatencyNanos / MinLatencyNanos > 10) suggests:
    /// - Inconsistent message complexity
    /// - GPU thermal throttling
    /// - External system interference
    /// </remarks>
    public ulong MinLatencyNanos;

    /// <summary>
    /// Last error code reported by the ring kernel (0 = no error).
    /// Custom error codes defined by application (e.g., 1 = OOM, 2 = invalid message).
    /// </summary>
    /// <remarks>
    /// GPU kernel can set this field when encountering errors:
    /// <code>
    /// if (outOfMemory)
    /// {
    ///     telemetry[0].ErrorCode = 1;
    ///     return; // Early exit
    /// }
    /// </code>
    ///
    /// CPU can poll for errors:
    /// <code>
    /// if (telemetry.ErrorCode != 0)
    /// {
    ///     logger.LogError($"Kernel error: {telemetry.ErrorCode}");
    /// }
    /// </code>
    /// </remarks>
    public ushort ErrorCode;

    /// <summary>
    /// Reserved for future expansion (maintains 64-byte alignment).
    /// Do not use in application code.
    /// </summary>
    public ushort Reserved;

    // Padding to ensure 64-byte cache-line alignment (7 * 8 + 2 + 2 = 60 bytes, need 4 more)
    private readonly uint _padding1;

    /// <summary>
    /// Initializes a new instance of <see cref="RingKernelTelemetry"/> with default values.
    /// Sets MinLatencyNanos to ulong.MaxValue (will be updated on first message).
    /// </summary>
    public RingKernelTelemetry()
    {
        MessagesProcessed = 0;
        MessagesDropped = 0;
        LastProcessedTimestamp = 0;
        QueueDepth = 0;
        TotalLatencyNanos = 0;
        MaxLatencyNanos = 0;
        MinLatencyNanos = ulong.MaxValue;  // Will be updated on first message
        ErrorCode = 0;
        Reserved = 0;
        _padding1 = 0;
    }

    /// <summary>
    /// Calculates the average message processing latency in nanoseconds.
    /// Returns 0 if no messages have been processed.
    /// </summary>
    public readonly ulong AverageLatencyNanos =>
        MessagesProcessed > 0 ? TotalLatencyNanos / MessagesProcessed : 0;

    /// <summary>
    /// Gets the current message throughput in messages per second.
    /// Requires uptime in seconds to calculate.
    /// </summary>
    /// <param name="uptimeSeconds">Kernel uptime in seconds.</param>
    /// <returns>Messages per second, or 0 if uptime is 0.</returns>
    public readonly double GetThroughput(double uptimeSeconds) =>
        uptimeSeconds > 0 ? MessagesProcessed / uptimeSeconds : 0.0;

    /// <summary>
    /// Indicates whether the kernel is healthy (processing messages and no errors).
    /// A kernel is considered stuck if it hasn't processed messages for too long.
    /// </summary>
    /// <param name="currentTimestamp">Current GPU timestamp in nanoseconds.</param>
    /// <param name="stuckThresholdNanos">Threshold in nanoseconds (default: 1 second).</param>
    /// <returns>True if kernel is healthy; false if stuck or errored.</returns>
    public readonly bool IsHealthy(long currentTimestamp, long stuckThresholdNanos = 1_000_000_000)
    {
        // Check for errors
        if (ErrorCode != 0)
        {
            return false;
        }

        // Check if stuck (no messages processed in threshold time)
        if (MessagesProcessed > 0 && currentTimestamp - LastProcessedTimestamp > stuckThresholdNanos)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates the latency variance (MaxLatency / MinLatency ratio).
    /// High variance (&gt;10) indicates inconsistent performance.
    /// </summary>
    /// <returns>Variance ratio, or 0 if MinLatencyNanos is still at initial value.</returns>
    public readonly double GetLatencyVariance() =>
        MinLatencyNanos < ulong.MaxValue && MinLatencyNanos > 0
            ? (double)MaxLatencyNanos / MinLatencyNanos
            : 0.0;
}
