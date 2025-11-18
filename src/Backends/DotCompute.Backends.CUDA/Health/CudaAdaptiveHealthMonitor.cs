// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Health;
using DotCompute.Abstractions.Temporal;

namespace DotCompute.Backends.CUDA.Health;

/// <summary>
/// CUDA implementation of adaptive health monitoring with HLC-based causal analysis.
/// Maintains a sliding window of timestamped health snapshots and adaptively adjusts
/// monitoring intervals based on detected health trends.
/// </summary>
public sealed class CudaAdaptiveHealthMonitor : IAdaptiveHealthMonitor
{
    private readonly string _monitorId;
    private readonly int _maxHistorySize;

    // Sliding window of timestamped snapshots (thread-safe)
    private readonly ConcurrentQueue<TimestampedHealthSnapshot> _history;
    private readonly ReaderWriterLockSlim _historyLock;

    // Current state
    private HealthStatus _currentStatus;
    private TimeSpan _currentInterval;
    private long _totalSnapshotCount;

    // Adaptive interval thresholds
    private static readonly TimeSpan IntervalCritical = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan IntervalDegraded = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan IntervalHealthy = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IntervalOptimal = TimeSpan.FromSeconds(10);

    // Trend analysis thresholds
    private const double TemperatureRisingThreshold = 5.0; // °C per minute
    private const double MemoryPressureThreshold = 0.9; // 90% utilization
    private const double FailureRiskCritical = 0.8;
    private const double FailureRiskHigh = 0.5;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaAdaptiveHealthMonitor"/> class.
    /// </summary>
    /// <param name="monitorId">Unique identifier for this monitor.</param>
    /// <param name="maxHistorySize">Maximum number of snapshots to retain (default: 10,000).</param>
    public CudaAdaptiveHealthMonitor(string monitorId, int maxHistorySize = 10_000)
    {
        if (string.IsNullOrWhiteSpace(monitorId))
        {
            throw new ArgumentException("Monitor ID cannot be null or whitespace.", nameof(monitorId));
        }
        if (maxHistorySize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHistorySize), "Max history size must be positive.");
        }

        _monitorId = monitorId;
        _maxHistorySize = maxHistorySize;
        _history = new ConcurrentQueue<TimestampedHealthSnapshot>();
        _historyLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _currentStatus = HealthStatus.Healthy;
        _currentInterval = IntervalHealthy;
        _totalSnapshotCount = 0;
    }

    /// <inheritdoc/>
    public string MonitorId => _monitorId;

    /// <inheritdoc/>
    public TimeSpan CurrentMonitoringInterval
    {
        get
        {
            _historyLock.EnterReadLock();
            try
            {
                return _currentInterval;
            }
            finally
            {
                _historyLock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc/>
    public long TotalSnapshotCount => Interlocked.Read(ref _totalSnapshotCount);

    /// <inheritdoc/>
    public Task RecordSnapshotAsync(
        DeviceHealthSnapshot snapshot,
        HlcTimestamp timestamp,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var timestamped = new TimestampedHealthSnapshot
        {
            Timestamp = timestamp,
            Snapshot = snapshot,
            MonitoringInterval = _currentInterval
        };

        // Add to history
        _history.Enqueue(timestamped);
        Interlocked.Increment(ref _totalSnapshotCount);

        // Trim history if exceeds max size
        _historyLock.EnterWriteLock();
        try
        {
            while (_history.Count > _maxHistorySize)
            {
                _history.TryDequeue(out _);
            }
        }
        finally
        {
            _historyLock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<HealthAnalysisResult> AnalyzeTrendsAsync(
        HlcTimestamp currentTime,
        TimeSpan analysisWindow = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (analysisWindow == default)
        {
            analysisWindow = TimeSpan.FromMinutes(1);
        }

        _historyLock.EnterReadLock();
        try
        {
            var snapshot = _history.ToArray();

            if (snapshot.Length == 0)
            {
                return Task.FromResult(new HealthAnalysisResult
                {
                    OverallStatus = HealthStatus.Healthy,
                    RecommendedInterval = IntervalHealthy,
                    FailureRiskScore = 0.0,
                    DetectedTrends = Array.Empty<string>().AsReadOnly(),
                    SnapshotsAnalyzed = 0,
                    AnalysisTimeRange = new HlcTimestampRange
                    {
                        Start = currentTime,
                        End = currentTime
                    }
                });
            }

            // Filter to analysis window
            var windowStartNanos = currentTime.PhysicalTimeNanos -
                                   (analysisWindow.Ticks * 100);
            var windowStart = new HlcTimestamp
            {
                PhysicalTimeNanos = windowStartNanos,
                LogicalCounter = 0
            };

            var windowSnapshots = snapshot
                .Where(s => s.Timestamp >= windowStart && s.Timestamp <= currentTime)
                .OrderBy(s => s.Timestamp)
                .ToArray();

            if (windowSnapshots.Length == 0)
            {
                return Task.FromResult(new HealthAnalysisResult
                {
                    OverallStatus = _currentStatus,
                    RecommendedInterval = _currentInterval,
                    FailureRiskScore = 0.0,
                    DetectedTrends = Array.Empty<string>().AsReadOnly(),
                    SnapshotsAnalyzed = 0,
                    AnalysisTimeRange = new HlcTimestampRange
                    {
                        Start = windowStart,
                        End = currentTime
                    }
                });
            }

            // Analyze trends
            var trends = new List<string>();
            var predictions = new List<FailurePrediction>();
            double failureRisk = 0.0;

            // Temperature trend
            var tempTrend = AnalyzeTemperatureTrend(windowSnapshots, analysisWindow);
            if (tempTrend.IsRising)
            {
                trends.Add($"Temperature rising at {tempTrend.RatePerMinute:F1}°C/min");
                failureRisk = Math.Max(failureRisk, tempTrend.Risk);

                if (tempTrend.Risk > FailureRiskHigh)
                {
                    predictions.Add(new FailurePrediction
                    {
                        Type = FailureType.Overheating,
                        PredictedTime = PredictFailureTime(currentTime, tempTrend.TimeToFailure),
                        Confidence = tempTrend.Risk,
                        Reason = $"Temperature rising rapidly at {tempTrend.RatePerMinute:F1}°C/min"
                    });
                }
            }

            // Memory pressure trend
            var memTrend = AnalyzeMemoryTrend(windowSnapshots);
            if (memTrend.HighPressure)
            {
                trends.Add($"Memory pressure at {memTrend.UtilizationPercent:F1}%");
                failureRisk = Math.Max(failureRisk, memTrend.Risk);

                if (memTrend.Risk > FailureRiskHigh)
                {
                    predictions.Add(new FailurePrediction
                    {
                        Type = FailureType.MemoryExhaustion,
                        PredictedTime = PredictFailureTime(currentTime, memTrend.TimeToExhaustion),
                        Confidence = memTrend.Risk,
                        Reason = $"Memory utilization at {memTrend.UtilizationPercent:F1}%"
                    });
                }
            }

            // Performance degradation
            var perfTrend = AnalyzePerformanceTrend(windowSnapshots);
            if (perfTrend.IsDegrading)
            {
                trends.Add($"Performance degrading: {perfTrend.DegradationPercent:F1}% slower");
                failureRisk = Math.Max(failureRisk, perfTrend.Risk);

                if (perfTrend.Risk > FailureRiskHigh)
                {
                    predictions.Add(new FailurePrediction
                    {
                        Type = FailureType.PerformanceDegradation,
                        PredictedTime = currentTime,
                        Confidence = perfTrend.Risk,
                        Reason = $"Performance {perfTrend.DegradationPercent:F1}% below baseline"
                    });
                }
            }

            // Determine overall status
            var newStatus = DetermineHealthStatus(failureRisk, windowSnapshots);
            var newInterval = DetermineMonitoringInterval(newStatus);

            // Update current state
            _currentStatus = newStatus;
            _currentInterval = newInterval;

            return Task.FromResult(new HealthAnalysisResult
            {
                OverallStatus = newStatus,
                RecommendedInterval = newInterval,
                FailureRiskScore = failureRisk,
                DetectedTrends = trends.AsReadOnly(),
                Predictions = predictions.Count > 0 ? predictions.AsReadOnly() : null,
                SnapshotsAnalyzed = windowSnapshots.Length,
                AnalysisTimeRange = new HlcTimestampRange
                {
                    Start = windowSnapshots[0].Timestamp,
                    End = windowSnapshots[^1].Timestamp
                }
            });
        }
        finally
        {
            _historyLock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public Task<CausalFailureAnalysis> AnalyzeCausalFailuresAsync(
        HlcTimestamp failureTimestamp,
        TimeSpan lookbackWindow = default,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (lookbackWindow == default)
        {
            lookbackWindow = TimeSpan.FromMinutes(5);
        }

        _historyLock.EnterReadLock();
        try
        {
            var snapshot = _history.ToArray();

            // Find all failures that happened-before the target failure
            var lookbackStartNanos = failureTimestamp.PhysicalTimeNanos -
                                     (lookbackWindow.Ticks * 100);
            var lookbackStart = new HlcTimestamp
            {
                PhysicalTimeNanos = lookbackStartNanos,
                LogicalCounter = 0
            };

            var precedingFailures = snapshot
                .Where(s => s.Timestamp < failureTimestamp &&
                           s.Timestamp >= lookbackStart &&
                           IsFailureState(s.Snapshot))
                .OrderBy(s => s.Timestamp)
                .ToArray();

            TimestampedHealthSnapshot? rootCause = null;
            if (precedingFailures.Length > 0)
            {
                rootCause = precedingFailures[0]; // Earliest failure in causal chain
            }

            TimeSpan propagationTime = TimeSpan.Zero;
            if (rootCause.HasValue)
            {
                long propagationNanos = failureTimestamp.PhysicalTimeNanos -
                                       rootCause.Value.Timestamp.PhysicalTimeNanos;
                propagationTime = TimeSpan.FromTicks(propagationNanos / 100);
            }

            string summary = BuildCausalAnalysisSummary(
                precedingFailures,
                rootCause,
                propagationTime);

            return Task.FromResult(new CausalFailureAnalysis
            {
                TargetFailureTime = failureTimestamp,
                PrecedingFailures = precedingFailures.AsReadOnly(),
                RootCause = rootCause,
                CausalChainLength = precedingFailures.Length,
                PropagationTime = propagationTime,
                Summary = summary
            });
        }
        finally
        {
            _historyLock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TimestampedHealthSnapshot>> GetRecentHistoryAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be positive.");
        }
        if (count > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot exceed 1000.");
        }

        _historyLock.EnterReadLock();
        try
        {
            var snapshot = _history.ToArray();
            var recent = snapshot
                .OrderByDescending(s => s.Timestamp)
                .Take(count)
                .OrderBy(s => s.Timestamp) // Return in chronological order
                .ToArray();

            return Task.FromResult<IReadOnlyList<TimestampedHealthSnapshot>>(recent);
        }
        finally
        {
            _historyLock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public Task ResetAsync()
    {
        ThrowIfDisposed();

        _historyLock.EnterWriteLock();
        try
        {
            _history.Clear();
            _currentStatus = HealthStatus.Healthy;
            _currentInterval = IntervalHealthy;
            Interlocked.Exchange(ref _totalSnapshotCount, 0);
        }
        finally
        {
            _historyLock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _historyLock.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static (bool IsRising, double RatePerMinute, double Risk, TimeSpan TimeToFailure)
        AnalyzeTemperatureTrend(TimestampedHealthSnapshot[] snapshots, TimeSpan window)
    {
        if (snapshots.Length < 2)
        {
            return (false, 0.0, 0.0, TimeSpan.MaxValue);
        }

        var first = snapshots[0];
        var last = snapshots[^1];

        var firstTemp = first.Snapshot.GetSensorValue(SensorType.Temperature);
        var lastTemp = last.Snapshot.GetSensorValue(SensorType.Temperature);

        if (!firstTemp.HasValue || !lastTemp.HasValue)
        {
            return (false, 0.0, 0.0, TimeSpan.MaxValue);
        }

        double tempDelta = lastTemp.Value - firstTemp.Value;
        long timeNanos = last.Timestamp.PhysicalTimeNanos - first.Timestamp.PhysicalTimeNanos;
        double timeMinutes = TimeSpan.FromTicks(timeNanos / 100).TotalMinutes;

        if (timeMinutes <= 0)
        {
            return (false, 0.0, 0.0, TimeSpan.MaxValue);
        }

        double ratePerMinute = tempDelta / timeMinutes;
        bool isRising = ratePerMinute > TemperatureRisingThreshold;

        // Calculate risk based on current temp and rate
        double currentTemp = lastTemp.Value;
        double risk = 0.0;
        TimeSpan timeToFailure = TimeSpan.MaxValue;

        if (isRising)
        {
            // Assume critical temp is 95°C
            const double criticalTemp = 95.0;
            double tempToFailure = criticalTemp - currentTemp;

            if (tempToFailure > 0 && ratePerMinute > 0)
            {
                double minutesToFailure = tempToFailure / ratePerMinute;
                timeToFailure = TimeSpan.FromMinutes(minutesToFailure);

                // Risk increases as we approach critical temp
                risk = Math.Min(1.0, currentTemp / criticalTemp);

                // Adjust risk based on rate
                if (ratePerMinute > TemperatureRisingThreshold * 2)
                {
                    risk = Math.Min(1.0, risk * 1.5);
                }
            }
        }

        return (isRising, ratePerMinute, risk, timeToFailure);
    }

    private static (bool HighPressure, double UtilizationPercent, double Risk, TimeSpan TimeToExhaustion)
        AnalyzeMemoryTrend(TimestampedHealthSnapshot[] snapshots)
    {
        if (snapshots.Length == 0)
        {
            return (false, 0.0, 0.0, TimeSpan.MaxValue);
        }

        var last = snapshots[^1];
        var memUsed = last.Snapshot.GetSensorValue(SensorType.MemoryUsedBytes);
        var memTotal = last.Snapshot.GetSensorValue(SensorType.MemoryTotalBytes);

        if (!memUsed.HasValue || !memTotal.HasValue || memTotal.Value <= 0)
        {
            return (false, 0.0, 0.0, TimeSpan.MaxValue);
        }

        double utilization = memUsed.Value / memTotal.Value;
        double utilizationPercent = utilization * 100.0;
        bool highPressure = utilization > MemoryPressureThreshold;

        double risk = 0.0;
        TimeSpan timeToExhaustion = TimeSpan.MaxValue;

        if (highPressure)
        {
            // Risk scales from 0.5 at 90% to 1.0 at 100%
            risk = 0.5 + (utilization - MemoryPressureThreshold) / (1.0 - MemoryPressureThreshold) * 0.5;

            // If we have growth trend, estimate time to exhaustion
            if (snapshots.Length >= 2)
            {
                var first = snapshots[0];
                var firstMemUsed = first.Snapshot.GetSensorValue(SensorType.MemoryUsedBytes);

                if (firstMemUsed.HasValue)
                {
                    double memoryGrowth = memUsed.Value - firstMemUsed.Value;
                    long timeNanos = last.Timestamp.PhysicalTimeNanos - first.Timestamp.PhysicalTimeNanos;

                    if (memoryGrowth > 0 && timeNanos > 0)
                    {
                        double memoryRemaining = memTotal.Value - memUsed.Value;
                        double nanosToExhaustion = memoryRemaining / memoryGrowth * timeNanos;
                        timeToExhaustion = TimeSpan.FromTicks((long)nanosToExhaustion / 100);
                    }
                }
            }
        }

        return (highPressure, utilizationPercent, risk, timeToExhaustion);
    }

    private static (bool IsDegrading, double DegradationPercent, double Risk)
        AnalyzePerformanceTrend(TimestampedHealthSnapshot[] snapshots)
    {
        if (snapshots.Length < 2)
        {
            return (false, 0.0, 0.0);
        }

        // Compare recent performance to earlier baseline
        int recentCount = Math.Min(5, snapshots.Length / 2);
        var recent = snapshots.Skip(snapshots.Length - recentCount).ToArray();
        var baseline = snapshots.Take(recentCount).ToArray();

        var recentUtilizations = recent
            .Select(s => s.Snapshot.GetSensorValue(SensorType.ComputeUtilization))
            .Where(u => u.HasValue)
            .Select(u => u!.Value)
            .ToArray();

        var baselineUtilizations = baseline
            .Select(s => s.Snapshot.GetSensorValue(SensorType.ComputeUtilization))
            .Where(u => u.HasValue)
            .Select(u => u!.Value)
            .ToArray();

        if (recentUtilizations.Length == 0 || baselineUtilizations.Length == 0)
        {
            return (false, 0.0, 0.0);
        }

        double recentAvgUtilization = recentUtilizations.Average();
        double baselineAvgUtilization = baselineUtilizations.Average();

        // Performance degradation if utilization drops significantly
        double degradationPercent = baselineAvgUtilization - recentAvgUtilization;
        bool isDegrading = degradationPercent > 20.0; // 20% drop

        double risk = isDegrading ? Math.Min(1.0, degradationPercent / 50.0) : 0.0;

        return (isDegrading, degradationPercent, risk);
    }

    private static HealthStatus DetermineHealthStatus(
        double failureRisk,
        TimestampedHealthSnapshot[] snapshots)
    {
        if (failureRisk >= FailureRiskCritical)
        {
            return HealthStatus.Critical;
        }

        if (failureRisk >= FailureRiskHigh)
        {
            return HealthStatus.Degraded;
        }

        if (snapshots.Length == 0)
        {
            return HealthStatus.Healthy;
        }

        // Check recent snapshots for errors
        var recentErrors = snapshots
            .Skip(Math.Max(0, snapshots.Length - 10))
            .Any(s => s.Snapshot.ErrorCount > 0);

        if (recentErrors)
        {
            return HealthStatus.Degraded;
        }

        // Check static sensor readings from most recent snapshot
        var recent = snapshots[^1];
        var temp = recent.Snapshot.GetSensorValue(SensorType.Temperature);
        var memUsed = recent.Snapshot.GetSensorValue(SensorType.MemoryUsedBytes);
        var memTotal = recent.Snapshot.GetSensorValue(SensorType.MemoryTotalBytes);
        var utilization = recent.Snapshot.GetSensorValue(SensorType.ComputeUtilization);

        // Check for critical conditions
        if (temp.HasValue && temp.Value > 85.0)
        {
            return HealthStatus.Critical;
        }

        if (memUsed.HasValue && memTotal.HasValue && memTotal.Value > 0)
        {
            double memUtilization = memUsed.Value / memTotal.Value;
            if (memUtilization > 0.95)
            {
                return HealthStatus.Critical;
            }
        }

        // Check for degraded conditions
        if (temp.HasValue && temp.Value > 75.0)
        {
            return HealthStatus.Degraded;
        }

        if (memUsed.HasValue && memTotal.HasValue && memTotal.Value > 0)
        {
            double memUtilization = memUsed.Value / memTotal.Value;
            if (memUtilization > 0.85)
            {
                return HealthStatus.Degraded;
            }
        }

        // Check for optimal conditions
        if (temp.HasValue && temp.Value < 70.0 &&
            memUsed.HasValue && memTotal.HasValue && memTotal.Value > 0)
        {
            double memUtilization = memUsed.Value / memTotal.Value;
            if (memUtilization < 0.6)
            {
                return HealthStatus.Optimal;
            }
        }

        return HealthStatus.Healthy;
    }

    private static TimeSpan DetermineMonitoringInterval(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Critical => IntervalCritical,
            HealthStatus.Degraded => IntervalDegraded,
            HealthStatus.Healthy => IntervalHealthy,
            HealthStatus.Optimal => IntervalOptimal,
            _ => IntervalHealthy
        };
    }

    private static HlcTimestamp PredictFailureTime(HlcTimestamp currentTime, TimeSpan timeToFailure)
    {
        long futureNanos = currentTime.PhysicalTimeNanos + (timeToFailure.Ticks * 100);
        return new HlcTimestamp
        {
            PhysicalTimeNanos = futureNanos,
            LogicalCounter = 0
        };
    }

    private static bool IsFailureState(DeviceHealthSnapshot snapshot)
    {
        if (snapshot.ErrorCount > 0)
        {
            return true;
        }

        var temp = snapshot.GetSensorValue(SensorType.Temperature);
        if (temp.HasValue && temp.Value > 90.0)
        {
            return true;
        }

        var memUsed = snapshot.GetSensorValue(SensorType.MemoryUsedBytes);
        var memTotal = snapshot.GetSensorValue(SensorType.MemoryTotalBytes);
        if (memUsed.HasValue && memTotal.HasValue && memTotal.Value > 0)
        {
            double utilization = memUsed.Value / memTotal.Value;
            if (utilization > 0.95)
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildCausalAnalysisSummary(
        TimestampedHealthSnapshot[] precedingFailures,
        TimestampedHealthSnapshot? rootCause,
        TimeSpan propagationTime)
    {
        if (precedingFailures.Length == 0)
        {
            return "No causal failures detected in lookback window.";
        }

        var summary = new System.Text.StringBuilder();
        summary.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Causal failure chain detected ({precedingFailures.Length} failures):");

        if (rootCause.HasValue)
        {
            var root = rootCause.Value;
            summary.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Root Cause: {GetFailureReason(root.Snapshot)} at HLC {root.Timestamp.PhysicalTimeNanos}");
            summary.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  Propagation Time: {propagationTime.TotalMilliseconds:F1}ms");
        }

        summary.AppendLine("  Failure Chain:");
        for (int i = 0; i < Math.Min(5, precedingFailures.Length); i++)
        {
            var failure = precedingFailures[i];
            summary.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"    {i + 1}. {GetFailureReason(failure.Snapshot)} at HLC {failure.Timestamp.PhysicalTimeNanos}");
        }

        if (precedingFailures.Length > 5)
        {
            summary.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"    ... and {precedingFailures.Length - 5} more failures");
        }

        return summary.ToString();
    }

    private static string GetFailureReason(DeviceHealthSnapshot snapshot)
    {
        if (snapshot.ErrorCount > 0)
        {
            return $"Error count: {snapshot.ErrorCount}";
        }

        var temp = snapshot.GetSensorValue(SensorType.Temperature);
        if (temp.HasValue && temp.Value > 90.0)
        {
            return $"Overheating ({temp.Value:F1}°C)";
        }

        var memUsed = snapshot.GetSensorValue(SensorType.MemoryUsedBytes);
        var memTotal = snapshot.GetSensorValue(SensorType.MemoryTotalBytes);
        if (memUsed.HasValue && memTotal.HasValue && memTotal.Value > 0)
        {
            double utilization = memUsed.Value / memTotal.Value;
            if (utilization > 0.95)
            {
                return $"Memory exhaustion ({utilization * 100:F1}%)";
            }
        }

        return "Unknown failure";
    }
}
