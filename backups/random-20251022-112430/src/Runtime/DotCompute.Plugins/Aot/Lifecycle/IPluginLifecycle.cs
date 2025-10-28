// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;

namespace DotCompute.Plugins.Aot.Lifecycle;

/// <summary>
/// Interface for plugin lifecycle management operations.
/// Segregated interface following the Interface Segregation Principle.
/// </summary>
public interface IPluginLifecycle : IDisposable
{
    /// <summary>
    /// Unloads and disposes a plugin by its unique identifier.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to unload.</param>
    /// <returns>True if the plugin was successfully unloaded; false if not found or unloading failed.</returns>
    /// <exception cref="ArgumentException">Thrown when pluginId is null, empty, or whitespace.</exception>
    public bool UnloadPlugin(string pluginId);

    /// <summary>
    /// Attempts to unload a plugin, providing detailed information about the operation.
    /// </summary>
    /// <param name="pluginId">The unique identifier of the plugin to unload.</param>
    /// <param name="errorMessage">Error message if unloading failed.</param>
    /// <returns>True if the plugin was successfully unloaded; false otherwise.</returns>
    public bool TryUnloadPlugin(string pluginId, out string? errorMessage);

    /// <summary>
    /// Unloads all currently loaded plugins.
    /// </summary>
    /// <returns>The number of plugins that were successfully unloaded.</returns>
    public int UnloadAllPlugins();

    /// <summary>
    /// Registers a plugin instance for lifecycle management.
    /// </summary>
    /// <param name="plugin">The plugin instance to register for management.</param>
    /// <exception cref="ArgumentNullException">Thrown when plugin is null.</exception>
    public void RegisterPluginForManagement(IBackendPlugin plugin);

    /// <summary>
    /// Unregisters a plugin from lifecycle management without disposing it.
    /// </summary>
    /// <param name="pluginId">The plugin ID to unregister.</param>
    /// <returns>True if the plugin was unregistered; false if not found.</returns>
    public bool UnregisterPlugin(string pluginId);
    /// <summary>
    /// Occurs when plugin loaded.
    /// </summary>

    /// <summary>
    /// Event raised when a plugin is successfully loaded and registered.
    /// </summary>
    public event EventHandler<PluginLifecycleEventArgs>? PluginLoaded;
    /// <summary>
    /// Occurs when plugin unloaded.
    /// </summary>

    /// <summary>
    /// Event raised when a plugin is unloaded and disposed.
    /// </summary>
    public event EventHandler<PluginLifecycleEventArgs>? PluginUnloaded;
    /// <summary>
    /// Occurs when lifecycle error.
    /// </summary>

    /// <summary>
    /// Event raised when a plugin lifecycle operation encounters an error.
    /// </summary>
    public event EventHandler<PluginLifecycleErrorEventArgs>? LifecycleError;
}

/// <summary>
/// Event arguments for plugin lifecycle events.
/// </summary>
public class PluginLifecycleEventArgs(string pluginId, string pluginName) : EventArgs
{
    /// <summary>
    /// Gets the unique identifier of the plugin.
    /// </summary>
    public string PluginId { get; } = pluginId;

    /// <summary>
    /// Gets the display name of the plugin.
    /// </summary>
    public string PluginName { get; } = pluginName;

    /// <summary>
    /// Gets the timestamp when the lifecycle event occurred.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for plugin lifecycle errors.
/// </summary>
public class PluginLifecycleErrorEventArgs(string pluginId, Exception exception, string operation) : EventArgs
{
    /// <summary>
    /// Gets the unique identifier of the plugin that caused the error.
    /// </summary>
    public string PluginId { get; } = pluginId;

    /// <summary>
    /// Gets the exception that occurred during the lifecycle operation.
    /// </summary>
    public Exception Exception { get; } = exception;

    /// <summary>
    /// Gets the lifecycle operation that was being performed when the error occurred.
    /// </summary>
    public string Operation { get; } = operation;

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}