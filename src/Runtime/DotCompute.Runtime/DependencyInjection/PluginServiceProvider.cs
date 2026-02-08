// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using DotCompute.Runtime.DependencyInjection.Scopes;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.DependencyInjection;


/// <summary>
/// Implementation of plugin service provider with DI support
/// </summary>
public class PluginServiceProvider(IServiceProvider rootServiceProvider, ILogger<PluginServiceProvider> logger) : IPluginServiceProvider, IDisposable
{
    private readonly IServiceProvider _rootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
    private readonly ILogger<PluginServiceProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<string, IServiceScope> _pluginScopes = new();
    private readonly ConcurrentDictionary<string, IServiceCollection> _pluginServices = new();
    private readonly ConcurrentDictionary<string, IServiceProvider> _pluginProviders = new();
    private bool _disposed;
    /// <summary>
    /// Creates a new plugin scope.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The created plugin scope.</returns>

    public IServiceScope CreatePluginScope(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        ObjectDisposedException.ThrowIf(_disposed, this);

        return _pluginScopes.GetOrAdd(pluginId, id =>
        {
            _logger.LogDebugMessage($"Creating service scope for plugin {id}");
            var scope = _rootServiceProvider.CreateScope();

            // If plugin has custom services, create an isolated provider
            if (_pluginServices.TryGetValue(id, out var services))
            {
                var isolatedProvider = CreatePluginServiceProvider(id, services, scope.ServiceProvider);
                _pluginProviders[id] = isolatedProvider;
            }

            return scope;
        });
    }
    /// <summary>
    /// Performs register plugin services.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="configureServices">The configure services.</param>

    public void RegisterPluginServices(string pluginId, Action<IServiceCollection> configureServices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(configureServices);

        ObjectDisposedException.ThrowIf(_disposed, this);

        var services = new ServiceCollection();
        configureServices(services);

        _ = _pluginServices.AddOrUpdate(pluginId, services, (_, existing) =>
        {
            foreach (var service in services)
            {
                existing.Add(service);
            }
            return existing;
        });

        _logger.LogDebugMessage($"Registered {services.Count} services for plugin {pluginId}");
    }
    /// <summary>
    /// Gets the plugin service.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The plugin service.</returns>

    public T? GetPluginService<T>(string pluginId) where T : class => (T?)GetPluginService(pluginId, typeof(T));
    /// <summary>
    /// Gets the plugin service.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="serviceType">The service type.</param>
    /// <returns>The plugin service.</returns>

    public object? GetPluginService(string pluginId, Type serviceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(serviceType);

        ObjectDisposedException.ThrowIf(_disposed, this);

        // First try custom plugin provider
        if (_pluginProviders.TryGetValue(pluginId, out var pluginProvider))
        {
            var service = pluginProvider.GetService(serviceType);
            if (service != null)
            {
                return service;
            }
        }

        // Fallback to plugin scope
        if (_pluginScopes.TryGetValue(pluginId, out var scope))
        {
            return scope.ServiceProvider.GetService(serviceType);
        }

        // Last resort: root provider
        return _rootServiceProvider.GetService(serviceType);
    }
    /// <summary>
    /// Determines whether service registered.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="serviceType">The service type.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>

    public bool IsServiceRegistered(string pluginId, Type serviceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(serviceType);

        if (_disposed)
        {
            return false;
        }

        // Check custom plugin services
        if (_pluginServices.TryGetValue(pluginId, out var services))
        {
            if (services.Any(s => s.ServiceType == serviceType))
            {
                return true;
            }
        }

        // Check if available in root provider
        return _rootServiceProvider.GetService(serviceType) != null;
    }
    /// <summary>
    /// Gets dispose plugin services asynchronously.
    /// </summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask DisposePluginServicesAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        _logger.LogDebugMessage("Disposing services for plugin {pluginId}");

        // Dispose custom provider
        if (_pluginProviders.TryRemove(pluginId, out var provider))
        {
            if (provider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            else if (provider is IAsyncDisposable asyncDisposableProvider)
            {
                await asyncDisposableProvider.DisposeAsync();
            }
        }

        // Dispose scope
        if (_pluginScopes.TryRemove(pluginId, out var scope))
        {
            scope.Dispose();
        }

        // Remove service collection
        _ = _pluginServices.TryRemove(pluginId, out _);
    }

