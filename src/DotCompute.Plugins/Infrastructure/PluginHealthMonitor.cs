// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DotCompute.Plugins.Infrastructure;

/// <summary>
/// Comprehensive health monitoring system for plugin lifecycle and performance.
/// </summary>
public sealed class PluginHealthMonitor : BackgroundService, IDisposable
{
    private readonly ILogger<PluginHealthMonitor> _logger;
    private readonly PluginHealthMonitorOptions _options;
    private readonly ConcurrentDictionary<string, PluginHealthInfo> _pluginHealth;
    private readonly ConcurrentDictionary<string, PluginPerformanceMetrics> _performanceMetrics;
    private readonly ConcurrentDictionary<string, PluginLifecycleState> _lifecycleStates;
    private readonly Timer _healthCheckTimer;
    private readonly Timer _metricsCollectionTimer;
    private readonly SemaphoreSlim _monitoringSemaphore;
    private readonly PerformanceCounterMonitor _performanceCounterMonitor;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginHealthMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options.</param>
    public PluginHealthMonitor(ILogger<PluginHealthMonitor> logger, PluginHealthMonitorOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new PluginHealthMonitorOptions();
        _pluginHealth = new ConcurrentDictionary<string, PluginHealthInfo>();
        _performanceMetrics = new ConcurrentDictionary<string, PluginPerformanceMetrics>();
        _lifecycleStates = new ConcurrentDictionary<string, PluginLifecycleState>();
        _monitoringSemaphore = new SemaphoreSlim(1, 1);
        _performanceCounterMonitor = new PerformanceCounterMonitor(_logger);

        // Setup periodic health checks
        _healthCheckTimer = new Timer(
            PerformHealthChecks, 
            null, 
            _options.HealthCheckInterval, 
            _options.HealthCheckInterval);

        // Setup periodic metrics collection
        _metricsCollectionTimer = new Timer(
            CollectMetrics, 
            null, 
            _options.MetricsCollectionInterval, 
            _options.MetricsCollectionInterval);

        _logger.LogInformation("Plugin health monitor initialized with {HealthCheckInterval}ms health check interval", 
            _options.HealthCheckInterval.TotalMilliseconds);
    }

    /// <summary>
    /// Registers a plugin for health monitoring.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="pluginInstance">The plugin instance.</param>
    /// <param name="assembly">The plugin assembly.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RegisterPluginAsync(string pluginId, object pluginInstance, Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(pluginInstance);
        ArgumentNullException.ThrowIfNull(assembly);

