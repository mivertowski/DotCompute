// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Types.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Core registry for managing algorithm plugin registration and lookup.
/// Handles plugin collection, metadata storage, and state management.
/// </summary>
public sealed class AlgorithmPluginRegistry : IDisposable
{
    private readonly ILogger<AlgorithmPluginRegistry> _logger;
    private readonly ConcurrentDictionary<string, LoadedPlugin> _plugins = new();
    private bool _disposed;

    /// <summary>
    /// Represents a loaded plugin with its context and metadata.
    /// </summary>
    public sealed class LoadedPlugin
    {
        public required IAlgorithmPlugin Plugin { get; init; }
        public required PluginAssemblyLoadContext LoadContext { get; init; }
        public required Assembly Assembly { get; init; }
        public required PluginMetadata Metadata { get; init; }
        public required DateTime LoadTime { get; init; }
        public PluginState State { get; set; } = PluginState.Loaded;
        public PluginHealth Health { get; set; } = PluginHealth.Unknown;
        public long ExecutionCount { get; set; }
        public DateTime LastExecution { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public Exception? LastError { get; set; }
    }

    public AlgorithmPluginRegistry(ILogger<AlgorithmPluginRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the registered plugin IDs.
    /// </summary>
    public IEnumerable<string> RegisteredPlugins => _plugins.Keys;

    /// <summary>
    /// Gets the number of registered plugins.
    /// </summary>
    public int Count => _plugins.Count;

    /// <summary>
    /// Registers a plugin in the registry.
    /// </summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <param name="loadContext">The assembly load context.</param>
    /// <param name="assembly">The plugin assembly.</param>
    /// <param name="metadata">The plugin metadata.</param>
    /// <returns>True if registered successfully; otherwise, false.</returns>
    public bool RegisterPlugin(
        IAlgorithmPlugin plugin,
        PluginAssemblyLoadContext loadContext,
        Assembly assembly,
        PluginMetadata metadata)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(loadContext);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(metadata);

        var loadedPlugin = new LoadedPlugin
        {
            Plugin = plugin,
            LoadContext = loadContext,
            Assembly = assembly,
            Metadata = metadata,
            LoadTime = DateTime.UtcNow,
            State = PluginState.Loaded,
            Health = PluginHealth.Unknown
        };

        if (_plugins.TryAdd(plugin.Id, loadedPlugin))
        {
            LogPluginRegistered(plugin.Id, plugin.Name);
            return true;
        }

        LogPluginAlreadyRegistered(plugin.Id);
        return false;
    }

    /// <summary>
    /// Unregisters a plugin from the registry.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>The unregistered plugin if found; otherwise, null.</returns>
    public LoadedPlugin? UnregisterPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (_plugins.TryRemove(pluginId, out var loadedPlugin))
        {
            LogPluginUnregistered(pluginId);
            return loadedPlugin;
        }

        return null;
    }

