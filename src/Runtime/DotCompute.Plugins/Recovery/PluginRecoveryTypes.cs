// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Recovery
{
    /// <summary>
    /// Plugin recovery strategy enumeration
    /// </summary>
    public enum PluginRecoveryStrategy
    {
        /// <summary>
        /// Restart the plugin process
        /// </summary>
        RestartPlugin,

        /// <summary>
        /// Reload plugin assembly
        /// </summary>
        ReloadPlugin,

        /// <summary>
        /// Isolate plugin in separate container
        /// </summary>
        IsolatePlugin,

        /// <summary>
        /// Shutdown plugin safely
        /// </summary>
        ShutdownPlugin,

        /// <summary>
        /// Rollback to previous plugin version
        /// </summary>
        RollbackVersion
    }

    /// <summary>
    /// Plugin health status enumeration
    /// </summary>
    public enum PluginHealthStatus
    {
        /// <summary>
        /// Plugin status is unknown
        /// </summary>
        Unknown,

        /// <summary>
        /// Plugin is operating normally
        /// </summary>
        Healthy,

        /// <summary>
        /// Plugin has warnings but is functional
        /// </summary>
        Warning,

        /// <summary>
        /// Plugin is in critical state but still running
        /// </summary>
        Critical,

        /// <summary>
        /// Plugin has failed and is not operational
        /// </summary>
        Failed,

        /// <summary>
        /// Plugin is currently being recovered
        /// </summary>
        Recovering,

        /// <summary>
        /// Plugin is isolated for safety
        /// </summary>
        Isolated,

        /// <summary>
        /// Plugin is shutting down
        /// </summary>
        ShuttingDown
    }

    /// <summary>
    /// Comprehensive plugin health information
    /// </summary>
    public sealed class PluginHealthInfo
    {
        /// <summary>
        /// Plugin identifier
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// Whether the plugin is currently healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Whether the plugin is running in isolation
        /// </summary>
        public bool IsIsolated { get; set; }

        /// <summary>
        /// Total number of errors encountered
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Number of times plugin has been restarted
        /// </summary>
        public int RestartCount { get; set; }

        /// <summary>
        /// Last error that occurred
        /// </summary>
        public Exception? LastError { get; set; }

        /// <summary>
        /// Timestamp of the last error
        /// </summary>
        public DateTimeOffset LastErrorTime { get; set; }

        /// <summary>
        /// Timestamp of the last restart
        /// </summary>
        public DateTimeOffset? LastRestart { get; set; }

        /// <summary>
        /// Timestamp of the last health check
        /// </summary>
        public DateTimeOffset LastHealthCheck { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Number of consecutive failures
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// Uptime percentage over monitoring period
        /// </summary>
        public double UptimePercent { get; set; } = 100.0;

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
        /// Additional custom metrics
        /// </summary>
        public Dictionary<string, object> CustomMetrics { get; set; } = [];

        /// <summary>
        /// Plugin start time
        /// </summary>
        public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Total runtime duration
        /// </summary>
        public TimeSpan Runtime => DateTimeOffset.UtcNow - StartTime;

        public override string ToString()
            => $"Plugin={PluginId}, Healthy={IsHealthy}, Errors={ErrorCount}, Restarts={RestartCount}, Uptime={UptimePercent:F1}%";
    }

    /// <summary>
    /// Plugin compatibility assessment result
    /// </summary>
    public sealed class PluginCompatibilityResult
    {
        /// <summary>
        /// Plugin identifier being assessed
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// Overall compatibility status
        /// </summary>
        public bool IsCompatible { get; set; }

        /// <summary>
        /// Framework version requirements
        /// </summary>
        public string RequiredFramework { get; set; } = string.Empty;

        /// <summary>
        /// Current framework version
        /// </summary>
        public string CurrentFramework { get; set; } = string.Empty;

        /// <summary>
        /// Required dependencies
        /// </summary>
        public List<string> RequiredDependencies { get; set; } = [];

        /// <summary>
        /// Missing dependencies
        /// </summary>
        public List<string> MissingDependencies { get; set; } = [];

        /// <summary>
        /// Version conflicts
        /// </summary>
        public List<string> VersionConflicts { get; set; } = [];

        /// <summary>
        /// Compatibility warnings
        /// </summary>
        public List<string> Warnings { get; set; } = [];

        /// <summary>
        /// Error message if compatibility check fails
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Assessment timestamp
        /// </summary>
        public DateTimeOffset AssessmentTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Detailed assessment report
        /// </summary>
        public string Report { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether framework is compatible
        /// </summary>
        public bool FrameworkCompatible { get; set; } = true;

        /// <summary>
        /// Gets or sets dependency conflicts
        /// </summary>
        public List<string> DependencyConflicts { get; set; } = [];

        /// <summary>
        /// Gets security issues
        /// </summary>
        public List<string> SecurityIssues { get; set; } = [];

        public override string ToString()
            => $"Plugin={PluginId}, Compatible={IsCompatible}, MissingDeps={MissingDependencies.Count}, Conflicts={VersionConflicts.Count}";
    }

    /// <summary>
    /// Plugin health state tracker
    /// </summary>
    public sealed class PluginHealthState(string pluginId, PluginRecoveryConfiguration config)
    {
        private readonly string _pluginId = pluginId;
        private readonly PluginRecoveryConfiguration _config = config;
        private readonly List<Exception> _recentErrors = [];
        private readonly object _lock = new();

        public string PluginId => _pluginId;
        public PluginHealthStatus Status { get; private set; } = PluginHealthStatus.Unknown;
        public int ErrorCount { get; private set; }
        public int RestartCount { get; private set; }
        public int ConsecutiveFailures { get; private set; }
        public DateTimeOffset LastError { get; private set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastRestart { get; private set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastSuccessfulOperation { get; private set; } = DateTimeOffset.UtcNow;

        public void RecordError(Exception error)
        {
            lock (_lock)
            {
                ErrorCount++;
                ConsecutiveFailures++;
                LastError = DateTimeOffset.UtcNow;
                _recentErrors.Add(error);

                // Keep only recent errors
                while (_recentErrors.Count > _config.MaxRecentErrors)
                {
                    _recentErrors.RemoveAt(0);
                }

                UpdateHealthStatus();
            }
        }

        public void RecordSuccessfulRecovery()
        {
            lock (_lock)
            {
                RestartCount++;
                ConsecutiveFailures = 0;
                LastRestart = DateTimeOffset.UtcNow;
                LastSuccessfulOperation = DateTimeOffset.UtcNow;
                UpdateHealthStatus();
            }
        }

        public void RecordSuccessfulOperation()
        {
            lock (_lock)
            {
                ConsecutiveFailures = 0;
                LastSuccessfulOperation = DateTimeOffset.UtcNow;
                UpdateHealthStatus();
            }
        }

        public bool ShouldRestart()
        {
            lock (_lock)
            {
                if (!_config.EnableAutoRestart)
                {
                    return false;
                }


                if (RestartCount >= _config.MaxRestartAttempts)
                {
                    return false;
                }


                if (ConsecutiveFailures < _config.FailureThresholdForRestart)
                {
                    return false;
                }


                var timeSinceLastRestart = DateTimeOffset.UtcNow - LastRestart;
                return timeSinceLastRestart >= _config.MinRestartInterval;
            }
        }

        public bool ShouldIsolate()
        {
            lock (_lock)
            {
                if (!_config.EnablePluginIsolation)
                {
                    return false;
                }


                return ConsecutiveFailures >= _config.FailureThresholdForIsolation;
            }
        }

        public List<Exception> GetRecentErrors()
        {
            lock (_lock)
            {
                return [.. _recentErrors];
            }
        }

        private void UpdateHealthStatus()
        {
            if (ConsecutiveFailures == 0)
            {
                Status = PluginHealthStatus.Healthy;
            }
            else if (ConsecutiveFailures < _config.WarningThreshold)
            {
                Status = PluginHealthStatus.Warning;
            }
            else if (ConsecutiveFailures < _config.CriticalThreshold)
            {
                Status = PluginHealthStatus.Critical;
            }
            else
            {
                Status = PluginHealthStatus.Failed;
            }
        }

        /// <summary>
        /// Records a failed recovery attempt
        /// </summary>
        public void RecordFailedRecovery()
        {
            lock (_lock)
            {
                ConsecutiveFailures++;
                UpdateHealthStatus();
            }
        }

        /// <summary>
        /// Sets the plugin as isolated
        /// </summary>
        public void SetIsolated()
        {
            lock (_lock)
            {
                Status = PluginHealthStatus.Isolated;
            }
        }

        /// <summary>
        /// Sets the plugin for emergency shutdown
        /// </summary>
        public void SetEmergencyShutdown()
        {
            lock (_lock)
            {
                Status = PluginHealthStatus.ShuttingDown;
            }
        }

        /// <summary>
        /// Checks if the plugin is currently healthy
        /// </summary>
        public bool IsHealthy => Status == PluginHealthStatus.Healthy;

        /// <summary>
        /// Checks if the plugin is isolated
        /// </summary>
        public bool IsIsolated => Status == PluginHealthStatus.Isolated;

        /// <summary>
        /// Gets the last health check timestamp
        /// </summary>
        public DateTimeOffset LastHealthCheck { get; private set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Calculates uptime percentage over monitoring period
        /// </summary>
        public double CalculateUptimePercent()
        {
            lock (_lock)
            {
                var totalTime = DateTimeOffset.UtcNow - LastRestart;
                if (totalTime.TotalMinutes < 1) return 100.0; // Not enough data

                var errorTime = TimeSpan.FromMinutes(ConsecutiveFailures);
                var uptime = totalTime - errorTime;

                return Math.Max(0, Math.Min(100, (uptime.TotalMilliseconds / totalTime.TotalMilliseconds) * 100));
            }
        }

        /// <summary>
        /// Updates the last health check timestamp
        /// </summary>
        public void UpdateHealthCheck()
        {
            lock (_lock)
            {
                LastHealthCheck = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// Records a restart event
        /// </summary>
        public void RecordRestart()
        {
            lock (_lock)
            {
                RestartCount++;
                LastRestart = DateTimeOffset.UtcNow;
                UpdateHealthStatus();
            }
        }
    }

    /// <summary>
    /// Plugin resource usage tracking
    /// </summary>
    public sealed class PluginResourceUsage
    {
        public string PluginId { get; set; } = string.Empty;
        public long MemoryUsageBytes { get; set; }
        public double CpuUsagePercent { get; set; }
        public int ThreadCount { get; set; }
        public int FileHandleCount { get; set; }
        public long NetworkBytesReceived { get; set; }
        public long NetworkBytesSent { get; set; }
        public TimeSpan ProcessorTime { get; set; }
        public DateTimeOffset MeasurementTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Resource usage thresholds for warnings
        /// </summary>
        public PluginResourceThresholds Thresholds { get; set; } = new();

        /// <summary>
        /// Checks if resource usage exceeds warning thresholds
        /// </summary>
        public bool ExceedsWarningThresholds()
        {
            return MemoryUsageBytes > Thresholds.MemoryWarningBytes ||
                   CpuUsagePercent > Thresholds.CpuWarningPercent ||
                   ThreadCount > Thresholds.ThreadWarningCount ||
                   FileHandleCount > Thresholds.FileHandleWarningCount;
        }

        /// <summary>
        /// Checks if resource usage exceeds critical thresholds
        /// </summary>
        public bool ExceedsCriticalThresholds()
        {
            return MemoryUsageBytes > Thresholds.MemoryCriticalBytes ||
                   CpuUsagePercent > Thresholds.CpuCriticalPercent ||
                   ThreadCount > Thresholds.ThreadCriticalCount ||
                   FileHandleCount > Thresholds.FileHandleCriticalCount;
        }

        public override string ToString()
            => $"Plugin={PluginId}, Memory={MemoryUsageBytes / 1024 / 1024}MB, CPU={CpuUsagePercent:F1}%, Threads={ThreadCount}";
    }

    /// <summary>
    /// Resource usage thresholds
    /// </summary>
    public sealed class PluginResourceThresholds
    {
        public long MemoryWarningBytes { get; set; } = 512 * 1024 * 1024; // 512MB
        public long MemoryCriticalBytes { get; set; } = 1024 * 1024 * 1024; // 1GB
        public double CpuWarningPercent { get; set; } = 75.0;
        public double CpuCriticalPercent { get; set; } = 90.0;
        public int ThreadWarningCount { get; set; } = 50;
        public int ThreadCriticalCount { get; set; } = 100;
        public int FileHandleWarningCount { get; set; } = 100;
        public int FileHandleCriticalCount { get; set; } = 200;
    }

    /// <summary>
    /// Plugin health report containing overall system health
    /// </summary>
    public sealed class PluginHealthReport
    {
        public List<PluginHealthInfo> PluginHealths { get; set; } = [];
        public int TotalPlugins => PluginHealths.Count;
        public int HealthyPlugins => PluginHealths.Count(p => p.IsHealthy);
        public int UnhealthyPlugins => TotalPlugins - HealthyPlugins;
        public int IsolatedPlugins => PluginHealths.Count(p => p.IsIsolated);
        public double OverallHealthPercent => TotalPlugins == 0 ? 100.0 : (double)HealthyPlugins / TotalPlugins * 100.0;
        public DateTimeOffset ReportTime { get; set; } = DateTimeOffset.UtcNow;

        // Additional properties for compatibility with PluginHealthMonitor
        public string PluginId { get; set; } = string.Empty;
        public PluginHealthStatus Status { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        public long MemoryUsageBytes { get; set; }
        public double CpuUsagePercent { get; set; }
        public int ActiveOperations { get; set; }
        public double OverallHealth { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = [];

        /// <summary>
        /// Gets critical plugins that need immediate attention
        /// </summary>
        public List<PluginHealthInfo> GetCriticalPlugins()
            => [.. PluginHealths.Where(p => !p.IsHealthy && p.ConsecutiveFailures > 5)];

        /// <summary>
        /// Gets summary statistics
        /// </summary>
        public string GetSummary()
            => $"Plugins: {TotalPlugins}, Healthy: {HealthyPlugins}, Unhealthy: {UnhealthyPlugins}, Isolated: {IsolatedPlugins}, Health: {OverallHealthPercent:F1}%";

        public override string ToString() => GetSummary();
    }


}