// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// using DotCompute.Algorithms.Types // Commented out - missing reference.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Runtime.DependencyInjection;


/// <summary>
/// Provides dependency injection services for plugins
/// </summary>
public interface IPluginServiceProvider
{
    /// <summary>
    /// Creates a service scope for a plugin
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <returns>A service scope for the plugin</returns>
    public IServiceScope CreatePluginScope(string pluginId);

    /// <summary>
    /// Registers plugin-specific services
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <param name="configureServices">Action to configure plugin services</param>
    public void RegisterPluginServices(string pluginId, Action<IServiceCollection> configureServices);

    /// <summary>
    /// Resolves a service for a specific plugin
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <param name="pluginId">The plugin identifier</param>
    /// <returns>The resolved service instance</returns>
    public T? GetPluginService<T>(string pluginId) where T : class;

    /// <summary>
    /// Resolves a service for a specific plugin
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <param name="serviceType">The service type</param>
    /// <returns>The resolved service instance</returns>
    public object? GetPluginService(string pluginId, Type serviceType);

    /// <summary>
    /// Checks if a service is registered for a plugin
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <param name="serviceType">The service type</param>
    /// <returns>True if the service is registered</returns>
    public bool IsServiceRegistered(string pluginId, Type serviceType);

    /// <summary>
    /// Disposes plugin-specific services
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    public ValueTask DisposePluginServicesAsync(string pluginId);
}

/// <summary>
/// Resolves plugin dependencies
/// </summary>
public interface IPluginDependencyResolver
{
    /// <summary>
    /// Resolves constructor dependencies for a plugin
    /// </summary>
    /// <param name="pluginType">The plugin type</param>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>Constructor arguments</returns>
    public object[] ResolveConstructorDependencies(Type pluginType, IServiceProvider serviceProvider);

    /// <summary>
    /// Injects property dependencies into a plugin instance
    /// </summary>
    /// <param name="pluginInstance">The plugin instance</param>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>A task representing the async operation</returns>
    public Task InjectPropertiesAsync(object pluginInstance, IServiceProvider serviceProvider);

    /// <summary>
    /// Validates that all plugin dependencies can be resolved
    /// </summary>
    /// <param name="pluginType">The plugin type</param>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>Validation result</returns>
    public PluginDependencyValidationResult ValidateDependencies(Type pluginType, IServiceProvider serviceProvider);

    /// <summary>
    /// Gets the required services for a plugin type
    /// </summary>
    /// <param name="pluginType">The plugin type</param>
    /// <returns>Required service types</returns>
    public IEnumerable<Type> GetRequiredServices(Type pluginType);
}

/// <summary>
/// Manages plugin lifecycles with DI support
/// </summary>
public interface IPluginLifecycleManager
{
    /// <summary>
    /// Creates a plugin instance with dependency injection
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <param name="pluginType">The concrete plugin type</param>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The plugin instance</returns>
    public Task<T> CreatePluginAsync<T>(Type pluginType, IServiceProvider serviceProvider) where T : class;

    /// <summary>
    /// Initializes a plugin with its dependencies
    /// </summary>
    /// <param name="plugin">The plugin instance</param>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>A task representing the async operation</returns>
    public Task InitializePluginAsync(object plugin, IServiceProvider serviceProvider);

    /// <summary>
    /// Disposes a plugin and its dependencies
    /// </summary>
    /// <param name="plugin">The plugin instance</param>
    /// <returns>A task representing the async operation</returns>
    public ValueTask DisposePluginAsync(object plugin);

    /// <summary>
    /// Gets the lifecycle state of a plugin
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <returns>The lifecycle state</returns>
    public PluginLifecycleState GetPluginState(string pluginId);

    /// <summary>
    /// Registers lifecycle event handlers for a plugin
    /// </summary>
    /// <param name="pluginId">The plugin identifier</param>
    /// <param name="eventHandlers">Event handlers</param>
    public void RegisterLifecycleHandlers(string pluginId, PluginLifecycleHandlers eventHandlers);
}