    /// <summary>
    /// Gets a registered plugin by ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin instance if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.TryGetValue(pluginId, out var loadedPlugin) ? loadedPlugin.Plugin : null;
    }

    /// <summary>
    /// Gets detailed information about a loaded plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The loaded plugin information if found; otherwise, null.</returns>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            return null;
        }

        return new LoadedPluginInfo
        {
            Plugin = loadedPlugin.Plugin,
            Metadata = loadedPlugin.Metadata,
            State = loadedPlugin.State,
            Health = loadedPlugin.Health,
            LoadTime = loadedPlugin.LoadTime,
            ExecutionCount = loadedPlugin.ExecutionCount,
            LastExecution = loadedPlugin.LastExecution,
            TotalExecutionTime = loadedPlugin.TotalExecutionTime,
            LastError = loadedPlugin.LastError,
            AssemblyLocation = loadedPlugin.Assembly.Location,
            LoadContextName = loadedPlugin.LoadContext.Name ?? "Unknown"
        };
    }

    /// <summary>
    /// Gets the internal loaded plugin data.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The loaded plugin if found; otherwise, null.</returns>
    internal LoadedPlugin? GetLoadedPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.TryGetValue(pluginId, out var loadedPlugin) ? loadedPlugin : null;
    }

    /// <summary>
    /// Gets all loaded plugins.
    /// </summary>
    /// <returns>Collection of all loaded plugins.</returns>
    internal IEnumerable<LoadedPlugin> GetAllLoadedPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values.ToList();
    }

    /// <summary>
    /// Gets all plugins that support the specified accelerator type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values
            .Where(lp => lp.Health != PluginHealth.Critical && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin)
            .Where(p => p.SupportedAccelerators.Contains(acceleratorType));
    }

    /// <summary>
    /// Gets all plugins that can process the specified input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType)
    {
        ArgumentNullException.ThrowIfNull(inputType);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _plugins.Values
            .Where(lp => lp.Health != PluginHealth.Critical && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin)
            .Where(p => p.InputTypes.Contains(inputType));
    }

    /// <summary>
    /// Gets all healthy and running plugins.
    /// </summary>
    /// <returns>Collection of healthy plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values
            .Where(lp => lp.Health == PluginHealth.Healthy && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin);
    }

    /// <summary>
    /// Gets information about all registered plugins.
    /// </summary>
    /// <returns>Collection of plugin information.</returns>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _plugins.Values.Select(lp => new AlgorithmPluginInfo
        {
            Id = lp.Plugin.Id,
            Name = lp.Plugin.Name,
            Version = lp.Plugin.Version,
            Description = lp.Plugin.Description,
            SupportedAccelerators = lp.Plugin.SupportedAccelerators,
            InputTypes = [.. lp.Plugin.InputTypes.Select(t => t.FullName ?? t.Name)],
            OutputType = lp.Plugin.OutputType.FullName ?? lp.Plugin.OutputType.Name,
            PerformanceProfile = lp.Plugin.GetPerformanceProfile()
        });
    }

    /// <summary>
    /// Checks if a plugin is registered.
    /// </summary>
    /// <param name="pluginId">The plugin ID to check.</param>
    /// <returns>True if the plugin is registered; otherwise, false.</returns>
    public bool IsPluginRegistered(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return !string.IsNullOrEmpty(pluginId) && _plugins.ContainsKey(pluginId);
    }

    /// <summary>
    /// Updates the execution statistics for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="executionTime">The execution time to add.</param>
    /// <param name="error">Optional error to record.</param>
    public void UpdateExecutionStats(string pluginId, TimeSpan executionTime, Exception? error = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            loadedPlugin.ExecutionCount++;
            loadedPlugin.LastExecution = DateTime.UtcNow;
            loadedPlugin.TotalExecutionTime += executionTime;

            if (error != null)
            {
                loadedPlugin.LastError = error;
                loadedPlugin.Health = error is OutOfMemoryException or StackOverflowException
                    ? PluginHealth.Critical
                    : PluginHealth.Degraded;
            }
            else
            {
                loadedPlugin.Health = PluginHealth.Healthy;
                loadedPlugin.LastError = null;
            }
        }
    }

    /// <summary>
    /// Updates the health status of a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="health">The new health status.</param>
    public void UpdatePluginHealth(string pluginId, PluginHealth health)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            var oldHealth = loadedPlugin.Health;
            loadedPlugin.Health = health;

            if (oldHealth != health)
            {
                LogPluginHealthChanged(pluginId, oldHealth, health);
            }
        }
    }

    /// <summary>
    /// Updates the state of a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="state">The new state.</param>
    public void UpdatePluginState(string pluginId, PluginState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            loadedPlugin.State = state;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _plugins.Clear();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered algorithm plugin {PluginId} ({PluginName})")]
    private partial void LogPluginRegistered(string pluginId, string pluginName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Algorithm plugin {PluginId} already registered")]
    private partial void LogPluginAlreadyRegistered(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unregistered algorithm plugin {PluginId}")]
    private partial void LogPluginUnregistered(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin health changed for {PluginId}: {OldHealth} -> {NewHealth}")]
    private partial void LogPluginHealthChanged(string pluginId, PluginHealth oldHealth, PluginHealth newHealth);
}