// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Aot.Registration;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

namespace DotCompute.Plugins.Aot.Discovery;

/// <summary>
/// Provides plugin discovery and creation services for AOT scenarios.
/// Implements the Interface Segregation Principle by focusing solely on discovery operations.
/// </summary>
public sealed class PluginDiscoveryService : IPluginDiscovery
{
    private readonly ILogger<PluginDiscoveryService> _logger;
    private readonly PluginRegistrationService _registrationService;
    private readonly Dictionary<string, IBackendPlugin> _loadedPlugins;
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDiscoveryService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for this service.</param>
    /// <param name="registrationService">The plugin registration service for accessing factories.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger or registrationService is null.</exception>
    public PluginDiscoveryService(ILogger<PluginDiscoveryService> logger, PluginRegistrationService registrationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _loadedPlugins = [];


        _logger.LogDebugMessage("PluginDiscoveryService initialized");
    }

    /// <summary>
    /// Gets the count of currently loaded plugins.
    /// </summary>
    public int LoadedPluginCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lock (_lock)
            {
                return _loadedPlugins.Count;
            }
        }
    }

    /// <summary>
    /// Creates a plugin instance using registered factory methods.
    /// </summary>
    /// <param name="pluginTypeName">The type name of the plugin to create.</param>
    /// <returns>The created plugin instance, or null if creation failed or no factory exists.</returns>
    /// <exception cref="ArgumentException">Thrown when pluginTypeName is null, empty, or whitespace.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when the plugin is not supported on the current platform.</exception>
    public IBackendPlugin? CreatePlugin(string pluginTypeName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginTypeName))
        {
            throw new ArgumentException("Plugin type name cannot be null, empty, or whitespace.", nameof(pluginTypeName));
        }

        lock (_lock)
        {
            try
            {
                var factory = _registrationService.GetFactory(pluginTypeName);
                if (factory == null)
                {
                    _logger.LogWarningMessage("No factory found for plugin type: {pluginTypeName}");
                    return null;
                }

                var plugin = factory();
                _loadedPlugins[plugin.Id] = plugin;

                _logger.LogInfoMessage($"Successfully created plugin {plugin.Id} ({plugin.Name}) from factory");

                return plugin;
            }
            catch (PlatformNotSupportedException)
            {
                // Re-throw platform not supported exceptions so callers can handle them appropriately
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to create plugin {pluginTypeName}");
                return null;
            }
        }
    }

    /// <summary>
    /// Attempts to create a plugin instance, providing detailed error information on failure.
    /// </summary>
    /// <param name="pluginTypeName">The type name of the plugin to create.</param>
    /// <param name="plugin">The created plugin instance if successful.</param>
    /// <param name="errorMessage">Error message if creation failed.</param>
    /// <returns>True if the plugin was successfully created; false otherwise.</returns>
    public bool TryCreatePlugin(string pluginTypeName, out IBackendPlugin? plugin, out string? errorMessage)
    {
        plugin = null;
        errorMessage = null;

        if (_disposed)
        {
            errorMessage = "Discovery service has been disposed";
            return false;
        }

        if (string.IsNullOrWhiteSpace(pluginTypeName))
        {
            errorMessage = "Plugin type name cannot be null, empty, or whitespace";
            return false;
        }

        lock (_lock)
        {
            try
            {
                var factory = _registrationService.GetFactory(pluginTypeName);
                if (factory == null)
                {
                    errorMessage = $"No factory registered for plugin type: {pluginTypeName}";
                    return false;
                }

                plugin = factory();
                _loadedPlugins[plugin.Id] = plugin;

                _logger.LogInfoMessage($"Successfully created plugin {plugin.Id} ({plugin.Name}) from factory");

                return true;
            }
            catch (PlatformNotSupportedException ex)
            {
                errorMessage = $"Plugin {pluginTypeName} is not supported on this platform: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to create plugin {pluginTypeName}: {ex.Message}";
                _logger.LogErrorMessage(ex, $"Failed to create plugin {pluginTypeName}");
                return false;
            }
        }
    }

    /// <summary>
    /// Gets a loaded plugin by its unique identifier.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <returns>The plugin instance if found and loaded; null otherwise.</returns>
    public IBackendPlugin? GetPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return null;
        }

        lock (_lock)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }
    }

    /// <summary>
    /// Gets all currently loaded and active plugin instances.
    /// </summary>
    /// <returns>A read-only collection of loaded plugin instances.</returns>
    public IReadOnlyCollection<IBackendPlugin> GetLoadedPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            return [.. _loadedPlugins.Values];
        }
    }

    /// <summary>
    /// Checks if a plugin with the specified ID is currently loaded.
    /// </summary>
    /// <param name="pluginId">The plugin ID to check.</param>
    /// <returns>True if the plugin is loaded; false otherwise.</returns>
    public bool IsPluginLoaded(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        lock (_lock)
        {
            return _loadedPlugins.ContainsKey(pluginId);
        }
    }

    /// <summary>
    /// Registers an existing plugin instance for tracking.
    /// This is used internally by lifecycle managers.
    /// </summary>
    /// <param name="plugin">The plugin instance to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when plugin is null.</exception>
    internal void RegisterLoadedPlugin(IBackendPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _loadedPlugins[plugin.Id] = plugin;
            _logger.LogDebugMessage("Registered loaded plugin {plugin.Id}");
        }
    }

    /// <summary>
    /// Unregisters a plugin from tracking without disposing it.
    /// This is used internally by lifecycle managers.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>The unregistered plugin instance, or null if not found.</returns>
    internal IBackendPlugin? UnregisterLoadedPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return null;
        }

        lock (_lock)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                _ = _loadedPlugins.Remove(pluginId);
                _logger.LogDebugMessage("Unregistered loaded plugin {pluginId}");
                return plugin;
            }
            return null;
        }
    }

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

        lock (_lock)
        {
            // Note: We don't dispose the plugins here as that's the responsibility of the lifecycle manager
            _loadedPlugins.Clear();
        }

        _logger.LogInfoMessage("PluginDiscoveryService disposed");
    }
}