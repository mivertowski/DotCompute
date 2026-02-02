// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Backends.OpenCL.DeviceManagement;
using DotCompute.Backends.OpenCL.Factory;
using DotCompute.Core.Telemetry;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL;

/// <summary>
/// OpenCL backend plugin for DotCompute framework.
/// Provides registration and lifecycle management for OpenCL compute backend.
/// </summary>
/// <remarks>
/// <strong>EXPERIMENTAL:</strong> The OpenCL backend is experimental and has limited cross-vendor testing.
/// Use CUDA backend for production NVIDIA workloads, Metal for Apple Silicon.
/// </remarks>
[Experimental("DOTCOMPUTE0003", UrlFormat = "https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{0}.md")]
public sealed class OpenCLBackendPlugin : IBackendPlugin
{
    private readonly ILogger<OpenCLBackendPlugin> _logger;
    private readonly object _lock = new();

    private PluginState _state = PluginState.Loaded;
    private PluginHealth _health = PluginHealth.Unknown;
    private bool _disposed;

    /// <summary>
    /// Gets the unique identifier for this OpenCL backend plugin.
    /// </summary>
    public string Id => "DotCompute.Backends.OpenCL";

    /// <summary>
    /// Gets the display name of the OpenCL backend plugin.
    /// </summary>
    public string Name => "OpenCL Backend";

    /// <summary>
    /// Gets the description of the OpenCL backend capabilities.
    /// </summary>
    public string Description => "Cross-platform OpenCL compute backend supporting CPUs, GPUs, and accelerators";

    /// <summary>
    /// Gets the version of this plugin implementation.
    /// </summary>
    public Version Version => new(1, 0, 0);

    /// <summary>
    /// Gets the author of this plugin.
    /// </summary>
    public string Author => "DotCompute Team";

    /// <summary>
    /// Gets the capabilities supported by this OpenCL backend plugin.
    /// </summary>
    public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend |
        PluginCapabilities.Scalable |
        PluginCapabilities.CrossPlatform;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLBackendPlugin"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLBackendPlugin(ILogger<OpenCLBackendPlugin> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Configures services for dependency injection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">The configuration for the plugin.</param>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        _logger.LogDebug("Configuring OpenCL backend services");

        // Register OpenCL backend services
        services.AddSingleton<OpenCLDeviceManager>();
        services.AddSingleton<OpenCLAcceleratorFactory>();
        services.AddTransient<OpenCLAccelerator>();

        // Register factory as singleton for efficient device enumeration
        services.AddSingleton<ILogger<OpenCLDeviceManager>>(provider =>
            provider.GetRequiredService<ILogger<OpenCLDeviceManager>>());

