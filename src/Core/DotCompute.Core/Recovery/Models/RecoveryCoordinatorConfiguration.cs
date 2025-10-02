// <copyright file="RecoveryCoordinatorConfiguration.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Core.Recovery.Configuration;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Configuration for the recovery coordinator.
/// </summary>
public class RecoveryCoordinatorConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether automatic recovery is enabled.
    /// </summary>
    public bool EnableAutomaticRecovery { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of recovery attempts.
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between recovery attempts.
    /// </summary>
    public TimeSpan RecoveryAttemptDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets a value indicating whether to use exponential backoff.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum backoff delay.
    /// </summary>
    public TimeSpan MaxBackoffDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the health check interval.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the health check timeout.
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether to enable cascading recovery.
    /// </summary>
    public bool EnableCascadingRecovery { get; set; } = true;

    /// <summary>
    /// Gets or sets the recovery strategies to use.
    /// </summary>
    public IList<RecoveryStrategyConfig> RecoveryStrategies { get; } = [];

    /// <summary>
    /// Gets or sets the components to monitor.
    /// </summary>
    public IList<string> MonitoredComponents { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to enable recovery metrics.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable recovery logging.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the log level for recovery operations.
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets a value indicating whether to enable notifications.
    /// </summary>
    public bool EnableNotifications { get; set; }


    /// <summary>
    /// Gets or sets the notification endpoints.
    /// </summary>
    public IList<string> NotificationEndpoints { get; } = [];

    /// <summary>
    /// Gets or sets the recovery priority levels.
    /// </summary>
    public Dictionary<string, int> ComponentPriorities { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to enable failover.
    /// </summary>
    public bool EnableFailover { get; set; }


    /// <summary>
    /// Gets or sets the failover configuration.
    /// </summary>
    public FailoverConfiguration? FailoverConfig { get; set; }

    /// <summary>
    /// Gets or sets the recovery mode.
    /// </summary>
    public RecoveryMode Mode { get; set; } = RecoveryMode.Automatic;

    /// <summary>
    /// Gets or sets custom recovery parameters.
    /// </summary>
    public Dictionary<string, object> CustomParameters { get; } = [];

    /// <summary>
    /// Gets or sets the GPU recovery configuration.
    /// </summary>
    public GpuRecoveryConfiguration GpuRecoveryConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the memory recovery configuration.
    /// </summary>
    public MemoryRecoveryConfiguration MemoryRecoveryConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the compilation fallback configuration.
    /// </summary>
    public CompilationFallbackConfiguration CompilationFallbackConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the circuit breaker configuration.
    /// </summary>
    public CircuitBreakerConfiguration CircuitBreakerConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the plugin recovery configuration.
    /// </summary>
    public PluginRecoveryConfiguration PluginRecoveryConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the metrics report interval.
    /// </summary>
    public TimeSpan MetricsReportInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static RecoveryCoordinatorConfiguration Default => new();
}

/// <summary>
/// Configuration for a specific recovery strategy.
/// </summary>
public class RecoveryStrategyConfig
{
    /// <summary>
    /// Gets or sets the strategy name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the strategy is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the strategy priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the strategy timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets strategy-specific parameters.
    /// </summary>
    public Dictionary<string, object> Parameters { get; } = [];
}

/// <summary>
/// Configuration for failover functionality.
/// </summary>
public class FailoverConfiguration
{
    /// <summary>
    /// Gets or sets the failover targets.
    /// </summary>
    public IList<string> FailoverTargets { get; } = [];

    /// <summary>
    /// Gets or sets the failover timeout.
    /// </summary>
    public TimeSpan FailoverTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to auto-failback.
    /// </summary>
    public bool AutoFailback { get; set; } = true;

    /// <summary>
    /// Gets or sets the failback delay.
    /// </summary>
    public TimeSpan FailbackDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the health check count before failback.
    /// </summary>
    public int HealthCheckCountBeforeFailback { get; set; } = 3;
}

/// <summary>
/// Specifies the recovery mode.
/// </summary>
public enum RecoveryMode
{
    /// <summary>
    /// Automatic recovery without user intervention.
    /// </summary>
    Automatic,

    /// <summary>
    /// Semi-automatic recovery with user confirmation.
    /// </summary>
    SemiAutomatic,

    /// <summary>
    /// Manual recovery only.
    /// </summary>
    Manual,

    /// <summary>
    /// Monitoring only, no recovery actions.
    /// </summary>
    MonitorOnly
}