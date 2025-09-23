// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Abstractions.Interfaces.Plugins;

/// <summary>
/// Base interface for all DotCompute backend plugins.
/// Provides standardized lifecycle management and configuration for compute backend plugins.
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
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>Compute backend capability.</summary>
    ComputeBackend = 1 << 0,

    /// <summary>Storage provider capability.</summary>
    StorageProvider = 1 << 1,

    /// <summary>Network provider capability.</summary>
    NetworkProvider = 1 << 2,

    /// <summary>Security provider capability.</summary>
    SecurityProvider = 1 << 3,

    /// <summary>Monitoring provider capability.</summary>
    MonitoringProvider = 1 << 4,

    /// <summary>Scheduler provider capability.</summary>
    SchedulerProvider = 1 << 5,

    /// <summary>Cache provider capability.</summary>
    CacheProvider = 1 << 6,

    /// <summary>Logging provider capability.</summary>
    LoggingProvider = 1 << 7,

    /// <summary>Metrics provider capability.</summary>
    MetricsProvider = 1 << 8,

    /// <summary>Configuration provider capability.</summary>
    ConfigurationProvider = 1 << 9,

    /// <summary>Extension provider capability.</summary>
    ExtensionProvider = 1 << 10,

    /// <summary>Hot reloadable capability.</summary>
    HotReloadable = 1 << 11,

    /// <summary>Scalable capability.</summary>
    Scalable = 1 << 12,

    /// <summary>Clusterable capability.</summary>
    Clusterable = 1 << 13,

    /// <summary>Persistent capability.</summary>
    Persistent = 1 << 14,

    /// <summary>Transactional capability.</summary>
    Transactional = 1 << 15,

    /// <summary>Cross-platform support capability.</summary>
    CrossPlatform = 1 << 16
}

/// <summary>
/// Represents the state of a plugin.
/// </summary>
public enum PluginState
{
    /// <summary>Unknown state.</summary>
    Unknown,

    /// <summary>Loading state.</summary>
    Loading,

    /// <summary>Loaded state.</summary>
    Loaded,

    /// <summary>Initializing state.</summary>
    Initializing,

    /// <summary>Initialized state.</summary>
    Initialized,

    /// <summary>Starting state.</summary>
    Starting,

    /// <summary>Running state.</summary>
    Running,

    /// <summary>Stopping state.</summary>
    Stopping,

    /// <summary>Stopped state.</summary>
    Stopped,

    /// <summary>Failed state.</summary>
    Failed,

    /// <summary>Unloading state.</summary>
    Unloading,

    /// <summary>Unloaded state.</summary>
    Unloaded
}

/// <summary>
/// Represents the health status of a plugin.
/// </summary>
public enum PluginHealth
{
    /// <summary>Unknown health.</summary>
    Unknown,

    /// <summary>Healthy state.</summary>
    Healthy,

    /// <summary>Degraded performance.</summary>
    Degraded,

    /// <summary>Unhealthy state.</summary>
    Unhealthy,

    /// <summary>Critical failure.</summary>
    Critical
}

/// <summary>
/// Represents the result of plugin validation.
/// </summary>
public class PluginValidationResult
{
    /// <summary>
    /// Gets or sets whether the plugin is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets additional validation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Represents plugin performance metrics.
/// </summary>
public class PluginMetrics
{
    /// <summary>
    /// Gets or sets the timestamp when metrics were collected.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the plugin uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the total request count.
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the CPU usage percentage.
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Gets custom plugin-specific metrics.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; } = [];
}

/// <summary>
/// Event arguments for plugin state changes.
/// </summary>
public class PluginStateChangedEventArgs(PluginState oldState, PluginState newState, string? reason = null) : EventArgs
{
    /// <summary>
    /// Gets the previous plugin state.
    /// </summary>
    public PluginState OldState { get; } = oldState;

    /// <summary>
    /// Gets the new plugin state.
    /// </summary>
    public PluginState NewState { get; } = newState;

    /// <summary>
    /// Gets the timestamp of the state change.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the reason for the state change.
    /// </summary>
    public string? Reason { get; } = reason;
}

/// <summary>
/// Event arguments for plugin errors.
/// </summary>
public class PluginErrorEventArgs(Exception exception, string context, Dictionary<string, object>? additionalData = null) : EventArgs
{
    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the context in which the error occurred.
    /// </summary>
    public string Context { get; } = context;

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets additional error data.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; } = additionalData ?? [];
}

/// <summary>
/// Event arguments for plugin health changes.
/// </summary>
public class PluginHealthChangedEventArgs(PluginHealth oldHealth, PluginHealth newHealth, string? reason = null, Dictionary<string, object>? healthData = null) : EventArgs
{
    /// <summary>
    /// Gets the previous health status.
    /// </summary>
    public PluginHealth OldHealth { get; } = oldHealth;

    /// <summary>
    /// Gets the new health status.
    /// </summary>
    public PluginHealth NewHealth { get; } = newHealth;

    /// <summary>
    /// Gets the timestamp of the health change.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the reason for the health change.
    /// </summary>
    public string? Reason { get; } = reason;

    /// <summary>
    /// Gets additional health data.
    /// </summary>
    public Dictionary<string, object> HealthData { get; } = healthData ?? [];
}