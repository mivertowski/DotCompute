// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using global::System.Runtime.Loader;
using System.Net.Sockets;
using System.Net.Http;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Management.Infrastructure;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Services;

/// <summary>
/// High-level orchestrator for algorithm plugin management operations.
/// Coordinates discovery, loading, validation, registration, and execution of plugins.
/// </summary>
public sealed partial class AlgorithmPluginOrchestrator : IAsyncDisposable
{
    private readonly ILogger<AlgorithmPluginOrchestrator> _logger;
    private readonly IAccelerator _accelerator;
    private readonly AlgorithmPluginManagerOptions _options;

    // Core components
    private readonly AlgorithmPluginRegistry _registry;
    private readonly AlgorithmPluginValidator _validator;
    private readonly AlgorithmPluginLoader _loader;
    private readonly AlgorithmPluginDiscovery _discovery;

    // Health monitoring
    private readonly Timer _healthCheckTimer;
    private bool _disposed;

    public AlgorithmPluginOrchestrator(
        IAccelerator accelerator,
        ILogger<AlgorithmPluginOrchestrator> logger,
        AlgorithmPluginManagerOptions? options = null)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new AlgorithmPluginManagerOptions();

        // Initialize components
        var registryLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginRegistry>.Instance;
        _registry = new AlgorithmPluginRegistry(registryLogger);

        var validatorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginValidator>.Instance;
        _validator = new AlgorithmPluginValidator(validatorLogger, _options);

        var loaderLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginLoader>.Instance;
        _loader = new AlgorithmPluginLoader(loaderLogger, _options);

        var discoveryLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginDiscovery>.Instance;
        _discovery = new AlgorithmPluginDiscovery(discoveryLogger, _options);

        // Setup event handlers
        _discovery.PluginFileChanged += OnPluginFileChanged;
        // _discovery.WatcherError += OnWatcherError; // Event does not exist

