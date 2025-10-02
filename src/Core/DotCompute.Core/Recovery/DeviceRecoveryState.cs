// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Tracks recovery state for a specific GPU device
/// </summary>
public class DeviceRecoveryState(string deviceId)
{
    private readonly ConcurrentQueue<Exception> _recentErrors = new();
    private readonly object _lock = new();
    private DateTimeOffset _lastErrorTime = DateTimeOffset.MinValue;
    private int _consecutiveFailures;
    private int _totalRecoveryAttempts;
    private int _successfulRecoveries;

    public string DeviceId { get; } = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
    public bool IsHealthy { get; private set; } = true;
    public Exception? LastError { get; private set; }
    public int ConsecutiveFailures => _consecutiveFailures;
    public int TotalRecoveryAttempts => _totalRecoveryAttempts;
    public int SuccessfulRecoveries => _successfulRecoveries;
    public DateTimeOffset LastHealthCheck { get; private set; } = DateTimeOffset.UtcNow;

    public void RecordError(Exception error)
    {
        lock (_lock)
        {
            LastError = error;
            _lastErrorTime = DateTimeOffset.UtcNow;
            _consecutiveFailures++;
            _recentErrors.Enqueue(error);

            // Keep only recent errors (last 50)
            while (_recentErrors.Count > 50 && _recentErrors.TryDequeue(out _))
            {
                // Remove old errors
            }

            IsHealthy = _consecutiveFailures < 3;
        }
    }

    public void RecordSuccessfulRecovery()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _totalRecoveryAttempts++;
            _successfulRecoveries++;
            IsHealthy = true;
        }
    }

    public void RecordFailedRecovery()
    {
        lock (_lock)
        {
            _totalRecoveryAttempts++;
            IsHealthy = false;
        }
    }

    public double CalculateErrorRate()
    {
        var now = DateTimeOffset.UtcNow;
        var recentErrors = _recentErrors.ToArray()
            .Count(e => (now - _lastErrorTime).TotalMinutes < 60);

        return recentErrors; // Errors per hour
    }

    public void UpdateHealthCheck()
    {
        LastHealthCheck = DateTimeOffset.UtcNow;

        // Auto-recovery after some time without errors
        if (_consecutiveFailures > 0 && (DateTimeOffset.UtcNow - _lastErrorTime).TotalMinutes > 30)
        {
            lock (_lock)
            {
                _consecutiveFailures = Math.Max(0, _consecutiveFailures - 1);
                IsHealthy = _consecutiveFailures < 3;
            }
        }
    }

    public async Task ResetAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                _consecutiveFailures = 0;
                IsHealthy = true;
                LastError = null;

                // Clear recent errors
                while (_recentErrors.TryDequeue(out _))
                {
                    // Clear queue
                }
            }
        });
    }

    public async Task ResetContextAsync()
    {
        // Platform-specific context reset would be implemented here
        await Task.Delay(100);

        lock (_lock)
        {
            _consecutiveFailures = Math.Max(0, _consecutiveFailures - 1);
            IsHealthy = _consecutiveFailures < 3;
        }
    }
}