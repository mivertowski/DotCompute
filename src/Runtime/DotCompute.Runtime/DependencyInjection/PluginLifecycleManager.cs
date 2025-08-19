// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// using DotCompute.Algorithms.Types // Commented out - missing reference.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;
using DotCompute.Abstractions;

namespace DotCompute.Runtime.DependencyInjection;


/// <summary>
/// Implementation of plugin lifecycle manager with DI support
/// </summary>
public class PluginLifecycleManager : IPluginLifecycleManager, IDisposable
{
private readonly IPluginDependencyResolver _dependencyResolver;
private readonly ILogger<PluginLifecycleManager> _logger;
private readonly ConcurrentDictionary<string, PluginLifecycleState> _pluginStates = new();
private readonly ConcurrentDictionary<string, object> _pluginInstances = new();
private readonly ConcurrentDictionary<string, PluginLifecycleHandlers> _lifecycleHandlers = new();
private readonly ConcurrentDictionary<string, IServiceScope> _pluginScopes = new();
private bool _disposed;

public PluginLifecycleManager(
    IPluginDependencyResolver dependencyResolver,
    ILogger<PluginLifecycleManager> logger)
{
    _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}

public async Task<T> CreatePluginAsync<T>(Type pluginType, IServiceProvider serviceProvider) 
    where T : class
{
    ArgumentNullException.ThrowIfNull(pluginType);
    ArgumentNullException.ThrowIfNull(serviceProvider);

    if (_disposed)
    {
        throw new ObjectDisposedException(nameof(PluginLifecycleManager));
    }

    var pluginId = GetPluginId(pluginType);
    
    try
    {
        await SetStateAsync(pluginId, PluginLifecycleState.Loading);
        
        _logger.LogDebug("Creating plugin instance of type {PluginType}", pluginType.Name);

        // Validate dependencies first
        var validation = _dependencyResolver.ValidateDependencies(pluginType, serviceProvider);
        if (!validation.IsValid)
        {
            await SetStateAsync(pluginId, PluginLifecycleState.Error);
            throw new InvalidOperationException(
                $"Plugin {pluginType.Name} has unresolved dependencies: {string.Join(", ", validation.Errors)}");
        }

        // Resolve constructor dependencies
        var dependencies = _dependencyResolver.ResolveConstructorDependencies(pluginType, serviceProvider);
        
        // Create instance
        var instance = Activator.CreateInstance(pluginType, dependencies);
        if (instance == null)
        {
            await SetStateAsync(pluginId, PluginLifecycleState.Error);
            throw new InvalidOperationException($"Failed to create instance of plugin {pluginType.Name}");
        }

        if (instance is not T typedInstance)
        {
            await SetStateAsync(pluginId, PluginLifecycleState.Error);
            throw new InvalidOperationException(
                $"Plugin {pluginType.Name} does not implement required interface {typeof(T).Name}");
        }

        _pluginInstances[pluginId] = instance;
        await SetStateAsync(pluginId, PluginLifecycleState.Loaded);

        // Inject properties
        await _dependencyResolver.InjectPropertiesAsync(instance, serviceProvider);
        
        return typedInstance;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create plugin instance of type {PluginType}", pluginType.Name);
        await SetStateAsync(pluginId, PluginLifecycleState.Error);
        await NotifyErrorAsync(pluginId, null, ex);
        throw;
    }
}

public async Task InitializePluginAsync(object plugin, IServiceProvider serviceProvider)
{
    ArgumentNullException.ThrowIfNull(plugin);
    ArgumentNullException.ThrowIfNull(serviceProvider);

    if (_disposed)
    {
        throw new ObjectDisposedException(nameof(PluginLifecycleManager));
    }

    var pluginId = GetPluginId(plugin.GetType());
    
    try
    {
        await SetStateAsync(pluginId, PluginLifecycleState.Initializing);
        
        _logger.LogDebug("Initializing plugin {PluginType}", plugin.GetType().Name);

        // Call plugin-specific initialization if it exists
        // Note: IAlgorithmPlugin interface is commented out due to missing reference
        // This would be enabled when the proper plugin interfaces are available
        /*
        if (plugin is IAlgorithmPlugin algorithmPlugin)
        {
            var accelerator = serviceProvider.GetService<IAccelerator>();
            if (accelerator != null)
            {
                await algorithmPlugin.InitializeAsync(accelerator);
            }
        }
        */

        // Call generic initialization method if available
        var initMethod = plugin.GetType().GetMethod("InitializeAsync");
        if (initMethod != null && initMethod.ReturnType == typeof(Task))
        {
            var result = initMethod.Invoke(plugin, Array.Empty<object>());
            if (result is Task task)
            {
                await task;
            }
        }

        await SetStateAsync(pluginId, PluginLifecycleState.Ready);
        await NotifyReadyAsync(pluginId, plugin);
        
        _logger.LogInformation("Plugin {PluginType} initialized successfully", plugin.GetType().Name);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize plugin {PluginType}", plugin.GetType().Name);
        await SetStateAsync(pluginId, PluginLifecycleState.Error);
        await NotifyErrorAsync(pluginId, plugin, ex);
        throw;
    }
}

public async ValueTask DisposePluginAsync(object plugin)
{
    ArgumentNullException.ThrowIfNull(plugin);

    var pluginId = GetPluginId(plugin.GetType());
    
    try
    {
        await SetStateAsync(pluginId, PluginLifecycleState.Disposing);
        await NotifyDisposingAsync(pluginId, plugin);
        
        _logger.LogDebug("Disposing plugin {PluginType}", plugin.GetType().Name);

        // Dispose plugin if it implements IDisposable/IAsyncDisposable
        switch (plugin)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        // Clean up tracking
        _pluginInstances.TryRemove(pluginId, out _);
        _lifecycleHandlers.TryRemove(pluginId, out _);
        
        if (_pluginScopes.TryRemove(pluginId, out var scope))
        {
            scope.Dispose();
        }

        await SetStateAsync(pluginId, PluginLifecycleState.Disposed);
        
        _logger.LogDebug("Plugin {PluginType} disposed successfully", plugin.GetType().Name);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error disposing plugin {PluginType}", plugin.GetType().Name);
        await SetStateAsync(pluginId, PluginLifecycleState.Error);
        throw;
    }
}

public PluginLifecycleState GetPluginState(string pluginId)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
    
