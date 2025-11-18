// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Temporal;

namespace DotCompute.Abstractions.Health;

/// <summary>
/// Adaptive health monitoring with HLC-based causal analysis and ML-powered failure prediction.
/// Adjusts monitoring intervals dynamically based on system health trends.
/// </summary>
/// <remarks>
/// <para><b>Key Features:</b></para>
/// <list type="bullet">
/// <item>HLC timestamping of all health events for causal analysis</item>
/// <item>Adaptive monitoring intervals (faster when unhealthy, slower when stable)</item>
/// <item>Historical trend analysis with sliding windows</item>
/// <item>ML-powered failure prediction using simple heuristics</item>
/// <item>Causal failure correlation via HLC happened-before relationships</item>
/// </list>
///
/// <para><b>Monitoring Intervals:</b></para>
/// <list type="bullet">
/// <item><b>Critical:</b> 100ms - System in critical state, maximum monitoring</item>
/// <item><b>Degraded:</b> 500ms - System degraded, increased monitoring</item>
/// <item><b>Healthy:</b> 2s - System stable, normal monitoring</item>
/// <item><b>Optimal:</b> 10s - System optimal, reduced monitoring overhead</item>
/// </list>
/// </remarks>
public interface IAdaptiveHealthMonitor : IDisposable
{
    /// <summary>
    /// Gets the monitor identifier.
    /// </summary>
    public string MonitorId { get; }

    /// <summary>
    /// Gets the current monitoring interval based on system health.
    /// </summary>
    public TimeSpan CurrentMonitoringInterval { get; }

    /// <summary>
    /// Gets the total number of health snapshots collected.
    /// </summary>
    public long TotalSnapshotCount { get; }

    /// <summary>
    /// Records a health snapshot with HLC timestamp for causal analysis.
    /// </summary>
    /// <param name="snapshot">The health snapshot to record.</param>
    /// <param name="timestamp">HLC timestamp for causal ordering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    public Task RecordSnapshotAsync(
        DeviceHealthSnapshot snapshot,
        HlcTimestamp timestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes health trends and predicts potential failures.
    /// </summary>
    /// <param name="currentTime">Current HLC timestamp for analysis window.</param>
    /// <param name="analysisWindow">Time window for trend analysis (default: 1 minute).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health analysis results with failure predictions.</returns>
    public Task<HealthAnalysisResult> AnalyzeTrendsAsync(
        HlcTimestamp currentTime,
        TimeSpan analysisWindow = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs causal failure analysis using HLC happened-before relationships.
    /// Identifies which failures may have causally contributed to a given failure event.
    /// </summary>
    /// <param name="failureTimestamp">HLC timestamp of the failure to analyze.</param>
    /// <param name="lookbackWindow">How far back to search for causal failures (default: 5 minutes).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Causal analysis results with happened-before failure chain.</returns>
    public Task<CausalFailureAnalysis> AnalyzeCausalFailuresAsync(
        HlcTimestamp failureTimestamp,
        TimeSpan lookbackWindow = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent health history for manual inspection.
    /// </summary>
    /// <param name="count">Number of recent snapshots to retrieve (default: 100, max: 1000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent health snapshots ordered by HLC timestamp.</returns>
    public Task<IReadOnlyList<TimestampedHealthSnapshot>> GetRecentHistoryAsync(
        int count = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets monitoring state and clears history (for testing or recovery scenarios).
    /// </summary>
    public Task ResetAsync();
}

/// <summary>
/// Health snapshot with HLC timestamp for causal ordering.
/// </summary>
public readonly struct TimestampedHealthSnapshot : IEquatable<TimestampedHealthSnapshot>, IComparable<TimestampedHealthSnapshot>
{
    /// <summary>
    /// Gets the HLC timestamp when this snapshot was taken.
    /// </summary>
    public required HlcTimestamp Timestamp { get; init; }

    /// <summary>
    /// Gets the health snapshot data.
    /// </summary>
    public required DeviceHealthSnapshot Snapshot { get; init; }

    /// <summary>
    /// Gets the monitoring interval that was active when this snapshot was taken.
    /// </summary>
    public TimeSpan MonitoringInterval { get; init; }

    /// <inheritdoc/>
    public readonly bool Equals(TimestampedHealthSnapshot other)
    {
        return Timestamp.Equals(other.Timestamp) &&
               Snapshot.Equals(other.Snapshot);
    }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is TimestampedHealthSnapshot other && Equals(other);

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(Timestamp, Snapshot);

    /// <inheritdoc/>
    public readonly int CompareTo(TimestampedHealthSnapshot other) => Timestamp.CompareTo(other.Timestamp);

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TimestampedHealthSnapshot left, TimestampedHealthSnapshot right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TimestampedHealthSnapshot left, TimestampedHealthSnapshot right) => !left.Equals(right);

    /// <summary>
    /// Less than operator (by HLC timestamp).
    /// </summary>
    public static bool operator <(TimestampedHealthSnapshot left, TimestampedHealthSnapshot right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Greater than operator (by HLC timestamp).
    /// </summary>
    public static bool operator >(TimestampedHealthSnapshot left, TimestampedHealthSnapshot right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Less than or equal operator (by HLC timestamp).
    /// </summary>
    public static bool operator <=(TimestampedHealthSnapshot left, TimestampedHealthSnapshot right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Greater than or equal operator (by HLC timestamp).
    /// </summary>
    public static bool operator >=(TimestampedHealthSnapshot left, TimestampedHealthSnapshot right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// Results of health trend analysis with failure predictions.
/// </summary>
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "Data carrier struct, equality not required")]
public readonly struct HealthAnalysisResult
{
    /// <summary>
    /// Gets the overall health status based on trend analysis.
    /// </summary>
    public required HealthStatus OverallStatus { get; init; }

    /// <summary>
    /// Gets the predicted monitoring interval for the next period.
    /// </summary>
    public required TimeSpan RecommendedInterval { get; init; }

    /// <summary>
    /// Gets the failure risk score (0.0 = no risk, 1.0 = imminent failure).
    /// </summary>
    public double FailureRiskScore { get; init; }

    /// <summary>
    /// Gets detected health trends (e.g., "Temperature rising", "Memory pressure increasing").
    /// </summary>
    public required IReadOnlyList<string> DetectedTrends { get; init; }

    /// <summary>
    /// Gets failure predictions if risk score > 0.5.
    /// </summary>
    public IReadOnlyList<FailurePrediction>? Predictions { get; init; }

    /// <summary>
    /// Gets the number of snapshots analyzed in this window.
    /// </summary>
    public int SnapshotsAnalyzed { get; init; }

    /// <summary>
    /// Gets the HLC timestamp range covered by this analysis.
    /// </summary>
    public required HlcTimestampRange AnalysisTimeRange { get; init; }
}

/// <summary>
/// Failure prediction with estimated time and confidence.
/// </summary>
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "Data carrier struct, equality not required")]
public readonly struct FailurePrediction
{
    /// <summary>
    /// Gets the type of failure predicted.
    /// </summary>
    public required FailureType Type { get; init; }

    /// <summary>
    /// Gets the predicted HLC timestamp of failure (may be in the future).
    /// </summary>
    public required HlcTimestamp PredictedTime { get; init; }

    /// <summary>
    /// Gets the confidence level (0.0-1.0) of this prediction.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the reason for this prediction (human-readable).
    /// </summary>
    public required string Reason { get; init; }
}

/// <summary>
/// Causal analysis of failures using HLC happened-before relationships.
/// </summary>
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "Data carrier struct, equality not required")]
public readonly struct CausalFailureAnalysis
{
    /// <summary>
    /// Gets the failure timestamp being analyzed.
    /// </summary>
    public required HlcTimestamp TargetFailureTime { get; init; }

    /// <summary>
    /// Gets the failures that happened-before the target failure (potential causes).
    /// Ordered by HLC timestamp (earliest first).
    /// </summary>
    public required IReadOnlyList<TimestampedHealthSnapshot> PrecedingFailures { get; init; }

    /// <summary>
    /// Gets the root cause failure (earliest in the causal chain).
    /// </summary>
    public TimestampedHealthSnapshot? RootCause { get; init; }

    /// <summary>
    /// Gets the causal chain length (number of failures in the happened-before sequence).
    /// </summary>
    public int CausalChainLength { get; init; }

    /// <summary>
    /// Gets the time span from root cause to target failure.
    /// </summary>
    public TimeSpan PropagationTime { get; init; }

    /// <summary>
    /// Gets human-readable causal analysis summary.
    /// </summary>
    public required string Summary { get; init; }
}

/// <summary>
/// Overall health status classification.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// System is performing optimally, no degradation detected.
    /// </summary>
    Optimal = 0,

