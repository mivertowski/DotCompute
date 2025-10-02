// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Manages registration and discovery of algorithm plugins.
/// </summary>
public sealed partial class AlgorithmRegistry(ILogger<AlgorithmRegistry> logger) : IDisposable
{
    private readonly ILogger<AlgorithmRegistry> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, LoadedPluginInfo> _plugins = new();
    private bool _disposed;

    /// <summary>
    /// Gets the registered plugin IDs.
    /// </summary>
    public IEnumerable<string> RegisteredPlugins => _plugins.Keys;

    /// <summary>
    /// Registers a plugin with the registry.
    /// </summary>
    /// <param name="loadedPlugin">The loaded plugin information.</param>
    /// <returns>True if registered successfully; otherwise, false.</returns>
    public bool RegisterPlugin(LoadedPluginInfo loadedPlugin)
    {
        ArgumentNullException.ThrowIfNull(loadedPlugin);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_plugins.TryAdd(loadedPlugin.Plugin.Id, loadedPlugin))
        {
            LogPluginRegistered(loadedPlugin.Plugin.Id, loadedPlugin.Plugin.Name);
            return true;
        }

        LogPluginAlreadyRegistered(loadedPlugin.Plugin.Id);
        return false;
    }

    /// <summary>
    /// Unregisters a plugin from the registry.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if unregistered successfully; otherwise, false.</returns>
    public bool UnregisterPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_plugins.TryRemove(pluginId, out var loadedPlugin))
        {
            LogPluginUnregistered(pluginId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets a registered plugin by ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin instance if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
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
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _plugins.TryGetValue(pluginId, out var loadedPlugin) ? loadedPlugin : null;
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
            .Where(lp => lp.Plugin.SupportedAcceleratorTypes.Contains(acceleratorType))
            .Select(lp => lp.Plugin);
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
            .Where(lp => lp.Plugin.InputTypes.Contains(inputType))
            .Select(lp => lp.Plugin);
    }

    /// <summary>
    /// Gets all healthy and running plugins.
    /// </summary>
    /// <returns>Collection of healthy plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _plugins.Values
            .Where(lp => lp.State == PluginState.Running &&
                        (lp.Health == PluginHealth.Healthy || lp.Health == PluginHealth.Degraded))
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
            SupportedAccelerators = [.. lp.Plugin.SupportedAcceleratorTypes],
            InputTypes = [.. lp.Plugin.InputTypes.Select(t => t.Name)],
            OutputType = lp.Plugin.OutputType.Name,
            PerformanceProfile = lp.Plugin.GetPerformanceProfile(),
            LoadTime = lp.LoadTime,
            State = lp.State,
            Health = lp.Health,
            ExecutionCount = lp.ExecutionCount,
            LastExecution = lp.LastExecution,
            TotalExecutionTime = lp.TotalExecutionTime,
            LastError = lp.LastError?.Message
        });
    }

    /// <summary>
    /// Updates plugin runtime statistics.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="executionTime">The execution time to add.</param>
    /// <param name="error">Optional error that occurred.</param>
    public void UpdatePluginStats(string pluginId, TimeSpan executionTime, Exception? error = null)
    {
        if (_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            loadedPlugin.ExecutionCount++;
            loadedPlugin.LastExecution = DateTime.UtcNow;
            loadedPlugin.TotalExecutionTime += executionTime;
            loadedPlugin.LastError = error;

            if (error != null)
            {
                loadedPlugin.Health = PluginHealth.Degraded;
            }
            else if (loadedPlugin.Health == PluginHealth.Degraded && loadedPlugin.ExecutionCount % 10 == 0)
            {
                // Consider recovery after successful executions
                loadedPlugin.Health = PluginHealth.Healthy;
            }
        }
    }

    /// <summary>
    /// Updates plugin health status.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="health">The new health status.</param>
    public void UpdatePluginHealth(string pluginId, PluginHealth health)
    {
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
    /// Updates plugin state.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="state">The new state.</param>
    public void UpdatePluginState(string pluginId, PluginState state)
    {
        if (_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            var oldState = loadedPlugin.State;
            loadedPlugin.State = state;

            if (oldState != state)
            {
                LogPluginStateChanged(pluginId, oldState, state);
            }
        }
    }

    /// <summary>
    /// Gets all loaded plugin information.
    /// </summary>
    /// <returns>Collection of all loaded plugin information.</returns>
    public IEnumerable<LoadedPluginInfo> GetAllLoadedPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values.ToList();
    }

    /// <summary>
    /// Gets plugin count by state.
    /// </summary>
    /// <param name="state">The plugin state to count.</param>
    /// <returns>Number of plugins in the specified state.</returns>
    public int GetPluginCountByState(PluginState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values.Count(lp => lp.State == state);
    }

    /// <summary>
    /// Gets plugin count by health status.
    /// </summary>
    /// <param name="health">The health status to count.</param>
    /// <returns>Number of plugins with the specified health status.</returns>
    public int GetPluginCountByHealth(PluginHealth health)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _plugins.Values.Count(lp => lp.Health == health);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _plugins.Clear();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered algorithm plugin {PluginId} ({PluginName})")]
    private partial void LogPluginRegistered(string pluginId, string pluginName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Algorithm plugin {PluginId} already registered")]
    private partial void LogPluginAlreadyRegistered(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unregistered algorithm plugin {PluginId}")]
    private partial void LogPluginUnregistered(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin health changed for {PluginId}: {OldHealth} -> {NewHealth}")]
    private partial void LogPluginHealthChanged(string pluginId, PluginHealth oldHealth, PluginHealth newHealth);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin state changed for {PluginId}: {OldState} -> {NewState}")]
    private partial void LogPluginStateChanged(string pluginId, PluginState oldState, PluginState newState);

    #endregion
}