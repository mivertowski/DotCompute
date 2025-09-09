// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Aot.Discovery;
using DotCompute.Plugins.Aot.Lifecycle;
using DotCompute.Plugins.Aot.Registration;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Platform;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - AOT plugin registry uses dynamic logging

namespace DotCompute.Plugins.Aot.Registry;

/// <summary>
/// Refactored AOT-compatible plugin registry that follows SOLID principles.
/// This registry is composed of segregated services rather than being a monolithic class.
/// Replaces the original god object with focused, testable components.
/// </summary>
public sealed class RefactoredAotPluginRegistry : IDisposable
{
    private readonly ILogger<RefactoredAotPluginRegistry> _logger;
    private readonly PluginRegistrationService _registrationService;
    private readonly PluginDiscoveryService _discoveryService;
    private readonly PluginLifecycleManager _lifecycleManager;
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefactoredAotPluginRegistry"/> class.
    /// This constructor demonstrates the Dependency Injection pattern with composed services.
    /// </summary>
    /// <param name="logger">The logger for this registry.</param>
    /// <param name="registrationService">The plugin registration service.</param>
    /// <param name="discoveryService">The plugin discovery service.</param>
    /// <param name="lifecycleManager">The plugin lifecycle manager.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public RefactoredAotPluginRegistry(
        ILogger<RefactoredAotPluginRegistry> logger,
        PluginRegistrationService registrationService,
        PluginDiscoveryService discoveryService,
        PluginLifecycleManager lifecycleManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));

        RegisterKnownPlugins();
        _logger.LogInfoMessage("RefactoredAotPluginRegistry initialized with {3} composed services");
    }

    /// <summary>
    /// Simplified constructor that creates the composed services internally.
    /// This maintains backward compatibility while providing the refactored architecture.
    /// </summary>
    /// <param name="logger">The logger for this registry.</param>
    public RefactoredAotPluginRegistry(ILogger<RefactoredAotPluginRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create composed services - this demonstrates the Factory pattern
        var registrationLogger = _logger as ILogger<PluginRegistrationService> ??
                                Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginRegistrationService>.Instance;
        var discoveryLogger = _logger as ILogger<PluginDiscoveryService> ??
                            Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginDiscoveryService>.Instance;
        var lifecycleLogger = _logger as ILogger<PluginLifecycleManager> ??
                            Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginLifecycleManager>.Instance;

        _registrationService = new PluginRegistrationService(registrationLogger);
        _discoveryService = new PluginDiscoveryService(discoveryLogger, _registrationService);
        _lifecycleManager = new PluginLifecycleManager(lifecycleLogger, _discoveryService);

        RegisterKnownPlugins();
        _logger.LogInfoMessage("RefactoredAotPluginRegistry initialized with internally created services");
    }

    /// <summary>
    /// Gets the plugin registration service for factory management.
    /// This provides access to registration operations while maintaining encapsulation.
    /// </summary>
    public IPluginRegistration RegistrationService => _registrationService;

    /// <summary>
    /// Gets the plugin discovery service for plugin creation and retrieval.
    /// This provides access to discovery operations while maintaining encapsulation.
    /// </summary>
    public IPluginDiscovery DiscoveryService => _discoveryService;

    /// <summary>
    /// Gets the plugin lifecycle manager for plugin unloading and disposal.
    /// This provides access to lifecycle operations while maintaining encapsulation.
    /// </summary>
    public IPluginLifecycle LifecycleManager => _lifecycleManager;

    /// <summary>
    /// Registers all known plugin types for AOT compatibility.
    /// This method demonstrates the Template Method pattern by defining the algorithm
    /// while allowing the individual registration steps to be customized.
    /// </summary>
    private void RegisterKnownPlugins()
    {
        _logger.LogInfoMessage("Registering known plugins for AOT compatibility");

        var registeredCount = 0;

        // Register CPU backend
        RegisterCpuBackend();
        registeredCount++;

        // Register GPU backends with platform checks
        if (RegisterCudaBackend())
        {
            registeredCount++;
        }

        if (RegisterMetalBackend())
        {
            registeredCount++;
        }

        if (RegisterOpenClBackend())
        {
            registeredCount++;
        }


        if (RegisterDirectComputeBackend())
        {
            registeredCount++;
        }


        if (RegisterVulkanBackend())
        {
            registeredCount++;
        }


        _logger.LogInfoMessage("Registered {registeredCount} plugin factories");
    }

    /// <summary>
    /// Registers the CPU backend plugin factory.
    /// </summary>
    private void RegisterCpuBackend()
    {
        _registrationService.RegisterPluginFactory("DotCompute.Backends.CPU", () =>
        {
            _logger.LogDebugMessage("Creating CPU backend plugin");
            return new Backends.AotCpuBackendPlugin();
        });
    }

    /// <summary>
    /// Registers the CUDA backend plugin factory if the platform supports it.
    /// </summary>
    /// <returns>True if the factory was registered; false if the platform doesn't support CUDA.</returns>
    private bool RegisterCudaBackend()
    {
        try
        {
            _registrationService.RegisterPluginFactory("DotCompute.Backends.CUDA", () =>
            {
                _logger.LogDebugMessage("Creating CUDA backend plugin");
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.CUDA);
                return new Backends.AotCudaBackendPlugin();
            });
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            _logger.LogDebugMessage("CUDA backend not supported on this platform, skipping registration");
            return false;
        }
    }

    /// <summary>
    /// Registers the Metal backend plugin factory if the platform supports it.
    /// </summary>
    /// <returns>True if the factory was registered; false if the platform doesn't support Metal.</returns>
    private bool RegisterMetalBackend()
    {
        try
        {
            _registrationService.RegisterPluginFactory("DotCompute.Backends.Metal", () =>
            {
                _logger.LogDebugMessage("Creating Metal backend plugin");
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.Metal);
                return new Backends.AotMetalBackendPlugin();
            });
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            _logger.LogDebugMessage("Metal backend not supported on this platform, skipping registration");
            return false;
        }
    }

    /// <summary>
    /// Registers the OpenCL backend plugin factory if the platform supports it.
    /// </summary>
    /// <returns>True if the factory was registered; false if the platform doesn't support OpenCL.</returns>
    private bool RegisterOpenClBackend()
    {
        try
        {
            _registrationService.RegisterPluginFactory("DotCompute.Backends.OpenCL", () =>
            {
                _logger.LogDebugMessage("Creating OpenCL backend plugin");
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.OpenCL);
                return new Backends.AotOpenClBackendPlugin();
            });
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            _logger.LogDebugMessage("OpenCL backend not supported on this platform, skipping registration");
            return false;
        }
    }

    /// <summary>
    /// Registers the DirectCompute backend plugin factory if the platform supports it.
    /// </summary>
    /// <returns>True if the factory was registered; false if the platform doesn't support DirectCompute.</returns>
    private bool RegisterDirectComputeBackend()
    {
        try
        {
            _registrationService.RegisterPluginFactory("DotCompute.Backends.DirectCompute", () =>
            {
                _logger.LogDebugMessage("Creating DirectCompute backend plugin");
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.DirectCompute);
                return new Backends.AotDirectComputeBackendPlugin();
            });
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            _logger.LogDebugMessage("DirectCompute backend not supported on this platform, skipping registration");
            return false;
        }
    }

    /// <summary>
    /// Registers the Vulkan backend plugin factory if the platform supports it.
    /// </summary>
    /// <returns>True if the factory was registered; false if the platform doesn't support Vulkan.</returns>
    private bool RegisterVulkanBackend()
    {
        try
        {
            _registrationService.RegisterPluginFactory("DotCompute.Backends.Vulkan", () =>
            {
                _logger.LogDebugMessage("Creating Vulkan compute backend plugin");
                PlatformDetection.ValidateBackendAvailability(ComputeBackendType.Vulkan);
                return new Backends.AotVulkanBackendPlugin();
            });
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            _logger.LogDebugMessage("Vulkan backend not supported on this platform, skipping registration");
            return false;
        }
    }

    // Facade methods that delegate to the appropriate composed services
    // These maintain backward compatibility while using the new architecture

    /// <summary>
    /// Creates a plugin instance using static factory methods instead of reflection.
    /// This method acts as a facade over the discovery service.
    /// </summary>
    /// <param name="pluginTypeName">The type name of the plugin to create.</param>
    /// <returns>The created plugin instance, or null if creation failed.</returns>
    public IBackendPlugin? CreatePlugin(string pluginTypeName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            var plugin = _discoveryService.CreatePlugin(pluginTypeName);
            if (plugin != null)
            {
                _lifecycleManager.RegisterPluginForManagement(plugin);
            }
            return plugin;
        }
    }

    /// <summary>
    /// Gets a loaded plugin by ID. This method acts as a facade over the discovery service.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <returns>The plugin instance if found; null otherwise.</returns>
    public IBackendPlugin? GetPlugin(string pluginId) => _discoveryService.GetPlugin(pluginId);

    /// <summary>
    /// Gets all loaded plugins. This method acts as a facade over the discovery service.
    /// </summary>
    /// <returns>A read-only collection of loaded plugin instances.</returns>
    public IReadOnlyCollection<IBackendPlugin> GetLoadedPlugins() => _discoveryService.GetLoadedPlugins();

    /// <summary>
    /// Lists all available plugin types that can be created.
    /// This method acts as a facade over the registration service.
    /// </summary>
    /// <returns>A read-only collection of available plugin type names.</returns>
    public IReadOnlyCollection<string> GetAvailablePluginTypes() => _registrationService.GetAvailablePluginTypes();

    /// <summary>
    /// Unloads a plugin by ID. This method acts as a facade over the lifecycle manager.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to unload.</param>
    /// <returns>True if the plugin was successfully unloaded; false otherwise.</returns>
    public bool UnloadPlugin(string pluginId) => _lifecycleManager.UnloadPlugin(pluginId);

    /// <summary>
    /// Registers a custom plugin factory for AOT scenarios.
    /// This method acts as a facade over the registration service.
    /// </summary>
    /// <param name="pluginTypeName">The plugin type name.</param>
    /// <param name="factory">The factory function.</param>
    public void RegisterPluginFactory(string pluginTypeName, Func<IBackendPlugin> factory)
        => _registrationService.RegisterPluginFactory(pluginTypeName, factory);

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// This demonstrates the Composite pattern by disposing all composed services.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_lock)
        {
            try
            {
                _lifecycleManager.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error disposing lifecycle manager");
            }

            try
            {
                _discoveryService.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error disposing discovery service");
            }

            try
            {
                _registrationService.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error disposing registration service");
            }
        }

        _logger.LogInfoMessage("RefactoredAotPluginRegistry disposed");
    }
}