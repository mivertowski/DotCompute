#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Execution;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Core orchestrator for the algorithm plugin manager that coordinates all components.
/// </summary>
public sealed class AlgorithmPluginManagerCore : IAlgorithmPluginManagerCore
{
    private readonly ILogger<AlgorithmPluginManagerCore> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginManagerCore"/> class.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for plugins.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options for the plugin manager.</param>
    public AlgorithmPluginManagerCore(
        IAccelerator accelerator,
        ILogger<AlgorithmPluginManagerCore> logger,
        AlgorithmPluginManagerOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var pluginOptions = options ?? new AlgorithmPluginManagerOptions();

        // Create security validator
        var securityValidatorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Validation.SecurityValidator>.Instance;
        var securityValidator = new Validation.SecurityValidator(securityValidatorLogger, pluginOptions);

        // Create lifecycle manager (concrete type needed for health monitor)
        var lifecycleLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginLifecycleManager>.Instance;
        var lifecycleManagerConcrete = new PluginLifecycleManager(lifecycleLogger, accelerator);
        LifecycleManager = lifecycleManagerConcrete;

        // Create health monitor
        var healthMonitorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HealthMonitor>.Instance;
        HealthMonitor = new HealthMonitor(healthMonitorLogger, lifecycleManagerConcrete, pluginOptions);

        // Create hot reload service
        var hotReloadLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<HotReloadService>.Instance;
        HotReloadService = new HotReloadService(hotReloadLogger, LifecycleManager, pluginOptions);

        // Create discovery service
        var discoveryLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginDiscoveryService>.Instance;
        DiscoveryService = new PluginDiscoveryService(discoveryLogger, LifecycleManager, securityValidator, pluginOptions);

        // Create NuGet service
        var nugetLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginService>.Instance;
        var discoveryService = new PluginDiscoveryService(discoveryLogger, LifecycleManager, securityValidator, pluginOptions);
        NuGetService = new NuGetPluginService(nugetLogger, LifecycleManager, discoveryService, pluginOptions);

        // Create executor
        var executorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PluginExecutor>.Instance;
        Executor = new PluginExecutor(executorLogger, LifecycleManager);

        // Start health monitoring if enabled
        HealthMonitor.StartHealthMonitoring();
    }

    /// <inheritdoc/>
    public IPluginLifecycleManager LifecycleManager { get; }

    /// <inheritdoc/>
    public IPluginDiscoveryService DiscoveryService { get; }

    /// <inheritdoc/>
    public INuGetPluginService NuGetService { get; }

    /// <inheritdoc/>
    public IPluginExecutor Executor { get; }

    /// <inheritdoc/>
    public IHealthMonitor HealthMonitor { get; }

    /// <inheritdoc/>
    public IHotReloadService HotReloadService { get; }

    /// <inheritdoc/>
    public IEnumerable<string> RegisteredPlugins => LifecycleManager.RegisteredPlugins;

    /// <inheritdoc/>
    public async Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var count = await DiscoveryService.DiscoverAndLoadPluginsAsync(pluginDirectory, cancellationToken).ConfigureAwait(false);

        // Setup hot reload for discovered plugins if enabled

        if (count > 0)
        {
            var pluginFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);
            foreach (var pluginFile in pluginFiles)
            {
                HotReloadService.SetupHotReload(pluginFile);
            }
        }


        return count;
    }

    /// <inheritdoc/>
    public async Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var count = await DiscoveryService.LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);

        // Setup hot reload for the assembly if plugins were loaded

        if (count > 0)
        {
            HotReloadService.SetupHotReload(assemblyPath);
        }


        return count;
    }

    /// <inheritdoc/>
    public async Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await NuGetService.LoadPluginsFromNuGetPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> UpdateNuGetPackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await NuGetService.UpdateNuGetPackageAsync(packageId, targetFramework, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearNuGetCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await NuGetService.ClearNuGetCacheAsync(olderThan, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await NuGetService.GetCachedNuGetPackagesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await NuGetService.ValidateNuGetPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RegisterPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await LifecycleManager.RegisterPluginAsync(plugin, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LifecycleManager.GetPlugin(pluginId);
    }

    /// <inheritdoc/>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LifecycleManager.GetLoadedPluginInfo(pluginId);
    }

    /// <inheritdoc/>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LifecycleManager.GetPluginsByAcceleratorType(acceleratorType);
    }

    /// <inheritdoc/>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LifecycleManager.GetPluginsByInputType(inputType);
    }

    /// <inheritdoc/>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LifecycleManager.GetHealthyPlugins();
    }

    /// <inheritdoc/>
    public async Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var startTime = DateTime.UtcNow;
        Exception? executionError = null;


        try
        {
            var result = await Executor.ExecutePluginAsync(pluginId, inputs, parameters, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            executionError = ex;
            throw;
        }
        finally
        {
            // Update execution statistics in lifecycle manager
            var executionTime = DateTime.UtcNow - startTime;
            if (LifecycleManager is PluginLifecycleManager concreteManager)
            {
                concreteManager.UpdateExecutionStatistics(pluginId, executionTime, executionError);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UnregisterPluginAsync(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await LifecycleManager.UnregisterPluginAsync(pluginId).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await LifecycleManager.ReloadPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return LifecycleManager.GetPluginInfo();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Stop monitoring services
            HealthMonitor.StopHealthMonitoring();
            HotReloadService.StopAllHotReload();

            // Dispose services that implement IDisposable
            if (HealthMonitor is IDisposable healthDisposable)
            {
                healthDisposable.Dispose();
            }


            if (HotReloadService is IDisposable hotReloadDisposable)
            {

                hotReloadDisposable.Dispose();
            }

            // Note: Other services would need to implement IAsyncDisposable if they have async cleanup needs
            // For now, they don't have significant cleanup requirements

        }
        return ValueTask.CompletedTask;
    }
}