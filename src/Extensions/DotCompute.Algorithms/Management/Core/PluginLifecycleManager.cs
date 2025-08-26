// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using global::System.Runtime.Loader;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Types.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using DotCompute.Algorithms.Types.Loading;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Service responsible for managing plugin lifecycle operations.
/// </summary>
public sealed partial class PluginLifecycleManager : IPluginLifecycleManager
{
    private readonly ILogger<PluginLifecycleManager> _logger;
    private readonly IAccelerator _accelerator;
    private readonly IHotReloadService? _hotReloadService;
    private readonly ConcurrentDictionary<string, LoadedPlugin> _plugins = new();
    private readonly ConcurrentDictionary<string, PluginAssemblyLoadContext> _loadContexts = new();

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginLifecycleManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="accelerator">The accelerator to use for plugins.</param>
    /// <param name="hotReloadService">Optional hot reload service.</param>
    public PluginLifecycleManager(
        ILogger<PluginLifecycleManager> logger,
        IAccelerator accelerator,
        IHotReloadService? hotReloadService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _hotReloadService = hotReloadService;
    }

    /// <inheritdoc/>
    public IEnumerable<string> RegisteredPlugins => _plugins.Keys;

    /// <inheritdoc/>
    public async Task RegisterPluginAsync(
        IAlgorithmPlugin plugin,
        PluginAssemblyLoadContext loadContext,
        Assembly assembly,
        PluginMetadata metadata,
        CancellationToken cancellationToken = default)
    {
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

        if (!_plugins.TryAdd(plugin.Id, loadedPlugin))
        {
            LogPluginAlreadyRegistered(plugin.Id);
            throw new InvalidOperationException($"Plugin with ID '{plugin.Id}' is already registered.");
        }

        // Store load context
        var assemblyName = assembly.GetName().Name ?? "Unknown";
        if (!_loadContexts.TryAdd(assemblyName, loadContext))
        {
            // If context already exists, clean up the new one
            loadContext.Unload();
        }

        try
        {
            // Initialize plugin
            loadedPlugin.State = PluginState.Initializing;
            await plugin.InitializeAsync(_accelerator, cancellationToken).ConfigureAwait(false);

            loadedPlugin.State = PluginState.Running;
            loadedPlugin.Health = PluginHealth.Healthy;

            LogPluginRegistered(plugin.Id, plugin.Name);
        }
        catch (Exception ex)
        {
            loadedPlugin.State = PluginState.Failed;
            loadedPlugin.Health = PluginHealth.Critical;
            loadedPlugin.LastError = ex;

            // Remove from plugins collection on initialization failure
            _plugins.TryRemove(plugin.Id, out _);

            LogPluginInitializationFailed(plugin.Id, ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RegisterPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var metadata = new PluginMetadata
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Version = plugin.Version.ToString(),
            Description = plugin.Description,
            Author = "External",
            AssemblyPath = plugin.GetType().Assembly.Location,
            LoadTime = DateTime.UtcNow
        };

        // Use default load context for external plugins
        var loadContext = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly) as PluginAssemblyLoadContext
                          ?? new PluginAssemblyLoadContext($"External_{plugin.Id}", plugin.GetType().Assembly.Location, false);

        await RegisterPluginAsync(plugin, loadContext, plugin.GetType().Assembly, metadata, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> UnregisterPluginAsync(string pluginId)
    {
        if (_plugins.TryRemove(pluginId, out var loadedPlugin))
        {
            try
            {
                // Update state
                loadedPlugin.State = PluginState.Stopping;

                // Stop hot reload watching if active
                _hotReloadService?.StopHotReload(loadedPlugin.Metadata.AssemblyPath);

                // Dispose the plugin
                await loadedPlugin.Plugin.DisposeAsync().ConfigureAwait(false);

                loadedPlugin.State = PluginState.Unloaded;

                LogPluginUnregistered(pluginId);
                return true;
            }
            catch (Exception ex)
            {
                loadedPlugin.State = PluginState.Failed;
                loadedPlugin.LastError = ex;
                LogPluginUnloadFailed(pluginId, ex.Message);
                return false;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            return false;
        }

        LogPluginReloading(pluginId);

        try
        {
            // Store assembly path
            var assemblyPath = loadedPlugin.Metadata.AssemblyPath;

            // Unregister current plugin
            await UnregisterPluginAsync(pluginId).ConfigureAwait(false);

            // Small delay to allow for file system events
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            // Note: For reload to work, we'd need access to the discovery service
            // This would typically be handled by the main manager that orchestrates all services

            LogPluginReloaded(pluginId, true);
            return true;
        }
        catch (Exception ex)
        {
            LogPluginReloadFailed(pluginId, ex.Message);
            return false;
        }
    }

    /// <inheritdoc/>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        return _plugins.TryGetValue(pluginId, out var loadedPlugin) ? loadedPlugin.Plugin : null;
    }

    /// <inheritdoc/>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId)
    {
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

    /// <inheritdoc/>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo()
    {
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

    /// <inheritdoc/>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType)
    {
        return _plugins.Values
            .Where(lp => lp.Health != PluginHealth.Critical && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin)
            .Where(p => p.SupportedAccelerators.Contains(acceleratorType));
    }

    /// <inheritdoc/>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType)
    {
        ArgumentNullException.ThrowIfNull(inputType);

        return _plugins.Values
            .Where(lp => lp.Health != PluginHealth.Critical && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin)
            .Where(p => p.InputTypes.Contains(inputType));
    }

    /// <inheritdoc/>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins()
    {
        return _plugins.Values
            .Where(lp => lp.Health == PluginHealth.Healthy && lp.State == PluginState.Running)
            .Select(lp => lp.Plugin);
    }

    /// <summary>
    /// Updates plugin execution statistics.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="executionTime">The execution time.</param>
    /// <param name="error">Any error that occurred.</param>
    public void UpdateExecutionStatistics(string pluginId, TimeSpan executionTime, Exception? error = null)
    {
        if (_plugins.TryGetValue(pluginId, out var loadedPlugin))
        {
            loadedPlugin.ExecutionCount++;
            loadedPlugin.LastExecution = DateTime.UtcNow;
            loadedPlugin.TotalExecutionTime += executionTime;
            loadedPlugin.LastError = error;

            if (error == null)
            {
                loadedPlugin.Health = PluginHealth.Healthy;
            }
            else
            {
                // Determine health impact based on error
                if (error is OutOfMemoryException or StackOverflowException)
                {
                    loadedPlugin.Health = PluginHealth.Critical;
                    loadedPlugin.State = PluginState.Failed;
                }
                else
                {
                    loadedPlugin.Health = PluginHealth.Degraded;
                }
            }
        }
    }

    /// <summary>
    /// Gets all loaded plugins for health monitoring.
    /// </summary>
    /// <returns>Collection of loaded plugins.</returns>
    internal IReadOnlyCollection<LoadedPlugin> GetAllLoadedPlugins()
    {
        return _plugins.Values.ToList().AsReadOnly();
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered algorithm plugin {PluginId} ({PluginName})")]
    private partial void LogPluginRegistered(string pluginId, string pluginName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Algorithm plugin {PluginId} already registered")]
    private partial void LogPluginAlreadyRegistered(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin initialization failed for {PluginId}: {Reason}")]
    private partial void LogPluginInitializationFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unregistered algorithm plugin {PluginId}")]
    private partial void LogPluginUnregistered(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin unload failed for {PluginId}: {Reason}")]
    private partial void LogPluginUnloadFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloading plugin: {PluginId}")]
    private partial void LogPluginReloading(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin reload completed for {PluginId}, success: {Success}")]
    private partial void LogPluginReloaded(string pluginId, bool success);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin reload failed for {PluginId}: {Reason}")]
    private partial void LogPluginReloadFailed(string pluginId, string reason);

    #endregion
}