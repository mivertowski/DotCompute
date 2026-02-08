// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.RingKernels.Resilience;

/// <summary>
/// Configuration options for ring kernel fault recovery and watchdog behavior.
/// </summary>
public sealed class RingKernelFaultRecoveryOptions
{
    /// <summary>
    /// Gets the default options instance.
    /// </summary>
    public static RingKernelFaultRecoveryOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets whether the kernel watchdog is enabled.
    /// When enabled, monitors kernel health and triggers recovery on detected issues.
    /// </summary>
    public bool EnableWatchdog { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between watchdog health checks.
    /// </summary>
    public TimeSpan WatchdogInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the timeout after which a kernel is considered stalled.
    /// If a kernel doesn't process messages within this time, recovery is attempted.
    /// </summary>
    public TimeSpan KernelStallTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum time to wait for a kernel to respond to heartbeat.
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets whether automatic restart is enabled for crashed kernels.
    /// </summary>
    public bool EnableAutoRestart { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of automatic restart attempts before giving up.
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between restart attempts.
    /// </summary>
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets whether to use exponential backoff for restart delays.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum delay when using exponential backoff.
    /// </summary>
    public TimeSpan MaxRestartDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the window for tracking kernel failures.
    /// Failures outside this window are not counted toward the circuit breaker.
    /// </summary>
    public TimeSpan FailureTrackingWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the failure threshold that triggers the circuit breaker.
    /// When this many failures occur within the tracking window, the circuit opens.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration the circuit breaker remains open before attempting recovery.
    /// </summary>
    public TimeSpan CircuitBreakerOpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to reset the failure count after successful kernel execution.
    /// </summary>
    public bool ResetFailuresOnSuccess { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to notify health monitor of kernel failures.
    /// </summary>
    public bool NotifyHealthMonitor { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum time a kernel must run successfully before
    /// the restart count is reset (prevents rapid failure-restart cycles).
    /// </summary>
    public TimeSpan SuccessfulRunThreshold { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Validates the options and throws if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option is out of valid range.</exception>
    public void Validate()
    {
        if (WatchdogInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(WatchdogInterval), "Watchdog interval must be positive.");
        }

        if (KernelStallTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(KernelStallTimeout), "Kernel stall timeout must be positive.");
        }

        if (HeartbeatTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(HeartbeatTimeout), "Heartbeat timeout must be positive.");
        }

        if (MaxRestartAttempts < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRestartAttempts), "Max restart attempts cannot be negative.");
        }

        if (RestartDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RestartDelay), "Restart delay must be positive.");
        }

        if (CircuitBreakerFailureThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CircuitBreakerFailureThreshold), "Circuit breaker threshold must be at least 1.");
        }

        if (CircuitBreakerOpenDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(CircuitBreakerOpenDuration), "Circuit breaker duration must be positive.");
        }
    }
}
