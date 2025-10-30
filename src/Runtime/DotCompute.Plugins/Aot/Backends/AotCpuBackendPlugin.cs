// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Plugins.Aot.Backends;

/// <summary>
/// Minimal CPU backend plugin implementation for AOT compatibility.
/// This implementation provides multi-threaded CPU compute capabilities with SIMD acceleration.
/// </summary>
internal sealed class AotCpuBackendPlugin : IBackendPlugin
{
    private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
    private PluginState _state = PluginState.Loaded;
    private PluginHealth _health = PluginHealth.Healthy;
#pragma warning restore IDE0044

    /// <summary>
    /// Gets the unique identifier for this CPU backend plugin.
    /// </summary>
    public string Id => "DotCompute.Backends.CPU";

    /// <summary>
    /// Gets the display name of the CPU backend plugin.
    /// </summary>
    public string Name => "CPU Backend";

    /// <summary>
    /// Gets a dynamic description of the CPU backend capabilities based on the current hardware.
    /// </summary>
    public string Description => GetCpuBackendDescription();

    /// <summary>
    /// Gets the version of this plugin implementation.
    /// </summary>
    public Version Version => new(1, 0, 0);

    /// <summary>
    /// Gets the author of this plugin.
    /// </summary>
    public string Author => "DotCompute Team";

    /// <summary>
    /// Gets the capabilities supported by this CPU backend plugin.
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
    /// Occurs when state changed.
    /// </summary>

    /// <summary>
    /// Event raised when the plugin's state changes.
    /// </summary>
    public event EventHandler<PluginStateChangedEventArgs>? StateChanged;

#pragma warning disable CS0067 // Event is never used - minimal implementation for AOT compatibility
    /// <summary>
    /// Event raised when the plugin encounters an error.
    /// </summary>
    public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
    /// <summary>
    /// Occurs when health changed.
    /// </summary>

    /// <summary>
    /// Event raised when the plugin's health status changes.
    /// </summary>
    public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

    /// <summary>
    /// Configures services for dependency injection.
    /// This is a minimal implementation suitable for AOT scenarios.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration for the plugin.</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Minimal implementation for AOT - CPU backend typically doesn't require additional services
    }

    /// <summary>
    /// Initializes the CPU backend plugin asynchronously.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var oldState = _state;
            _state = PluginState.Initialized;
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the CPU backend plugin asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var oldState = _state;
            _state = PluginState.Running;
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the CPU backend plugin asynchronously.
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
    /// Validates the CPU backend plugin's configuration and dependencies.
    /// The CPU backend is always considered valid as it doesn't have external dependencies.
    /// </summary>
    /// <returns>A validation result indicating the plugin is valid.</returns>
    public PluginValidationResult Validate() => new() { IsValid = true };

    /// <summary>
    /// Gets the plugin's configuration schema as a JSON string.
    /// The CPU backend has minimal configuration requirements.
    /// </summary>
    /// <returns>An empty JSON object representing the minimal configuration schema.</returns>
    public string GetConfigurationSchema() => "{}";

    /// <summary>
    /// Handles configuration changes at runtime.
    /// The CPU backend has minimal configuration, so this is a no-op.
    /// </summary>
    /// <param name="configuration">The new configuration.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task.</returns>
    public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Gets the CPU backend plugin's performance metrics.
    /// </summary>
    /// <returns>An empty metrics object for the minimal implementation.</returns>
    public PluginMetrics GetMetrics() => new();

    /// <summary>
    /// Generates a dynamic description of the CPU backend capabilities based on the current hardware.
    /// This method demonstrates hardware detection and capability reporting.
    /// </summary>
    /// <returns>A string describing the CPU backend's capabilities.</returns>
    private static string GetCpuBackendDescription()
    {
        var hardware = PlatformDetection.Hardware;
        var capabilities = new List<string> { "Multi-threaded CPU compute backend" };

        // Add SIMD capabilities based on hardware detection
        var simdFeatures = new List<string>();
        if (hardware.SupportsAvx512F)
        {
            simdFeatures.Add("AVX-512");
        }
        else if (hardware.SupportsAvx2)
        {
            simdFeatures.Add("AVX2");
        }
        else if (hardware.SupportsAvx)
        {
            simdFeatures.Add("AVX");
        }
        else if (hardware.SupportsSse42)
        {
            simdFeatures.Add("SSE4.2");
        }
        else if (hardware.SupportsSse2)
        {
            simdFeatures.Add("SSE2");
        }

        if (hardware.SupportsArmBase)
        {
            simdFeatures.Add("NEON");
        }

        if (simdFeatures.Count > 0)
        {
            capabilities.Add($"SIMD acceleration ({string.Join(", ", simdFeatures)})");
        }

        capabilities.Add($"Vector size: {hardware.VectorSizeBytes} bytes");
        capabilities.Add($"{PlatformDetection.Current.ProcessorCount} threads");

        return string.Join(" | ", capabilities);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// The CPU backend has no unmanaged resources to dispose.
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose for the CPU backend
    }
}
