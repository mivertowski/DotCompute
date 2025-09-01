// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Tracks and aggregates recovery metrics across the system.
/// </summary>
public class RecoveryMetrics
{
    private readonly ConcurrentBag<RecoveryAttempt> _attempts = [];
    private long _successCount;
    private long _failureCount;

    /// <summary>
    /// Gets the total number of recovery attempts.
    /// </summary>
    public long TotalAttempts => _successCount + _failureCount;

    /// <summary>
    /// Gets the number of successful recoveries.
    /// </summary>
    public long SuccessfulRecoveries => _successCount;

    /// <summary>
    /// Gets the number of failed recoveries.
    /// </summary>
    public long FailedRecoveries => _failureCount;

    /// <summary>
    /// Gets the success rate as a value between 0.0 and 1.0.
    /// </summary>
    public double SuccessRate => TotalAttempts > 0 ? (double)_successCount / TotalAttempts : 0.0;

    /// <summary>
    /// Gets the average recovery time across all attempts.
    /// </summary>
    public TimeSpan AverageRecoveryTime
    {
        get
        {
            if (!_attempts.Any())
            {

                return TimeSpan.Zero;
            }


            var averageTicks = _attempts.Average(a => a.Duration.Ticks);
            return TimeSpan.FromTicks((long)averageTicks);
        }
    }

    /// <summary>
    /// Gets the fastest recovery time.
    /// </summary>
    public TimeSpan FastestRecoveryTime
    {
        get
        {
            if (!_attempts.Any())
            {

                return TimeSpan.Zero;
            }


            return _attempts.Min(a => a.Duration);
        }
    }

    /// <summary>
    /// Gets the slowest recovery time.
    /// </summary>
    public TimeSpan SlowestRecoveryTime
    {
        get
        {
            if (!_attempts.Any())
            {

                return TimeSpan.Zero;
            }


            return _attempts.Max(a => a.Duration);
        }
    }

    /// <summary>
    /// Gets the last recovery timestamp.
    /// </summary>
    public DateTimeOffset? LastRecoveryTime
    {
        get
        {
            if (!_attempts.Any())
            {

                return null;
            }


            return _attempts.Max(a => a.Timestamp);
        }
    }

    /// <summary>
    /// Records a successful recovery attempt.
    /// </summary>
    /// <param name="duration">The duration of the recovery.</param>
    public void RecordSuccess(TimeSpan duration)
    {
        Interlocked.Increment(ref _successCount);
        _attempts.Add(new RecoveryAttempt
        {
            Success = true,
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow
        });

        // Keep only last 1000 attempts to prevent unbounded growth
        while (_attempts.Count > 1000)
        {
            _attempts.TryTake(out _);
        }
    }

    /// <summary>
    /// Records a failed recovery attempt.
    /// </summary>
    /// <param name="duration">The duration of the recovery attempt.</param>
    /// <param name="error">The error that caused the failure.</param>
    public void RecordFailure(TimeSpan duration, Exception? error = null)
    {
        Interlocked.Increment(ref _failureCount);
        _attempts.Add(new RecoveryAttempt
        {
            Success = false,
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow,
            Error = error
        });

        // Keep only last 1000 attempts to prevent unbounded growth
        while (_attempts.Count > 1000)
        {
            _attempts.TryTake(out _);
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        _attempts.Clear();
        _successCount = 0;
        _failureCount = 0;
    }

    private class RecoveryAttempt
    {
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public Exception? Error { get; set; }
    }
}