    private IServiceProvider CreatePluginServiceProvider(string pluginId, IServiceCollection pluginServices, IServiceProvider parentProvider)
    {
        // Create isolated service provider for the plugin
        var isolatedProvider = new IsolatedPluginServiceProvider(
            pluginId: pluginId,
            pluginServices: pluginServices,
            parentServiceProvider: parentProvider,
            allowParentFallback: true,
            blockedParentServices: null,
            logger: _logger);

        _logger.LogDebugMessage($"Created isolated service provider for plugin {pluginId}");
        return isolatedProvider;
    }

    private static IEnumerable<ServiceDescriptor> GetServiceDescriptors(IServiceProvider provider)
    {
        // This is a simplified approach. In production, you might want to use a more sophisticated method
        // to extract service descriptors from an existing provider
        if (provider.GetService<IServiceCollection>() is { } serviceCollection)
        {
            return serviceCollection;
        }

        return Array.Empty<ServiceDescriptor>();
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebugMessage("Disposing PluginServiceProvider");

        // Dispose all plugin providers
        foreach (var provider in _pluginProviders.Values)
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        // Dispose all scopes
        foreach (var scope in _pluginScopes.Values)
        {
            scope.Dispose();
        }

        _pluginProviders.Clear();
        _pluginScopes.Clear();
        _pluginServices.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Implementation of plugin dependency resolver
/// </summary>
public class PluginDependencyResolver(ILogger<PluginDependencyResolver> logger) : IPluginDependencyResolver
{
    private readonly ILogger<PluginDependencyResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ConcurrentDictionary<Type, ConstructorInfo> _constructorCache = new();
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
    /// <summary>
    /// Gets resolve constructor dependencies.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The result of the operation.</returns>

    public object[] ResolveConstructorDependencies(Type pluginType, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var constructor = GetBestConstructor(pluginType);
        var parameters = constructor.GetParameters();
        var dependencies = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameterType = parameters[i].ParameterType;
            var service = serviceProvider.GetService(parameterType);

            if (service == null && !IsOptionalParameter(parameters[i]))
            {
                throw new InvalidOperationException($"Required dependency '{parameterType.Name}' could not be resolved for plugin '{pluginType.Name}'");
            }

            dependencies[i] = service!;
        }

        return dependencies;
    }
    /// <summary>
    /// Gets inject properties asynchronously.
    /// </summary>
    /// <param name="pluginInstance">The plugin instance.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The result of the operation.</returns>

    public async Task InjectPropertiesAsync(object pluginInstance, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(pluginInstance);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var pluginType = pluginInstance.GetType();
        var properties = GetInjectableProperties(pluginType);

        foreach (var property in properties)
        {
            if (!property.CanWrite)
            {
                continue;
            }

            var service = serviceProvider.GetService(property.PropertyType);
            if (service != null)
            {
                property.SetValue(pluginInstance, service);
                _logger.LogTrace("Injected {PropertyType} into {PluginType}.{PropertyName}",
                    property.PropertyType.Name, pluginType.Name, property.Name);
            }
            else if (IsRequiredProperty(property))
            {
                throw new InvalidOperationException($"Required property dependency '{property.PropertyType.Name}' could not be resolved for plugin '{pluginType.Name}'");
            }
        }

        // Call initialization method if available
        var initMethod = pluginType.GetMethod("InitializeAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (initMethod != null && initMethod.ReturnType == typeof(Task))
        {
            var result = initMethod.Invoke(pluginInstance, []);
            if (result is Task task)
            {
                await task;
            }
        }
    }
    /// <summary>
    /// Validates the dependencies.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The result of the operation.</returns>

    public PluginDependencyValidationResult ValidateDependencies(Type pluginType, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var errors = new List<string>();
        var missingDependencies = new List<Type>();

        try
        {
            // Validate constructor dependencies
            var constructor = GetBestConstructor(pluginType);
            var parameters = constructor.GetParameters();

            foreach (var parameter in parameters)
            {
                var service = serviceProvider.GetService(parameter.ParameterType);
                if (service == null && !IsOptionalParameter(parameter))
                {
                    errors.Add($"Required constructor dependency '{parameter.ParameterType.Name}' is not registered");
                    missingDependencies.Add(parameter.ParameterType);
                }
            }

            // Validate property dependencies
            var properties = GetInjectableProperties(pluginType);
            foreach (var property in properties.Where(IsRequiredProperty))
            {
                var service = serviceProvider.GetService(property.PropertyType);
                if (service == null)
                {
                    errors.Add($"Required property dependency '{property.PropertyType.Name}' is not registered");
                    missingDependencies.Add(property.PropertyType);
                }
            }

            // Check for circular dependencies (simplified check)
            var circularDeps = DetectCircularDependencies(pluginType, serviceProvider, []);

            return errors.Count == 0 && circularDeps.Count == 0
                ? PluginDependencyValidationResult.Success()
                : PluginDependencyValidationResult.Failure(errors, missingDependencies, circularDeps);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error validating dependencies for plugin {pluginType.Name}");
            return PluginDependencyValidationResult.Failure([ex.Message], missingDependencies);
        }
    }
    /// <summary>
    /// Gets the required services.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>The required services.</returns>

    public IEnumerable<Type> GetRequiredServices(Type pluginType)
    {
        ArgumentNullException.ThrowIfNull(pluginType);

        var services = new HashSet<Type>();

        // Get constructor dependencies
        var constructor = GetBestConstructor(pluginType);
        foreach (var parameter in constructor.GetParameters())
        {
            if (!IsOptionalParameter(parameter))
            {
                _ = services.Add(parameter.ParameterType);
            }
        }

        // Get property dependencies
        var properties = GetInjectableProperties(pluginType);
        foreach (var property in properties.Where(IsRequiredProperty))
        {
            _ = services.Add(property.PropertyType);
        }

        return services;
    }

    private ConstructorInfo GetBestConstructor(Type type)
    {
        return _constructorCache.GetOrAdd(type, t =>
        {
            var constructors = t.GetConstructors();

            // Prefer constructor with most parameters (dependency injection friendly)
            return constructors.OrderByDescending(c => c.GetParameters().Length).First();
        });
    }

    private PropertyInfo[] GetInjectableProperties(Type type) => _propertyCache.GetOrAdd(type, t => [.. t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(p => p.GetCustomAttributes(typeof(InjectAttribute), false).Length != 0)]);

    private static bool IsOptionalParameter(ParameterInfo parameter)
    {
        return parameter.HasDefaultValue ||
               parameter.GetCustomAttributes(typeof(OptionalAttribute), false).Length != 0 ||
               Nullable.GetUnderlyingType(parameter.ParameterType) != null;
    }

    private static bool IsRequiredProperty(PropertyInfo property)
    {
        var injectAttr = property.GetCustomAttributes(typeof(InjectAttribute), false).FirstOrDefault() as InjectAttribute;
        return injectAttr?.Required != false;
    }

    private List<string> DetectCircularDependencies(Type type, IServiceProvider serviceProvider, HashSet<Type> visited)
    {
        var circular = new List<string>();

        if (visited.Contains(type))
        {
            circular.Add($"Circular dependency detected involving {type.Name}");
            return circular;
        }

        _ = visited.Add(type);

        try
        {
            var dependencies = GetRequiredServices(type);
            foreach (var dependency in dependencies)
            {
                var dependencyCircular = DetectCircularDependencies(dependency, serviceProvider, [.. visited]);
                circular.AddRange(dependencyCircular);
            }
        }
        catch
        {
            // Ignore errors in dependency detection for circular check
        }
        finally
        {
            _ = visited.Remove(type);
        }

        return circular;
    }
}

/// <summary>
/// Attribute to mark properties for dependency injection
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class InjectAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether this dependency is required
    /// </summary>
    public bool Required { get; set; } = true;
}

/// <summary>
/// Attribute to mark parameters as optional for dependency injection
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class OptionalAttribute : Attribute
{
}
