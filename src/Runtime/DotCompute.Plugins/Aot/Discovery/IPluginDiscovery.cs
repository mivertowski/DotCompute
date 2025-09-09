// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;

namespace DotCompute.Plugins.Aot.Discovery;

/// <summary>
/// Interface for plugin discovery and creation operations.
/// Segregated interface following the Interface Segregation Principle.
/// </summary>
public interface IPluginDiscovery : IDisposable
{
    /// <summary>
    /// Creates a plugin instance using registered factory methods.
    /// </summary>
    /// <param name="pluginTypeName">The type name of the plugin to create.</param>
    /// <returns>The created plugin instance, or null if creation failed or no factory exists.</returns>
    /// <exception cref="ArgumentException">Thrown when pluginTypeName is null, empty, or whitespace.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown when the plugin is not supported on the current platform.</exception>
    public IBackendPlugin? CreatePlugin(string pluginTypeName);

    /// <summary>
    /// Attempts to create a plugin instance, providing detailed error information on failure.
    /// </summary>
    /// <param name="pluginTypeName">The type name of the plugin to create.</param>
    /// <param name="plugin">The created plugin instance if successful.</param>
    /// <param name="errorMessage">Error message if creation failed.</param>
    /// <returns>True if the plugin was successfully created; false otherwise.</returns>
    public bool TryCreatePlugin(string pluginTypeName, out IBackendPlugin? plugin, out string? errorMessage);

    /// <summary>
    /// Gets a loaded plugin by its unique identifier.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin.</param>
    /// <returns>The plugin instance if found and loaded; null otherwise.</returns>
    public IBackendPlugin? GetPlugin(string pluginId);

    /// <summary>
    /// Gets all currently loaded and active plugin instances.
    /// </summary>
    /// <returns>A read-only collection of loaded plugin instances.</returns>
    public IReadOnlyCollection<IBackendPlugin> GetLoadedPlugins();

    /// <summary>
    /// Checks if a plugin with the specified ID is currently loaded.
    /// </summary>
    /// <param name="pluginId">The plugin ID to check.</param>
    /// <returns>True if the plugin is loaded; false otherwise.</returns>
    public bool IsPluginLoaded(string pluginId);

    /// <summary>
    /// Gets the count of currently loaded plugins.
    /// </summary>
    public int LoadedPluginCount { get; }
}