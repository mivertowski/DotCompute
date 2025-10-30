// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.


namespace DotCompute.Abstractions.Interfaces.Recovery;

/// <summary>
/// Tracks metrics for recovery operations
/// </summary>
public class RecoveryMetrics
{
    private long _totalAttempts;
    private long _successfulRecoveries;
    private long _failedRecoveries;
    private readonly List<TimeSpan> _recoveryTimes = [];
    private readonly List<Exception?> _recentExceptions = [];
    private readonly Lock _lock = new();

    public long TotalAttempts => _totalAttempts;
    public long SuccessfulRecoveries => _successfulRecoveries;
    public long FailedRecoveries => _failedRecoveries;
    public double SuccessRate => _totalAttempts == 0 ? 0.0 : (double)_successfulRecoveries / _totalAttempts;
    public TimeSpan AverageRecoveryTime
    {
        get
        {
            lock (_lock)
            {
                return _recoveryTimes.Count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(_recoveryTimes.Sum(t => t.Ticks) / _recoveryTimes.Count);
            }
        }
    }

    public void RecordSuccess(TimeSpan duration)
    {
        lock (_lock)
        {
            _ = Interlocked.Increment(ref _totalAttempts);
            _ = Interlocked.Increment(ref _successfulRecoveries);
            _recoveryTimes.Add(duration);

            // Keep only recent times (last 100)
            if (_recoveryTimes.Count > 100)
            {
                _recoveryTimes.RemoveAt(0);
            }
        }
    }

    public void RecordFailure(TimeSpan duration, Exception? exception)
    {
        lock (_lock)
        {
            _ = Interlocked.Increment(ref _totalAttempts);
            _ = Interlocked.Increment(ref _failedRecoveries);
            _recoveryTimes.Add(duration);
            _recentExceptions.Add(exception);

            // Keep only recent data
            if (_recoveryTimes.Count > 100)
            {
                _recoveryTimes.RemoveAt(0);
            }
            if (_recentExceptions.Count > 50)
            {
                _recentExceptions.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<Exception?> GetRecentExceptions()
    {
        lock (_lock)
        {
            return _recentExceptions.ToList().AsReadOnly();
        }
    }
}
