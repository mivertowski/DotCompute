// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Types.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Refactored algorithm plugin manager using component-based architecture.
/// This class serves as a facade over the decomposed component architecture.
/// </summary>
public sealed class AlgorithmPluginManagerRefactored : IAsyncDisposable
{
    private readonly ILogger<AlgorithmPluginManagerRefactored> _logger;
    private readonly IAlgorithmPluginManagerCore _core;
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
    public AlgorithmPluginManagerRefactored(
        IAccelerator accelerator,
        ILogger<AlgorithmPluginManagerRefactored> logger,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create core orchestrator
        var coreLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AlgorithmPluginManagerCore>.Instance;
        _core = new AlgorithmPluginManagerCore(accelerator, coreLogger, options);

        _logger.LogInformation("Algorithm Plugin Manager initialized with refactored architecture");
    }

    /// <summary>
    /// Gets the registered plugin IDs.
    /// </summary>
    public IEnumerable<string> RegisteredPlugins => _core.RegisteredPlugins;

    /// <summary>
    /// Discovers plugins in the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins discovered and loaded.</returns>
    public async Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _core.DiscoverAndLoadPluginsAsync(pluginDirectory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads plugins from a NuGet package.
    /// </summary>
    /// <param name="packageSource">The path to the .nupkg file or package ID (with optional version).</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    public async Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _core.LoadPluginsFromNuGetPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates plugins from a NuGet package to the latest version.
    /// </summary>
    /// <param name="packageId">The package ID to update.</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded from the updated package.</returns>
    public async Task<int> UpdateNuGetPackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _core.UpdateNuGetPackageAsync(packageId, targetFramework, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the NuGet package cache.
    /// </summary>
    /// <param name="olderThan">Optional age filter - only clear packages older than this timespan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cache clearing operation.</returns>
    public async Task ClearNuGetCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _core.ClearNuGetCacheAsync(olderThan, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets information about cached NuGet packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of cached NuGet package information.</returns>
    public async Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _core.GetCachedNuGetPackagesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates a NuGet package before loading it.
    /// </summary>
    /// <param name="packageSource">The package source to validate.</param>
    /// <param name="targetFramework">Target framework for validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with details about the package.</returns>
    public async Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _core.ValidateNuGetPackageAsync(packageSource, targetFramework, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads plugins from an assembly file with advanced isolation and security validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    public async Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _core.LoadPluginsFromAssemblyAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Registers a plugin instance (simplified overload for external plugins).
    /// </summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the registration operation.</returns>
    public async Task RegisterPluginAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _core.RegisterPluginAsync(plugin, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a registered plugin by ID.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin instance if found; otherwise, null.</returns>
    public IAlgorithmPlugin? GetPlugin(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _core.GetPlugin(pluginId);
    }

    /// <summary>
    /// Gets detailed information about a loaded plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The loaded plugin information if found; otherwise, null.</returns>
    public LoadedPluginInfo? GetLoadedPluginInfo(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _core.GetLoadedPluginInfo(pluginId);
    }

    /// <summary>
    /// Gets all plugins that support the specified accelerator type.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByAcceleratorType(AcceleratorType acceleratorType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _core.GetPluginsByAcceleratorType(acceleratorType);
    }

    /// <summary>
    /// Gets all plugins that can process the specified input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <returns>Collection of compatible plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetPluginsByInputType(Type inputType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _core.GetPluginsByInputType(inputType);
    }

    /// <summary>
    /// Gets all healthy and running plugins.
    /// </summary>
    /// <returns>Collection of healthy plugins.</returns>
    public IEnumerable<IAlgorithmPlugin> GetHealthyPlugins()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _core.GetHealthyPlugins();
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
        return await _core.ExecutePluginAsync(pluginId, inputs, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unregisters a plugin with proper cleanup and isolation handling.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if the plugin was unregistered; otherwise, false.</returns>
    public async Task<bool> UnregisterPluginAsync(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _core.UnregisterPluginAsync(pluginId).ConfigureAwait(false);
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
        return await _core.ReloadPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets information about all registered plugins.
    /// </summary>
    /// <returns>Collection of plugin information.</returns>
    public IEnumerable<AlgorithmPluginInfo> GetPluginInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _core.GetPluginInfo();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _core.DisposeAsync().ConfigureAwait(false);
            _logger.LogInformation("Algorithm Plugin Manager disposed");
        }
    }
}