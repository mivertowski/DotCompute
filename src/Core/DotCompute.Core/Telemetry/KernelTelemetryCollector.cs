// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions.Telemetry;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Thread-safe production implementation of kernel telemetry collection.
/// </summary>
/// <remarks>
/// Uses lock-free concurrent data structures for minimal contention during
/// high-throughput kernel execution. Maintains per-kernel metrics with
/// automatic aggregation and statistical analysis.
/// </remarks>
public sealed class KernelTelemetryCollector : IKernelTelemetryProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, KernelMetricsState> _metrics = new();
    private readonly ILogger<KernelTelemetryCollector> _logger;
    private readonly Stopwatch _globalStopwatch = Stopwatch.StartNew();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelTelemetryCollector"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public KernelTelemetryCollector(ILogger<KernelTelemetryCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
#pragma warning disable XFIX003 // Use LoggerMessage.Define - acceptable for initialization
        _logger.LogInformation("Kernel telemetry collector initialized");
#pragma warning restore XFIX003
    }

    /// <inheritdoc/>
    public void RecordExecutionStart(string kernelId, long timestamp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var state = GetOrCreateState(kernelId);
        state.RecordStart(timestamp);
    }

    /// <inheritdoc/>
    public void RecordExecutionEnd(string kernelId, long timestamp, bool success)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var state = GetOrCreateState(kernelId);
        state.RecordEnd(timestamp, success);
    }

    /// <inheritdoc/>
    public void RecordMemoryAllocation(string kernelId, long bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var state = GetOrCreateState(kernelId);
        state.RecordAllocation(bytes);
    }

    /// <inheritdoc/>
    public void RecordMemoryDeallocation(string kernelId, long bytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var state = GetOrCreateState(kernelId);
        state.RecordDeallocation(bytes);
    }

    /// <inheritdoc/>
    public void RecordMessageProcessed(string kernelId, int messageSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var state = GetOrCreateState(kernelId);
        state.RecordMessage(messageSize);
    }

    /// <inheritdoc/>
    public TelemetryMetrics GetMetrics(string kernelId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _metrics.TryGetValue(kernelId, out var state)
            ? state.ToMetrics()
            : TelemetryMetrics.Empty(kernelId);
    }

    /// <inheritdoc/>
    public TelemetryMetrics GetAggregatedMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_metrics.IsEmpty)
        {
            return TelemetryMetrics.Empty("__aggregated__");
        }

        long totalExecutions = 0;
        long totalSuccessful = 0;
        long totalFailed = 0;
        long totalTimeNanos = 0;
        var minLatency = long.MaxValue;
        long maxLatency = 0;
        long totalMemoryAllocated = 0;
        long totalMemoryDeallocated = 0;
        long currentMemory = 0;
        long peakMemory = 0;
        long totalMessages = 0;

        foreach (var state in _metrics.Values)
        {
            totalExecutions += state.ExecutionCount;
            totalSuccessful += state.SuccessfulExecutions;
            totalFailed += state.FailedExecutions;
            totalTimeNanos += state.TotalExecutionTimeNanos;
            minLatency = Math.Min(minLatency, state.MinLatencyNanos);
            maxLatency = Math.Max(maxLatency, state.MaxLatencyNanos);
            totalMemoryAllocated += state.TotalMemoryAllocatedBytes;
            totalMemoryDeallocated += state.TotalMemoryDeallocatedBytes;
            currentMemory += state.CurrentMemoryUsageBytes;
            peakMemory = Math.Max(peakMemory, state.PeakMemoryUsageBytes);
            totalMessages += state.TotalMessagesProcessed;
        }

        var avgLatency = totalExecutions > 0 ? totalTimeNanos / totalExecutions : 0;
        var elapsedSeconds = _globalStopwatch.Elapsed.TotalSeconds;
        var throughput = elapsedSeconds > 0 ? totalMessages / elapsedSeconds : 0.0;

        return new TelemetryMetrics(
            KernelId: "__aggregated__",
            ExecutionCount: totalExecutions,
            SuccessfulExecutions: totalSuccessful,
            FailedExecutions: totalFailed,
            TotalExecutionTimeNanos: totalTimeNanos,
            AverageLatencyNanos: avgLatency,
            MinLatencyNanos: minLatency == long.MaxValue ? 0 : minLatency,
            MaxLatencyNanos: maxLatency,
            ThroughputMessagesPerSecond: throughput,
            TotalMemoryAllocatedBytes: totalMemoryAllocated,
            TotalMemoryDeallocatedBytes: totalMemoryDeallocated,
            CurrentMemoryUsageBytes: currentMemory,
            PeakMemoryUsageBytes: peakMemory,
            TotalMessagesProcessed: totalMessages,
            CollectionTimestamp: DateTime.UtcNow
        );
    }

    /// <inheritdoc/>
    public void ResetMetrics(string kernelId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_metrics.TryRemove(kernelId, out _))
        {
#pragma warning disable XFIX003 // Use LoggerMessage.Define - acceptable for reset operations
            _logger.LogInformation("Reset metrics for kernel: {KernelId}", kernelId);
#pragma warning restore XFIX003
        }
    }

    /// <inheritdoc/>
    public void ResetAllMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _metrics.Clear();
        _globalStopwatch.Restart();
#pragma warning disable XFIX003 // Use LoggerMessage.Define - acceptable for reset operations
        _logger.LogInformation("Reset all kernel metrics");
#pragma warning restore XFIX003
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _globalStopwatch.Stop();
#pragma warning disable XFIX003 // Use LoggerMessage.Define - acceptable for disposal
        _logger.LogInformation("Kernel telemetry collector disposed. Final metrics: {Count} kernels tracked", _metrics.Count);
