// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Aot.Discovery;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Aot.Lifecycle;

/// <summary>
/// Provides plugin lifecycle management services for AOT scenarios.
/// Implements the Interface Segregation Principle by focusing solely on lifecycle operations.
/// </summary>
public sealed class PluginLifecycleManager : IPluginLifecycle
{
    private readonly ILogger<PluginLifecycleManager> _logger;
    private readonly PluginDiscoveryService _discoveryService;
    private readonly Dictionary<string, IBackendPlugin> _managedPlugins;
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLifecycleManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for this service.</param>
    /// <param name="discoveryService">The plugin discovery service for coordination.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger or discoveryService is null.</exception>
    public PluginLifecycleManager(ILogger<PluginLifecycleManager> logger, PluginDiscoveryService discoveryService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _managedPlugins = [];
        
        _logger.LogDebug("PluginLifecycleManager initialized");
    }

    /// <summary>
    /// Event raised when a plugin is successfully loaded and registered.
    /// </summary>
    public event EventHandler<PluginLifecycleEventArgs>? PluginLoaded;

    /// <summary>
    /// Event raised when a plugin is unloaded and disposed.
    /// </summary>
    public event EventHandler<PluginLifecycleEventArgs>? PluginUnloaded;

    /// <summary>
    /// Event raised when a plugin lifecycle operation encounters an error.
    /// </summary>
    public event EventHandler<PluginLifecycleErrorEventArgs>? LifecycleError;

    /// <summary>
    /// Registers a plugin instance for lifecycle management.
    /// </summary>
    /// <param name="plugin">The plugin instance to register for management.</param>
    /// <exception cref="ArgumentNullException">Thrown when plugin is null.</exception>
    public void RegisterPluginForManagement(IBackendPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            _managedPlugins[plugin.Id] = plugin;
            _discoveryService.RegisterLoadedPlugin(plugin);
            
            _logger.LogInformation("Registered plugin {PluginId} ({PluginName}) for lifecycle management",
                plugin.Id, plugin.Name);

            try
            {
                PluginLoaded?.Invoke(this, new PluginLifecycleEventArgs(plugin.Id, plugin.Name));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invoking PluginLoaded event for {PluginId}", plugin.Id);
            }
        }
    }

    /// <summary>
    /// Unloads and disposes a plugin by its unique identifier.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to unload.</param>
    /// <returns>True if the plugin was successfully unloaded; false if not found or unloading failed.</returns>
    /// <exception cref="ArgumentException">Thrown when pluginId is null, empty, or whitespace.</exception>
    public bool UnloadPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            throw new ArgumentException("Plugin ID cannot be null, empty, or whitespace.", nameof(pluginId));
        }

        lock (_lock)
        {
            if (_managedPlugins.TryGetValue(pluginId, out var plugin))
            {
                try
                {
                    _logger.LogInformation("Unloading plugin {PluginId} ({PluginName})", pluginId, plugin.Name);

                    // Remove from management tracking first
                    _ = _managedPlugins.Remove(pluginId);
                    _ = _discoveryService.UnregisterLoadedPlugin(pluginId);

                    // Dispose the plugin
                    plugin.Dispose();
                    
                    _logger.LogInformation("Successfully unloaded plugin {PluginId}", pluginId);

                    try
                    {
                        PluginUnloaded?.Invoke(this, new PluginLifecycleEventArgs(pluginId, plugin.Name));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error invoking PluginUnloaded event for {PluginId}", pluginId);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unload plugin {PluginId}", pluginId);
                    
                    try
                    {
                        LifecycleError?.Invoke(this, new PluginLifecycleErrorEventArgs(pluginId, ex, "Unload"));
                    }
                    catch (Exception eventEx)
                    {
                        _logger.LogWarning(eventEx, "Error invoking LifecycleError event for {PluginId}", pluginId);
                    }

                    return false;
                }
            }

            _logger.LogWarning("Plugin {PluginId} not found for unloading", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Attempts to unload a plugin, providing detailed information about the operation.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to unload.</param>
    /// <param name="errorMessage">Error message if unloading failed.</param>
    /// <returns>True if the plugin was successfully unloaded; false otherwise.</returns>
    public bool TryUnloadPlugin(string pluginId, out string? errorMessage)
    {
        errorMessage = null;

        if (_disposed)
        {
            errorMessage = "Lifecycle manager has been disposed";
            return false;
        }

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            errorMessage = "Plugin ID cannot be null, empty, or whitespace";
            return false;
        }

        lock (_lock)
        {
            if (!_managedPlugins.TryGetValue(pluginId, out var plugin))
            {
                errorMessage = $"Plugin {pluginId} not found in lifecycle management";
                return false;
            }

            try
            {
                _logger.LogInformation("Unloading plugin {PluginId} ({PluginName})", pluginId, plugin.Name);

                // Remove from management tracking first
                _ = _managedPlugins.Remove(pluginId);
                _ = _discoveryService.UnregisterLoadedPlugin(pluginId);

                // Dispose the plugin
                plugin.Dispose();
                
                _logger.LogInformation("Successfully unloaded plugin {PluginId}", pluginId);

                try
                {
                    PluginUnloaded?.Invoke(this, new PluginLifecycleEventArgs(pluginId, plugin.Name));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error invoking PluginUnloaded event for {PluginId}", pluginId);
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to unload plugin {pluginId}: {ex.Message}";
                _logger.LogError(ex, "Failed to unload plugin {PluginId}", pluginId);
                
                try
                {
                    LifecycleError?.Invoke(this, new PluginLifecycleErrorEventArgs(pluginId, ex, "Unload"));
                }
                catch (Exception eventEx)
                {
                    _logger.LogWarning(eventEx, "Error invoking LifecycleError event for {PluginId}", pluginId);
                }

                return false;
            }
        }
    }

    /// <summary>
    /// Unloads all currently loaded plugins.
    /// </summary>
    /// <returns>The number of plugins that were successfully unloaded.</returns>
    public int UnloadAllPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            var pluginIds = _managedPlugins.Keys.ToList();
            var unloadedCount = 0;

            _logger.LogInformation("Unloading {PluginCount} plugins", pluginIds.Count);

            foreach (var pluginId in pluginIds)
            {
                if (TryUnloadPlugin(pluginId, out _))
                {
                    unloadedCount++;
                }
            }

            _logger.LogInformation("Successfully unloaded {UnloadedCount} of {TotalCount} plugins", 
                unloadedCount, pluginIds.Count);

            return unloadedCount;
        }
    }

    /// <summary>
    /// Unregisters a plugin from lifecycle management without disposing it.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if the plugin was unregistered; false if not found.</returns>
    public bool UnregisterPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        lock (_lock)
        {
            var removed = _managedPlugins.Remove(pluginId);
            if (removed)
            {
                _ = _discoveryService.UnregisterLoadedPlugin(pluginId);
                _logger.LogInformation("Unregistered plugin {PluginId} from lifecycle management (without disposal)", pluginId);
            }
            else
            {
                _logger.LogDebug("Plugin {PluginId} not found for unregistration", pluginId);
            }
            return removed;
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
            var totalPlugins = _managedPlugins.Count;
            _logger.LogInformation("Disposing PluginLifecycleManager and unloading {PluginCount} plugins", totalPlugins);

            foreach (var plugin in _managedPlugins.Values.ToList())
            {
                try
                {
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing plugin {PluginId} during lifecycle manager disposal", plugin.Id);
                }
            }

            _managedPlugins.Clear();
        }

        _logger.LogInformation("PluginLifecycleManager disposed");
    }
}