// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Management.Execution;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Abstractions;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Core interface for the algorithm plugin manager that orchestrates all components.
/// </summary>
public interface IAlgorithmPluginManagerCore : IAsyncDisposable
{
    /// <summary>
    /// Gets the plugin lifecycle manager.
    /// </summary>
    public IPluginLifecycleManager LifecycleManager { get; }

    /// <summary>
    /// Gets the plugin discovery service.
    /// </summary>
    public IPluginDiscoveryService DiscoveryService { get; }

    /// <summary>
    /// Gets the NuGet plugin service.
    /// </summary>
    public INuGetPluginService NuGetService { get; }

    /// <summary>
    /// Gets the plugin executor.
    /// </summary>
    public IPluginExecutor Executor { get; }

    /// <summary>
    /// Gets the health monitor.
    /// </summary>
    public IHealthMonitor HealthMonitor { get; }

    /// <summary>
    /// Gets the hot reload service.
    /// </summary>
    public IHotReloadService HotReloadService { get; }

    /// <summary>
    /// Gets the registered plugin IDs.
    /// </summary>
    public IEnumerable<string> RegisteredPlugins { get; }

    /// <summary>
    /// Discovers plugins in the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins discovered and loaded.</returns>
    public Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads plugins from an assembly file with advanced isolation and security validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    public Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads plugins from a NuGet package.
    /// </summary>
    /// <param name="packageSource">The path to the .nupkg file or package ID (with optional version).</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    public Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates plugins from a NuGet package to the latest version.
    /// </summary>
    /// <param name="packageId">The package ID to update.</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded from the updated package.</returns>
    public Task<int> UpdateNuGetPackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the NuGet package cache.
    /// </summary>
    /// <param name="olderThan">Optional age filter - only clear packages older than this timespan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cache clearing operation.</returns>
    public Task ClearNuGetCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about cached NuGet packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of cached NuGet package information.</returns>
    public Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a NuGet package before loading it.
    /// </summary>
    /// <param name="packageSource">The package source to validate.</param>
    /// <param name="targetFramework">Target framework for validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with details about the package.</returns>
    public Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a plugin instance.
    /// </summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the registration operation.</returns>
    public Task RegisterPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a registered plugin by ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin instance if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// Gets detailed information about a loaded plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The loaded plugin information if found; otherwise, null.</returns>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId);

    /// <summary>
    /// Gets all plugins that support the specified accelerator type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType);

    /// <summary>
    /// Gets all plugins that can process the specified input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType);

    /// <summary>
    /// Gets all healthy and running plugins.
    /// </summary>
    /// <returns>Collection of healthy plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins();

    /// <summary>
    /// Executes a plugin with the specified inputs and enhanced monitoring.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a plugin with proper cleanup and isolation handling.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if the plugin was unregistered; otherwise, false.</returns>
    public Task<bool> UnregisterPluginAsync(string pluginId);

    /// <summary>
    /// Reloads a plugin (hot reload).
    /// </summary>
    /// <param name="pluginId">The plugin ID to reload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the plugin was reloaded successfully; otherwise, false.</returns>
    public Task<bool> ReloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about all registered plugins.
    /// </summary>
    /// <returns>Collection of plugin information.</returns>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo();
}