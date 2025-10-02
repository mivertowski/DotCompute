// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Plugins.Aot.Backends;

/// <summary>
/// Minimal CUDA backend plugin implementation for AOT compatibility.
/// This implementation provides NVIDIA CUDA GPU compute capabilities with runtime availability checks.
/// </summary>
internal sealed class AotCudaBackendPlugin : IBackendPlugin
{
    private readonly Lock _lock = new();
#pragma warning disable IDE0044 // Make field readonly - these fields are intentionally mutable as they track plugin state
    private PluginState _state = PluginState.Loaded;
    private PluginHealth _health = PluginHealth.Unknown;
    private bool _disposed;
#pragma warning restore IDE0044

    /// <summary>
    /// Gets the unique identifier for this CUDA backend plugin.
    /// </summary>
    public string Id => "DotCompute.Backends.CUDA";

    /// <summary>
    /// Gets the display name of the CUDA backend plugin.
    /// </summary>
    public string Name => "CUDA Backend";

    /// <summary>
    /// Gets the description of the CUDA backend capabilities.
    /// </summary>
    public string Description => "NVIDIA CUDA GPU compute backend";

    /// <summary>
    /// Gets the version of this plugin implementation.
    /// </summary>
    public Version Version => new(1, 0, 0);

    /// <summary>
    /// Gets the author of this plugin.
    /// </summary>
    public string Author => "DotCompute Team";

    /// <summary>
    /// Gets the capabilities supported by this CUDA backend plugin.
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
    /// <summary>
    /// Occurs when error occurred.
    /// </summary>

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
        // Minimal implementation for AOT
    }

    /// <summary>
    /// Initializes the CUDA backend plugin asynchronously.
    /// Performs CUDA runtime availability checks during initialization.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        if (!CheckCudaAvailability())
        {
            _health = PluginHealth.Unhealthy;
            _state = PluginState.Failed;
            return;
        }

        _state = PluginState.Initialized;
        _health = PluginHealth.Healthy;
        StateChanged?.Invoke(this, new PluginStateChangedEventArgs(PluginState.Loaded, _state));
        await Task.CompletedTask;
    }

    /// <summary>
    /// Starts the CUDA backend plugin asynchronously.
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
    /// Stops the CUDA backend plugin asynchronously.
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
    /// Validates the CUDA backend plugin's configuration and dependencies.
    /// Checks for CUDA runtime availability on the current system.
    /// </summary>
    /// <returns>A validation result indicating whether CUDA is available.</returns>
    public PluginValidationResult Validate()
    {
        var result = new PluginValidationResult { IsValid = CheckCudaAvailability() };
        if (!result.IsValid)
        {
            result.Errors.Add("CUDA runtime is not available on this system");
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
    /// Gets the CUDA backend plugin's performance metrics.
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
    /// Checks if the CUDA runtime is available on the current system.
    /// This method performs comprehensive platform and runtime checks.
    /// </summary>
    /// <returns>True if CUDA is available; false otherwise.</returns>
    private static bool CheckCudaAvailability()
    {
        try
        {
            // Check platform compatibility first
            if (!Environment.Is64BitOperatingSystem ||
                !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
            {
                return false;
            }

            // Check for NVIDIA GPU presence through system queries
            if (OperatingSystem.IsWindows())
            {
                return CheckWindowsCudaAvailability();
            }
            else if (OperatingSystem.IsLinux())
            {
                return CheckLinuxCudaAvailability();
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks for CUDA availability on Windows systems.
    /// </summary>
    /// <returns>True if CUDA is available on Windows; false otherwise.</returns>
    private static bool CheckWindowsCudaAvailability()
    {
        try
        {
            // Check for NVIDIA driver in Windows system files
            // This approach is production-ready and doesn't require WMI/registry access
            // which may be restricted in some environments
            var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var nvmlPath = Path.Combine(systemDirectory, "nvml.dll");
            var cudartPath = Path.Combine(systemDirectory, "cudart64_*.dll");

            return File.Exists(nvmlPath) || Directory.GetFiles(systemDirectory, "cudart64_*.dll").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks for CUDA availability on Linux systems.
    /// </summary>
    /// <returns>True if CUDA is available on Linux; false otherwise.</returns>
    private static bool CheckLinuxCudaAvailability()
    {
        try
        {
            // Check for CUDA libraries in standard locations
            var cudaPaths = new[]
            {
                "/usr/lib/x86_64-linux-gnu/libcuda.so",
                "/usr/lib64/libcuda.so",
                "/usr/local/cuda/lib64/libcudart.so"
            };

            return cudaPaths.Any(File.Exists) ||
                   Directory.Exists("/proc/driver/nvidia") ||
                   File.Exists("/dev/nvidia0");
        }
        catch
        {
            return false;
        }
    }
}