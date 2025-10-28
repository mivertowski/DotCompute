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
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>

    public string DeviceId { get; } = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
    /// <summary>
    /// Gets or sets a value indicating whether healthy.
    /// </summary>
    /// <value>The is healthy.</value>
    public bool IsHealthy { get; private set; } = true;
    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    /// <value>The last error.</value>
    public Exception? LastError { get; private set; }
    /// <summary>
    /// Gets or sets the consecutive failures.
    /// </summary>
    /// <value>The consecutive failures.</value>
    public int ConsecutiveFailures => _consecutiveFailures;
    /// <summary>
    /// Gets or sets the total recovery attempts.
    /// </summary>
    /// <value>The total recovery attempts.</value>
    public int TotalRecoveryAttempts => _totalRecoveryAttempts;
    /// <summary>
    /// Gets or sets the successful recoveries.
    /// </summary>
    /// <value>The successful recoveries.</value>
    public int SuccessfulRecoveries => _successfulRecoveries;
    /// <summary>
    /// Gets or sets the last health check.
    /// </summary>
    /// <value>The last health check.</value>
    public DateTimeOffset LastHealthCheck { get; private set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Performs record error.
    /// </summary>
    /// <param name="error">The error.</param>

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
    /// <summary>
    /// Performs record successful recovery.
    /// </summary>

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
    /// <summary>
    /// Performs record failed recovery.
    /// </summary>

    public void RecordFailedRecovery()
    {
        lock (_lock)
        {
            _totalRecoveryAttempts++;
            IsHealthy = false;
        }
    }
    /// <summary>
    /// Calculates the error rate.
    /// </summary>
    /// <returns>The calculated error rate.</returns>

    public double CalculateErrorRate()
    {
        var now = DateTimeOffset.UtcNow;
        var recentErrors = _recentErrors.ToArray()
            .Count(e => (now - _lastErrorTime).TotalMinutes < 60);

        return recentErrors; // Errors per hour
    }
    /// <summary>
    /// Updates the health check.
    /// </summary>

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
    /// <summary>
    /// Gets reset asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
    /// <summary>
    /// Gets reset context asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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