    return _pluginStates.GetValueOrDefault(pluginId, PluginLifecycleState.NotLoaded);
}

public void RegisterLifecycleHandlers(string pluginId, PluginLifecycleHandlers eventHandlers)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
    ArgumentNullException.ThrowIfNull(eventHandlers);

    if (_disposed)
    {
        throw new ObjectDisposedException(nameof(PluginLifecycleManager));
    }

    _lifecycleHandlers[pluginId] = eventHandlers;
    _logger.LogDebug("Registered lifecycle handlers for plugin {PluginId}", pluginId);
}

private async Task SetStateAsync(string pluginId, PluginLifecycleState newState)
{
    var oldState = _pluginStates.GetValueOrDefault(pluginId, PluginLifecycleState.NotLoaded);
    _pluginStates[pluginId] = newState;
    
    _logger.LogTrace("Plugin {PluginId} state changed from {OldState} to {NewState}", 
        pluginId, oldState, newState);

    // Call appropriate lifecycle event
    if (_pluginInstances.TryGetValue(pluginId, out var instance))
    {
        switch (newState)
        {
            case PluginLifecycleState.Loaded:
                await NotifyLoadedAsync(pluginId, instance);
                break;
            case PluginLifecycleState.Ready:
                await NotifyInitializedAsync(pluginId, instance);
                break;
        }
    }
}

