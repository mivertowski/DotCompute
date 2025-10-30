// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Configuration for plugin recovery behavior
/// </summary>
public sealed class PluginRecoveryConfiguration
{
    /// <summary>
    /// Maximum number of recovery attempts before giving up
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = 3;

    /// <summary>
    /// Time window for tracking consecutive failures
    /// </summary>
    public TimeSpan FailureTrackingWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Interval between health checks
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for recovery operations
    /// </summary>
    public TimeSpan RecoveryTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Number of failures before triggering circuit breaker
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Duration to keep circuit breaker open
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Enable automatic recovery
    /// </summary>
    public bool EnableAutoRecovery { get; set; } = true;

    /// <summary>
    /// Enable plugin isolation on failure
    /// </summary>
    public bool EnableIsolation { get; set; } = true;

    /// <summary>
    /// Enable health monitoring
    /// </summary>
    public bool EnableHealthMonitoring { get; set; } = true;

    /// <summary>
    /// Enable state persistence
    /// </summary>
    public bool EnableStatePersistence { get; set; }

    /// <summary>
    /// Path for persisting recovery state
    /// </summary>
    public string? StateFilePath { get; set; }

    /// <summary>
    /// Default recovery strategy
    /// </summary>
    public PluginRecoveryStrategy DefaultStrategy { get; set; } = PluginRecoveryStrategy.RestartPlugin;

    /// <summary>
    /// Circuit breaker reset interval
    /// </summary>
    public TimeSpan CircuitBreakerResetInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Circuit breaker half-open maximum attempts
    /// </summary>
    public int CircuitBreakerHalfOpenMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Circuit breaker failure threshold
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker timeout
    /// </summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enable plugin isolation
    /// </summary>
    public bool EnablePluginIsolation { get; set; } = true;

    /// <summary>
    /// Enable auto restart on failure
    /// </summary>
    public bool EnableAutoRestart { get; set; } = true;

    /// <summary>
    /// Maximum failure history size to track
    /// </summary>
    public int MaxFailureHistorySize { get; set; } = 100;

    /// <summary>
    /// Maximum age for state snapshots
    /// </summary>
    public TimeSpan StateSnapshotMaxAge { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Maximum consecutive failures before triggering recovery
    /// </summary>
    public int MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>
    /// Delay before restarting a failed plugin
    /// </summary>
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of recent errors to track
    /// </summary>
    public int MaxRecentErrors { get; set; } = 50;

    /// <summary>
    /// Maximum number of restarts allowed
    /// </summary>
    public int MaxRestarts { get; set; } = 10;

    /// <summary>
    /// Maximum restart attempts before giving up
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 5;

    /// <summary>
    /// Failure threshold for restart trigger
    /// </summary>
    public int FailureThresholdForRestart { get; set; } = 3;

    /// <summary>
    /// Minimum interval between restarts
    /// </summary>
    public TimeSpan MinRestartInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Failure threshold for isolation trigger
    /// </summary>
    public int FailureThresholdForIsolation { get; set; } = 10;

    /// <summary>
    /// Warning threshold for health monitoring
    /// </summary>
    public int WarningThreshold { get; set; } = 5;

    /// <summary>
    /// Critical threshold for health monitoring
    /// </summary>
    public int CriticalThreshold { get; set; } = 15;

    /// <summary>
    /// Default configuration instance
    /// </summary>
    public static PluginRecoveryConfiguration Default { get; } = new PluginRecoveryConfiguration();
}



// CircuitBreakerState enum moved to PluginRecoveryTypes.cs to avoid duplication
