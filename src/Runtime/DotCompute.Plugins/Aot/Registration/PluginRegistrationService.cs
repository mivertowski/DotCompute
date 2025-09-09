// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Aot.Registry;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

namespace DotCompute.Plugins.Aot.Registration;

/// <summary>
/// Provides plugin factory registration and management services for AOT scenarios.
/// Implements the Interface Segregation Principle by focusing solely on registration operations.
/// </summary>
public sealed class PluginRegistrationService : IPluginRegistration
{
    private readonly ILogger<PluginRegistrationService> _logger;
    private readonly Dictionary<string, Func<IBackendPlugin>> _factories;
    private readonly Lock _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginRegistrationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for this service.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public PluginRegistrationService(ILogger<PluginRegistrationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _factories = [];


        _logger.LogDebugMessage("PluginRegistrationService initialized");
    }

    /// <summary>
    /// Gets the count of registered plugin factories.
    /// </summary>
    public int RegisteredFactoryCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            lock (_lock)
            {
                return _factories.Count;
            }
        }
    }

    /// <summary>
    /// Registers a plugin factory for a specific plugin type.
    /// </summary>
    /// <param name="pluginTypeName">The unique name identifier for the plugin type.</param>
    /// <param name="factory">The factory function that creates plugin instances.</param>
    /// <exception cref="ArgumentException">Thrown when pluginTypeName is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when factory is null.</exception>
    public void RegisterPluginFactory(string pluginTypeName, Func<IBackendPlugin> factory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginTypeName))
        {
            throw new ArgumentException("Plugin type name cannot be null, empty, or whitespace.", nameof(pluginTypeName));
        }
        ArgumentNullException.ThrowIfNull(factory);

        lock (_lock)
        {
            var isReplacing = _factories.ContainsKey(pluginTypeName);
            _factories[pluginTypeName] = factory;


            _logger.LogInformation(
                isReplacing ? "Replaced plugin factory for {PluginType}" : "Registered plugin factory for {PluginType}",
                pluginTypeName);
        }
    }

    /// <summary>
    /// Unregisters a plugin factory for a specific plugin type.
    /// </summary>
    /// <param name="pluginTypeName">The unique name identifier for the plugin type.</param>
    /// <returns>True if the factory was successfully unregistered; false if no factory was found.</returns>
    public bool UnregisterPluginFactory(string pluginTypeName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginTypeName))
        {
            return false;
        }

        lock (_lock)
        {
            var removed = _factories.Remove(pluginTypeName);
            if (removed)
            {
                _logger.LogInfoMessage("Unregistered plugin factory for {pluginTypeName}");
            }
            else
            {
                _logger.LogDebugMessage("No factory found to unregister for {pluginTypeName}");
            }
            return removed;
        }
    }

    /// <summary>
    /// Gets all available plugin types that have registered factories.
    /// </summary>
    /// <returns>A read-only collection of plugin type names that can be instantiated.</returns>
    public IReadOnlyCollection<string> GetAvailablePluginTypes()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            return [.. _factories.Keys];
        }
    }

    /// <summary>
    /// Checks if a factory is registered for the specified plugin type.
    /// </summary>
    /// <param name="pluginTypeName">The plugin type name to check.</param>
    /// <returns>True if a factory is registered; false otherwise.</returns>
    public bool IsPluginTypeRegistered(string pluginTypeName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginTypeName))
        {
            return false;
        }

        lock (_lock)
        {
            return _factories.ContainsKey(pluginTypeName);
        }
    }

    /// <summary>
    /// Gets the factory function for a specific plugin type.
    /// This is an internal method for use by discovery services.
    /// </summary>
    /// <param name="pluginTypeName">The plugin type name.</param>
    /// <returns>The factory function if registered; null otherwise.</returns>
    internal Func<IBackendPlugin>? GetFactory(string pluginTypeName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(pluginTypeName))
        {
            return null;
        }

        lock (_lock)
        {
            return _factories.TryGetValue(pluginTypeName, out var factory) ? factory : null;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_lock)
        {
            _factories.Clear();
        }

        _logger.LogInfoMessage("PluginRegistrationService disposed");
    }
}