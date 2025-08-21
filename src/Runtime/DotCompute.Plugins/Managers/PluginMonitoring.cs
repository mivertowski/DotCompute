// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Managers;

/// <summary>
/// Health monitor for plugins with comprehensive diagnostics.
/// </summary>
internal class PluginHealthMonitor : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, PluginHealthState> _healthStates = new();
    private readonly Timer _healthCheckTimer;
    private bool _disposed;

    public PluginHealthMonitor(ILogger logger)
    {
        _logger = logger;
        _healthCheckTimer = new Timer(PeriodicHealthCheck, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts health monitoring.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        _ = _healthCheckTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        _logger.LogInformation("Plugin health monitoring started");
    }

    /// <summary>
    /// Starts monitoring a specific plugin.
    /// </summary>
    public void StartMonitoring(string pluginId, ManagedPlugin managedPlugin)
    {
        var healthState = new PluginHealthState
        {
            PluginId = pluginId,
            Plugin = managedPlugin,
            LastCheck = DateTimeOffset.UtcNow,
            Health = PluginHealth.Healthy,
            CheckCount = 0,
            ConsecutiveFailures = 0
        };

        _ = _healthStates.TryAdd(pluginId, healthState);
        _logger.LogDebug("Started monitoring plugin: {PluginId}", pluginId);
    }

    /// <summary>
    /// Stops monitoring a specific plugin.
    /// </summary>
    public void StopMonitoring(string pluginId)
    {
        if (_healthStates.TryRemove(pluginId, out _))
        {
            _logger.LogDebug("Stopped monitoring plugin: {PluginId}", pluginId);
        }
    }

    /// <summary>
    /// Checks the health of a specific plugin.
    /// </summary>
    public async Task<PluginHealthCheckResult> CheckPluginHealthAsync(string pluginId, ManagedPlugin managedPlugin)
    {
        var result = new PluginHealthCheckResult
        {
            PluginId = pluginId,
            CheckTime = DateTimeOffset.UtcNow,
            Health = PluginHealth.Unknown
        };

        try
        {
            if (managedPlugin.Plugin == null)
            {
                result.Health = PluginHealth.Critical;
                result.Issues.Add("Plugin instance is null");
                return result;
            }

            // Check plugin state
            var state = managedPlugin.Plugin.State;
            switch (state)
            {
                case PluginState.Running:
                    result.Health = PluginHealth.Healthy;
                    break;
                case PluginState.Failed:
                    result.Health = PluginHealth.Critical;
                    result.Issues.Add("Plugin is in failed state");
                    break;
                case PluginState.Loading:
                case PluginState.Starting:
                case PluginState.Stopping:
                    result.Health = PluginHealth.Degraded;
                    result.Issues.Add($"Plugin is in transitional state: {state}");
                    break;
                default:
                    result.Health = PluginHealth.Degraded;
                    result.Issues.Add($"Plugin is not running: {state}");
                    break;
            }

            // Check memory usage
            await CheckMemoryUsageAsync(managedPlugin, result);

            // Check response time
            await CheckResponseTimeAsync(managedPlugin, result);

            // Check error rates
            CheckErrorRates(managedPlugin, result);

            // Update health state
            if (_healthStates.TryGetValue(pluginId, out var healthState))
            {
                healthState.LastCheck = result.CheckTime;
                healthState.Health = result.Health;
                healthState.CheckCount++;

                if (result.Health is PluginHealth.Critical or PluginHealth.Unhealthy)
                {
                    healthState.ConsecutiveFailures++;
                }
                else
                {
                    healthState.ConsecutiveFailures = 0;
                }

                result.ConsecutiveFailures = healthState.ConsecutiveFailures;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for plugin: {PluginId}", pluginId);
            result.Health = PluginHealth.Critical;
            result.Issues.Add($"Health check exception: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Generates a comprehensive health report for all plugins.
    /// </summary>
    public async Task<PluginHealthReport> GenerateHealthReportAsync(IEnumerable<ManagedPlugin> plugins, CancellationToken cancellationToken = default)
    {
        var report = new PluginHealthReport
        {
            ReportTime = DateTimeOffset.UtcNow,
            TotalPlugins = plugins.Count()
        };

        var healthCheckTasks = plugins.Select(async plugin =>
        {
            var pluginId = plugin.LoadedPluginInfo?.Manifest.Id ?? "Unknown";
            var healthCheck = await CheckPluginHealthAsync(pluginId, plugin);
            return healthCheck;
        });

        var healthChecks = await Task.WhenAll(healthCheckTasks);

        foreach (var healthCheck in healthChecks)
        {
            report.PluginHealthChecks.Add(healthCheck);

            switch (healthCheck.Health)
            {
                case PluginHealth.Healthy:
                    report.HealthyPlugins++;
                    break;
                case PluginHealth.Degraded:
                    report.DegradedPlugins++;
                    break;
                case PluginHealth.Unhealthy:
                    report.UnhealthyPlugins++;
                    break;
                case PluginHealth.Critical:
                    report.CriticalPlugins++;
                    break;
            }
        }

        // Determine overall system health
        if (report.CriticalPlugins > 0)
        {
            report.OverallHealth = PluginHealth.Critical;
        }
        else if (report.UnhealthyPlugins > 0)
        {
            report.OverallHealth = PluginHealth.Unhealthy;
        }
        else if (report.DegradedPlugins > 0)
        {
            report.OverallHealth = PluginHealth.Degraded;
        }
        else
        {
            report.OverallHealth = PluginHealth.Healthy;
        }

        return report;
    }

    private async Task CheckMemoryUsageAsync(ManagedPlugin managedPlugin, PluginHealthCheckResult result)
    {
        try
        {
            var metrics = managedPlugin.Plugin?.GetMetrics();
            if (metrics != null)
            {
                result.MemoryUsage = metrics.MemoryUsage;

                // Check if memory usage is excessive (> 500MB)
                if (metrics.MemoryUsage > 500 * 1024 * 1024)
                {
                    result.Issues.Add($"High memory usage: {metrics.MemoryUsage / (1024 * 1024)}MB");
                    if (result.Health == PluginHealth.Healthy)
                    {
                        result.Health = PluginHealth.Degraded;
                    }
                }

                // Check if memory usage is critical (> 1GB)
                if (metrics.MemoryUsage > 1024 * 1024 * 1024)
                {
                    result.Health = PluginHealth.Critical;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check memory usage for plugin");
            result.Issues.Add("Unable to check memory usage");
        }

        await Task.CompletedTask;
    }

    private async Task CheckResponseTimeAsync(ManagedPlugin managedPlugin, PluginHealthCheckResult result)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Simple responsiveness test - call GetMetrics()

            var metrics = managedPlugin.Plugin?.GetMetrics();


            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;

            // Check if response time is slow (> 5 seconds)
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(5))
            {
                result.Issues.Add($"Slow response time: {stopwatch.ElapsedMilliseconds}ms");
                if (result.Health == PluginHealth.Healthy)
                {
                    result.Health = PluginHealth.Degraded;
                }
            }

            // Check if response time is critical (> 30 seconds)
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(30))
            {
                result.Health = PluginHealth.Unhealthy;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check response time for plugin");
            result.Issues.Add($"Response time check failed: {ex.Message}");
            result.Health = PluginHealth.Unhealthy;
        }

        await Task.CompletedTask;
    }

    private void CheckErrorRates(ManagedPlugin managedPlugin, PluginHealthCheckResult result)
    {
        try
        {
            var metrics = managedPlugin.Plugin?.GetMetrics();
            if (metrics != null)
            {
                result.ErrorCount = metrics.ErrorCount;
                result.RequestCount = metrics.RequestCount;

                if (metrics.RequestCount > 0)
                {
                    var errorRate = (double)metrics.ErrorCount / metrics.RequestCount;
                    result.ErrorRate = errorRate;

                    // Check if error rate is high (> 10%)
                    if (errorRate > 0.1)
                    {
                        result.Issues.Add($"High error rate: {errorRate:P2}");
                        if (result.Health == PluginHealth.Healthy)
                        {
                            result.Health = PluginHealth.Degraded;
                        }
                    }

                    // Check if error rate is critical (> 50%)
                    if (errorRate > 0.5)
                    {
                        result.Health = PluginHealth.Critical;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check error rates for plugin");
            result.Issues.Add("Unable to check error rates");
        }
    }

    private async void PeriodicHealthCheck(object? state)
    {
        try
        {
            foreach (var (pluginId, healthState) in _healthStates)
            {
                if (healthState.Plugin != null)
                {
                    _ = await CheckPluginHealthAsync(pluginId, healthState.Plugin);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic health check");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _healthStates.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Metrics collector for plugins with performance tracking.
/// </summary>
internal class PluginMetricsCollector : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, PluginMetricsState> _metricsStates = new();
    private readonly Timer _metricsTimer;
    private bool _disposed;

    public PluginMetricsCollector(ILogger logger)
    {
        _logger = logger;
        _metricsTimer = new Timer(CollectMetrics, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts metrics collection.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        _ = _metricsTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        _logger.LogInformation("Plugin metrics collection started");
    }

    /// <summary>
    /// Adds a plugin to metrics collection.
    /// </summary>
    public void AddPlugin(string pluginId, ManagedPlugin managedPlugin)
    {
        var metricsState = new PluginMetricsState
        {
            PluginId = pluginId,
            Plugin = managedPlugin,
            LastCollection = DateTimeOffset.UtcNow,
            MetricsHistory = []
        };

        _ = _metricsStates.TryAdd(pluginId, metricsState);
        _logger.LogDebug("Added plugin to metrics collection: {PluginId}", pluginId);
    }

    /// <summary>
    /// Removes a plugin from metrics collection.
    /// </summary>
    public void RemovePlugin(string pluginId)
    {
        if (_metricsStates.TryRemove(pluginId, out _))
        {
            _logger.LogDebug("Removed plugin from metrics collection: {PluginId}", pluginId);
        }
    }

    /// <summary>
    /// Generates a comprehensive metrics report for all plugins.
    /// </summary>
    public async Task<PluginMetricsReport> GenerateMetricsReportAsync(IEnumerable<ManagedPlugin> plugins, CancellationToken cancellationToken = default)
    {
        var report = new PluginMetricsReport
        {
            ReportTime = DateTimeOffset.UtcNow,
            TotalPlugins = plugins.Count()
        };

        foreach (var plugin in plugins)
        {
            var pluginId = plugin.LoadedPluginInfo?.Manifest.Id ?? "Unknown";


            if (_metricsStates.TryGetValue(pluginId, out var metricsState))
            {
                var metrics = await CollectPluginMetricsAsync(pluginId, plugin);
                if (metrics != null)
                {
                    report.PluginMetrics.Add(metrics);

                    // Aggregate system-wide metrics

                    report.TotalRequests += metrics.RequestCount;
                    report.TotalErrors += metrics.ErrorCount;
                    report.TotalMemoryUsage += metrics.MemoryUsage;


                    if (metrics.AverageResponseTime > 0)
                    {
                        report.AverageResponseTime = (report.AverageResponseTime * (report.PluginMetrics.Count - 1) + metrics.AverageResponseTime) / report.PluginMetrics.Count;
                    }
                }
            }
        }

        // Calculate system-wide error rate
        if (report.TotalRequests > 0)
        {
            report.SystemErrorRate = (double)report.TotalErrors / report.TotalRequests;
        }

        return report;
    }

    /// <summary>
    /// Cleans up old metrics data.
    /// </summary>
    public void CleanupOldMetrics()
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(24));

        foreach (var (pluginId, metricsState) in _metricsStates)
        {
            var oldMetrics = metricsState.MetricsHistory
                .Where(m => m.Timestamp < cutoffTime)
                .ToList();

            foreach (var oldMetric in oldMetrics)
            {
                _ = metricsState.MetricsHistory.Remove(oldMetric);
            }

            _logger.LogDebug("Cleaned up {Count} old metrics for plugin: {PluginId}", oldMetrics.Count, pluginId);
        }
    }

    private Task<PluginMetrics?> CollectPluginMetricsAsync(string pluginId, ManagedPlugin managedPlugin)
    {
        try
        {
            var metrics = managedPlugin.Plugin?.GetMetrics();
            if (metrics != null)
            {
                // Store in history
                if (_metricsStates.TryGetValue(pluginId, out var metricsState))
                {
                    metricsState.MetricsHistory.Add(metrics);
                    metricsState.LastCollection = DateTimeOffset.UtcNow;

                    // Keep only the last 1000 entries

                    if (metricsState.MetricsHistory.Count > 1000)
                    {
                        metricsState.MetricsHistory.RemoveRange(0, metricsState.MetricsHistory.Count - 1000);
                    }
                }

                return Task.FromResult(metrics)!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect metrics for plugin: {PluginId}", pluginId);
        }

        return Task.FromResult<PluginMetrics?>(null);
    }

    private async void CollectMetrics(object? state)
    {
        try
        {
            foreach (var (pluginId, metricsState) in _metricsStates)
            {
                if (metricsState.Plugin != null)
                {
                    _ = await CollectPluginMetricsAsync(pluginId, metricsState.Plugin);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic metrics collection");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsTimer?.Dispose();
            _metricsStates.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Health state for a monitored plugin.
/// </summary>
internal class PluginHealthState
{
    public required string PluginId { get; set; }
    public ManagedPlugin? Plugin { get; set; }
    public DateTimeOffset LastCheck { get; set; }
    public PluginHealth Health { get; set; }
    public long CheckCount { get; set; }
    public int ConsecutiveFailures { get; set; }
}

/// <summary>
/// Metrics state for a monitored plugin.
/// </summary>
internal class PluginMetricsState
{
    public required string PluginId { get; set; }
    public ManagedPlugin? Plugin { get; set; }
    public DateTimeOffset LastCollection { get; set; }
    public List<PluginMetrics> MetricsHistory { get; set; } = [];
}

/// <summary>
/// Result of a plugin health check.
/// </summary>
public class PluginHealthCheckResult
{
    /// <summary>
    /// Gets or sets the plugin ID.
    /// </summary>
    public string PluginId { get; set; } = "";

    /// <summary>
    /// Gets or sets the check time.
    /// </summary>
    public DateTimeOffset CheckTime { get; set; }

    /// <summary>
    /// Gets or sets the health status.
    /// </summary>
    public PluginHealth Health { get; set; }

    /// <summary>
    /// Gets the health issues found.
    /// </summary>
    public List<string> Issues { get; } = [];

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the response time.
    /// </summary>
    public TimeSpan ResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the request count.
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the error rate.
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Gets or sets the consecutive failures count.
    /// </summary>
    public int ConsecutiveFailures { get; set; }
}

/// <summary>
/// Comprehensive health report for all plugins.
/// </summary>
public class PluginHealthReport
{
    /// <summary>
    /// Gets or sets the report time.
    /// </summary>
    public DateTimeOffset ReportTime { get; set; }

    /// <summary>
    /// Gets or sets the total number of plugins.
    /// </summary>
    public int TotalPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of healthy plugins.
    /// </summary>
    public int HealthyPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of degraded plugins.
    /// </summary>
    public int DegradedPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of unhealthy plugins.
    /// </summary>
    public int UnhealthyPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of critical plugins.
    /// </summary>
    public int CriticalPlugins { get; set; }

    /// <summary>
    /// Gets or sets the overall system health.
    /// </summary>
    public PluginHealth OverallHealth { get; set; }

    /// <summary>
    /// Gets the individual plugin health checks.
    /// </summary>
    public List<PluginHealthCheckResult> PluginHealthChecks { get; } = [];
}

/// <summary>
/// Comprehensive metrics report for all plugins.
/// </summary>
public class PluginMetricsReport
{
    /// <summary>
    /// Gets or sets the report time.
    /// </summary>
    public DateTimeOffset ReportTime { get; set; }

    /// <summary>
    /// Gets or sets the total number of plugins.
    /// </summary>
    public int TotalPlugins { get; set; }

    /// <summary>
    /// Gets or sets the total requests across all plugins.
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the total errors across all plugins.
    /// </summary>
    public long TotalErrors { get; set; }

    /// <summary>
    /// Gets or sets the total memory usage across all plugins.
    /// </summary>
    public long TotalMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the average response time across all plugins.
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the system-wide error rate.
    /// </summary>
    public double SystemErrorRate { get; set; }

    /// <summary>
    /// Gets the individual plugin metrics.
    /// </summary>
    public List<PluginMetrics> PluginMetrics { get; } = [];
}