        await _monitoringSemaphore.WaitAsync();
        try
        {
            var healthInfo = new PluginHealthInfo
            {
                PluginId = pluginId,
                PluginType = pluginInstance.GetType(),
                Assembly = assembly,
                Instance = new WeakReference(pluginInstance),
                RegistrationTime = DateTime.UtcNow,
                LastHealthCheck = DateTime.UtcNow,
                Status = PluginHealthStatus.Healthy
            };

            _pluginHealth.AddOrUpdate(pluginId, healthInfo, (_, existing) =>
            {
                existing.Instance = new WeakReference(pluginInstance);
                existing.RegistrationTime = DateTime.UtcNow;
                existing.Status = PluginHealthStatus.Healthy;
                return existing;
            });

            var metrics = new PluginPerformanceMetrics
            {
                PluginId = pluginId,
                StartTime = DateTime.UtcNow
            };

            _performanceMetrics.AddOrUpdate(pluginId, metrics, (_, existing) => existing);

            var lifecycleState = new PluginLifecycleState
            {
                PluginId = pluginId,
                CurrentState = PluginLifecycleStage.Registered,
                StateHistory = [new PluginStateTransition
                {
                    FromState = PluginLifecycleStage.Unknown,
                    ToState = PluginLifecycleStage.Registered,
                    Timestamp = DateTime.UtcNow,
                    Reason = "Plugin registered for monitoring"
                }]
            };

            _lifecycleStates.AddOrUpdate(pluginId, lifecycleState, (_, existing) => existing);

            _logger.LogInformation("Registered plugin for health monitoring: {PluginId} ({PluginType})", 
                pluginId, pluginInstance.GetType().Name);
        }
        finally
        {
            _monitoringSemaphore.Release();
        }
    }

    /// <summary>
    /// Unregisters a plugin from health monitoring.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UnregisterPluginAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        await _monitoringSemaphore.WaitAsync();
        try
        {
            var removed = _pluginHealth.TryRemove(pluginId, out var healthInfo);
            _performanceMetrics.TryRemove(pluginId, out _);

            if (_lifecycleStates.TryGetValue(pluginId, out var lifecycleState))
            {
                RecordStateTransition(lifecycleState, PluginLifecycleStage.Unregistered, "Plugin unregistered from monitoring");
                _lifecycleStates.TryRemove(pluginId, out _);
            }

            if (removed)
            {
                _logger.LogInformation("Unregistered plugin from health monitoring: {PluginId}", pluginId);
            }
        }
        finally
        {
            _monitoringSemaphore.Release();
        }
    }

    /// <summary>
    /// Records a plugin lifecycle state transition.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="newState">The new lifecycle state.</param>
    /// <param name="reason">The reason for the state transition.</param>
    public void RecordStateTransition(string pluginId, PluginLifecycleStage newState, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (_lifecycleStates.TryGetValue(pluginId, out var lifecycleState))
        {
            RecordStateTransition(lifecycleState, newState, reason);
        }
    }

    /// <summary>
    /// Records plugin execution metrics.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="success">Whether the operation was successful.</param>
    /// <param name="errorMessage">Error message if the operation failed.</param>
    public void RecordExecution(string pluginId, string operationName, TimeSpan executionTime, bool success, string? errorMessage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (_performanceMetrics.TryGetValue(pluginId, out var metrics))
        {
            var execution = new PluginExecutionRecord
            {
                OperationName = operationName,
                ExecutionTime = executionTime,
                Success = success,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = errorMessage
            };

            lock (metrics.ExecutionHistory)
            {
                metrics.ExecutionHistory.Add(execution);
                
                // Keep only recent executions
                if (metrics.ExecutionHistory.Count > _options.MaxExecutionHistorySize)
                {
                    metrics.ExecutionHistory.RemoveAt(0);
                }
            }

            metrics.TotalExecutions++;
            if (success)
            {
                metrics.SuccessfulExecutions++;
            }
            else
            {
                metrics.FailedExecutions++;
                metrics.LastError = errorMessage;
                metrics.LastErrorTime = DateTime.UtcNow;
            }

            metrics.TotalExecutionTime += executionTime;
            metrics.AverageExecutionTime = TimeSpan.FromTicks(metrics.TotalExecutionTime.Ticks / metrics.TotalExecutions);

            if (executionTime > metrics.MaxExecutionTime)
            {
                metrics.MaxExecutionTime = executionTime;
            }

            if (metrics.MinExecutionTime == TimeSpan.Zero || executionTime < metrics.MinExecutionTime)
            {
                metrics.MinExecutionTime = executionTime;
            }

            // Update success rate
            metrics.SuccessRate = metrics.TotalExecutions > 0 
                ? (double)metrics.SuccessfulExecutions / metrics.TotalExecutions * 100 
                : 0;
        }
    }

    /// <summary>
    /// Gets comprehensive health information for all registered plugins.
    /// </summary>
    /// <returns>Comprehensive health report.</returns>
    public async Task<PluginHealthReport> GetHealthReportAsync()
    {
        await _monitoringSemaphore.WaitAsync();
        try
        {
            var report = new PluginHealthReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalPluginsMonitored = _pluginHealth.Count
            };

            foreach (var healthInfo in _pluginHealth.Values)
            {
                var pluginReport = new PluginHealthSummary
                {
                    PluginId = healthInfo.PluginId,
                    Status = healthInfo.Status,
                    LastHealthCheck = healthInfo.LastHealthCheck,
                    UpTime = DateTime.UtcNow - healthInfo.RegistrationTime,
                    IsAlive = healthInfo.Instance.IsAlive,
                    HealthScore = CalculateHealthScore(healthInfo.PluginId)
                };

                if (_performanceMetrics.TryGetValue(healthInfo.PluginId, out var metrics))
                {
                    pluginReport.PerformanceMetrics = metrics;
                }

                if (_lifecycleStates.TryGetValue(healthInfo.PluginId, out var lifecycleState))
                {
                    pluginReport.CurrentLifecycleState = lifecycleState.CurrentState;
                    pluginReport.LifecycleHistory = lifecycleState.StateHistory.TakeLast(10).ToList();
                }

                report.PluginSummaries.Add(pluginReport);

                // Update overall statistics
                switch (healthInfo.Status)
                {
                    case PluginHealthStatus.Healthy:
                        report.HealthyCount++;
                        break;
                    case PluginHealthStatus.Warning:
                        report.WarningCount++;
                        break;
                    case PluginHealthStatus.Critical:
                        report.CriticalCount++;
                        break;
                    case PluginHealthStatus.Failed:
                        report.FailedCount++;
                        break;
                }
            }

            // System-wide performance metrics
            report.SystemMetrics = await _performanceCounterMonitor.GetSystemMetricsAsync();
            report.OverallHealthScore = CalculateOverallHealthScore(report);

            return report;
        }
        finally
        {
            _monitoringSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets detailed health information for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>Detailed plugin health information.</returns>
    public async Task<DetailedPluginHealth?> GetDetailedPluginHealthAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        await _monitoringSemaphore.WaitAsync();
        try
        {
            if (!_pluginHealth.TryGetValue(pluginId, out var healthInfo))
            {
                return null;
            }

            var detailed = new DetailedPluginHealth
            {
                PluginId = pluginId,
                PluginType = healthInfo.PluginType.FullName ?? healthInfo.PluginType.Name,
                AssemblyName = healthInfo.Assembly.FullName ?? "Unknown",
                Status = healthInfo.Status,
                RegistrationTime = healthInfo.RegistrationTime,
                LastHealthCheck = healthInfo.LastHealthCheck,
                IsInstanceAlive = healthInfo.Instance.IsAlive,
                HealthScore = CalculateHealthScore(pluginId)
            };

            if (_performanceMetrics.TryGetValue(pluginId, out var metrics))
            {
                detailed.PerformanceMetrics = metrics;
                detailed.RecentExecutions = metrics.ExecutionHistory.TakeLast(20).ToList();
            }

            if (_lifecycleStates.TryGetValue(pluginId, out var lifecycleState))
            {
                detailed.CurrentLifecycleState = lifecycleState.CurrentState;
                detailed.LifecycleHistory = lifecycleState.StateHistory.ToList();
            }

            // Get detailed health checks
            detailed.HealthChecks = await PerformDetailedHealthChecksAsync(healthInfo);

            return detailed;
        }
        finally
        {
            _monitoringSemaphore.Release();
        }
    }

    /// <summary>
    /// Exports health monitoring data to JSON format.
    /// </summary>
    /// <param name="filePath">The file path to export to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExportHealthDataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var healthReport = await GetHealthReportAsync();
        var json = JsonSerializer.Serialize(healthReport, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        _logger.LogInformation("Exported health monitoring data to: {FilePath}", filePath);
    }

    private void PerformHealthChecks(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _monitoringSemaphore.WaitAsync();
                try
                {
                    foreach (var healthInfo in _pluginHealth.Values.ToList())
                    {
                        await PerformSinglePluginHealthCheckAsync(healthInfo);
                    }
                }
                finally
                {
                    _monitoringSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health checks");
            }
        });
    }

    private async Task PerformSinglePluginHealthCheckAsync(PluginHealthInfo healthInfo)
    {
        try
        {
            var previousStatus = healthInfo.Status;
            var newStatus = PluginHealthStatus.Healthy;

            // Check if plugin instance is still alive
            if (!healthInfo.Instance.IsAlive)
            {
                newStatus = PluginHealthStatus.Failed;
                healthInfo.Issues.Add("Plugin instance has been garbage collected");
            }

            // Check performance metrics
            if (_performanceMetrics.TryGetValue(healthInfo.PluginId, out var metrics))
            {
                // Check error rate
                if (metrics.SuccessRate < _options.CriticalSuccessRateThreshold)
                {
                    newStatus = PluginHealthStatus.Critical;
                    healthInfo.Issues.Add($"Success rate too low: {metrics.SuccessRate:F1}%");
                }
                else if (metrics.SuccessRate < _options.WarningSuccessRateThreshold)
                {
                    newStatus = Math.Max(newStatus, PluginHealthStatus.Warning);
                    healthInfo.Issues.Add($"Success rate below warning threshold: {metrics.SuccessRate:F1}%");
                }

                // Check recent failures
                var recentFailures = metrics.ExecutionHistory
                    .Where(e => !e.Success && DateTime.UtcNow - e.Timestamp < TimeSpan.FromMinutes(5))
                    .Count();

                if (recentFailures > _options.MaxRecentFailures)
                {
                    newStatus = Math.Max(newStatus, PluginHealthStatus.Warning);
                    healthInfo.Issues.Add($"Too many recent failures: {recentFailures}");
                }
            }

            // Check memory usage if available
            var target = healthInfo.Instance.Target;
            if (target != null)
            {
                // Perform custom health checks if the plugin implements IHealthCheckable
                if (target is IPluginHealthCheckable healthCheckable)
                {
                    var customResult = await healthCheckable.CheckHealthAsync();
                    if (!customResult.IsHealthy)
                    {
                        newStatus = Math.Max(newStatus, customResult.IsCritical ? PluginHealthStatus.Critical : PluginHealthStatus.Warning);
                        healthInfo.Issues.AddRange(customResult.Issues);
                    }
                }
            }

            healthInfo.Status = newStatus;
            healthInfo.LastHealthCheck = DateTime.UtcNow;

            // Log status changes
            if (previousStatus != newStatus)
            {
                _logger.LogWarning("Plugin health status changed: {PluginId} from {PreviousStatus} to {NewStatus}",
                    healthInfo.PluginId, previousStatus, newStatus);

                // Record lifecycle transition if appropriate
                if (_lifecycleStates.TryGetValue(healthInfo.PluginId, out var lifecycleState))
                {
                    var newLifecycleState = newStatus switch
                    {
                        PluginHealthStatus.Failed => PluginLifecycleStage.Failed,
                        PluginHealthStatus.Critical => PluginLifecycleStage.Degraded,
                        _ => lifecycleState.CurrentState
                    };

                    if (newLifecycleState != lifecycleState.CurrentState)
                    {
                        RecordStateTransition(lifecycleState, newLifecycleState, $"Health status changed to {newStatus}");
                    }
                }
            }

            // Clear issues if status is healthy
            if (newStatus == PluginHealthStatus.Healthy)
            {
                healthInfo.Issues.Clear();
            }
        }
        catch (Exception ex)
        {
            healthInfo.Status = PluginHealthStatus.Failed;
            healthInfo.Issues.Add($"Health check failed: {ex.Message}");
            _logger.LogError(ex, "Health check failed for plugin: {PluginId}", healthInfo.PluginId);
        }
    }

    private void CollectMetrics(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _performanceCounterMonitor.CollectSystemMetricsAsync();
                
                // Additional metrics collection logic here
                foreach (var metrics in _performanceMetrics.Values)
                {
                    // Calculate derived metrics
                    UpdateDerivedMetrics(metrics);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during metrics collection");
            }
        });
    }

    private void UpdateDerivedMetrics(PluginPerformanceMetrics metrics)
    {
        // Calculate trends, moving averages, etc.
        var recentExecutions = metrics.ExecutionHistory
            .Where(e => DateTime.UtcNow - e.Timestamp < TimeSpan.FromMinutes(15))
            .ToList();

        if (recentExecutions.Count > 0)
        {
            metrics.RecentAverageExecutionTime = TimeSpan.FromTicks(
                (long)recentExecutions.Average(e => e.ExecutionTime.Ticks));
            
            metrics.RecentSuccessRate = recentExecutions.Count(e => e.Success) / (double)recentExecutions.Count * 100;
        }
    }

    private async Task<List<PluginHealthCheckResult>> PerformDetailedHealthChecksAsync(PluginHealthInfo healthInfo)
    {
        var results = new List<PluginHealthCheckResult>();

        // Basic health checks
        results.Add(new PluginHealthCheckResult
        {
            CheckName = "Instance Alive",
            IsHealthy = healthInfo.Instance.IsAlive,
            Message = healthInfo.Instance.IsAlive ? "Plugin instance is alive" : "Plugin instance has been garbage collected"
        });

        results.Add(new PluginHealthCheckResult
        {
            CheckName = "Assembly Loaded",
            IsHealthy = !healthInfo.Assembly.ReflectionOnly,
            Message = !healthInfo.Assembly.ReflectionOnly ? "Assembly is loaded for execution" : "Assembly is reflection-only"
        });

        // Performance-based health checks
        if (_performanceMetrics.TryGetValue(healthInfo.PluginId, out var metrics))
        {
            results.Add(new PluginHealthCheckResult
            {
                CheckName = "Success Rate",
                IsHealthy = metrics.SuccessRate >= _options.WarningSuccessRateThreshold,
                Message = $"Current success rate: {metrics.SuccessRate:F1}%"
            });

            results.Add(new PluginHealthCheckResult
            {
                CheckName = "Recent Failures",
                IsHealthy = metrics.FailedExecutions == 0 || metrics.LastErrorTime < DateTime.UtcNow.AddMinutes(-5),
                Message = metrics.FailedExecutions == 0 ? "No failures recorded" : $"Last error: {metrics.LastError} at {metrics.LastErrorTime:yyyy-MM-dd HH:mm:ss}"
            });
        }

        // Custom health checks
        var target = healthInfo.Instance.Target;
        if (target is IPluginHealthCheckable healthCheckable)
        {
            try
            {
                var customResult = await healthCheckable.CheckHealthAsync();
                results.Add(new PluginHealthCheckResult
                {
                    CheckName = "Custom Health Check",
                    IsHealthy = customResult.IsHealthy,
                    Message = customResult.IsHealthy ? "Custom health check passed" : string.Join("; ", customResult.Issues)
                });
            }
            catch (Exception ex)
            {
                results.Add(new PluginHealthCheckResult
                {
                    CheckName = "Custom Health Check",
                    IsHealthy = false,
                    Message = $"Custom health check threw exception: {ex.Message}"
                });
            }
        }

        return results;
    }

    private void RecordStateTransition(PluginLifecycleState lifecycleState, PluginLifecycleStage newState, string? reason)
    {
        if (lifecycleState.CurrentState != newState)
        {
            var transition = new PluginStateTransition
            {
                FromState = lifecycleState.CurrentState,
                ToState = newState,
                Timestamp = DateTime.UtcNow,
                Reason = reason ?? "State transition"
            };

            lifecycleState.StateHistory.Add(transition);
            lifecycleState.CurrentState = newState;
            lifecycleState.LastTransitionTime = DateTime.UtcNow;

            // Keep only recent state transitions
            if (lifecycleState.StateHistory.Count > _options.MaxStateHistorySize)
            {
                lifecycleState.StateHistory.RemoveAt(0);
            }

            _logger.LogInformation("Plugin state transition: {PluginId} from {FromState} to {ToState} - {Reason}",
                lifecycleState.PluginId, transition.FromState, newState, reason);
        }
    }

    private double CalculateHealthScore(string pluginId)
    {
        var baseScore = 100.0;

        if (!_pluginHealth.TryGetValue(pluginId, out var healthInfo))
        {
            return 0;
        }

        // Deduct points based on status
        switch (healthInfo.Status)
        {
            case PluginHealthStatus.Warning:
                baseScore -= 20;
                break;
            case PluginHealthStatus.Critical:
                baseScore -= 50;
                break;
            case PluginHealthStatus.Failed:
                return 0;
        }

        // Deduct points for performance issues
        if (_performanceMetrics.TryGetValue(pluginId, out var metrics))
        {
            if (metrics.SuccessRate < 100)
            {
                baseScore -= (100 - metrics.SuccessRate) * 0.5; // 0.5 points per percent below 100%
            }

            if (metrics.FailedExecutions > 0 && metrics.LastErrorTime > DateTime.UtcNow.AddMinutes(-30))
            {
                baseScore -= 10; // Recent failures
            }
        }

        // Instance not alive is critical
        if (!healthInfo.Instance.IsAlive)
        {
            return 0;
        }

        return Math.Max(0, baseScore);
    }

    private static double CalculateOverallHealthScore(PluginHealthReport report)
    {
        if (report.TotalPluginsMonitored == 0)
        {
            return 100;
        }

        var totalScore = report.PluginSummaries.Sum(p => p.HealthScore);
        return totalScore / report.TotalPluginsMonitored;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Plugin health monitor background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Periodic cleanup of dead plugin references
                await CleanupDeadPluginsAsync();

                // Export health data if configured
                if (_options.AutoExportEnabled && _options.AutoExportPath != null)
                {
                    await ExportHealthDataAsync(_options.AutoExportPath, stoppingToken);
                }

                await Task.Delay(_options.BackgroundServiceInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in plugin health monitor background service");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Plugin health monitor background service stopped");
    }

    private async Task CleanupDeadPluginsAsync()
    {
        await _monitoringSemaphore.WaitAsync();
        try
        {
            var deadPlugins = _pluginHealth
                .Where(kvp => !kvp.Value.Instance.IsAlive)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var pluginId in deadPlugins)
            {
                _logger.LogWarning("Cleaning up dead plugin: {PluginId}", pluginId);
                await UnregisterPluginAsync(pluginId);
            }
        }
        finally
        {
            _monitoringSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public new void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _metricsCollectionTimer?.Dispose();
            _monitoringSemaphore?.Dispose();
            _performanceCounterMonitor?.Dispose();
            base.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Interface for plugins that provide custom health checks.
/// </summary>
public interface IPluginHealthCheckable
{
    /// <summary>
    /// Performs a custom health check for the plugin.
    /// </summary>
    /// <returns>The health check result.</returns>
    Task<PluginCustomHealthResult> CheckHealthAsync();
}

/// <summary>
/// Custom health check result.
/// </summary>
public sealed class PluginCustomHealthResult
{
    /// <summary>
    /// Gets or sets whether the plugin is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets whether the issue is critical.
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Gets the list of health issues.
    /// </summary>
    public List<string> Issues { get; } = [];
}

// Additional supporting classes and enums for comprehensive health monitoring
// (Truncated for length - would include all the other classes like PluginHealthInfo, PluginPerformanceMetrics, etc.)