    /// <summary>
    /// System is healthy, minor variations within normal range.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// System is degraded, noticeable performance impact or resource pressure.
    /// </summary>
    Degraded = 2,

    /// <summary>
    /// System is in critical state, failure imminent or already occurring.
    /// </summary>
    Critical = 3
}

/// <summary>
/// Types of failures that can be predicted.
/// </summary>
public enum FailureType
{
    /// <summary>
    /// GPU temperature exceeding safe operating limits.
    /// </summary>
    Overheating,

    /// <summary>
    /// Memory exhaustion or OOM condition imminent.
    /// </summary>
    MemoryExhaustion,

    /// <summary>
    /// Performance degradation detected (slowdown).
    /// </summary>
    PerformanceDegradation,

    /// <summary>
    /// Device hang or unresponsive state.
    /// </summary>
    DeviceHang,

    /// <summary>
    /// Hardware error or corruption detected.
    /// </summary>
    HardwareError,

    /// <summary>
    /// Resource leak detected (growing over time).
    /// </summary>
    ResourceLeak
}

/// <summary>
/// HLC timestamp range for analysis windows.
/// </summary>
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "Data carrier struct, equality not required")]
public readonly struct HlcTimestampRange
{
    /// <summary>
    /// Gets the start of the time range (inclusive).
    /// </summary>
    public required HlcTimestamp Start { get; init; }

    /// <summary>
    /// Gets the end of the time range (inclusive).
    /// </summary>
    public required HlcTimestamp End { get; init; }

    /// <summary>
    /// Gets the duration of this time range.
    /// </summary>
    public readonly TimeSpan Duration
    {
        get
        {
            long durationNanos = End.PhysicalTimeNanos - Start.PhysicalTimeNanos;
            return TimeSpan.FromTicks(durationNanos / 100);
        }
    }

    /// <summary>
    /// Checks if a timestamp falls within this range.
    /// </summary>
    public readonly bool Contains(HlcTimestamp timestamp)
    {
        return timestamp >= Start && timestamp <= End;
    }
}
