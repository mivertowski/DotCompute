// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Management.Loading;

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Interface for plugin discovery operations.
/// </summary>
public interface IPluginDiscoveryService
{
    /// <summary>
    /// Discovers plugins in the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">The directory to scan for plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins discovered and loaded.</returns>
    Task<int> DiscoverAndLoadPluginsAsync(string pluginDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads plugins from an assembly file with advanced isolation and security validation.
    /// </summary>
    /// <param name="assemblyPath">The path to the assembly file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    Task<int> LoadPluginsFromAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default);
}