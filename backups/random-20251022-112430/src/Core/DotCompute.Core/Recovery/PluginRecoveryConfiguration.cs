// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Configuration for plugin recovery operations
/// </summary>
public class PluginRecoveryConfiguration
{
    /// <summary>
    /// Maximum time to wait for plugin recovery operations
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
    public TimeSpan RecoveryTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Number of recovery attempts before marking plugin as failed
    /// </summary>
    [Range(1, 10)]
    public int MaxRecoveryAttempts { get; set; } = 3;

    /// <summary>
    /// Interval between recovery attempts
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to enable automatic plugin restart on failure
    /// </summary>
    public bool EnableAutoRestart { get; set; } = true;

    /// <summary>
    /// Whether to preserve plugin state during recovery
    /// </summary>
    public bool PreserveState { get; set; } = true;

    /// <summary>
    /// Plugin health check interval
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:10", "00:30:00")]
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum memory usage threshold before triggering recovery (in bytes)
    /// </summary>
    [Range(1024 * 1024, long.MaxValue)] // Min 1MB
    public long MaxMemoryUsageBytes { get; set; } = 1024L * 1024L * 1024L; // 1GB

    /// <summary>
    /// Maximum CPU usage percentage before triggering recovery
    /// </summary>
    [Range(0.1, 100.0)]
    public double MaxCpuUsagePercent { get; set; } = 90.0;

    /// <summary>
    /// Whether to enable detailed recovery logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// List of critical exceptions that should trigger immediate recovery
    /// </summary>
    public HashSet<string> CriticalExceptionTypes { get; } =
    [
        typeof(OutOfMemoryException).FullName!,
        typeof(AccessViolationException).FullName!,
        typeof(StackOverflowException).FullName!
    ];

    /// <summary>
    /// Whether to enable plugin isolation in separate containers
    /// </summary>
    public bool EnablePluginIsolation { get; set; } = true;

    /// <summary>
    /// Delay before restarting a failed plugin
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum number of consecutive failures before taking drastic action
    /// </summary>
    [Range(1, 20)]
    public int MaxConsecutiveFailures { get; set; } = 5;

    /// <summary>
    /// Maximum number of restarts allowed per plugin
    /// </summary>
    [Range(1, 50)]
    public int MaxRestarts { get; set; } = 10;

    /// <summary>
    /// Default configuration instance
    /// </summary>
    public static PluginRecoveryConfiguration Default => new();

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (RecoveryTimeout <= TimeSpan.Zero)
        {

            throw new ArgumentException("Recovery timeout must be positive", nameof(RecoveryTimeout));
        }


        if (MaxRecoveryAttempts <= 0)
        {

            throw new ArgumentException("Max recovery attempts must be positive", nameof(MaxRecoveryAttempts));
        }


        if (RecoveryInterval <= TimeSpan.Zero)
        {

            throw new ArgumentException("Recovery interval must be positive", nameof(RecoveryInterval));
        }


        if (HealthCheckInterval <= TimeSpan.Zero)
        {

            throw new ArgumentException("Health check interval must be positive", nameof(HealthCheckInterval));
        }


        if (MaxMemoryUsageBytes <= 0)
        {

            throw new ArgumentException("Max memory usage must be positive", nameof(MaxMemoryUsageBytes));
        }


        if (MaxCpuUsagePercent is <= 0 or > 100)
        {

            throw new ArgumentException("Max CPU usage must be between 0 and 100", nameof(MaxCpuUsagePercent));
        }


        if (RestartDelay <= TimeSpan.Zero)
        {

            throw new ArgumentException("Restart delay must be positive", nameof(RestartDelay));
        }


        if (MaxConsecutiveFailures <= 0)
        {

            throw new ArgumentException("Max consecutive failures must be positive", nameof(MaxConsecutiveFailures));
        }


        if (MaxRestarts <= 0)
        {

            throw new ArgumentException("Max restarts must be positive", nameof(MaxRestarts));
        }
    }
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()

        => $"Timeout={RecoveryTimeout}, MaxAttempts={MaxRecoveryAttempts}, Interval={RecoveryInterval}";
}

/// <summary>
/// Plugin health report containing status and metrics
/// </summary>
public class PluginHealthReport
{
    /// <summary>
    /// Plugin identifier
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Current health status
    /// </summary>
    public PluginHealthStatus Status { get; set; } = PluginHealthStatus.Unknown;

    /// <summary>
    /// Timestamp of the health check
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Current memory usage in bytes
    /// </summary>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Current CPU usage percentage
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Number of active operations
    /// </summary>
    public int ActiveOperations { get; set; }

    /// <summary>
    /// Last error message, if any
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Last error timestamp
    /// </summary>
    public DateTimeOffset? LastErrorTimestamp { get; set; }

    /// <summary>
    /// Recovery statistics
    /// </summary>
    public PluginRecoveryStats RecoveryStats { get; set; } = new();

    /// <summary>
    /// Additional metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; init; } = [];

    /// <summary>
    /// Overall health score (0.0 to 1.0)
    /// </summary>
    public double OverallHealth { get; set; }
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()

        => $"Plugin={PluginId}, Status={Status}, Memory={MemoryUsageBytes / 1024.0 / 1024.0:F1}MB, CPU={CpuUsagePercent:F1}%";
}
/// <summary>
/// An plugin health status enumeration.
/// </summary>

/// <summary>
/// Plugin health status enumeration
/// </summary>
public enum PluginHealthStatus
{
    Unknown,
    Healthy,
    Warning,
    Critical,
    Failed,
    Recovering
}

/// <summary>
/// Plugin recovery statistics
/// </summary>
public class PluginRecoveryStats
{
    /// <summary>
    /// Total number of recovery attempts
    /// </summary>
    public int TotalRecoveryAttempts { get; set; }

    /// <summary>
    /// Number of successful recoveries
    /// </summary>
    public int SuccessfulRecoveries { get; set; }

    /// <summary>
    /// Number of failed recoveries
    /// </summary>
    public int FailedRecoveries { get; set; }

    /// <summary>
    /// Average recovery time
    /// </summary>
    public TimeSpan AverageRecoveryTime { get; set; }

    /// <summary>
    /// Last recovery timestamp
    /// </summary>
    public DateTimeOffset? LastRecoveryTimestamp { get; set; }

    /// <summary>
    /// Recovery success rate (0.0 to 1.0)
    /// </summary>
    public double SuccessRate => TotalRecoveryAttempts > 0 ? (double)SuccessfulRecoveries / TotalRecoveryAttempts : 0.0;
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()

        => $"Total={TotalRecoveryAttempts}, Success={SuccessfulRecoveries}, Failed={FailedRecoveries}, Rate={SuccessRate:P1}";
}