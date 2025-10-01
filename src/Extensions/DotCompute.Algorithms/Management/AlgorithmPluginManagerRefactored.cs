// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Management.Infrastructure;
using DotCompute.Algorithms.Management.Services;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Main facade for algorithm plugin management, orchestrating all plugin-related operations.
/// This class delegates to specialized components for discovery, loading, validation, caching,
/// lifecycle management, metrics, and orchestration while maintaining a clean, unified API.
/// </summary>
public sealed partial class AlgorithmPluginManagerRefactored : IAsyncDisposable
{
    private readonly ILogger<AlgorithmPluginManagerRefactored> _logger;
    private readonly IAccelerator _accelerator;
    private readonly AlgorithmPluginManagerOptions _options;

    // Core components - focused responsibilities
    private readonly AlgorithmRegistry _registry;
    private readonly AlgorithmLoader _loader;
    private readonly AlgorithmPluginValidator _validator;
    private readonly AlgorithmLifecycleManager _lifecycleManager;
    private readonly AlgorithmDependencyResolver _dependencyResolver;
    private readonly AlgorithmMetadata _metadataManager;

    // Infrastructure components
    private readonly AlgorithmPluginDiscovery _discovery;
    private readonly AlgorithmPluginCache _cache;

    // Service components
    private readonly AlgorithmPluginOrchestrator _orchestrator;
    private readonly AlgorithmPluginMetrics _metrics;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginManagerRefactored"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for plugins.</param>
    /// <param name="logger">The logger instance.</param>
    public AlgorithmPluginManagerRefactored(IAccelerator accelerator, ILogger<AlgorithmPluginManagerRefactored> logger)
        : this(accelerator, logger, new AlgorithmPluginManagerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginManagerRefactored"/> class with options.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for plugins.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options for the plugin manager.</param>
    public AlgorithmPluginManagerRefactored(IAccelerator accelerator, ILogger<AlgorithmPluginManagerRefactored> logger, AlgorithmPluginManagerOptions options)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize core components with proper logger instances
        _registry = new AlgorithmRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmRegistry>.Instance);
        _loader = new AlgorithmLoader(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmLoader>.Instance, _options);
        _validator = new AlgorithmPluginValidator(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginValidator>.Instance, _options);
        _lifecycleManager = new AlgorithmLifecycleManager(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmLifecycleManager>.Instance, _options, _registry);
        _dependencyResolver = new AlgorithmDependencyResolver(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmDependencyResolver>.Instance, _options, _registry);
        _metadataManager = new AlgorithmMetadata(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmMetadata>.Instance);

        // Initialize infrastructure components
        _discovery = new AlgorithmPluginDiscovery(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginDiscovery>.Instance, _options);
        _cache = new AlgorithmPluginCache(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginCache>.Instance, _options);

        // Initialize service components
        _metrics = new AlgorithmPluginMetrics(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginMetrics>.Instance, _options, new AlgorithmPluginRegistry(Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginRegistry>.Instance));
        _orchestrator = new AlgorithmPluginOrchestrator(_accelerator, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginOrchestrator>.Instance, _options);

