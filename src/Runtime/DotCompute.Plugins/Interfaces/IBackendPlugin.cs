// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Plugins.Interfaces;

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
    /// <summary>
    /// The none
    /// </summary>
    None = 0,

    /// <summary>
    /// The compute backend
    /// </summary>
    ComputeBackend = 1 << 0,

    /// <summary>
    /// The storage provider
    /// </summary>
    StorageProvider = 1 << 1,

    /// <summary>
    /// The network provider
    /// </summary>
    NetworkProvider = 1 << 2,

    /// <summary>
    /// The security provider
    /// </summary>
    SecurityProvider = 1 << 3,

    /// <summary>
    /// The monitoring provider
    /// </summary>
    MonitoringProvider = 1 << 4,

    /// <summary>
    /// The scheduler provider
    /// </summary>
    SchedulerProvider = 1 << 5,

    /// <summary>
    /// The cache provider
    /// </summary>
    CacheProvider = 1 << 6,

    /// <summary>
    /// The logging provider
    /// </summary>
    LoggingProvider = 1 << 7,

    /// <summary>
    /// The metrics provider
    /// </summary>
    MetricsProvider = 1 << 8,

    /// <summary>
    /// The configuration provider
    /// </summary>
    ConfigurationProvider = 1 << 9,

    /// <summary>
    /// The extension provider
    /// </summary>
    ExtensionProvider = 1 << 10,

    /// <summary>
    /// The hot reloadable
    /// </summary>
    HotReloadable = 1 << 11,

    /// <summary>
    /// The scalable
    /// </summary>
    Scalable = 1 << 12,

    /// <summary>
    /// The clusterable
    /// </summary>
    Clusterable = 1 << 13,

    /// <summary>
    /// The persistent
    /// </summary>
    Persistent = 1 << 14,

    /// <summary>
    /// The transactional
    /// </summary>
    Transactional = 1 << 15
}

/// <summary>
/// Represents the state of a plugin.
/// </summary>
public enum PluginState
{
    /// <summary>
    /// The unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// The loading
    /// </summary>
    Loading,

    /// <summary>
    /// The loaded
    /// </summary>
    Loaded,

    /// <summary>
    /// The initializing
    /// </summary>
    Initializing,

    /// <summary>
    /// The initialized
    /// </summary>
    Initialized,

    /// <summary>
    /// The starting
    /// </summary>
    Starting,

    /// <summary>
    /// The running
    /// </summary>
    Running,

    /// <summary>
    /// The stopping
    /// </summary>
    Stopping,

    /// <summary>
    /// The stopped
    /// </summary>
    Stopped,

    /// <summary>
    /// The failed
    /// </summary>
    Failed,

    /// <summary>
    /// The unloading
    /// </summary>
    Unloading,

    /// <summary>
    /// The unloaded
    /// </summary>
    Unloaded
}

/// <summary>
/// Represents the health status of a plugin.
/// </summary>
public enum PluginHealth
{
    /// <summary>
    /// The unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// The healthy
    /// </summary>
    Healthy,

    /// <summary>
    /// The degraded
    /// </summary>
    Degraded,

    /// <summary>
    /// The unhealthy
    /// </summary>
    Unhealthy,

    /// <summary>
    /// The critical
    /// </summary>
    Critical
}

/// <summary>
/// Represents the result of plugin validation.
/// </summary>
public class PluginValidationResult
{
    /// <summary>
    /// Returns true if ... is valid.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
    /// </value>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the errors.
    /// </summary>
    /// <value>
    /// The errors.
    /// </value>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets the warnings.
    /// </summary>
    /// <value>
    /// The warnings.
    /// </value>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets the metadata.
    /// </summary>
    /// <value>
    /// The metadata.
    /// </value>
    public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Represents plugin performance metrics.
/// </summary>
public class PluginMetrics
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>
    /// The timestamp.
    /// </value>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the uptime.
    /// </summary>
    /// <value>
    /// The uptime.
    /// </value>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the request count.
    /// </summary>
    /// <value>
    /// The request count.
    /// </value>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    /// <value>
    /// The error count.
    /// </value>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    /// <value>
    /// The average response time.
    /// </value>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the memory usage.
    /// </summary>
    /// <value>
    /// The memory usage.
    /// </value>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the cpu usage.
    /// </summary>
    /// <value>
    /// The cpu usage.
    /// </value>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Gets the custom metrics.
    /// </summary>
    /// <value>
    /// The custom metrics.
    /// </value>
    public Dictionary<string, object> CustomMetrics { get; } = [];
}

/// <summary>
/// Event arguments for plugin state changes.
/// </summary>
public class PluginStateChangedEventArgs(PluginState oldState, PluginState newState, string? reason = null) : EventArgs
{
    /// <summary>
    /// Gets the old state.
    /// </summary>
    /// <value>
    /// The old state.
    /// </value>
    public PluginState OldState { get; } = oldState;

    /// <summary>
    /// Creates new state.
    /// </summary>
    /// <value>
    /// The new state.
    /// </value>
    public PluginState NewState { get; } = newState;

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    /// <value>
    /// The timestamp.
    /// </value>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the reason.
    /// </summary>
    /// <value>
    /// The reason.
    /// </value>
    public string? Reason { get; } = reason;
}

/// <summary>
/// Event arguments for plugin errors.
/// </summary>
public class PluginErrorEventArgs(Exception exception, string context, Dictionary<string, object>? additionalData = null) : EventArgs
{
    /// <summary>
    /// Gets the exception.
    /// </summary>
    /// <value>
    /// The exception.
    /// </value>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the context.
    /// </summary>
    /// <value>
    /// The context.
    /// </value>
    public string Context { get; } = context;

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    /// <value>
    /// The timestamp.
    /// </value>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the additional data.
    /// </summary>
    /// <value>
    /// The additional data.
    /// </value>
    public Dictionary<string, object> AdditionalData { get; } = additionalData ?? [];
}

/// <summary>
/// Event arguments for plugin health changes.
/// </summary>
public class PluginHealthChangedEventArgs(PluginHealth oldHealth, PluginHealth newHealth, string? reason = null, Dictionary<string, object>? healthData = null) : EventArgs
{
    /// <summary>
    /// Gets the old health.
    /// </summary>
    /// <value>
    /// The old health.
    /// </value>
    public PluginHealth OldHealth { get; } = oldHealth;

    /// <summary>
    /// Creates new health.
    /// </summary>
    /// <value>
    /// The new health.
    /// </value>
    public PluginHealth NewHealth { get; } = newHealth;

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    /// <value>
    /// The timestamp.
    /// </value>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the reason.
    /// </summary>
    /// <value>
    /// The reason.
    /// </value>
    public string? Reason { get; } = reason;

    /// <summary>
    /// Gets the health data.
    /// </summary>
    /// <value>
    /// The health data.
    /// </value>
    public Dictionary<string, object> HealthData { get; } = healthData ?? [];
}
