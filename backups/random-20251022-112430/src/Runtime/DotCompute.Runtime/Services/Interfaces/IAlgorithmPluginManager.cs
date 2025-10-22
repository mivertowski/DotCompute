// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Algorithm plugin manager interface
/// </summary>
public interface IAlgorithmPluginManager
{
    /// <summary>
    /// Loads all algorithm plugins from configured directories
    /// </summary>
    /// <returns>A task representing the load operation</returns>
    public Task LoadPluginsAsync();

    /// <summary>
    /// Gets a plugin by its ID
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <param name="pluginId">The plugin ID</param>
    /// <returns>The plugin instance or null if not found</returns>
    public Task<T?> GetPluginAsync<T>(string pluginId) where T : class;

    /// <summary>
    /// Gets all plugins of a specific type
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <returns>All plugins of the specified type</returns>
    public Task<IEnumerable<T>> GetPluginsAsync<T>() where T : class;

    /// <summary>
    /// Registers a plugin instance
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <param name="pluginId">The plugin ID</param>
    /// <param name="plugin">The plugin instance</param>
    /// <returns>A task representing the registration operation</returns>
    public Task RegisterPluginAsync<T>(string pluginId, T plugin) where T : class;

    /// <summary>
    /// Unloads a plugin
    /// </summary>
    /// <param name="pluginId">The plugin ID</param>
    /// <returns>A task representing the unload operation</returns>
    public Task UnloadPluginAsync(string pluginId);

    /// <summary>
    /// Gets plugin information
    /// </summary>
    /// <returns>Information about all loaded plugins</returns>
    public IEnumerable<PluginInfo> GetPluginInfo();

    /// <summary>
    /// Reloads all plugins (if hot reload is enabled)
    /// </summary>
    /// <returns>A task representing the reload operation</returns>
    public Task ReloadPluginsAsync();
}