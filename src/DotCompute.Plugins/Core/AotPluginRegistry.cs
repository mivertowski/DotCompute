// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Core;

/// <summary>
/// AOT-compatible plugin registry that avoids reflection-based activation.
/// Replaces the dynamic plugin system with static registration.
/// </summary>
public sealed class AotPluginRegistry : IDisposable
{
    private readonly ILogger<AotPluginRegistry> _logger;
    private readonly Dictionary<string, IBackendPlugin> _plugins;
    private readonly Dictionary<string, Func<IBackendPlugin>> _factories;
    private readonly object _lock = new();
    private bool _disposed;

    public AotPluginRegistry(ILogger<AotPluginRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _plugins = new Dictionary<string, IBackendPlugin>();
        _factories = new Dictionary<string, Func<IBackendPlugin>>();
        
        RegisterKnownPlugins();
    }

    /// <summary>
    /// Registers all known plugin types for AOT compatibility.
    /// This replaces runtime plugin discovery with compile-time registration.
    /// </summary>
    private void RegisterKnownPlugins()
    {
        _logger.LogInformation("Registering known plugins for AOT compatibility");

        // Register CPU backend
        _factories["DotCompute.Backends.CPU"] = () => 
        {
            _logger.LogDebug("Creating CPU backend plugin");
            // This would create the actual CPU plugin instance
            // For now, we'll use a placeholder that would be replaced with actual implementation
            throw new NotImplementedException("CPU backend plugin factory not yet implemented");
        };

        // Register CUDA backend (when available)
        _factories["DotCompute.Backends.CUDA"] = () =>
        {
            _logger.LogDebug("Creating CUDA backend plugin");
            throw new NotImplementedException("CUDA backend plugin factory not yet implemented");
        };

        // Register Metal backend (when available)
        _factories["DotCompute.Backends.Metal"] = () =>
        {
            _logger.LogDebug("Creating Metal backend plugin");
            throw new NotImplementedException("Metal backend plugin factory not yet implemented");
        };

        _logger.LogInformation("Registered {Count} plugin factories", _factories.Count);
    }

    /// <summary>
    /// Creates a plugin instance using static factory methods instead of reflection.
    /// </summary>
    public IBackendPlugin? CreatePlugin(string pluginTypeName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginRegistry));

        ArgumentException.ThrowIfNullOrEmpty(pluginTypeName);

        lock (_lock)
        {
            try
            {
                if (_factories.TryGetValue(pluginTypeName, out var factory))
                {
                    var plugin = factory();
                    _plugins[plugin.Id] = plugin;
                    
                    _logger.LogInformation("Successfully created plugin {Id} ({Name}) from factory", 
                        plugin.Id, plugin.Name);
                    
                    return plugin;
                }

                _logger.LogWarning("No factory found for plugin type: {Type}", pluginTypeName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create plugin {Type}", pluginTypeName);
                return null;
            }
        }
    }

    /// <summary>
    /// Gets a loaded plugin by ID.
    /// </summary>
    public IBackendPlugin? GetPlugin(string pluginId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginRegistry));

        lock (_lock)
        {
            return _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;
        }
    }

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    public IReadOnlyCollection<IBackendPlugin> GetLoadedPlugins()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginRegistry));

        lock (_lock)
        {
            return _plugins.Values.ToList();
        }
    }

    /// <summary>
    /// Lists all available plugin types that can be created.
    /// </summary>
    public IReadOnlyCollection<string> GetAvailablePluginTypes()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginRegistry));

        return _factories.Keys.ToList();
    }

    /// <summary>
    /// Unloads a plugin by ID.
    /// </summary>
    public bool UnloadPlugin(string pluginId)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginRegistry));

        ArgumentException.ThrowIfNullOrEmpty(pluginId);

        lock (_lock)
        {
            if (_plugins.TryGetValue(pluginId, out var plugin))
            {
                try
                {
                    _logger.LogInformation("Unloading plugin {Id}", pluginId);
                    plugin.Dispose();
                    _plugins.Remove(pluginId);
                    _logger.LogInformation("Successfully unloaded plugin {Id}", pluginId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unload plugin {Id}", pluginId);
                    return false;
                }
            }

            _logger.LogWarning("Plugin {Id} not found for unloading", pluginId);
            return false;
        }
    }

    /// <summary>
    /// Registers a custom plugin factory for AOT scenarios.
    /// This allows applications to register additional plugins at startup.
    /// </summary>
    public void RegisterPluginFactory(string pluginTypeName, Func<IBackendPlugin> factory)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginRegistry));

        ArgumentException.ThrowIfNullOrEmpty(pluginTypeName);
        ArgumentNullException.ThrowIfNull(factory);

        lock (_lock)
        {
            _factories[pluginTypeName] = factory;
            _logger.LogInformation("Registered custom plugin factory for {Type}", pluginTypeName);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_lock)
        {
            foreach (var plugin in _plugins.Values.ToList())
            {
                try
                {
                    plugin.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing plugin {Id}", plugin.Id);
                }
            }
            
            _plugins.Clear();
            _factories.Clear();
        }

        _logger.LogInformation("AotPluginRegistry disposed");
    }
}