        _logger.LogDebug("OpenCL backend services configured successfully");
    }

    /// <summary>
    /// Initializes the OpenCL backend plugin asynchronously.
    /// Performs OpenCL runtime availability checks and device enumeration.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing OpenCL backend plugin");

        // Record experimental feature usage
        ExperimentalFeatureTelemetry.RecordUsage(
            "DOTCOMPUTE0003",
            "OpenCL Backend",
            context: "Plugin initialization");

        await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    var oldState = _state;
                    var oldHealth = _health;

                    // Check OpenCL availability
                    if (PlatformDetection.IsBackendAvailable(ComputeBackendType.OpenCL))
                    {
                        // Try to create device manager to validate OpenCL
                        var deviceManager = serviceProvider.GetRequiredService<OpenCLDeviceManager>();
                        var hasDevices = deviceManager.IsOpenCLAvailable;

                        if (hasDevices)
                        {
                            _state = PluginState.Initialized;
                            _health = PluginHealth.Healthy;

                            var deviceCount = deviceManager.AllDevices.Count();
                            _logger.LogInformation("OpenCL backend initialized successfully with {DeviceCount} devices", deviceCount);
                        }
                        else
                        {
                            _state = PluginState.Failed;
                            _health = PluginHealth.Unhealthy;
                            _logger.LogWarning("OpenCL runtime available but no compatible devices found");
                        }
                    }
                    else
                    {
                        _state = PluginState.Failed;
                        _health = PluginHealth.Unhealthy;
                        _logger.LogWarning("OpenCL runtime not available on this system");
                    }

                    // Notify state changes
                    if (oldState != _state)
                    {
                        StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
                    }

                    if (oldHealth != _health)
                    {
                        HealthChanged?.Invoke(this, new PluginHealthChangedEventArgs(oldHealth, _health));
                    }
                }
                catch (Exception ex)
                {
                    var oldState = _state;
                    var oldHealth = _health;

                    _state = PluginState.Failed;
                    _health = PluginHealth.Unhealthy;

                    _logger.LogError(ex, "Failed to initialize OpenCL backend plugin");

                    // Record error in experimental feature telemetry
                    ExperimentalFeatureTelemetry.RecordError("DOTCOMPUTE0003", ex);

                    ErrorOccurred?.Invoke(this, new PluginErrorEventArgs(ex, "Initialization failed"));
                    StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
                    HealthChanged?.Invoke(this, new PluginHealthChangedEventArgs(oldHealth, _health));
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Starts the OpenCL backend plugin asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous start operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_health != PluginHealth.Healthy)
            {
                _logger.LogWarning("Cannot start OpenCL backend plugin - health status: {HealthStatus}", _health);
                return Task.CompletedTask;
            }

            var oldState = _state;
            _state = PluginState.Running;

            _logger.LogInformation("OpenCL backend plugin started successfully");
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the OpenCL backend plugin asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var oldState = _state;
            _state = PluginState.Stopped;

            _logger.LogInformation("OpenCL backend plugin stopped");
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the OpenCL backend plugin's configuration and dependencies.
    /// Checks for OpenCL runtime availability and compatible devices.
    /// </summary>
    /// <returns>A validation result indicating whether OpenCL is available and usable.</returns>
    public PluginValidationResult Validate()
    {
        var result = new PluginValidationResult();

        try
        {
            // Check platform support
            var isAvailable = PlatformDetection.IsBackendAvailable(ComputeBackendType.OpenCL);
            if (!isAvailable)
            {
                result.IsValid = false;
                result.Errors.Add("OpenCL runtime not found on this system");
                return result;
            }

            // Try to enumerate devices
            var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(_logger)));
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
            var hasDevices = deviceManager.IsOpenCLAvailable;

            if (!hasDevices)
            {
                result.IsValid = false;
                result.Errors.Add("OpenCL runtime found but no compatible devices available");
                return result;
            }

            // Check for suitable devices
            var suitableDevices = deviceManager.AllDevices
                .Where(OpenCLAcceleratorFactory.IsDeviceSuitable)
                .ToList();

            if (suitableDevices.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No devices meet minimum compute requirements");
                return result;
            }

            result.IsValid = true;
            result.Warnings.Add($"Found {suitableDevices.Count} suitable OpenCL devices");

            foreach (var device in suitableDevices.Take(3)) // Log first 3 devices
            {
                result.Warnings.Add($"Device: {device.Name} ({device.Vendor}) - {device.Type}");
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation failed: {ex.Message}");
            _logger.LogError(ex, "OpenCL backend plugin validation failed");
        }

        return result;
    }

    /// <summary>
    /// Gets the plugin's configuration schema as a JSON string.
    /// </summary>
    /// <returns>Configuration schema for the OpenCL backend plugin.</returns>
    public string GetConfigurationSchema()
    {
        return """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "type": "object",
          "properties": {
            "preferredDeviceType": {
              "type": "string",
              "enum": ["GPU", "CPU", "Accelerator", "Auto"],
              "default": "Auto",
              "description": "Preferred device type for OpenCL operations"
            },
            "deviceIndex": {
              "type": "integer",
              "minimum": 0,
              "default": 0,
              "description": "Device index within the preferred type"
            },
            "enableProfiling": {
              "type": "boolean",
              "default": false,
              "description": "Enable OpenCL kernel profiling"
            },
            "maxMemoryAllocation": {
              "type": "integer",
              "minimum": 1048576,
              "description": "Maximum memory allocation size in bytes"
            }
          }
        }
        """;
    }

    /// <summary>
    /// Handles configuration changes at runtime.
    /// </summary>
    /// <param name="configuration">The new configuration.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A completed task.</returns>
    public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("OpenCL backend configuration updated");

        // In a full implementation, we might want to:
        // - Update preferred device selection
        // - Adjust memory allocation limits
        // - Enable/disable profiling

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the OpenCL backend plugin's performance metrics.
    /// </summary>
    /// <returns>Performance metrics for the plugin.</returns>
    public PluginMetrics GetMetrics()
    {
        var metrics = new PluginMetrics();

        try
        {
            if (_health == PluginHealth.Healthy)
            {
                // Add OpenCL-specific metrics
                var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(_logger)));
                var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());

                metrics.CustomMetrics["opencl_devices_available"] = deviceManager.AllDevices.Count();
                metrics.CustomMetrics["opencl_platforms_available"] = deviceManager.Platforms.Count;
                metrics.CustomMetrics["status"] = "Healthy";
            }
            else
            {
                metrics.CustomMetrics["status"] = $"Unhealthy - {_health}";
            }
        }
        catch (Exception ex)
        {
            metrics.CustomMetrics["status"] = $"Error - {ex.Message}";
            _logger.LogWarning(ex, "Failed to collect OpenCL backend metrics");
        }

        return metrics;
    }

    /// <summary>
    /// Disposes the OpenCL backend plugin.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogDebug("Disposing OpenCL backend plugin");

            var oldState = _state;
            _state = PluginState.Stopped;
            StateChanged?.Invoke(this, new PluginStateChangedEventArgs(oldState, _state));

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
