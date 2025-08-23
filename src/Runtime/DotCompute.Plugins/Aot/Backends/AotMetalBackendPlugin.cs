// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Plugins.Aot.Backends;

/// <summary>
/// Minimal Metal backend plugin implementation for AOT compatibility.
/// This implementation provides Apple Metal GPU compute capabilities on macOS and iOS platforms.
/// </summary>
internal sealed class AotMetalBackendPlugin : IBackendPlugin
{
    private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
    private PluginState _state = PluginState.Loaded;
    private PluginHealth _health = PluginHealth.Unknown;
    private bool _disposed;
#pragma warning restore IDE0044

    /// <summary>
    /// Gets the unique identifier for this Metal backend plugin.
    /// </summary>
    public string Id => "DotCompute.Backends.Metal";

    /// <summary>
    /// Gets the display name of the Metal backend plugin.
    /// </summary>
    public string Name => "Metal Backend";

    /// <summary>
    /// Gets the description of the Metal backend capabilities.
    /// </summary>
    public string Description => "Apple Metal GPU compute backend";

    /// <summary>
    /// Gets the version of this plugin implementation.
    /// </summary>
    public Version Version => new(1, 0, 0);

    /// <summary>
    /// Gets the author of this plugin.
    /// </summary>
    public string Author => "DotCompute Team";

    /// <summary>
    /// Gets the capabilities supported by this Metal backend plugin.
    /// </summary>
    public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;

    /// <summary>
    /// Gets the current operational state of the plugin.
    /// </summary>
    public PluginState State => _state;

    /// <summary>
    /// Gets the current health status of the plugin.
    /// </summary>
    public PluginHealth Health => _health;

    /// <summary>
    /// Event raised when the plugin's state changes.
    /// </summary>
    public event EventHandler<PluginStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when the plugin encounters an error.
    /// </summary>
    public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Event raised when the plugin's health status changes.
    /// </summary>
    public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;

    private void OnStateChanged(PluginStateChangedEventArgs e) => StateChanged?.Invoke(this, e);
    private void OnErrorOccurred(PluginErrorEventArgs e) => ErrorOccurred?.Invoke(this, e);
    private void OnHealthChanged(PluginHealthChangedEventArgs e) => HealthChanged?.Invoke(this, e);

    /// <summary>
    /// Configures services for dependency injection.
    /// This is a minimal implementation suitable for AOT scenarios.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration for the plugin.</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Minimal implementation for AOT
    }

    /// <summary>
    /// Initializes the Metal backend plugin asynchronously.
    /// Performs Metal availability checks during initialization.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var oldState = _state;
            var oldHealth = _health;

            if (!IsMetalAvailable())
            {
                _health = PluginHealth.Unhealthy;
                _state = PluginState.Failed;
                StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
                return;
            }

            _state = PluginState.Initialized;
            _health = PluginHealth.Healthy;
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Starts the Metal backend plugin asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_health != PluginHealth.Healthy)
            {
                return Task.CompletedTask;
            }

            var oldState = _state;
            _state = PluginState.Running;
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the Metal backend plugin asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var oldState = _state;
            _state = PluginState.Stopped;
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the Metal backend plugin's configuration and dependencies.
    /// Checks for Metal availability on the current platform.
    /// </summary>
    /// <returns>A validation result indicating whether Metal is available.</returns>
    public PluginValidationResult Validate()
    {
        var result = new PluginValidationResult { IsValid = IsMetalAvailable() };
        if (!result.IsValid)
        {
            result.Errors.Add("Metal is only available on macOS and iOS");
        }
        return result;
    }

    /// <summary>
    /// Gets the plugin's configuration schema as a JSON string.
    /// </summary>
    /// <returns>An empty JSON object for the minimal configuration schema.</returns>
    public string GetConfigurationSchema() => "{}";

    /// <summary>
    /// Handles configuration changes at runtime.
    /// </summary>
    /// <param name="configuration">The new configuration.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task.</returns>
    public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Gets the Metal backend plugin's performance metrics.
    /// </summary>
    /// <returns>An empty metrics object for the minimal implementation.</returns>
    public PluginMetrics GetMetrics() => new();

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
    }

    /// <summary>
    /// Checks if Metal compute is available on the current platform.
    /// Metal is only available on Apple platforms (macOS and iOS).
    /// </summary>
    /// <returns>True if Metal is available; false otherwise.</returns>
    private static bool IsMetalAvailable() => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();
}