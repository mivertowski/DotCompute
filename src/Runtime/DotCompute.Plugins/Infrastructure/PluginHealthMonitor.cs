// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Infrastructure
{

/// <summary>
/// Simplified Plugin Health Monitor - monitors basic plugin health status
/// </summary>
public class PluginHealthMonitor : IDisposable
{
    private readonly ILogger<PluginHealthMonitor> _logger;
    private readonly ConcurrentDictionary<string, DateTime> _pluginRegistrations;
    private readonly ConcurrentDictionary<string, Exception?> _pluginErrors;
    private readonly Timer? _healthCheckTimer;
    private bool _disposed;

    public PluginHealthMonitor(ILogger<PluginHealthMonitor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginRegistrations = new ConcurrentDictionary<string, DateTime>();
        _pluginErrors = new ConcurrentDictionary<string, Exception?>();
        
        // Set up periodic health checks every 5 minutes
        _healthCheckTimer = new Timer(PerformHealthCheck, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Register a plugin for health monitoring
    /// </summary>
    public void RegisterPlugin(string pluginId, string pluginName, string version)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

        _pluginRegistrations[pluginId] = DateTime.UtcNow;
        _pluginErrors.TryRemove(pluginId, out _);
        
        _logger.LogInformation("Registered plugin {PluginId} ({PluginName} v{Version}) for health monitoring",
            pluginId, pluginName, version);
    }

    /// <summary>
    /// Unregister a plugin from health monitoring
    /// </summary>
    public void UnregisterPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

        _pluginRegistrations.TryRemove(pluginId, out _);
        _pluginErrors.TryRemove(pluginId, out _);
        
        _logger.LogInformation("Unregistered plugin {PluginId} from health monitoring", pluginId);
    }

    /// <summary>
    /// Record an error for a specific plugin
    /// </summary>
    public void RecordPluginError(string pluginId, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

        _pluginErrors[pluginId] = exception;
        _logger.LogError(exception, "Recorded error for plugin {PluginId}", pluginId);
    }

    /// <summary>
    /// Get basic health status for all plugins
    /// </summary>
    public Dictionary<string, bool> GetPluginHealthStatus()
    {
        var status = new Dictionary<string, bool>();
        
        foreach (var registration in _pluginRegistrations)
        {
            var pluginId = registration.Key;
            var hasError = _pluginErrors.ContainsKey(pluginId);
            status[pluginId] = !hasError;
        }
        
        return status;
    }

    /// <summary>
    /// Get the error for a specific plugin, if any
    /// </summary>
    public Exception? GetPluginError(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return null;
            
        _pluginErrors.TryGetValue(pluginId, out var error);
        return error;
    }

    /// <summary>
    /// Clear error status for a plugin
    /// </summary>
    public void ClearPluginError(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            return;
            
        _pluginErrors.TryRemove(pluginId, out _);
        _logger.LogInformation("Cleared error status for plugin {PluginId}", pluginId);
    }

    private void PerformHealthCheck(object? state)
    {
        try
        {
            var healthyCount = 0;
            var unhealthyCount = 0;
            
            foreach (var registration in _pluginRegistrations)
            {
                if (_pluginErrors.ContainsKey(registration.Key))
                {
                    unhealthyCount++;
                }
                else
                {
                    healthyCount++;
                }
            }
            
            _logger.LogDebug("Health check completed: {HealthyCount} healthy, {UnhealthyCount} unhealthy plugins",
                healthyCount, unhealthyCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin health check");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _healthCheckTimer?.Dispose();
        _pluginRegistrations.Clear();
        _pluginErrors.Clear();
        _disposed = true;
    }
}}