        // Initialize health check timer if enabled
        if (_options.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(PerformHealthChecksWrapper, null,
                _options.HealthCheckInterval, _options.HealthCheckInterval);
        }
        else
        {
            _healthCheckTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Gets the registered plugin IDs.
    /// </summary>
    public IEnumerable<string> RegisteredPlugins => _registry.RegisteredPlugins;

    /// <summary>
    /// Discovers and loads plugins from the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins successfully loaded.</returns>
    public async Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        LogDiscoveringPlugins(pluginDirectory);

        try
        {
            // Discover plugin files
            var pluginFiles = await _discovery.DiscoverPluginsAsync(pluginDirectory, cancellationToken).ConfigureAwait(false);

            var totalLoaded = 0;
            foreach (var pluginFile in pluginFiles)
            {
                try
                {
                    var count = await LoadPluginsFromAssemblyAsync(pluginFile, cancellationToken).ConfigureAwait(false);
                    totalLoaded += count;
                }
                catch (Exception ex)
                {
                    LogPluginLoadFailed(pluginFile, ex.Message);
                }
            }

            LogPluginsDiscovered(totalLoaded, pluginDirectory);
            return totalLoaded;
        }
        catch (Exception ex)
        {
            LogDiscoveryFailed(pluginDirectory, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Loads plugins from an assembly file with comprehensive validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins successfully loaded.</returns>
    public async Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");
        }

        try
        {
            LogLoadingAssembly(assemblyPath);

            // Step 1: Security validation
            if (_options.EnableSecurityValidation && !await _validator.ValidateAssemblySecurityAsync(assemblyPath))
            {
                LogSecurityValidationFailed(assemblyPath);
                return 0;
            }

            // Step 2: Load assembly and discover plugin types
            var loadResult = await _loader.LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            if (loadResult == null || loadResult.Count == 0)
            {
                LogAssemblyLoadFailed(assemblyPath, "No plugins loaded from assembly");
                return 0;
            }

            // Step 3: Instantiate and register plugins
            var loadedCount = 0;
            foreach (var pluginResult in loadResult)
            {
                try
                {
                    if (pluginResult.Plugin != null)
                    {
                        await RegisterPluginAsync(pluginResult.Plugin, pluginResult.LoadContext, pluginResult.Assembly, pluginResult.Metadata, cancellationToken).ConfigureAwait(false);
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    var typeName = pluginResult.Plugin?.GetType().FullName ?? pluginResult.Assembly.FullName ?? "Unknown";
                    LogPluginRegistrationFailed(typeName, ex.Message);
                }
            }

            LogAssemblyLoaded(loadedCount, Path.GetFileNameWithoutExtension(assemblyPath));
            return loadedCount;
        }
        catch (Exception ex)
        {
            LogAssemblyLoadFailed(assemblyPath, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Loads plugins from a NuGet package.
    /// </summary>
    /// <param name="packageSource">The path to the .nupkg file or package ID.</param>
    /// <param name="targetFramework">Target framework for assembly selection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins successfully loaded.</returns>
    public async Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);

        LogLoadingFromNuGetPackage(packageSource);

        try
        {
            var loadResult = await _loader.LoadPluginsFromNuGetPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);
            if (loadResult == null || loadResult.Count == 0)
            {
                LogNuGetPackageLoadFailed(packageSource, "No plugins loaded from NuGet package");
                return 0;
            }

            var loadedCount = 0;
            foreach (var pluginResult in loadResult)
            {
                try
                {
                    if (pluginResult.Plugin != null)
                    {
                        await RegisterPluginAsync(pluginResult.Plugin, pluginResult.LoadContext, pluginResult.Assembly, pluginResult.Metadata, cancellationToken).ConfigureAwait(false);
                        loadedCount++;
                    }
                }
                catch (Exception ex)
                {
                    var typeName = pluginResult.Plugin?.GetType().FullName ?? pluginResult.Assembly.FullName ?? "Unknown";
                    LogPluginRegistrationFailed(typeName, ex.Message);
                }
            }

            // LogNuGetPackageLoaded("Unknown", "Unknown", loadedCount); // Package info not available from LoadResult
            LogPluginsDiscovered(loadedCount, packageSource);
            return loadedCount;
        }
        catch (Exception ex)
        {
            LogNuGetPackageLoadFailed(packageSource, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Registers a plugin with the orchestrator.
    /// </summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RegisterPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

    /// <summary>
    /// Executes a plugin with the specified inputs and enhanced monitoring.
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
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(inputs);

        var loadedPlugin = _registry.GetLoadedPlugin(pluginId);
        if (loadedPlugin == null)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' not found.");
        }

        // Check plugin health before execution
        if (loadedPlugin.Health == PluginHealth.Critical || loadedPlugin.State != PluginState.Running)
        {
            throw new InvalidOperationException($"Plugin '{pluginId}' is not in a healthy state for execution. Health: {loadedPlugin.Health}, State: {loadedPlugin.State}");
        }

        LogExecutingPlugin(pluginId);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Execute with error recovery
            var result = await ExecuteWithRetryAsync(loadedPlugin.Plugin, inputs, parameters, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _registry.UpdateExecutionStats(pluginId, stopwatch.Elapsed);

            LogPluginExecutionCompleted(pluginId, stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _registry.UpdateExecutionStats(pluginId, stopwatch.Elapsed, ex);

            LogPluginExecutionFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Unregisters a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if unregistered successfully; otherwise, false.</returns>
    public async Task<bool> UnregisterPluginAsync(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var loadedPlugin = _registry.UnregisterPlugin(pluginId);
        if (loadedPlugin != null)
        {
            try
            {
                _registry.UpdatePluginState(pluginId, PluginState.Stopping);

                // Stop hot reload monitoring
                _discovery.StopHotReload(loadedPlugin.Metadata.AssemblyPath);

                // Dispose the plugin
                await loadedPlugin.Plugin.DisposeAsync().ConfigureAwait(false);

                _registry.UpdatePluginState(pluginId, PluginState.Unloaded);

                LogPluginUnregistered(pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _registry.UpdatePluginState(pluginId, PluginState.Failed);
                LogPluginUnloadFailed(pluginId, ex.Message);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Reloads a plugin (hot reload).
    /// </summary>
    /// <param name="pluginId">The plugin ID to reload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if reloaded successfully; otherwise, false.</returns>
    public async Task<bool> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var loadedPlugin = _registry.GetLoadedPlugin(pluginId);
        if (loadedPlugin == null)
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

            // Reload from assembly
            var loadedCount = await LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);

            LogPluginReloaded(pluginId, loadedCount > 0);
            return loadedCount > 0;
        }
        catch (Exception ex)
        {
            LogPluginReloadFailed(pluginId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets a plugin by ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId) => _registry.GetPlugin(pluginId);

    /// <summary>
    /// Gets detailed plugin information.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin information if found; otherwise, null.</returns>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId) => _registry.GetLoadedPluginInfo(pluginId);

    /// <summary>
    /// Gets all plugins by accelerator type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType) =>
        _registry.GetPluginsByAcceleratorType(acceleratorType);

    /// <summary>
    /// Gets all plugins by input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <returns>Compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType) =>
        _registry.GetPluginsByInputType(inputType);

    /// <summary>
    /// Gets all healthy plugins.
    /// </summary>
    /// <returns>Healthy plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins() => _registry.GetHealthyPlugins();

    /// <summary>
    /// Gets plugin information for all registered plugins.
    /// </summary>
    /// <returns>Plugin information collection.</returns>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo() => _registry.GetPluginInfo();

    /// <summary>
    /// Internal method for registering plugins with full context information.
    /// </summary>
    private async Task RegisterPluginAsync(
        IAlgorithmPlugin plugin,
        PluginAssemblyLoadContext loadContext,
        Assembly assembly,
        PluginMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!_registry.RegisterPlugin(plugin, loadContext, assembly, metadata))
        {
            throw new InvalidOperationException($"Plugin with ID '{plugin.Id}' is already registered.");
        }

        try
        {
            // Initialize plugin
            _registry.UpdatePluginState(plugin.Id, PluginState.Initializing);
            await plugin.InitializeAsync(_accelerator, cancellationToken).ConfigureAwait(false);

            _registry.UpdatePluginState(plugin.Id, PluginState.Running);
            _registry.UpdatePluginHealth(plugin.Id, PluginHealth.Healthy);

            LogPluginRegistered(plugin.Id, plugin.Name);
        }
        catch (Exception ex)
        {
            _registry.UpdatePluginState(plugin.Id, PluginState.Failed);
            _registry.UpdatePluginHealth(plugin.Id, PluginHealth.Critical);

            // Remove from registry on initialization failure
            _registry.UnregisterPlugin(plugin.Id);

            LogPluginInitializationFailed(plugin.Id, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes plugin with retry logic for transient failures.
    /// </summary>
    private async Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object[] inputs,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await plugin.ExecuteAsync(inputs, parameters, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
            {
                LogPluginRetryingExecution(plugin.Id, attempt, ex.Message);
                await Task.Delay(retryDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        // Final attempt without retry handling
        return await plugin.ExecuteAsync(inputs, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines if an error is transient and worth retrying.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is SocketException ||
               (ex is IOException ioEx && ioEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Handles plugin file change events for hot reload.
    /// </summary>
    private async void OnPluginFileChanged(object? sender, PluginFileChangedEventArgs e)
    {
        try
        {
            // Small delay to allow file to be fully written
            await Task.Delay(500).ConfigureAwait(false);

            // Find plugins from this assembly
            var pluginsToReload = _registry.GetAllLoadedPlugins()
                .Where(lp => string.Equals(lp.Metadata.AssemblyPath, e.AssemblyPath, StringComparison.OrdinalIgnoreCase))
                .Select(lp => lp.Plugin.Id)
                .ToList();

            foreach (var pluginId in pluginsToReload)
            {
                _ = Task.Run(async () => await ReloadPluginAsync(pluginId).ConfigureAwait(false));
            }

            if (pluginsToReload.Count > 0)
            {
                LogHotReloadTriggered(e.ChangedFilePath, pluginsToReload.Count);
            }
        }
        catch (Exception ex)
        {
            LogHotReloadFailed(e.ChangedFilePath, ex.Message);
        }
    }

    /// <summary>
    /// Handles file watcher errors.
    /// </summary>
    private void OnWatcherError(object? sender, FileWatcherErrorEventArgs e)
    {
        LogWatcherError(e.Error.Message);
    }

    /// <summary>
    /// Performs health checks on all loaded plugins.
    /// </summary>
    private void PerformHealthChecksWrapper(object? state)
    {
        _ = Task.Run(async () => await PerformHealthChecks().ConfigureAwait(false));
    }

    /// <summary>
    /// Performs health checks on all loaded plugins.
    /// </summary>
    private async Task PerformHealthChecks()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            foreach (var loadedPlugin in _registry.GetAllLoadedPlugins().ToList())
            {
                await CheckPluginHealthAsync(loadedPlugin).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(ex.Message);
        }
    }

    /// <summary>
    /// Checks the health of a single plugin.
    /// </summary>
    private async Task CheckPluginHealthAsync(AlgorithmPluginRegistry.LoadedPlugin loadedPlugin)
    {
        try
        {
            var oldHealth = loadedPlugin.Health;

            // Check if plugin has been executing successfully
            if (loadedPlugin.LastError != null &&
                DateTime.UtcNow - loadedPlugin.LastExecution < TimeSpan.FromMinutes(5))
            {
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
            }
            else if (loadedPlugin.ExecutionCount > 0 && loadedPlugin.LastError == null)
            {
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Healthy);
            }

            await Task.CompletedTask; // Placeholder for async health checks
        }
        catch (Exception ex)
        {
            _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Critical);
            LogPluginHealthCheckFailed(loadedPlugin.Plugin.Id, ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Stop health check timer
            _healthCheckTimer.Dispose();

            // Unsubscribe from events
            _discovery.PluginFileChanged -= OnPluginFileChanged;
            // _discovery.WatcherError -= OnWatcherError; // Event does not exist

            // Dispose all components
            var disposeTasks = _registry.GetAllLoadedPlugins().Select(async lp =>
            {
                try
                {
                    await lp.Plugin.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogPluginDisposeFailed(lp.Plugin.Id, ex.Message);
                }
            });

            await Task.WhenAll(disposeTasks).ConfigureAwait(false);

            // Dispose components
            _registry.Dispose();
            _validator.Dispose();
            _loader.Dispose();
            _discovery.Dispose();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovering plugins in directory: {Directory}")]
    private partial void LogDiscoveringPlugins(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discovered and loaded {Count} plugins from directory: {Directory}")]
    private partial void LogPluginsDiscovered(int count, string directory);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin discovery failed for {Directory}: {Reason}")]
    private partial void LogDiscoveryFailed(string directory, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading algorithm plugin from assembly {AssemblyPath}")]
    private partial void LogLoadingAssembly(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Security validation failed for {AssemblyPath}")]
    private partial void LogSecurityValidationFailed(string assemblyPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Assembly load failed for {AssemblyPath}: {Reason}")]
    private partial void LogAssemblyLoadFailed(string assemblyPath, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {PluginCount} algorithm plugins from assembly {AssemblyName}")]
    private partial void LogAssemblyLoaded(int pluginCount, string assemblyName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin registration failed for {TypeName}: {Reason}")]
    private partial void LogPluginRegistrationFailed(string typeName, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin load failed for {PluginFile}: {Reason}")]
    private partial void LogPluginLoadFailed(string pluginFile, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading plugins from NuGet package: {PackageSource}")]
    private partial void LogLoadingFromNuGetPackage(string packageSource);

    [LoggerMessage(Level = LogLevel.Information, Message = "NuGet package loaded: {PackageId} v{Version}, {PluginCount} plugins")]
    private partial void LogNuGetPackageLoaded(string packageId, string version, int pluginCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "NuGet package load failed for {PackageSource}: {Reason}")]
    private partial void LogNuGetPackageLoadFailed(string packageSource, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered algorithm plugin {PluginId} ({PluginName})")]
    private partial void LogPluginRegistered(string pluginId, string pluginName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin initialization failed for {PluginId}: {Reason}")]
    private partial void LogPluginInitializationFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing plugin {PluginId}")]
    private partial void LogExecutingPlugin(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin execution completed for {PluginId} in {ElapsedMs} ms")]
    private partial void LogPluginExecutionCompleted(string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin execution failed for {PluginId}: {Reason}")]
    private partial void LogPluginExecutionFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retrying plugin execution for {PluginId}, attempt {Attempt}: {Reason}")]
    private partial void LogPluginRetryingExecution(string pluginId, int attempt, string reason);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot reload triggered for {FilePath}, reloading {PluginCount} plugins")]
    private partial void LogHotReloadTriggered(string filePath, int pluginCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Hot reload failed for {FilePath}: {Reason}")]
    private partial void LogHotReloadFailed(string filePath, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Watcher error: {Error}")]
    private partial void LogWatcherError(string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Health check failed: {Reason}")]
    private partial void LogHealthCheckFailed(string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin health check failed for {PluginId}: {Reason}")]
    private partial void LogPluginHealthCheckFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin dispose failed for {PluginId}: {Reason}")]
    private partial void LogPluginDisposeFailed(string pluginId, string reason);

    #endregion
}