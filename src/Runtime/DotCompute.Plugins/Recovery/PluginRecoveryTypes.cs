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
        public Dictionary<string, object> CustomMetrics { get; } = [];

        /// <summary>
        /// Plugin start time
        /// </summary>
        public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Total runtime duration
        /// </summary>
        public TimeSpan Runtime => DateTimeOffset.UtcNow - StartTime;
        /// <summary>
        /// Gets to string.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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
        public IList<string> RequiredDependencies { get; } = [];

        /// <summary>
        /// Missing dependencies
        /// </summary>
        public IList<string> MissingDependencies { get; } = [];

        /// <summary>
        /// Version conflicts
        /// </summary>
        public IList<string> VersionConflicts { get; } = [];

        /// <summary>
        /// Compatibility warnings
        /// </summary>
        public IList<string> Warnings { get; } = [];

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
        public IList<string> DependencyConflicts { get; } = [];

        /// <summary>
        /// Gets security issues
        /// </summary>
        public IList<string> SecurityIssues { get; } = [];
        /// <summary>
        /// Gets to string.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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
        /// <summary>
        /// Gets or sets the plugin identifier.
        /// </summary>
        /// <value>The plugin id.</value>

        public string PluginId => _pluginId;
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public PluginHealthStatus Status { get; private set; } = PluginHealthStatus.Unknown;
        /// <summary>
        /// Gets or sets the error count.
        /// </summary>
        /// <value>The error count.</value>
        public int ErrorCount { get; private set; }
        /// <summary>
        /// Gets or sets the restart count.
        /// </summary>
        /// <value>The restart count.</value>
        public int RestartCount { get; private set; }
        /// <summary>
        /// Gets or sets the consecutive failures.
        /// </summary>
        /// <value>The consecutive failures.</value>
        public int ConsecutiveFailures { get; private set; }
        /// <summary>
        /// Gets or sets the last error.
        /// </summary>
        /// <value>The last error.</value>
        public DateTimeOffset LastError { get; private set; } = DateTimeOffset.MinValue;
        /// <summary>
        /// Gets or sets the last restart.
        /// </summary>
        /// <value>The last restart.</value>
        public DateTimeOffset LastRestart { get; private set; } = DateTimeOffset.MinValue;
        /// <summary>
        /// Gets or sets the last successful operation.
        /// </summary>
        /// <value>The last successful operation.</value>
        public DateTimeOffset LastSuccessfulOperation { get; private set; } = DateTimeOffset.UtcNow;
        /// <summary>
        /// Performs record error.
        /// </summary>
        /// <param name="error">The error.</param>

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
        /// <summary>
        /// Performs record successful recovery.
        /// </summary>

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
        /// <summary>
        /// Performs record successful operation.
        /// </summary>

        public void RecordSuccessfulOperation()
        {
            lock (_lock)
            {
                ConsecutiveFailures = 0;
                LastSuccessfulOperation = DateTimeOffset.UtcNow;
                UpdateHealthStatus();
            }
        }
        /// <summary>
        /// Determines should restart.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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
        /// <summary>
        /// Determines should isolate.
        /// </summary>
        /// <returns>The result of the operation.</returns>

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
        /// <summary>
        /// Gets the recent errors.
        /// </summary>
        /// <returns>The recent errors.</returns>

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
        /// <summary>
        /// Gets or sets the plugin identifier.
        /// </summary>
        /// <value>The plugin id.</value>
        public string PluginId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the memory usage bytes.
        /// </summary>
        /// <value>The memory usage bytes.</value>
        public long MemoryUsageBytes { get; set; }
        /// <summary>
        /// Gets or sets the cpu usage percent.
        /// </summary>
        /// <value>The cpu usage percent.</value>
        public double CpuUsagePercent { get; set; }
        /// <summary>
        /// Gets or sets the thread count.
        /// </summary>
        /// <value>The thread count.</value>
        public int ThreadCount { get; set; }
        /// <summary>
        /// Gets or sets the file handle count.
        /// </summary>
        /// <value>The file handle count.</value>
        public int FileHandleCount { get; set; }
        /// <summary>
        /// Gets or sets the network bytes received.
        /// </summary>
        /// <value>The network bytes received.</value>
        public long NetworkBytesReceived { get; set; }
        /// <summary>
        /// Gets or sets the network bytes sent.
        /// </summary>
        /// <value>The network bytes sent.</value>
        public long NetworkBytesSent { get; set; }
        /// <summary>
        /// Gets or sets the processor time.
        /// </summary>
        /// <value>The processor time.</value>
        public TimeSpan ProcessorTime { get; set; }
        /// <summary>
        /// Gets or sets the measurement time.
        /// </summary>
        /// <value>The measurement time.</value>
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
        /// <summary>
        /// Gets to string.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public override string ToString()
            => $"Plugin={PluginId}, Memory={MemoryUsageBytes / 1024 / 1024}MB, CPU={CpuUsagePercent:F1}%, Threads={ThreadCount}";
    }

    /// <summary>
    /// Resource usage thresholds
    /// </summary>
    public sealed class PluginResourceThresholds
    {
        /// <summary>
        /// Gets or sets the memory warning bytes.
        /// </summary>
        /// <value>The memory warning bytes.</value>
        public long MemoryWarningBytes { get; set; } = 512 * 1024 * 1024; // 512MB
        /// <summary>
        /// Gets or sets the memory critical bytes.
        /// </summary>
        /// <value>The memory critical bytes.</value>
        public long MemoryCriticalBytes { get; set; } = 1024 * 1024 * 1024; // 1GB
        /// <summary>
        /// Gets or sets the cpu warning percent.
        /// </summary>
        /// <value>The cpu warning percent.</value>
        public double CpuWarningPercent { get; set; } = 75.0;
        /// <summary>
        /// Gets or sets the cpu critical percent.
        /// </summary>
        /// <value>The cpu critical percent.</value>
        public double CpuCriticalPercent { get; set; } = 90.0;
        /// <summary>
        /// Gets or sets the thread warning count.
        /// </summary>
        /// <value>The thread warning count.</value>
        public int ThreadWarningCount { get; set; } = 50;
        /// <summary>
        /// Gets or sets the thread critical count.
        /// </summary>
        /// <value>The thread critical count.</value>
        public int ThreadCriticalCount { get; set; } = 100;
        /// <summary>
        /// Gets or sets the file handle warning count.
        /// </summary>
        /// <value>The file handle warning count.</value>
        public int FileHandleWarningCount { get; set; } = 100;
        /// <summary>
        /// Gets or sets the file handle critical count.
        /// </summary>
        /// <value>The file handle critical count.</value>
        public int FileHandleCriticalCount { get; set; } = 200;
    }

    /// <summary>
    /// Plugin health report containing overall system health
    /// </summary>
    public sealed class PluginHealthReport
    {
        /// <summary>
        /// Gets or sets the plugin healths.
        /// </summary>
        /// <value>The plugin healths.</value>
        public IList<PluginHealthInfo> PluginHealths { get; } = [];
        /// <summary>
        /// Gets or sets the total plugins.
        /// </summary>
        /// <value>The total plugins.</value>
        public int TotalPlugins => PluginHealths.Count;
        /// <summary>
        /// Gets or sets the healthy plugins.
        /// </summary>
        /// <value>The healthy plugins.</value>
        public int HealthyPlugins => PluginHealths.Count(p => p.IsHealthy);
        /// <summary>
        /// Gets or sets the unhealthy plugins.
        /// </summary>
        /// <value>The unhealthy plugins.</value>
        public int UnhealthyPlugins => TotalPlugins - HealthyPlugins;
        /// <summary>
        /// Gets or sets a value indicating whether olated plugins.
        /// </summary>
        /// <value>The isolated plugins.</value>
        public int IsolatedPlugins => PluginHealths.Count(p => p.IsIsolated);
        /// <summary>
        /// Gets or sets the overall health percent.
        /// </summary>
        /// <value>The overall health percent.</value>
        public double OverallHealthPercent => TotalPlugins == 0 ? 100.0 : (double)HealthyPlugins / TotalPlugins * 100.0;
        /// <summary>
        /// Gets or sets the report time.
        /// </summary>
        /// <value>The report time.</value>
        public DateTimeOffset ReportTime { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>
        /// Gets or sets the plugin identifier.
        /// </summary>
        /// <value>The plugin id.</value>

        // Additional properties for compatibility with PluginHealthMonitor
        public string PluginId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public PluginHealthStatus Status { get; set; }
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>The timestamp.</value>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>
        /// Gets or sets the memory usage bytes.
        /// </summary>
        /// <value>The memory usage bytes.</value>
        public long MemoryUsageBytes { get; set; }
        /// <summary>
        /// Gets or sets the cpu usage percent.
        /// </summary>
        /// <value>The cpu usage percent.</value>
        public double CpuUsagePercent { get; set; }
        /// <summary>
        /// Gets or sets the active operations.
        /// </summary>
        /// <value>The active operations.</value>
        public int ActiveOperations { get; set; }
        /// <summary>
        /// Gets or sets the overall health.
        /// </summary>
        /// <value>The overall health.</value>
        public double OverallHealth { get; set; }
        /// <summary>
        /// Gets or sets the metrics.
        /// </summary>
        /// <value>The metrics.</value>
        public Dictionary<string, object> Metrics { get; } = [];

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
        /// <summary>
        /// Gets to string.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public override string ToString() => GetSummary();
    }


}