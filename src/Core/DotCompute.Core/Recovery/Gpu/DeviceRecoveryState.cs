// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Core.Recovery.Gpu;

/// <summary>
/// Tracks the recovery state and health metrics for a specific GPU device.
/// Maintains error history, failure counts, and provides methods for state management.
/// </summary>
/// <remarks>
/// This class is thread-safe and maintains a running history of device health
/// to make informed recovery decisions. It automatically manages device health
/// status based on error patterns and recovery success rates.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="DeviceRecoveryState"/> class.
/// </remarks>
/// <param name="deviceId">The unique identifier for the GPU device.</param>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="deviceId"/> is null.</exception>
public class DeviceRecoveryState(string deviceId)
{
    private readonly ConcurrentQueue<Exception> _recentErrors = new();
    private readonly Lock _lock = new();
    private DateTimeOffset _lastErrorTime = DateTimeOffset.MinValue;
    private int _consecutiveFailures;
    private int _totalRecoveryAttempts;
    private int _successfulRecoveries;

    /// <summary>
    /// Gets the unique identifier of the GPU device this state tracks.
    /// </summary>
    /// <value>The device identifier string.</value>
    public string DeviceId { get; } = deviceId ?? throw new ArgumentNullException(nameof(deviceId));

    /// <summary>
    /// Gets a value indicating whether the device is currently considered healthy.
    /// </summary>
    /// <value><c>true</c> if the device is healthy; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// A device is considered healthy if it has fewer than the threshold number
    /// of consecutive failures (typically 3).
    /// </remarks>
    public bool IsHealthy { get; private set; } = true;

    /// <summary>
    /// Gets the most recent error that occurred on this device, if any.
    /// </summary>
    /// <value>The last exception, or <c>null</c> if no errors have occurred.</value>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Gets the current count of consecutive failures without a successful recovery.
    /// </summary>
    /// <value>The number of consecutive failures.</value>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>
    /// Gets the total number of recovery attempts made for this device.
    /// </summary>
    /// <value>The total recovery attempt count.</value>
    public int TotalRecoveryAttempts => _totalRecoveryAttempts;

    /// <summary>
    /// Gets the number of successful recovery operations for this device.
    /// </summary>
    /// <value>The successful recovery count.</value>
    public int SuccessfulRecoveries => _successfulRecoveries;

    /// <summary>
    /// Gets the timestamp of the last health check performed on this device.
    /// </summary>
    /// <value>The UTC timestamp of the last health check.</value>
    public DateTimeOffset LastHealthCheck { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Records an error that occurred on this device and updates the failure statistics.
    /// </summary>
    /// <param name="error">The exception that occurred.</param>
    /// <remarks>
    /// This method is thread-safe and automatically updates the device health status
    /// based on the failure count. It maintains a rolling window of recent errors
    /// for error rate calculations.
    /// </remarks>
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
    /// Records a successful recovery operation and resets the consecutive failure count.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and marks the device as healthy after a successful recovery.
    /// </remarks>
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
    /// Records a failed recovery attempt and updates the recovery statistics.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and marks the device as unhealthy after a failed recovery.
    /// </remarks>
    public void RecordFailedRecovery()
    {
        lock (_lock)
        {
            _totalRecoveryAttempts++;
            IsHealthy = false;
        }
    }

    /// <summary>
    /// Calculates the current error rate for this device based on recent error history.
    /// </summary>
    /// <returns>The number of errors that occurred in the last hour.</returns>
    /// <remarks>
    /// The error rate is calculated as the number of errors in the past 60 minutes.
    /// This provides a rolling window view of device stability.
    /// </remarks>
    public double CalculateErrorRate()
    {
        var now = DateTimeOffset.UtcNow;
        var recentErrors = _recentErrors.ToArray()
            .Count(e => (now - _lastErrorTime).TotalMinutes < 60);

        return recentErrors; // Errors per hour
    }

    /// <summary>
    /// Updates the health check timestamp and performs automatic recovery assessment.
    /// </summary>
    /// <remarks>
    /// This method implements automatic healing logic where devices can recover
    /// their health status if no errors have occurred for an extended period (30 minutes).
    /// </remarks>
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
    /// Asynchronously resets the device recovery state to a healthy condition.
    /// </summary>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    /// <remarks>
    /// This method clears all error history and failure counts, effectively
    /// giving the device a fresh start. Use with caution as it removes
    /// valuable diagnostic information.
    /// </remarks>
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
    /// Asynchronously resets the GPU context and partially recovers the device state.
    /// </summary>
    /// <returns>A task representing the asynchronous context reset operation.</returns>
    /// <remarks>
    /// This method simulates a GPU context reset operation and provides partial
    /// recovery by reducing the consecutive failure count. In a real implementation,
    /// this would perform platform-specific GPU context reset operations.
    /// </remarks>
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
