// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.RingKernels.Profiling;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.RingKernels.Profiling;

/// <summary>
/// Implementation of performance profiler for ring kernels.
/// Provides latency histogram tracking, throughput measurement, and baseline comparison.
/// </summary>
public sealed partial class RingKernelPerformanceProfiler : IPerformanceProfiler
{
    private readonly ILogger<RingKernelPerformanceProfiler> _logger;
    private readonly ConcurrentDictionary<string, KernelProfilingSession> _sessions = new();
    private bool _disposed;

    // Event IDs: 9100-9199 for RingKernelPerformanceProfiler
    [LoggerMessage(EventId = 9100, Level = LogLevel.Information,
        Message = "Started profiling ring kernel '{KernelId}' with sampling rate {SamplingRate:P0}")]
    private static partial void LogProfilingStarted(ILogger logger, string kernelId, double samplingRate);

    [LoggerMessage(EventId = 9101, Level = LogLevel.Information,
        Message = "Stopped profiling ring kernel '{KernelId}'. Duration: {Duration:g}, Messages: {MessageCount}, P99: {P99Latency:F2}μs")]
    private static partial void LogProfilingStopped(ILogger logger, string kernelId, TimeSpan duration, long messageCount, double p99Latency);

    [LoggerMessage(EventId = 9102, Level = LogLevel.Information,
        Message = "Set baseline '{BaselineId}' for ring kernel '{KernelId}'")]
    private static partial void LogBaselineSet(ILogger logger, string baselineId, string kernelId);

    [LoggerMessage(EventId = 9103, Level = LogLevel.Warning,
        Message = "Performance anomaly detected in ring kernel '{KernelId}': {AnomalyType} - {Description} (observed: {ObservedValue:F2}, expected: {ExpectedValue:F2})")]
    private static partial void LogAnomalyDetected(ILogger logger, string kernelId, AnomalyType anomalyType, string description, double observedValue, double expectedValue);

    [LoggerMessage(EventId = 9104, Level = LogLevel.Error,
        Message = "Error stopping profiling for ring kernel '{KernelId}'")]
    private static partial void LogStopProfilingError(ILogger logger, Exception ex, string kernelId);

    /// <inheritdoc />
    public IReadOnlyCollection<string> ProfiledKernels => _sessions.Keys.ToList();

    /// <inheritdoc />
    public event EventHandler<PerformanceAnomalyEventArgs>? AnomalyDetected;

    /// <summary>
    /// Creates a new performance profiler instance.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public RingKernelPerformanceProfiler(ILogger<RingKernelPerformanceProfiler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartProfilingAsync(string kernelId, ProfilingOptions? options = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        options ??= ProfilingOptions.Default;

        var session = new KernelProfilingSession(kernelId, options, OnAnomalyDetected);

        if (!_sessions.TryAdd(kernelId, session))
        {
            throw new InvalidOperationException($"Profiling already active for kernel '{kernelId}'");
        }

        LogProfilingStarted(_logger, kernelId, options.SamplingRate);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PerformanceReport> StopProfilingAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (!_sessions.TryRemove(kernelId, out var session))
        {
            throw new InvalidOperationException($"No active profiling session for kernel '{kernelId}'");
        }

        var report = session.GenerateReport();
        session.Dispose();

        LogProfilingStopped(_logger, kernelId, report.Duration, report.Latency.TotalCount, report.Latency.P99Micros);

