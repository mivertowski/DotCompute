// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;

namespace DotCompute.Runtime.DependencyInjection;

/// <summary>
/// Implementation of plugin service provider with DI support
/// </summary>
public class PluginServiceProvider : IPluginServiceProvider, IDisposable
{
    private readonly IServiceProvider _rootServiceProvider;
    private readonly ILogger<PluginServiceProvider> _logger;
    private readonly ConcurrentDictionary<string, IServiceScope> _pluginScopes = new();
    private readonly ConcurrentDictionary<string, IServiceCollection> _pluginServices = new();
    private readonly ConcurrentDictionary<string, IServiceProvider> _pluginProviders = new();
    private bool _disposed;

    public PluginServiceProvider(IServiceProvider rootServiceProvider, ILogger<PluginServiceProvider> logger)
    {
        _rootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IServiceScope CreatePluginScope(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PluginServiceProvider));
        }

        return _pluginScopes.GetOrAdd(pluginId, id =>
        {
            _logger.LogDebug("Creating service scope for plugin {PluginId}", id);
            var scope = _rootServiceProvider.CreateScope();
            
            // If plugin has custom services, create a custom provider
            if (_pluginServices.TryGetValue(id, out var services))
            {
                var customProvider = CreatePluginServiceProvider(services, scope.ServiceProvider);
                _pluginProviders[id] = customProvider;
            }
            
            return scope;
        });
    }

    public void RegisterPluginServices(string pluginId, Action<IServiceCollection> configureServices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(configureServices);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PluginServiceProvider));
        }

        var services = new ServiceCollection();
        configureServices(services);
        
        _pluginServices.AddOrUpdate(pluginId, services, (_, existing) =>
        {
            foreach (var service in services)
            {
                existing.Add(service);
            }
            return existing;
        });

        _logger.LogDebug("Registered {Count} services for plugin {PluginId}", services.Count, pluginId);
    }

    public T? GetPluginService<T>(string pluginId) where T : class
    {
        return (T?)GetPluginService(pluginId, typeof(T));
    }

    public object? GetPluginService(string pluginId, Type serviceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(serviceType);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PluginServiceProvider));
        }

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

    public async ValueTask DisposePluginServicesAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        _logger.LogDebug("Disposing services for plugin {PluginId}", pluginId);

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
        _pluginServices.TryRemove(pluginId, out _);
    }

    private IServiceProvider CreatePluginServiceProvider(IServiceCollection pluginServices, IServiceProvider parentProvider)
    {
        // Simple fallback implementation - return the parent provider for now
        // TODO: Implement proper plugin service isolation when ServiceCollection extensions are available
        return parentProvider;
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing PluginServiceProvider");

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
    }
}

/// <summary>
/// Implementation of plugin dependency resolver
/// </summary>
public class PluginDependencyResolver : IPluginDependencyResolver
{
    private readonly ILogger<PluginDependencyResolver> _logger;
    private readonly ConcurrentDictionary<Type, ConstructorInfo> _constructorCache = new();
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    public PluginDependencyResolver(ILogger<PluginDependencyResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public object[] ResolveConstructorDependencies(Type pluginType, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(pluginType);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var constructor = GetBestConstructor(pluginType);
        var parameters = constructor.GetParameters();
        var dependencies = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
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
            var result = initMethod.Invoke(pluginInstance, Array.Empty<object>());
            if (result is Task task)
            {
                await task;
            }
        }
    }

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
            var circularDeps = DetectCircularDependencies(pluginType, serviceProvider, new HashSet<Type>());

            return errors.Count == 0 && circularDeps.Count == 0
                ? PluginDependencyValidationResult.Success()
                : PluginDependencyValidationResult.Failure(errors, missingDependencies, circularDeps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating dependencies for plugin {PluginType}", pluginType.Name);
            return PluginDependencyValidationResult.Failure(new[] { ex.Message }, missingDependencies);
        }
    }

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
                services.Add(parameter.ParameterType);
            }
        }

        // Get property dependencies
        var properties = GetInjectableProperties(pluginType);
        foreach (var property in properties.Where(IsRequiredProperty))
        {
            services.Add(property.PropertyType);
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

    private PropertyInfo[] GetInjectableProperties(Type type)
    {
        return _propertyCache.GetOrAdd(type, t =>
        {
            return t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.GetCustomAttributes(typeof(InjectAttribute), false).Any())
                .ToArray();
        });
    }

    private static bool IsOptionalParameter(ParameterInfo parameter)
    {
        return parameter.HasDefaultValue || 
               parameter.GetCustomAttributes(typeof(OptionalAttribute), false).Any() ||
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

        visited.Add(type);
        
        try
        {
            var dependencies = GetRequiredServices(type);
            foreach (var dependency in dependencies)
            {
                var dependencyCircular = DetectCircularDependencies(dependency, serviceProvider, new HashSet<Type>(visited));
                circular.AddRange(dependencyCircular);
            }
        }
        catch
        {
            // Ignore errors in dependency detection for circular check
        }
        finally
        {
            visited.Remove(type);
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