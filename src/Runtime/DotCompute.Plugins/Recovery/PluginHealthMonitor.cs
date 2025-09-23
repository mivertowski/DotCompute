// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;
using System.Collections.Concurrent;
using CorePluginHealthStatus = DotCompute.Core.Recovery.PluginHealthStatus;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Monitors plugin health and generates health reports
/// </summary>
public sealed class PluginHealthMonitor : IDisposable
{
    private readonly PluginRecoveryConfiguration _config;
    private readonly ILogger _logger;
    private readonly Timer _healthCheckTimer;
    private volatile bool _disposed;

    public PluginHealthMonitor(PluginRecoveryConfiguration config, ILogger logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Start health monitoring
        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            _config.HealthCheckInterval, _config.HealthCheckInterval);

        _logger.LogDebugMessage("Plugin Health Monitor initialized");
    }

    /// <summary>
    /// Generates comprehensive health report for all plugins
    /// </summary>
    public PluginHealthReport GenerateHealthReport(IEnumerable<PluginHealthState> pluginStates)
    {
        var pluginHealth = new Dictionary<string, PluginHealthInfo>();

        foreach (var state in pluginStates)
        {
            pluginHealth[state.PluginId] = new PluginHealthInfo
            {
                PluginId = state.PluginId,
                IsHealthy = state.IsHealthy,
                IsIsolated = state.IsIsolated,
                ErrorCount = state.ErrorCount,
                RestartCount = state.RestartCount,
                LastError = state.LastError,
                LastRestart = state.LastRestart,
                LastHealthCheck = state.LastHealthCheck,
                ConsecutiveFailures = state.ConsecutiveFailures,
                UptimePercent = state.CalculateUptimePercent()
            };
        }

        return new PluginHealthReport
        {
            PluginId = "All Plugins",
            Status = pluginHealth.Values.All(p => p.IsHealthy) ? CorePluginHealthStatus.Healthy : CorePluginHealthStatus.Warning,
            Timestamp = DateTimeOffset.UtcNow,
            MemoryUsageBytes = pluginHealth.Values.Sum(p => p.MemoryUsageBytes),
            CpuUsagePercent = pluginHealth.Values.Where(p => p.CpuUsagePercent > 0).DefaultIfEmpty(new PluginHealthInfo()).Average(p => p.CpuUsagePercent),
            ActiveOperations = pluginHealth.Values.Sum(p => p.ActiveOperations),
            OverallHealth = CalculateOverallHealth(pluginHealth),
            Metrics = new Dictionary<string, object>
            {
                ["PluginHealth"] = pluginHealth,
                ["TotalPlugins"] = pluginStates.Count(),
                ["HealthyPlugins"] = pluginHealth.Values.Count(p => p.IsHealthy),
                ["IsolatedPlugins"] = pluginHealth.Values.Count(p => p.IsIsolated)
            }
        };
    }

    /// <summary>
    /// Checks health of specific plugin state
    /// </summary>
    public PluginHealthInfo CheckPluginHealth(PluginHealthState state)
    {
        return new PluginHealthInfo
        {
            PluginId = state.PluginId,
            IsHealthy = state.IsHealthy,
            IsIsolated = state.IsIsolated,
            ErrorCount = state.ErrorCount,
            RestartCount = state.RestartCount,
            LastError = state.LastError,
            LastRestart = state.LastRestart,
            LastHealthCheck = DateTimeOffset.UtcNow,
            ConsecutiveFailures = state.ConsecutiveFailures,
            UptimePercent = state.CalculateUptimePercent()
        };
    }

    /// <summary>
    /// Performs automated health check on plugin states
    /// </summary>
    public async Task PerformHealthCheckAsync(IEnumerable<PluginHealthState> pluginStates)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var unhealthyPlugins = new List<string>();

            foreach (var healthState in pluginStates)
            {
                healthState.UpdateHealthCheck();

                if (!healthState.IsHealthy)
                {
                    unhealthyPlugins.Add(healthState.PluginId);
                    _logger.LogWarningMessage($"Plugin {healthState.PluginId} health degraded");
                }
            }

            if (unhealthyPlugins.Count > 0)
            {
                _logger.LogWarningMessage($"Health check found {unhealthyPlugins.Count} unhealthy plugins: {string.Join(", ", unhealthyPlugins)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin health check");
        }
    }

    private static double CalculateOverallHealth(Dictionary<string, PluginHealthInfo> pluginHealth)
    {
        if (pluginHealth.Count == 0)
        {
            return 1.0;
        }

        var healthyCount = pluginHealth.Values.Count(p => p.IsHealthy);
        return (double)healthyCount / pluginHealth.Count;
    }

    private static void PerformHealthCheck(object? state)
    {
        // This would be called from orchestrator with actual plugin states
        // Implementation moved to orchestrator to avoid circular dependencies
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _disposed = true;
            _logger.LogDebugMessage("Plugin Health Monitor disposed");
        }
    }
}