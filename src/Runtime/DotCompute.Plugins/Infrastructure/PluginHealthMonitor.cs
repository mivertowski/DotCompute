// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Plugins.Logging;
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
        /// <summary>
        /// Initializes a new instance of the PluginHealthMonitor class.
        /// </summary>
        /// <param name="logger">The logger.</param>

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
            {
                throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
            }

            _pluginRegistrations[pluginId] = DateTime.UtcNow;
            _ = _pluginErrors.TryRemove(pluginId, out _);

            _logger.LogInfoMessage($"Registered plugin {pluginId} ({pluginName} v{version}) for health monitoring");
        }

        /// <summary>
        /// Unregister a plugin from health monitoring
        /// </summary>
        public void UnregisterPlugin(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
            }

            _ = _pluginRegistrations.TryRemove(pluginId, out _);
            _ = _pluginErrors.TryRemove(pluginId, out _);

            _logger.LogInfoMessage("Unregistered plugin {pluginId} from health monitoring");
        }

        /// <summary>
        /// Record an error for a specific plugin
        /// </summary>
        public void RecordPluginError(string pluginId, Exception exception)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));
            }

            _pluginErrors[pluginId] = exception;
            _logger.LogErrorMessage(exception, $"Recorded error for plugin {pluginId}");
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
            {
                return null;
            }

            _ = _pluginErrors.TryGetValue(pluginId, out var error);
            return error;
        }

        /// <summary>
        /// Clear error status for a plugin
        /// </summary>
        public void ClearPluginError(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                return;
            }

            _ = _pluginErrors.TryRemove(pluginId, out _);
            _logger.LogInfoMessage("Cleared error status for plugin {pluginId}");
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

                _logger.LogDebugMessage($"Health check completed: {healthyCount} healthy, {unhealthyCount} unhealthy plugins");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during plugin health check");
            }
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _healthCheckTimer?.Dispose();
            _pluginRegistrations.Clear();
            _pluginErrors.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