#pragma warning restore XFIX003
        _disposed = true;
    }

    private KernelMetricsState GetOrCreateState(string kernelId)
    {
        return _metrics.GetOrAdd(kernelId, _ => new KernelMetricsState(kernelId, _globalStopwatch));
    }

    /// <summary>
    /// Thread-safe mutable state for a single kernel's metrics.
    /// </summary>
    private sealed class KernelMetricsState
    {
        private readonly string _kernelId;
        private readonly Stopwatch _globalStopwatch;
        private long _executionCount;
        private long _successfulExecutions;
        private long _failedExecutions;
        private long _totalExecutionTimeNanos;
        private long _minLatencyNanos = long.MaxValue;
        private long _maxLatencyNanos;
        private long _totalMemoryAllocatedBytes;
        private long _totalMemoryDeallocatedBytes;
        private long _currentMemoryUsageBytes;
        private long _peakMemoryUsageBytes;
        private long _totalMessagesProcessed;
        private long _startTimestamp;

        public long ExecutionCount => Interlocked.Read(ref _executionCount);
        public long SuccessfulExecutions => Interlocked.Read(ref _successfulExecutions);
        public long FailedExecutions => Interlocked.Read(ref _failedExecutions);
        public long TotalExecutionTimeNanos => Interlocked.Read(ref _totalExecutionTimeNanos);
        public long MinLatencyNanos => Interlocked.Read(ref _minLatencyNanos);
        public long MaxLatencyNanos => Interlocked.Read(ref _maxLatencyNanos);
        public long TotalMemoryAllocatedBytes => Interlocked.Read(ref _totalMemoryAllocatedBytes);
        public long TotalMemoryDeallocatedBytes => Interlocked.Read(ref _totalMemoryDeallocatedBytes);
        public long CurrentMemoryUsageBytes => Interlocked.Read(ref _currentMemoryUsageBytes);
        public long PeakMemoryUsageBytes => Interlocked.Read(ref _peakMemoryUsageBytes);
        public long TotalMessagesProcessed => Interlocked.Read(ref _totalMessagesProcessed);

        public KernelMetricsState(string kernelId, Stopwatch globalStopwatch)
        {
            _kernelId = kernelId;
            _globalStopwatch = globalStopwatch;
        }

        public void RecordStart(long timestamp)
        {
            Interlocked.Exchange(ref _startTimestamp, timestamp);
        }

        public void RecordEnd(long timestamp, bool success)
        {
            var start = Interlocked.Read(ref _startTimestamp);
            var latencyNanos = timestamp - start;

            Interlocked.Increment(ref _executionCount);
            if (success)
            {
                Interlocked.Increment(ref _successfulExecutions);
            }
            else
            {
                Interlocked.Increment(ref _failedExecutions);
            }

            Interlocked.Add(ref _totalExecutionTimeNanos, latencyNanos);

            // Update min latency (lock-free compare-and-swap)
            long currentMin;
            do
            {
                currentMin = Interlocked.Read(ref _minLatencyNanos);
                if (latencyNanos >= currentMin)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _minLatencyNanos, latencyNanos, currentMin) != currentMin);

            // Update max latency (lock-free compare-and-swap)
            long currentMax;
            do
            {
                currentMax = Interlocked.Read(ref _maxLatencyNanos);
                if (latencyNanos <= currentMax)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _maxLatencyNanos, latencyNanos, currentMax) != currentMax);
        }

        public void RecordAllocation(long bytes)
        {
            Interlocked.Add(ref _totalMemoryAllocatedBytes, bytes);
            var newCurrent = Interlocked.Add(ref _currentMemoryUsageBytes, bytes);

            // Update peak memory (lock-free compare-and-swap)
            long currentPeak;
            do
            {
                currentPeak = Interlocked.Read(ref _peakMemoryUsageBytes);
                if (newCurrent <= currentPeak)
                {
                    break;
                }
            } while (Interlocked.CompareExchange(ref _peakMemoryUsageBytes, newCurrent, currentPeak) != currentPeak);
        }

        public void RecordDeallocation(long bytes)
        {
            Interlocked.Add(ref _totalMemoryDeallocatedBytes, bytes);
            Interlocked.Add(ref _currentMemoryUsageBytes, -bytes);
        }

        public void RecordMessage(int messageSize)
        {
            Interlocked.Increment(ref _totalMessagesProcessed);
        }

        public TelemetryMetrics ToMetrics()
        {
            var execCount = ExecutionCount;
            var totalTimeNanos = TotalExecutionTimeNanos;
            var avgLatency = execCount > 0 ? totalTimeNanos / execCount : 0;

            var elapsedSeconds = _globalStopwatch.Elapsed.TotalSeconds;
            var throughput = elapsedSeconds > 0 ? TotalMessagesProcessed / elapsedSeconds : 0.0;

            return new TelemetryMetrics(
                KernelId: _kernelId,
                ExecutionCount: execCount,
                SuccessfulExecutions: SuccessfulExecutions,
                FailedExecutions: FailedExecutions,
                TotalExecutionTimeNanos: totalTimeNanos,
                AverageLatencyNanos: avgLatency,
                MinLatencyNanos: MinLatencyNanos == long.MaxValue ? 0 : MinLatencyNanos,
                MaxLatencyNanos: MaxLatencyNanos,
                ThroughputMessagesPerSecond: throughput,
                TotalMemoryAllocatedBytes: TotalMemoryAllocatedBytes,
                TotalMemoryDeallocatedBytes: TotalMemoryDeallocatedBytes,
                CurrentMemoryUsageBytes: CurrentMemoryUsageBytes,
                PeakMemoryUsageBytes: PeakMemoryUsageBytes,
                TotalMessagesProcessed: TotalMessagesProcessed,
                CollectionTimestamp: DateTime.UtcNow
            );
        }
    }
}