/// <summary>
/// Factory for creating plugin instances with DI support
/// </summary>
public interface IPluginFactory
{
    /// <summary>
    /// Creates a plugin instance with full DI support
    /// </summary>
    /// <typeparam name="T">The plugin interface type</typeparam>
    /// <param name="pluginType">The concrete plugin type</param>
    /// <param name="serviceScope">The service scope for the plugin</param>
    /// <returns>The plugin instance</returns>
    public Task<T> CreateAsync<T>(Type pluginType, IServiceScope serviceScope) where T : class;

    /// <summary>
    /// Checks if a type can be created as a plugin
    /// </summary>
    /// <param name="pluginType">The plugin type</param>
    /// <returns>True if the type can be created as a plugin</returns>
    public bool CanCreate(Type pluginType);

    /// <summary>
    /// Gets the required services for creating a plugin
    /// </summary>
    /// <param name="pluginType">The plugin type</param>
    /// <returns>Required service types</returns>
    public IEnumerable<ServiceDescriptor> GetRequiredServiceDescriptors(Type pluginType);
}

/// <summary>
/// Plugin dependency validation result
/// </summary>
public class PluginDependencyValidationResult
{
    /// <summary>
    /// Gets whether the validation was successful
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the missing dependencies
    /// </summary>
    public IReadOnlyList<Type> MissingDependencies { get; init; } = Array.Empty<Type>();

    /// <summary>
    /// Gets the circular dependencies detected
    /// </summary>
    public IReadOnlyList<string> CircularDependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <returns>A successful validation result</returns>
    public static PluginDependencyValidationResult Success()
        => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    /// <param name="errors">Validation errors</param>
    /// <param name="missingDependencies">Missing dependencies</param>
    /// <param name="circularDependencies">Circular dependencies</param>
    /// <returns>A failed validation result</returns>
    public static PluginDependencyValidationResult Failure(
        IEnumerable<string> errors,
        IEnumerable<Type>? missingDependencies = null,
        IEnumerable<string>? circularDependencies = null)
        => new()
        {
            IsValid = false,
            Errors = errors.ToList(),
            MissingDependencies = missingDependencies?.ToList() ?? [],
            CircularDependencies = circularDependencies?.ToList() ?? []
        };
}

/// <summary>
/// Plugin lifecycle states
/// </summary>
public enum PluginLifecycleState
{
    /// <summary>
    /// Plugin is not loaded
    /// </summary>
    NotLoaded,

    /// <summary>
    /// Plugin is being loaded
    /// </summary>
    Loading,

    /// <summary>
    /// Plugin is loaded but not initialized
    /// </summary>
    Loaded,

    /// <summary>
    /// Plugin is being initialized
    /// </summary>
    Initializing,

    /// <summary>
    /// Plugin is initialized and ready
    /// </summary>
    Ready,

    /// <summary>
    /// Plugin encountered an error
    /// </summary>
    Error,

    /// <summary>
    /// Plugin is being disposed
    /// </summary>
    Disposing,

    /// <summary>
    /// Plugin is disposed
    /// </summary>
    Disposed
}

/// <summary>
/// Plugin lifecycle event handlers
/// </summary>
public class PluginLifecycleHandlers
{
    /// <summary>
    /// Called when plugin is loaded
    /// </summary>
    public Func<object, Task>? OnLoaded { get; init; }

    /// <summary>
    /// Called when plugin is initialized
    /// </summary>
    public Func<object, Task>? OnInitialized { get; init; }

    /// <summary>
    /// Called when plugin is ready
    /// </summary>
    public Func<object, Task>? OnReady { get; init; }

    /// <summary>
    /// Called when plugin encounters an error
    /// </summary>
    public Func<object, Exception, Task>? OnError { get; init; }

    /// <summary>
    /// Called when plugin is being disposed
    /// </summary>
    public Func<object, Task>? OnDisposing { get; init; }
}