/// <summary>
/// AOT-compatible plugin system that uses static registration instead of dynamic loading.
/// This is a drop-in replacement for the reflection-based PluginSystem.
/// </summary>
public sealed class AotPluginSystem : IDisposable
{
    private readonly AotPluginRegistry _registry;
    private readonly ILogger<AotPluginSystem> _logger;
    private bool _disposed;

    public AotPluginSystem(ILogger<AotPluginSystem> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var registryLogger = _logger as ILogger<AotPluginRegistry> ?? 
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AotPluginRegistry>.Instance;
        
        _registry = new AotPluginRegistry(registryLogger);
    }

    /// <summary>
    /// Loads a plugin using static factory methods instead of assembly loading.
    /// This method signature maintains compatibility with the original PluginSystem.
    /// </summary>
    public async Task<IBackendPlugin?> LoadPluginAsync(
        string assemblyPath, 
        string pluginTypeName, 
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginSystem));

        _logger.LogInformation("Loading plugin {Type} (assembly path ignored in AOT mode)", pluginTypeName);

        // In AOT mode, we ignore the assembly path and use static registration
        await Task.Yield(); // Maintain async signature for compatibility
        
        return _registry.CreatePlugin(pluginTypeName);
    }

    /// <summary>
    /// Unloads a plugin.
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AotPluginSystem));

        await Task.Yield(); // Maintain async signature for compatibility
        
        return _registry.UnloadPlugin(pluginId);
    }

    /// <summary>
    /// Gets a loaded plugin by ID.
    /// </summary>
    public IBackendPlugin? GetPlugin(string pluginId) => _registry.GetPlugin(pluginId);

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    public IEnumerable<IBackendPlugin> GetLoadedPlugins() => _registry.GetLoadedPlugins();

    /// <summary>
    /// Gets available plugin types (replaces assembly discovery).
    /// </summary>
    public IEnumerable<string> GetAvailablePluginTypes() => _registry.GetAvailablePluginTypes();

    /// <summary>
    /// Registers a custom plugin factory.
    /// </summary>
    public void RegisterPluginFactory(string pluginTypeName, Func<IBackendPlugin> factory)
    {
        _registry.RegisterPluginFactory(pluginTypeName, factory);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _registry.Dispose();
    }
}

/// <summary>
/// Static helper class for AOT plugin management.
/// </summary>
public static class AotPluginHelpers
{
    /// <summary>
    /// Determines if the current runtime supports AOT plugin loading.
    /// </summary>
    public static bool IsAotCompatible =>
#if NETCOREAPP
        !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled;
#else
        false;
#endif

    /// <summary>
    /// Creates the appropriate plugin system based on runtime capabilities.
    /// </summary>
    public static IDisposable CreatePluginSystem(ILogger logger)
    {
        if (IsAotCompatible)
        {
            return new AotPluginSystem(logger as ILogger<AotPluginSystem> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<AotPluginSystem>.Instance);
        }
        else
        {
            return new PluginSystem(logger as ILogger<PluginSystem> ?? 
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginSystem>.Instance);
        }
    }
}