        return Task.FromResult(report);
    }

    /// <inheritdoc />
    public void RecordLatency(string kernelId, long latencyNanos)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return;
        }

        if (_sessions.TryGetValue(kernelId, out var session))
        {
            session.RecordLatency(latencyNanos);
        }
    }

    /// <inheritdoc />
    public void RecordThroughput(string kernelId, long messagesProcessed, long durationNanos)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return;
        }

        if (_sessions.TryGetValue(kernelId, out var session))
        {
            session.RecordThroughput(messagesProcessed, durationNanos);
        }
    }

    /// <inheritdoc />
    public void RecordAllocation(string kernelId, long bytesAllocated, string allocationType)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return;
        }

        if (_sessions.TryGetValue(kernelId, out var session))
        {
            session.RecordAllocation(bytesAllocated, allocationType);
        }
    }

    /// <inheritdoc />
    public void RecordDeallocation(string kernelId, long bytesDeallocated, string allocationType)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return;
        }

        if (_sessions.TryGetValue(kernelId, out var session))
        {
            session.RecordDeallocation(bytesDeallocated, allocationType);
        }
    }

    /// <inheritdoc />
    public MemoryStatistics? GetMemoryStatistics(string kernelId)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return null;
        }

        return _sessions.TryGetValue(kernelId, out var session) ? session.GetMemoryStatistics() : null;
    }

    /// <inheritdoc />
    public PerformanceSnapshot? GetSnapshot(string kernelId)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return null;
        }

        return _sessions.TryGetValue(kernelId, out var session) ? session.GetSnapshot() : null;
    }

    /// <inheritdoc />
    public LatencyPercentiles? GetLatencyPercentiles(string kernelId)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return null;
        }

        return _sessions.TryGetValue(kernelId, out var session) ? session.GetLatencyPercentiles() : null;
    }

    /// <inheritdoc />
    public void SetBaseline(string kernelId, PerformanceBaseline baseline)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ArgumentNullException.ThrowIfNull(baseline);

        if (_sessions.TryGetValue(kernelId, out var session))
        {
            session.SetBaseline(baseline);
            LogBaselineSet(_logger, baseline.BaselineId, kernelId);
        }
    }

    /// <inheritdoc />
    public BaselineComparison? CompareToBaseline(string kernelId)
    {
        if (_disposed || string.IsNullOrEmpty(kernelId))
        {
            return null;
        }

        return _sessions.TryGetValue(kernelId, out var session) ? session.CompareToBaseline() : null;
    }

    private void OnAnomalyDetected(string kernelId, PerformanceAnomaly anomaly)
    {
        LogAnomalyDetected(_logger, kernelId, anomaly.Type, anomaly.Description, anomaly.ObservedValue, anomaly.ExpectedValue);

        AnomalyDetected?.Invoke(this, new PerformanceAnomalyEventArgs
        {
            KernelId = kernelId,
            Anomaly = anomaly
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop all profiling sessions
        foreach (var kvp in _sessions)
        {
            try
            {
                await StopProfilingAsync(kvp.Key);
            }
            catch (Exception ex)
            {
                LogStopProfilingError(_logger, ex, kvp.Key);
            }
        }

        _sessions.Clear();
    }
}

/// <summary>
/// Per-kernel profiling session state.
/// </summary>
internal sealed class KernelProfilingSession : IDisposable
{
    private readonly string _kernelId;
    private readonly ProfilingOptions _options;
    private readonly Action<string, PerformanceAnomaly> _anomalyCallback;
    private readonly LatencyHistogram _latencyHistogram = new();
    private readonly List<ThroughputSample> _throughputSamples = new();
    private readonly object _throughputLock = new();
    private readonly List<PerformanceAnomaly> _anomalies = new();
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    private long _totalMessagesProcessed;
    private long _gpuBytesAllocated;
    private long _hostBytesAllocated;
    private long _unifiedBytesAllocated;
    private long _gpuBytesDeallocated;
    private long _hostBytesDeallocated;
    private long _unifiedBytesDeallocated;
    private long _peakGpuBytes;
    private long _peakHostBytes;
    private long _peakUnifiedBytes;
    private long _allocationCount;
    private long _deallocationCount;
    private readonly Queue<DateTimeOffset> _recentAllocations = new();
    private readonly object _allocationLock = new();
    private double _peakThroughput;
    private double _lastThroughput;
    private PerformanceBaseline? _baseline;
    private bool _disposed;

    public KernelProfilingSession(
        string kernelId,
        ProfilingOptions options,
        Action<string, PerformanceAnomaly> anomalyCallback)
    {
        _kernelId = kernelId;
        _options = options;
        _anomalyCallback = anomalyCallback;
    }

    public void RecordLatency(long latencyNanos)
    {
        if (_disposed || !_options.TrackLatencyHistogram)
        {
            return;
        }

        // Apply sampling using thread-safe Random.Shared (not for security purposes)
#pragma warning disable CA5394 // Random is not cryptographically secure - acceptable for performance sampling
        if (_options.SamplingRate < 1.0 && Random.Shared.NextDouble() > _options.SamplingRate)
        {
            return;
        }
#pragma warning restore CA5394

        _latencyHistogram.Record(latencyNanos);

        // Check for anomaly
        if (_options.EnableAnomalyDetection && _baseline != null)
        {
            CheckLatencyAnomaly(latencyNanos);
        }
    }

    public void RecordThroughput(long messagesProcessed, long durationNanos)
    {
        if (_disposed || !_options.TrackThroughput || durationNanos <= 0)
        {
            return;
        }

        Interlocked.Add(ref _totalMessagesProcessed, messagesProcessed);

        var throughputMps = messagesProcessed / (durationNanos / 1_000_000_000.0);

        lock (_throughputLock)
        {
            _throughputSamples.Add(new ThroughputSample
            {
                Timestamp = DateTimeOffset.UtcNow,
                MessagesPerSecond = throughputMps
            });

            // Trim old samples
            while (_throughputSamples.Count > _options.MaxThroughputSamples)
            {
                _throughputSamples.RemoveAt(0);
            }

            _lastThroughput = throughputMps;
            if (throughputMps > _peakThroughput)
            {
                _peakThroughput = throughputMps;
            }
        }

        // Check for anomaly
        if (_options.EnableAnomalyDetection && _baseline != null)
        {
            CheckThroughputAnomaly(throughputMps);
        }
    }

    public void RecordAllocation(long bytesAllocated, string allocationType)
    {
        if (_disposed || !_options.TrackAllocations)
        {
            return;
        }

        Interlocked.Increment(ref _allocationCount);

        switch (allocationType.ToUpperInvariant())
        {
            case "GPU":
            case "DEVICE":
                var newGpu = Interlocked.Add(ref _gpuBytesAllocated, bytesAllocated);
                UpdatePeakGpu(newGpu - _gpuBytesDeallocated);
                break;
            case "HOST":
            case "PINNED":
                var newHost = Interlocked.Add(ref _hostBytesAllocated, bytesAllocated);
                UpdatePeakHost(newHost - _hostBytesDeallocated);
                break;
            case "UNIFIED":
            case "MANAGED":
                var newUnified = Interlocked.Add(ref _unifiedBytesAllocated, bytesAllocated);
                UpdatePeakUnified(newUnified - _unifiedBytesDeallocated);
                break;
        }

        // Track allocation timing for rate calculation
        lock (_allocationLock)
        {
            _recentAllocations.Enqueue(DateTimeOffset.UtcNow);
            // Keep only last 60 seconds of allocations
            var cutoff = DateTimeOffset.UtcNow.AddSeconds(-60);
            while (_recentAllocations.Count > 0 && _recentAllocations.Peek() < cutoff)
            {
                _recentAllocations.Dequeue();
            }
        }

        // Check for memory spike anomaly
        if (_options.EnableAnomalyDetection && _baseline != null)
        {
            CheckMemoryAnomaly();
        }
    }

    public void RecordDeallocation(long bytesDeallocated, string allocationType)
    {
        if (_disposed || !_options.TrackAllocations)
        {
            return;
        }

        Interlocked.Increment(ref _deallocationCount);

        switch (allocationType.ToUpperInvariant())
        {
            case "GPU":
            case "DEVICE":
                Interlocked.Add(ref _gpuBytesDeallocated, bytesDeallocated);
                break;
            case "HOST":
            case "PINNED":
                Interlocked.Add(ref _hostBytesDeallocated, bytesDeallocated);
                break;
            case "UNIFIED":
            case "MANAGED":
                Interlocked.Add(ref _unifiedBytesDeallocated, bytesDeallocated);
                break;
        }
    }

    public MemoryStatistics GetMemoryStatistics()
    {
        var currentGpu = _gpuBytesAllocated - _gpuBytesDeallocated;
        var currentHost = _hostBytesAllocated - _hostBytesDeallocated;
        var currentUnified = _unifiedBytesAllocated - _unifiedBytesDeallocated;

        double allocationRate;
        lock (_allocationLock)
        {
            var windowSeconds = Math.Max(1, (DateTimeOffset.UtcNow - _startTime).TotalSeconds);
            if (windowSeconds < 60)
            {
                // For first minute, use actual rate
                allocationRate = _allocationCount / windowSeconds;
            }
            else
            {
                // Use recent 60-second window
                allocationRate = _recentAllocations.Count / 60.0;
            }
        }

        return new MemoryStatistics
        {
            KernelId = _kernelId,
            Timestamp = DateTimeOffset.UtcNow,
            CurrentGpuBytes = Math.Max(0, currentGpu),
            CurrentHostBytes = Math.Max(0, currentHost),
            CurrentUnifiedBytes = Math.Max(0, currentUnified),
            PeakGpuBytes = Volatile.Read(ref _peakGpuBytes),
            PeakHostBytes = Volatile.Read(ref _peakHostBytes),
            PeakUnifiedBytes = Volatile.Read(ref _peakUnifiedBytes),
            TotalAllocations = Volatile.Read(ref _allocationCount),
            TotalDeallocations = Volatile.Read(ref _deallocationCount),
            AllocationRatePerSecond = allocationRate
        };
    }

    private void UpdatePeakGpu(long currentGpu)
    {
        var current = Volatile.Read(ref _peakGpuBytes);
        while (currentGpu > current)
        {
            var original = Interlocked.CompareExchange(ref _peakGpuBytes, currentGpu, current);
            if (original == current)
            {
                break;
            }

            current = original;
        }
    }

    private void UpdatePeakHost(long currentHost)
    {
        var current = Volatile.Read(ref _peakHostBytes);
        while (currentHost > current)
        {
            var original = Interlocked.CompareExchange(ref _peakHostBytes, currentHost, current);
            if (original == current)
            {
                break;
            }

            current = original;
        }
    }

    private void UpdatePeakUnified(long currentUnified)
    {
        var current = Volatile.Read(ref _peakUnifiedBytes);
        while (currentUnified > current)
        {
            var original = Interlocked.CompareExchange(ref _peakUnifiedBytes, currentUnified, current);
            if (original == current)
            {
                break;
            }

            current = original;
        }
    }

    private void CheckMemoryAnomaly()
    {
        if (_baseline == null)
        {
            return;
        }

        var totalCurrent = (_gpuBytesAllocated - _gpuBytesDeallocated) +
                          (_hostBytesAllocated - _hostBytesDeallocated) +
                          (_unifiedBytesAllocated - _unifiedBytesDeallocated);

        var threshold = _baseline.ExpectedPeakMemoryBytes * _options.AnomalyThreshold;
        if (totalCurrent > threshold)
        {
            var anomaly = new PerformanceAnomaly
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = AnomalyType.MemorySpike,
                Severity = totalCurrent > threshold * 2 ? AnomalySeverity.Critical : AnomalySeverity.Warning,
                Description = $"Memory usage {totalCurrent / (1024.0 * 1024.0):F2}MB exceeds {_options.AnomalyThreshold}x baseline ({_baseline.ExpectedPeakMemoryBytes / (1024.0 * 1024.0):F2}MB)",
                ObservedValue = totalCurrent / (1024.0 * 1024.0),
                ExpectedValue = _baseline.ExpectedPeakMemoryBytes / (1024.0 * 1024.0)
            };

            _anomalies.Add(anomaly);
            _anomalyCallback(_kernelId, anomaly);
        }
    }

    public PerformanceSnapshot GetSnapshot()
    {
        var percentiles = _latencyHistogram.GetPercentiles();
        var (avgThroughput, _) = GetThroughputStats();

        return new PerformanceSnapshot
        {
            KernelId = _kernelId,
            Timestamp = DateTimeOffset.UtcNow,
            TotalMessagesProcessed = Volatile.Read(ref _totalMessagesProcessed),
            CurrentThroughputMps = _lastThroughput,
            AverageThroughputMps = avgThroughput,
            PeakThroughputMps = _peakThroughput,
            P50LatencyMicros = percentiles.P50Micros,
            P99LatencyMicros = percentiles.P99Micros,
            TotalBytesAllocated = _gpuBytesAllocated + _hostBytesAllocated + _unifiedBytesAllocated,
            Duration = DateTimeOffset.UtcNow - _startTime
        };
    }

    public LatencyPercentiles GetLatencyPercentiles() => _latencyHistogram.GetPercentiles();

    public void SetBaseline(PerformanceBaseline baseline)
    {
        _baseline = baseline;
    }

    public BaselineComparison? CompareToBaseline()
    {
        if (_baseline == null)
        {
            return null;
        }

        var percentiles = _latencyHistogram.GetPercentiles();
        var totalMemory = _gpuBytesAllocated + _hostBytesAllocated + _unifiedBytesAllocated;
        var (avgThroughput, _) = GetThroughputStats();

        return new BaselineComparison
        {
            Baseline = _baseline,
            P50LatencyRatio = _baseline.ExpectedP50Nanos > 0 ? (double)percentiles.P50Nanos / _baseline.ExpectedP50Nanos : 1.0,
            P99LatencyRatio = _baseline.ExpectedP99Nanos > 0 ? (double)percentiles.P99Nanos / _baseline.ExpectedP99Nanos : 1.0,
            ThroughputRatio = _baseline.ExpectedThroughputMps > 0 ? avgThroughput / _baseline.ExpectedThroughputMps : 1.0,
            MemoryRatio = _baseline.ExpectedPeakMemoryBytes > 0 ? (double)totalMemory / _baseline.ExpectedPeakMemoryBytes : 1.0
        };
    }

    public PerformanceReport GenerateReport()
    {
        var percentiles = _latencyHistogram.GetPercentiles();
        var (avgThroughput, stdDev) = GetThroughputStats();
        var minThroughput = _throughputSamples.Count > 0 ? _throughputSamples.Min(s => s.MessagesPerSecond) : 0;

        return new PerformanceReport
        {
            KernelId = _kernelId,
            StartTime = _startTime,
            EndTime = DateTimeOffset.UtcNow,
            Latency = percentiles,
            Throughput = new ThroughputStatistics
            {
                TotalMessages = _totalMessagesProcessed,
                AverageMps = avgThroughput,
                PeakMps = _peakThroughput,
                MinMps = minThroughput,
                StdDevMps = stdDev,
                SampleCount = _throughputSamples.Count
            },
            Allocations = new AllocationStatistics
            {
                TotalBytes = _gpuBytesAllocated + _hostBytesAllocated + _unifiedBytesAllocated,
                AllocationCount = _allocationCount,
                GpuBytes = _gpuBytesAllocated,
                HostBytes = _hostBytesAllocated,
                UnifiedBytes = _unifiedBytesAllocated
            },
            BaselineComparison = CompareToBaseline(),
            Anomalies = _anomalies.ToList()
        };
    }

    private (double Average, double StdDev) GetThroughputStats()
    {
        lock (_throughputLock)
        {
            if (_throughputSamples.Count == 0)
            {
                return (0, 0);
            }

            var avg = _throughputSamples.Average(s => s.MessagesPerSecond);
            var variance = _throughputSamples.Average(s => Math.Pow(s.MessagesPerSecond - avg, 2));
            return (avg, Math.Sqrt(variance));
        }
    }

    private void CheckLatencyAnomaly(long latencyNanos)
    {
        if (_baseline == null)
        {
            return;
        }

        // Check if latency exceeds threshold
        var threshold = _baseline.ExpectedP99Nanos * _options.AnomalyThreshold;
        if (latencyNanos > threshold)
        {
            var anomaly = new PerformanceAnomaly
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = AnomalyType.LatencySpike,
                Severity = latencyNanos > threshold * 2 ? AnomalySeverity.Critical : AnomalySeverity.Warning,
                Description = $"Latency {latencyNanos / 1000.0:F2}μs exceeds {_options.AnomalyThreshold}x baseline ({_baseline.ExpectedP99Nanos / 1000.0:F2}μs)",
                ObservedValue = latencyNanos / 1000.0,
                ExpectedValue = _baseline.ExpectedP99Nanos / 1000.0
            };

            _anomalies.Add(anomaly);
            _anomalyCallback(_kernelId, anomaly);
        }
    }

    private void CheckThroughputAnomaly(double throughputMps)
    {
        if (_baseline == null)
        {
            return;
        }

        // Check if throughput dropped below threshold
        var threshold = _baseline.ExpectedThroughputMps / _options.AnomalyThreshold;
        if (throughputMps < threshold)
        {
            var anomaly = new PerformanceAnomaly
            {
                Timestamp = DateTimeOffset.UtcNow,
                Type = AnomalyType.ThroughputDrop,
                Severity = throughputMps < threshold / 2 ? AnomalySeverity.Critical : AnomalySeverity.Warning,
                Description = $"Throughput {throughputMps:F0} msg/s below {_options.AnomalyThreshold}x baseline ({_baseline.ExpectedThroughputMps:F0} msg/s)",
                ObservedValue = throughputMps,
                ExpectedValue = _baseline.ExpectedThroughputMps
            };

            _anomalies.Add(anomaly);
            _anomalyCallback(_kernelId, anomaly);
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private readonly record struct ThroughputSample
    {
        public required DateTimeOffset Timestamp { get; init; }
        public required double MessagesPerSecond { get; init; }
    }
}
