// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;

namespace DotCompute.Plugins.Aot.Registry;

/// <summary>
/// Interface for plugin registration operations, focusing on factory management and registration.
/// Segregated interface following the Interface Segregation Principle.
/// </summary>
public interface IPluginRegistration : IDisposable
{
    /// <summary>
    /// Registers a plugin factory for a specific plugin type.
    /// </summary>
    /// <param name="pluginTypeName">The unique name identifier for the plugin type.</param>
    /// <param name="factory">The factory function that creates plugin instances.</param>
    /// <exception cref="ArgumentException">Thrown when pluginTypeName is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when factory is null.</exception>
    public void RegisterPluginFactory(string pluginTypeName, Func<IBackendPlugin> factory);

    /// <summary>
    /// Unregisters a plugin factory for a specific plugin type.
    /// </summary>
    /// <param name="pluginTypeName">The unique name identifier for the plugin type.</param>
    /// <returns>True if the factory was successfully unregistered; false if no factory was found.</returns>
    public bool UnregisterPluginFactory(string pluginTypeName);

    /// <summary>
    /// Gets all available plugin types that have registered factories.
    /// </summary>
    /// <returns>A read-only collection of plugin type names that can be instantiated.</returns>
    public IReadOnlyCollection<string> GetAvailablePluginTypes();

    /// <summary>
    /// Checks if a factory is registered for the specified plugin type.
    /// </summary>
    /// <param name="pluginTypeName">The plugin type name to check.</param>
    /// <returns>True if a factory is registered; false otherwise.</returns>
    public bool IsPluginTypeRegistered(string pluginTypeName);

    /// <summary>
    /// Gets the count of registered plugin factories.
    /// </summary>
    public int RegisteredFactoryCount { get; }
}