        LogManagerInitialized();
    }

    /// <summary>
    /// Gets the registered plugin IDs.
    /// </summary>
    public IEnumerable<string> RegisteredPlugins => _registry.RegisteredPlugins;

    /// <summary>
    /// Discovers plugins in the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins discovered and loaded.</returns>
    public async Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        LogDiscoveringPlugins(pluginDirectory);

        try
        {
            var discoveredPaths = await _discovery.DiscoverPluginsAsync(pluginDirectory, cancellationToken).ConfigureAwait(false);
            var totalLoaded = 0;

            foreach (var assemblyPath in discoveredPaths)
            {
                try
                {
                    var loadedCount = await LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
                    totalLoaded += loadedCount;
                }
                catch (Exception ex)
                {
                    LogPluginLoadFailed(assemblyPath, ex.Message);
                }
            }

            LogPluginsDiscovered(totalLoaded, pluginDirectory);
            return totalLoaded;
        }
        catch (Exception ex)
        {
            LogDiscoveryFailed(pluginDirectory, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Loads plugins from an assembly file.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    public async Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        try
        {
            // Security validation (simplified check since ValidateAssemblySecurityAsync doesn't exist)
            if (_options.EnableSecurityValidation && !File.Exists(assemblyPath))
            {
                LogSecurityValidationFailed(assemblyPath);
                return 0;
            }

            // Load plugins from assembly
            var loadedPlugins = await _loader.LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            var loadedCount = 0;

            foreach (var loadedPlugin in loadedPlugins)
            {
                // Register with registry
                if (_registry.RegisterPlugin(loadedPlugin))
                {
                    try
                    {
                        // Initialize plugin
                        await _lifecycleManager.InitializePluginAsync(loadedPlugin.Plugin.Id, _accelerator, cancellationToken).ConfigureAwait(false);
                        loadedCount++;
                    }
                    catch (Exception ex)
                    {
                        // Remove from registry if initialization failed
                        _registry.UnregisterPlugin(loadedPlugin.Plugin.Id);
                        LogPluginInitializationFailed(loadedPlugin.Plugin.Id, ex.Message);
                    }
                }
            }

            return loadedCount;
        }
        catch (Exception ex)
        {
            LogAssemblyLoadFailed(assemblyPath, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Registers a plugin instance.
    /// </summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RegisterPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(plugin);

        // Create metadata for external plugin
        var metadata = _metadataManager.CreateDefaultMetadata(plugin.GetType().Assembly.Location, plugin.GetType());

        // Create loaded plugin info
        var loadedPlugin = new LoadedPluginInfo
        {
            Plugin = plugin,
            Metadata = metadata,
            LoadContext = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly) as DotCompute.Algorithms.Management.Loading.PluginAssemblyLoadContext
                ?? throw new InvalidOperationException("Unable to get load context for plugin assembly"),
            Assembly = plugin.GetType().Assembly,
            AssemblyLocation = plugin.GetType().Assembly.Location,
            LoadContextName = $"External_{plugin.Id}",
            State = PluginState.Loaded,
            Health = PluginHealth.Unknown,
            LoadTime = DateTime.UtcNow
        };

        // Register and initialize
        if (_registry.RegisterPlugin(loadedPlugin))
        {
            await _lifecycleManager.InitializePluginAsync(plugin.Id, _accelerator, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets a registered plugin by ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin instance if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetPlugin(pluginId);
    }

    /// <summary>
    /// Gets detailed information about a loaded plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The loaded plugin information if found; otherwise, null.</returns>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetLoadedPluginInfo(pluginId);
    }

    /// <summary>
    /// Gets all plugins that support the specified accelerator type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetPluginsByAcceleratorType(acceleratorType);
    }

    /// <summary>
    /// Gets all plugins that can process the specified input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetPluginsByInputType(inputType);
    }

    /// <summary>
    /// Gets all healthy and running plugins.
    /// </summary>
    /// <returns>Collection of healthy plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetHealthyPlugins();
    }

    /// <summary>
    /// Executes a plugin with the specified inputs.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _lifecycleManager.ExecutePluginAsync(pluginId, inputs, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unregisters a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if the plugin was unregistered; otherwise, false.</returns>
    public async Task<bool> UnregisterPluginAsync(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await _lifecycleManager.DisposePluginAsync(pluginId).ConfigureAwait(false);
            return _registry.UnregisterPlugin(pluginId);
        }
        catch (Exception ex)
        {
            LogPluginUnloadFailed(pluginId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Reloads a plugin (hot reload).
    /// </summary>
    /// <param name="pluginId">The plugin ID to reload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the plugin was reloaded successfully; otherwise, false.</returns>
    public async Task<bool> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _lifecycleManager.ReloadPluginAsync(pluginId, _accelerator, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets information about all registered plugins.
    /// </summary>
    /// <returns>Collection of plugin information.</returns>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _registry.GetPluginInfo();
    }

    /// <summary>
    /// Gets comprehensive metrics for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>Plugin metrics if available; otherwise, null.</returns>
    public PluginMetrics? GetPluginMetrics(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _metrics.GetPluginMetrics(pluginId);
    }

    /// <summary>
    /// Gets system-wide metrics summary.
    /// </summary>
    /// <returns>System metrics summary.</returns>
    public SystemMetrics GetSystemMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _metrics.GetSystemMetrics();
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics.</returns>
    public CacheStatistics GetCacheStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _cache.GetStatistics();
    }

    /// <summary>
    /// Resolves the best plugin for the given requirements.
    /// </summary>
    /// <param name="requirements">The plugin requirements.</param>
    /// <returns>The best matching plugin if found; otherwise, null.</returns>
    public IAlgorithmPlugin? ResolvePlugin(PluginRequirements requirements)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _dependencyResolver.ResolvePlugin(requirements);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            try
            {
                // Dispose all plugins first
                foreach (var pluginId in _registry.RegisteredPlugins.ToList())
                {
                    try
                    {
                        await _lifecycleManager.DisposePluginAsync(pluginId).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogPluginDisposeFailed(pluginId, ex.Message);
                    }
                }

                // Dispose service components
                _metrics?.Dispose();
                _orchestrator?.Dispose();

                // Dispose infrastructure components
                _cache?.Dispose();
                _discovery?.Dispose();

                // Dispose core components
                _metadataManager?.Dispose();
                _dependencyResolver?.Dispose();
                _lifecycleManager?.Dispose();
                _validator?.Dispose();
                _loader?.Dispose();
                _registry?.Dispose();

                LogManagerDisposed();
            }
            catch (Exception ex)
            {
                LogDisposalFailed(ex.Message);
            }
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Algorithm plugin manager initialized")]
    private partial void LogManagerInitialized();

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovering plugins in directory: {Directory}")]
    private partial void LogDiscoveringPlugins(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered and loaded {Count} plugins from directory: {Directory}")]
    private partial void LogPluginsDiscovered(int count, string directory);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to discover plugins in directory {Directory}: {Reason}")]
    private partial void LogDiscoveryFailed(string directory, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load plugin from {AssemblyPath}: {Reason}")]
    private partial void LogPluginLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation failed for {AssemblyPath}")]
    private partial void LogSecurityValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin initialization failed for {PluginId}: {Reason}")]
    private partial void LogPluginInitializationFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load assembly {AssemblyPath}: {Reason}")]
    private partial void LogAssemblyLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin unload failed for {PluginId}: {Reason}")]
    private partial void LogPluginUnloadFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin dispose failed for {PluginId}: {Reason}")]
    private partial void LogPluginDisposeFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Algorithm plugin manager disposed")]
    private partial void LogManagerDisposed();

    [LoggerMessage(Level = LogLevel.Error, Message = "Manager disposal failed: {Reason}")]
    private partial void LogDisposalFailed(string reason);

    #endregion
}