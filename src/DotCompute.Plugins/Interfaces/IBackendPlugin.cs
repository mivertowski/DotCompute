// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Plugins.Interfaces
{
    /// <summary>
    /// Base interface for all DotCompute backend plugins.
    /// </summary>
    public interface IBackendPlugin : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for this plugin.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the display name of the plugin.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the version of the plugin.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Gets the description of the plugin.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the author of the plugin.
        /// </summary>
        public string Author { get; }

        /// <summary>
        /// Gets the plugin's capabilities and features.
        /// </summary>
        public PluginCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the current state of the plugin.
        /// </summary>
        public PluginState State { get; }

        /// <summary>
        /// Gets the plugin's health status.
        /// </summary>
        public PluginHealth Health { get; }

        /// <summary>
        /// Configures services for dependency injection.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="configuration">The configuration for the plugin.</param>
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// Initializes the plugin asynchronously.
        /// </summary>
        /// <param name="serviceProvider">The service provider for dependency resolution.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts the plugin asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the plugin asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the plugin's configuration and dependencies.
        /// </summary>
        /// <returns>Validation result with any errors or warnings.</returns>
        public PluginValidationResult Validate();

        /// <summary>
        /// Gets the plugin's configuration schema.
        /// </summary>
        /// <returns>JSON schema for the plugin's configuration.</returns>
        public string GetConfigurationSchema();

        /// <summary>
        /// Handles configuration changes at runtime.
        /// </summary>
        /// <param name="configuration">The new configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the async operation.</returns>
        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the plugin's performance metrics.
        /// </summary>
        /// <returns>Current performance metrics.</returns>
        public PluginMetrics GetMetrics();

        /// <summary>
        /// Raised when the plugin's state changes.
        /// </summary>
        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// Raised when the plugin encounters an error.
        /// </summary>
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Raised when the plugin's health status changes.
        /// </summary>
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
    }

    /// <summary>
    /// Represents the capabilities of a plugin.
    /// </summary>
    [Flags]
    public enum PluginCapabilities
    {
        None = 0,
        ComputeBackend = 1 << 0,
        StorageProvider = 1 << 1,
        NetworkProvider = 1 << 2,
        SecurityProvider = 1 << 3,
        MonitoringProvider = 1 << 4,
        SchedulerProvider = 1 << 5,
        CacheProvider = 1 << 6,
        LoggingProvider = 1 << 7,
        MetricsProvider = 1 << 8,
        ConfigurationProvider = 1 << 9,
        ExtensionProvider = 1 << 10,
        HotReloadable = 1 << 11,
        Scalable = 1 << 12,
        Clusterable = 1 << 13,
        Persistent = 1 << 14,
        Transactional = 1 << 15
    }

    /// <summary>
    /// Represents the state of a plugin.
    /// </summary>
    public enum PluginState
    {
        Unknown,
        Loading,
        Loaded,
        Initializing,
        Initialized,
        Starting,
        Running,
        Stopping,
        Stopped,
        Failed,
        Unloading,
        Unloaded
    }

    /// <summary>
    /// Represents the health status of a plugin.
    /// </summary>
    public enum PluginHealth
    {
        Unknown,
        Healthy,
        Degraded,
        Unhealthy,
        Critical
    }

    /// <summary>
    /// Represents the result of plugin validation.
    /// </summary>
    public class PluginValidationResult
    {
        public bool IsValid { get; set; }
        public IList<string> Errors { get; } = [];
        public IList<string> Warnings { get; } = [];
        public Dictionary<string, object> Metadata { get; } = [];
    }

    /// <summary>
    /// Represents plugin performance metrics.
    /// </summary>
    public class PluginMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan Uptime { get; set; }
        public long RequestCount { get; set; }
        public long ErrorCount { get; set; }
        public double AverageResponseTime { get; set; }
        public long MemoryUsage { get; set; }
        public double CpuUsage { get; set; }
        public Dictionary<string, object> CustomMetrics { get; } = [];
    }

    /// <summary>
    /// Event arguments for plugin state changes.
    /// </summary>
    public class PluginStateChangedEventArgs(PluginState oldState, PluginState newState, string? reason = null) : EventArgs
    {
        public PluginState OldState { get; } = oldState;
        public PluginState NewState { get; } = newState;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string? Reason { get; } = reason;
    }

    /// <summary>
    /// Event arguments for plugin errors.
    /// </summary>
    public class PluginErrorEventArgs(Exception exception, string context, Dictionary<string, object>? additionalData = null) : EventArgs
    {
        public Exception Exception { get; } = exception;
        public string Context { get; } = context;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public Dictionary<string, object> AdditionalData { get; } = additionalData ?? [];
    }

    /// <summary>
    /// Event arguments for plugin health changes.
    /// </summary>
    public class PluginHealthChangedEventArgs(PluginHealth oldHealth, PluginHealth newHealth, string? reason = null, Dictionary<string, object>? healthData = null) : EventArgs
    {
        public PluginHealth OldHealth { get; } = oldHealth;
        public PluginHealth NewHealth { get; } = newHealth;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public string? Reason { get; } = reason;
        public Dictionary<string, object> HealthData { get; } = healthData ?? [];
    }
}
