// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// using DotCompute.Algorithms.Types // Commented out - missing reference.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Runtime.DependencyInjection;


/// <summary>
/// Implementation of plugin lifecycle manager with DI support
/// </summary>
public class PluginLifecycleManager(
    IPluginDependencyResolver dependencyResolver,
    ILogger<PluginLifecycleManager> logger) : IPluginLifecycleManager, IDisposable
{
    private readonly IPluginDependencyResolver _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
    private readonly ILogger<PluginLifecycleManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, PluginLifecycleState> _pluginStates = new();
    private readonly ConcurrentDictionary<string, object> _pluginInstances = new();
    private readonly ConcurrentDictionary<string, PluginLifecycleHandlers> _lifecycleHandlers = new();
    private readonly ConcurrentDictionary<string, IServiceScope> _pluginScopes = new();
    private bool _disposed;
    /// <summary>
    /// Creates a new plugin async.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="pluginType">The plugin type.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The created plugin async.</returns>

    public async Task<T> CreatePluginAsync<T>(Type pluginType, IServiceProvider serviceProvider)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        ObjectDisposedException.ThrowIf(_disposed, this);

        var pluginId = GetPluginId(pluginType);

        try
        {
            await SetStateAsync(pluginId, PluginLifecycleState.Loading);

            _logger.LogDebugMessage("Creating plugin instance of type {pluginType.Name}");

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
            _logger.LogErrorMessage(ex, $"Failed to create plugin instance of type {pluginType.Name}");
            await SetStateAsync(pluginId, PluginLifecycleState.Error);
            await NotifyErrorAsync(pluginId, null, ex);
            throw;
        }
    }
    /// <summary>
    /// Initializes the plugin async.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The result of the operation.</returns>

    public async Task InitializePluginAsync(object plugin, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        ObjectDisposedException.ThrowIf(_disposed, this);

        var pluginId = GetPluginId(plugin.GetType());

        try
        {
            await SetStateAsync(pluginId, PluginLifecycleState.Initializing);

            _logger.LogDebugMessage($"Initializing plugin {plugin.GetType().Name}");

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
                var result = initMethod.Invoke(plugin, []);
                if (result is Task task)
                {
                    await task;
                }
            }

            await SetStateAsync(pluginId, PluginLifecycleState.Ready);
            await NotifyReadyAsync(pluginId, plugin);

            _logger.LogInfoMessage($"Plugin {plugin.GetType().Name} initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to initialize plugin {plugin.GetType().Name}");
            await SetStateAsync(pluginId, PluginLifecycleState.Error);
            await NotifyErrorAsync(pluginId, plugin, ex);
            throw;
        }
    }
    /// <summary>
    /// Gets dispose plugin asynchronously.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask DisposePluginAsync(object plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        var pluginId = GetPluginId(plugin.GetType());

        try
        {
            await SetStateAsync(pluginId, PluginLifecycleState.Disposing);
            await NotifyDisposingAsync(pluginId, plugin);

            _logger.LogDebugMessage($"Disposing plugin {plugin.GetType().Name}");

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
            _ = _pluginInstances.TryRemove(pluginId, out _);
            _ = _lifecycleHandlers.TryRemove(pluginId, out _);

            if (_pluginScopes.TryRemove(pluginId, out var scope))
            {
                scope.Dispose();
            }

            await SetStateAsync(pluginId, PluginLifecycleState.Disposed);

            _logger.LogDebugMessage($"Plugin {plugin.GetType().Name} disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error disposing plugin {plugin.GetType().Name}");
            await SetStateAsync(pluginId, PluginLifecycleState.Error);
            throw;
        }
    }
    /// <summary>
    /// Gets the plugin state.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin state.</returns>

    public PluginLifecycleState GetPluginState(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return _pluginStates.GetValueOrDefault(pluginId, PluginLifecycleState.NotLoaded);
    }
    /// <summary>
    /// Performs register lifecycle handlers.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="eventHandlers">The event handlers.</param>

    public void RegisterLifecycleHandlers(string pluginId, PluginLifecycleHandlers eventHandlers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(eventHandlers);

        ObjectDisposedException.ThrowIf(_disposed, this);

        _lifecycleHandlers[pluginId] = eventHandlers;
        _logger.LogDebugMessage("Registered lifecycle handlers for plugin {pluginId}");
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

    private static string GetPluginId(Type pluginType) => $"{pluginType.Assembly.GetName().Name}::{pluginType.FullName}";
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebugMessage("Disposing PluginLifecycleManager");

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
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Default implementation of plugin factory with DI support
/// </summary>
public class DefaultPluginFactory(
    IPluginLifecycleManager lifecycleManager,
    ILogger<DefaultPluginFactory> logger) : IPluginFactory
{
    private readonly IPluginLifecycleManager _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
    private readonly ILogger<DefaultPluginFactory> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    /// <summary>
    /// Creates a new async.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="pluginType">The plugin type.</param>
    /// <param name="serviceScope">The service scope.</param>
    /// <returns>The created async.</returns>

    public async Task<T> CreateAsync<T>(Type pluginType, IServiceScope serviceScope) where T : class
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        ArgumentNullException.ThrowIfNull(serviceScope);

        if (!CanCreate(pluginType))
        {
            throw new ArgumentException($"Cannot create plugin of type {pluginType.Name}", nameof(pluginType));
        }

        _logger.LogDebugMessage($"Creating plugin {pluginType.Name} with interface {typeof(T).Name}");

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
            _logger.LogErrorMessage(ex, $"Failed to create plugin {pluginType.Name}");
            throw;
        }
    }
    /// <summary>
    /// Determines whether create.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

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
            if (constructors.Length == 0)
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
    /// <summary>
    /// Gets the required service descriptors.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>The required service descriptors.</returns>

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
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' - Plugin DI requires runtime dependency resolution
                    descriptors.Add(ServiceDescriptor.Transient(parameter.ParameterType, parameter.ParameterType));
#pragma warning restore IL2072
                }
            }

            // Get property dependencies
            var properties = pluginType.GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(InjectAttribute), false).Length != 0);

            foreach (var property in properties)
            {
#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' - Plugin DI requires runtime dependency resolution
                descriptors.Add(ServiceDescriptor.Transient(property.PropertyType, property.PropertyType));
#pragma warning restore IL2072
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting service descriptors for {PluginType}", pluginType.Name);
        }

        return descriptors;
    }
}