private async Task NotifyLoadedAsync(string pluginId, object plugin)
{
    if (_lifecycleHandlers.TryGetValue(pluginId, out var handlers) && handlers.OnLoaded != null)
    {
        try
        {
            await handlers.OnLoaded(plugin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in OnLoaded handler for plugin {PluginId}", pluginId);
        }
    }
}

private async Task NotifyInitializedAsync(string pluginId, object plugin)
{
    if (_lifecycleHandlers.TryGetValue(pluginId, out var handlers) && handlers.OnInitialized != null)
    {
        try
        {
            await handlers.OnInitialized(plugin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in OnInitialized handler for plugin {PluginId}", pluginId);
        }
    }
}

private async Task NotifyReadyAsync(string pluginId, object plugin)
{
    if (_lifecycleHandlers.TryGetValue(pluginId, out var handlers) && handlers.OnReady != null)
    {
        try
        {
            await handlers.OnReady(plugin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in OnReady handler for plugin {PluginId}", pluginId);
        }
    }
}

private async Task NotifyErrorAsync(string pluginId, object? plugin, Exception error)
{
    if (_lifecycleHandlers.TryGetValue(pluginId, out var handlers) && handlers.OnError != null && plugin != null)
    {
        try
        {
            await handlers.OnError(plugin, error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in OnError handler for plugin {PluginId}", pluginId);
        }
    }
}

private async Task NotifyDisposingAsync(string pluginId, object plugin)
{
    if (_lifecycleHandlers.TryGetValue(pluginId, out var handlers) && handlers.OnDisposing != null)
    {
        try
        {
            await handlers.OnDisposing(plugin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in OnDisposing handler for plugin {PluginId}", pluginId);
        }
    }
}

private static string GetPluginId(Type pluginType)
{
    return $"{pluginType.Assembly.GetName().Name}::{pluginType.FullName}";
}

public void Dispose()
{
    if (_disposed)
    {
        return;
    }

    _logger.LogDebug("Disposing PluginLifecycleManager");

    // Dispose all plugin scopes
    foreach (var scope in _pluginScopes.Values)
    {
        try
        {
            scope.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing plugin scope");
        }
    }

    // Dispose all plugin instances
    foreach (var instance in _pluginInstances.Values)
    {
        try
        {
            switch (instance)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                case IAsyncDisposable asyncDisposable:
                    // Note: Can't await in Dispose, so we'll fire and forget
                    _ = asyncDisposable.DisposeAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing plugin instance");
        }
    }

    _pluginStates.Clear();
    _pluginInstances.Clear();
    _lifecycleHandlers.Clear();
    _pluginScopes.Clear();
    
    _disposed = true;
}
}

/// <summary>
/// Default implementation of plugin factory with DI support
/// </summary>
public class DefaultPluginFactory : IPluginFactory
{
private readonly IPluginLifecycleManager _lifecycleManager;
private readonly ILogger<DefaultPluginFactory> _logger;

public DefaultPluginFactory(
    IPluginLifecycleManager lifecycleManager,
    ILogger<DefaultPluginFactory> logger)
{
    _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}

public async Task<T> CreateAsync<T>(Type pluginType, IServiceScope serviceScope) where T : class
{
    ArgumentNullException.ThrowIfNull(pluginType);
    ArgumentNullException.ThrowIfNull(serviceScope);

    if (!CanCreate(pluginType))
    {
        throw new ArgumentException($"Cannot create plugin of type {pluginType.Name}", nameof(pluginType));
    }

    _logger.LogDebug("Creating plugin {PluginType} with interface {InterfaceType}", 
        pluginType.Name, typeof(T).Name);

    try
    {
        // Create the plugin instance
        var plugin = await _lifecycleManager.CreatePluginAsync<T>(pluginType, serviceScope.ServiceProvider);
        
        // Initialize the plugin
        await _lifecycleManager.InitializePluginAsync(plugin, serviceScope.ServiceProvider);
        
        return plugin;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create plugin {PluginType}", pluginType.Name);
        throw;
    }
}

public bool CanCreate(Type pluginType)
{
    ArgumentNullException.ThrowIfNull(pluginType);

    try
    {
        // Check if type is concrete
        if (pluginType.IsAbstract || pluginType.IsInterface)
        {
            return false;
        }

        // Check if type has a public constructor
        var constructors = pluginType.GetConstructors();
        if (!constructors.Any())
        {
            return false;
        }

        // Type looks creatable
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Error checking if type {PluginType} can be created", pluginType.Name);
        return false;
    }
}

public IEnumerable<ServiceDescriptor> GetRequiredServiceDescriptors(Type pluginType)
{
    ArgumentNullException.ThrowIfNull(pluginType);

    var descriptors = new List<ServiceDescriptor>();

    try
    {
        // Get constructor dependencies
        var constructors = pluginType.GetConstructors();
        var bestConstructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
        
        if (bestConstructor != null)
        {
            foreach (var parameter in bestConstructor.GetParameters())
            {
                descriptors.Add(ServiceDescriptor.Transient(parameter.ParameterType, parameter.ParameterType));
            }
        }

        // Get property dependencies
        var properties = pluginType.GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(InjectAttribute), false).Any());
        
        foreach (var property in properties)
        {
            descriptors.Add(ServiceDescriptor.Transient(property.PropertyType, property.PropertyType));
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Error getting service descriptors for {PluginType}", pluginType.Name);
    }

    return descriptors;
}
}
