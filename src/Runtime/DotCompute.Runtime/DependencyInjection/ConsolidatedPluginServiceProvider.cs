// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace DotCompute.Runtime.DependencyInjection;

/// <summary>
/// Consolidated plugin service provider combining features from both Runtime and Plugins implementations.
/// Provides comprehensive dependency injection with isolation, lifecycle management, and advanced features.
/// </summary>
public sealed class ConsolidatedPluginServiceProvider : IPluginServiceProvider, IServiceProvider, IDisposable
{
    private readonly IServiceProvider _hostServiceProvider;
    private readonly IServiceCollection _pluginServices;
    private readonly IServiceProvider _pluginServiceProvider;
    private readonly ILogger<ConsolidatedPluginServiceProvider> _logger;
    private readonly ConcurrentDictionary<Type, object> _singletonCache;
    private readonly ConcurrentDictionary<Assembly, IServiceScope> _assemblyScopes;
    private readonly ConcurrentDictionary<string, IServiceScope> _pluginScopes;
    private readonly ConcurrentDictionary<string, IServiceCollection> _pluginServiceCollections;
    private readonly ConcurrentDictionary<string, IServiceProvider> _pluginProviders;
    private readonly ConsolidatedPluginServiceProviderOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsolidatedPluginServiceProvider"/> class.
    /// </summary>
    /// <param name="hostServiceProvider">The host application's service provider.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options.</param>
    public ConsolidatedPluginServiceProvider(
        IServiceProvider hostServiceProvider,
        ILogger<ConsolidatedPluginServiceProvider> logger,
        ConsolidatedPluginServiceProviderOptions? options = null)
    {
        _hostServiceProvider = hostServiceProvider ?? throw new ArgumentNullException(nameof(hostServiceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ConsolidatedPluginServiceProviderOptions();

        _singletonCache = new ConcurrentDictionary<Type, object>();
        _assemblyScopes = new ConcurrentDictionary<Assembly, IServiceScope>();
        _pluginScopes = new ConcurrentDictionary<string, IServiceScope>();
        _pluginServiceCollections = new ConcurrentDictionary<string, IServiceCollection>();
        _pluginProviders = new ConcurrentDictionary<string, IServiceProvider>();
        _pluginServices = new ServiceCollection();

        // Configure plugin services
        ConfigurePluginServices();

        // Build the plugin service provider
        _pluginServiceProvider = _pluginServices.BuildServiceProvider();

        _logger.LogInfoMessage($"Consolidated plugin service provider initialized with {_pluginServices.Count} plugin services");
    }

    #region IPluginServiceProvider Implementation (from Runtime version)

    /// <inheritdoc/>
    public IServiceScope CreatePluginScope(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _pluginScopes.GetOrAdd(pluginId, id =>
        {
            _logger.LogDebugMessage("Creating service scope for plugin {id}");
            var scope = _hostServiceProvider.CreateScope();

            // If plugin has custom services, create a custom provider
            if (_pluginServiceCollections.TryGetValue(id, out var services))
            {
                var customProvider = CreatePluginServiceProvider(services, scope.ServiceProvider);
                _pluginProviders[id] = customProvider;
            }

            return scope;
        });
    }

    /// <inheritdoc/>
    public void RegisterPluginServices(string pluginId, Action<IServiceCollection> configureServices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(configureServices);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var services = new ServiceCollection();
        configureServices(services);

        _ = _pluginServiceCollections.AddOrUpdate(pluginId, services, (_, existing) =>
        {
            foreach (var service in services)
            {
                existing.Add(service);
            }
            return existing;
        });

        _logger.LogDebugMessage($"Registered {services.Count} services for plugin {pluginId}");
    }

    /// <inheritdoc/>
    public T? GetPluginService<T>(string pluginId) where T : class => (T?)GetPluginService(pluginId, typeof(T));

    /// <inheritdoc/>
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
        return _hostServiceProvider.GetService(serviceType);
    }

    /// <inheritdoc/>
    public bool IsServiceRegistered(string pluginId, Type serviceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(serviceType);

        if (_disposed)
        {
            return false;
        }

        // Check custom plugin services
        if (_pluginServiceCollections.TryGetValue(pluginId, out var services))
        {
            if (services.Any(s => s.ServiceType == serviceType))
            {
                return true;
            }
        }

        // Check if available in root provider
        return _hostServiceProvider.GetService(serviceType) != null;
    }

    /// <inheritdoc/>
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
        _ = _pluginServiceCollections.TryRemove(pluginId, out _);
    }

    #endregion

    #region Advanced Features (from Plugins version)

    /// <summary>
    /// Creates a scoped service provider for plugin execution.
    /// </summary>
    /// <param name="pluginAssembly">The plugin assembly that will use this scope.</param>
    /// <returns>A scoped service provider.</returns>
    public IServiceScope CreatePluginScope(Assembly? pluginAssembly = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var scope = _pluginServiceProvider.CreateScope();

        if (pluginAssembly != null)
        {
            // Inject assembly-specific services if configured
            var scopedProvider = new PluginScopedServiceProvider(scope, pluginAssembly, _logger);
            return new PluginServiceScope(scopedProvider, scope);
        }

        return scope;
    }

    /// <summary>
    /// Registers services from a plugin assembly.
    /// </summary>
    /// <param name="pluginAssembly">The plugin assembly to register services from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RegisterPluginServicesAsync(Assembly pluginAssembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginAssembly);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogDebugMessage("Registering services from plugin assembly: {pluginAssembly.FullName}");

        try
        {
            // Create a scope for this assembly's services
            var scope = _pluginServiceProvider.CreateScope();
            _ = _assemblyScopes.TryAdd(pluginAssembly, scope);

            // Discover and register services from the assembly
            await DiscoverAndRegisterServicesAsync(pluginAssembly, scope.ServiceProvider, cancellationToken);

            _logger.LogInfoMessage($"Successfully registered services from plugin assembly: {pluginAssembly.GetName().Name}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to register services from plugin assembly: {pluginAssembly.FullName}");
            throw;
        }
    }

    /// <summary>
    /// Unregisters services from a plugin assembly.
    /// </summary>
    /// <param name="pluginAssembly">The plugin assembly to unregister services from.</param>
    public void UnregisterPluginServices(Assembly pluginAssembly)
    {
        ArgumentNullException.ThrowIfNull(pluginAssembly);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_assemblyScopes.TryRemove(pluginAssembly, out var scope))
        {
            scope.Dispose();
            _logger.LogInfoMessage($"Unregistered services from plugin assembly: {pluginAssembly.GetName().Name}");
        }
    }

    /// <summary>
    /// Injects dependencies into an existing object.
    /// </summary>
    /// <param name="instance">The object to inject dependencies into.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InjectDependenciesAsync(object instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.Run(() => InjectDependencies(instance), cancellationToken);
    }

    /// <summary>
    /// Gets comprehensive health information for the plugin service provider.
    /// </summary>
    /// <returns>Health information including service counts, memory usage, and errors.</returns>
    public ConsolidatedPluginServiceProviderHealth GetHealth()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var totalServices = _pluginServices.Count;
            var activeScopes = _assemblyScopes.Count;
            var singletonCount = _singletonCache.Count;

            // Calculate approximate memory usage
            var memoryUsage = GC.GetTotalMemory(false);

            return new ConsolidatedPluginServiceProviderHealth
            {
                IsHealthy = true,
                TotalServicesRegistered = totalServices,
                ActiveScopeCount = activeScopes,
                SingletonServiceCount = singletonCount,
                MemoryUsage = memoryUsage,
                LastHealthCheck = DateTime.UtcNow,
                RegisteredAssemblies = [.. _assemblyScopes.Keys.Select(a => a.GetName().Name ?? "Unknown")]
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to get plugin service provider health");
            return new ConsolidatedPluginServiceProviderHealth
            {
                IsHealthy = false,
                ErrorMessage = ex.Message,
                LastHealthCheck = DateTime.UtcNow
            };
        }
    }

    #endregion

    #region IServiceProvider Implementation

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // First, try to get from plugin services
            var service = _pluginServiceProvider.GetService(serviceType);
            if (service != null)
            {
                return service;
            }

            // Fall back to host services if allowed
            if (_options.AllowHostServiceFallback)
            {
                service = _hostServiceProvider.GetService(serviceType);
                if (service != null)
                {
                    _logger.LogDebugMessage("Falling back to host service for type: {serviceType.Name}");
                    return service;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error retrieving service of type: {serviceType.Name}");
            throw;
        }
    }

    /// <summary>
    /// Gets all services of the specified type.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>An enumerable of all services of the specified type.</returns>
    public IEnumerable<T> GetServices<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var pluginServices = _pluginServiceProvider.GetServices<T>();

        if (_options.AllowHostServiceFallback)
        {
            var hostServices = _hostServiceProvider.GetServices<T>();
            return pluginServices.Concat(hostServices);
        }

        return pluginServices;
    }

    /// <summary>
    /// Gets a required service of the specified type.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service is not found.</exception>
    public T GetRequiredService<T>() where T : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var service = GetService(typeof(T));
        if (service is T typedService)
        {
            return typedService;
        }

        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
    }

    #endregion

    #region Private Implementation

    private void ConfigurePluginServices()
    {
        // Register core plugin infrastructure services
        _ = _pluginServices.AddSingleton(_logger);
        _ = _pluginServices.AddScoped<IPluginActivator, PluginActivator>();
        _ = _pluginServices.AddScoped<IPluginValidator, PluginValidator>();
        _ = _pluginServices.AddScoped<IPluginMetrics, PluginMetrics>();

        // Register common services that plugins might need
        _ = _pluginServices.AddSingleton<ILoggerFactory>(_ =>
            _hostServiceProvider.GetRequiredService<ILoggerFactory>());

        // Allow host services to be injected if configured
        if (_options.AllowHostServiceFallback)
        {
            _ = _pluginServices.AddSingleton(_hostServiceProvider);
        }

        _logger.LogDebugMessage("Configured {_pluginServices.Count} base plugin services");
    }

    private async Task DiscoverAndRegisterServicesAsync(
        Assembly assembly,
        IServiceProvider scopeProvider,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            // Discover service registration methods
            var serviceRegistrationTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Any(m => m.Name == "ConfigureServices" &&
                              m.GetParameters().Length == 1 &&
                              m.GetParameters()[0].ParameterType == typeof(IServiceCollection)))
                .ToList();

            foreach (var type in serviceRegistrationTypes)
            {
                try
                {
                    var method = type.GetMethod("ConfigureServices",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        [typeof(IServiceCollection)],
                        null);

                    if (method != null)
                    {
                        _logger.LogDebugMessage("Invoking ConfigureServices from {type.FullName}");
                        _ = method.Invoke(null, [_pluginServices]);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to invoke ConfigureServices from {TypeName}", type.FullName);
                }
            }

            // Discover and register services with attributes
            RegisterServicesWithAttributes(assembly);

        }, cancellationToken);
    }

    private void RegisterServicesWithAttributes(Assembly assembly)
    {
        var typesToRegister = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetCustomAttributes<PluginServiceAttribute>().Any())
            .ToList();

        foreach (var type in typesToRegister)
        {
            var attributes = type.GetCustomAttributes<PluginServiceAttribute>().ToList();

            foreach (var attribute in attributes)
            {
                try
                {
                    var serviceType = attribute.ServiceType ?? type;

                    var serviceDescriptor = attribute.Lifetime switch
                    {
                        ServiceLifetime.Singleton => ServiceDescriptor.Singleton(serviceType, type),
                        ServiceLifetime.Scoped => ServiceDescriptor.Scoped(serviceType, type),
                        ServiceLifetime.Transient => ServiceDescriptor.Transient(serviceType, type),
                        _ => ServiceDescriptor.Scoped(serviceType, type)
                    };

                    _pluginServices.Add(serviceDescriptor);

                    _logger.LogDebugMessage($"Registered plugin service: {serviceType.Name} -> {type.Name} ({attribute.Lifetime})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register plugin service: {TypeName}", type.FullName);
                }
            }
        }
    }

    private void InjectDependencies(object instance)
    {
        var type = instance.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetCustomAttribute<PluginInjectAttribute>() != null)
            .ToList();

        foreach (var property in properties)
        {
            try
            {
                var service = GetService(property.PropertyType);
                if (service != null)
                {
                    property.SetValue(instance, service);
                    _logger.LogDebugMessage($"Injected {property.PropertyType.Name} into {property.Name} of {type.Name}");
                }
                else
                {
                    var injectAttr = property.GetCustomAttribute<PluginInjectAttribute>();
                    if (injectAttr?.Required == true)
                    {
                        throw new InvalidOperationException($"Required service {property.PropertyType.Name} not found for injection");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to inject {property.PropertyType.Name} into {property.Name}");
                throw;
            }
        }
    }

    private static IServiceProvider CreatePluginServiceProvider(IServiceCollection pluginServices, IServiceProvider parentProvider)
        // Simple fallback implementation - return the parent provider for now
        // TODO: Implement proper plugin service isolation when ServiceCollection extensions are available
        => parentProvider;

    #endregion

    #region Disposal

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Dispose all assembly scopes
            foreach (var scope in _assemblyScopes.Values)
            {
                scope.Dispose();
            }
            _assemblyScopes.Clear();

            // Dispose all plugin scopes
            foreach (var scope in _pluginScopes.Values)
            {
                scope.Dispose();
            }
            _pluginScopes.Clear();

            // Dispose all plugin providers
            foreach (var provider in _pluginProviders.Values)
            {
                if (provider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _pluginProviders.Clear();

            // Dispose plugin service provider
            if (_pluginServiceProvider is IDisposable disposableServiceProvider)
            {
                disposableServiceProvider.Dispose();
            }

            // Clear singleton cache
            foreach (var singleton in _singletonCache.Values.OfType<IDisposable>())
            {
                singleton.Dispose();
            }
            _singletonCache.Clear();

            _pluginServiceCollections.Clear();

            _disposed = true;
            _logger.LogInfoMessage("Consolidated plugin service provider disposed");
        }
    }

    #endregion
}

/// <summary>
/// Configuration options for consolidated plugin service provider.
/// </summary>
public sealed class ConsolidatedPluginServiceProviderOptions
{
    /// <summary>
    /// Gets or sets whether to allow fallback to host services.
    /// </summary>
    public bool AllowHostServiceFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of plugin scopes.
    /// </summary>
    public int MaxPluginScopes { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to enable service validation.
    /// </summary>
    public bool EnableServiceValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets the service resolution timeout.
    /// </summary>
    public TimeSpan ServiceResolutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Health information for consolidated plugin service provider.
/// </summary>
public sealed class ConsolidatedPluginServiceProviderHealth
{
    /// <summary>
    /// Gets or sets whether the provider is healthy.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the total number of registered services.
    /// </summary>
    public int TotalServicesRegistered { get; set; }

    /// <summary>
    /// Gets or sets the number of active scopes.
    /// </summary>
    public int ActiveScopeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of singleton services.
    /// </summary>
    public int SingletonServiceCount { get; set; }

    /// <summary>
    /// Gets or sets the approximate memory usage.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the last health check time.
    /// </summary>
    public DateTime LastHealthCheck { get; set; }

    /// <summary>
    /// Gets or sets the list of registered assemblies.
    /// </summary>
    public List<string> RegisteredAssemblies { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message if unhealthy.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

#region Supporting Types from Plugins Implementation

/// <summary>
/// Scoped service provider wrapper for plugins.
/// </summary>
internal sealed class PluginScopedServiceProvider : IServiceProvider
{
    private readonly IServiceScope _scope;
    private readonly Assembly _pluginAssembly;
    private readonly ILogger _logger;

    public PluginScopedServiceProvider(IServiceScope scope, Assembly pluginAssembly, ILogger logger)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _pluginAssembly = pluginAssembly ?? throw new ArgumentNullException(nameof(pluginAssembly));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public object? GetService(Type serviceType)
    {
        try
        {
            var service = _scope.ServiceProvider.GetService(serviceType);
            if (service != null)
            {
                _logger.LogTrace("Retrieved service {ServiceType} for plugin assembly {AssemblyName}",
                    serviceType.Name, _pluginAssembly.GetName().Name);
            }
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to get service {serviceType.Name} for plugin assembly {_pluginAssembly.GetName().Name}");
            throw;
        }
    }
}

/// <summary>
/// Plugin-specific service scope wrapper.
/// </summary>
internal sealed class PluginServiceScope : IServiceScope
{
    private readonly IServiceScope _innerScope;
    private bool _disposed;

    public PluginServiceScope(IServiceProvider serviceProvider, IServiceScope innerScope)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _innerScope = innerScope ?? throw new ArgumentNullException(nameof(innerScope));
    }

    public IServiceProvider ServiceProvider { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (ServiceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            _innerScope.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Attribute to mark a class for automatic service registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PluginServiceAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginServiceAttribute"/> class.
    /// </summary>
    /// <param name="lifetime">The service lifetime.</param>
    /// <param name="serviceType">The service type to register as.</param>
    public PluginServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped, Type? serviceType = null)
    {
        Lifetime = lifetime;
        ServiceType = serviceType;
    }

    /// <summary>
    /// Gets the service lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// Gets the service type to register as.
    /// </summary>
    public Type? ServiceType { get; }
}

/// <summary>
/// Attribute to mark properties for dependency injection.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PluginInjectAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginInjectAttribute"/> class.
    /// </summary>
    /// <param name="required">Whether the dependency is required.</param>
    public PluginInjectAttribute(bool required = true)
    {
        Required = required;
    }

    /// <summary>
    /// Gets whether the dependency is required.
    /// </summary>
    public bool Required { get; }
}

/// <summary>
/// Plugin activator interface.
/// </summary>
public interface IPluginActivator
{
    /// <summary>
    /// Creates an instance of the specified type with dependency injection.
    /// </summary>
    /// <param name="type">The type to create.</param>
    /// <returns>The created instance.</returns>
    public object CreateInstance(Type type);

    /// <summary>
    /// Creates an instance of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create.</typeparam>
    /// <returns>The created instance.</returns>
    public T CreateInstance<T>() where T : class;
}

/// <summary>
/// Default plugin activator implementation.
/// </summary>
internal sealed class PluginActivator : IPluginActivator
{
    private readonly IServiceProvider _serviceProvider;

    public PluginActivator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public object CreateInstance(Type type) => ActivatorUtilities.CreateInstance(_serviceProvider, type);

    public T CreateInstance<T>() where T : class => ActivatorUtilities.CreateInstance<T>(_serviceProvider);
}

/// <summary>
/// Plugin validator interface.
/// </summary>
public interface IPluginValidator
{
    /// <summary>
    /// Validates a plugin instance.
    /// </summary>
    /// <param name="plugin">The plugin to validate.</param>
    /// <returns>Validation result.</returns>
    public Task<PluginValidationResult> ValidateAsync(object plugin);
}

/// <summary>
/// Default plugin validator implementation.
/// </summary>
internal sealed class PluginValidator : IPluginValidator
{
    private readonly ILogger<PluginValidator> _logger;

    public PluginValidator(ILogger<PluginValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PluginValidationResult> ValidateAsync(object plugin)
    {
        await Task.CompletedTask;

        // Basic validation - can be extended
        if (plugin == null)
        {
            return new PluginValidationResult { IsValid = false, ErrorMessage = "Plugin instance is null" };
        }

        return new PluginValidationResult { IsValid = true };
    }
}

/// <summary>
/// Plugin validation result.
/// </summary>
public sealed class PluginValidationResult
{
    /// <summary>
    /// Gets or sets whether the plugin is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Plugin metrics interface.
/// </summary>
public interface IPluginMetrics
{
    /// <summary>
    /// Records plugin activation.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    public void RecordActivation(Type pluginType);

    /// <summary>
    /// Records plugin execution time.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <param name="executionTime">The execution time.</param>
    public void RecordExecutionTime(Type pluginType, TimeSpan executionTime);

    /// <summary>
    /// Gets plugin metrics.
    /// </summary>
    /// <returns>Plugin metrics data.</returns>
    public PluginMetricsData GetMetrics();
}

/// <summary>
/// Default plugin metrics implementation.
/// </summary>
internal sealed class PluginMetrics : IPluginMetrics
{
    private readonly ConcurrentDictionary<string, PluginMetric> _metrics = new();

    public void RecordActivation(Type pluginType)
    {
        var typeName = pluginType.FullName ?? pluginType.Name;
        _ = _metrics.AddOrUpdate(typeName,
            new PluginMetric { ActivationCount = 1 },
            (_, existing) => existing with { ActivationCount = existing.ActivationCount + 1 });
    }

    public void RecordExecutionTime(Type pluginType, TimeSpan executionTime)
    {
        var typeName = pluginType.FullName ?? pluginType.Name;
        _ = _metrics.AddOrUpdate(typeName,
            new PluginMetric { TotalExecutionTime = executionTime },
            (_, existing) => existing with
            {
                TotalExecutionTime = existing.TotalExecutionTime + executionTime,
                ExecutionCount = existing.ExecutionCount + 1
            });
    }

    public PluginMetricsData GetMetrics()
    {
        return new PluginMetricsData
        {
            PluginMetrics = _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            CollectionTime = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Plugin metrics data.
/// </summary>
public sealed class PluginMetricsData
{
    /// <summary>
    /// Gets or sets the plugin metrics by type name.
    /// </summary>
    public Dictionary<string, PluginMetric> PluginMetrics { get; set; } = [];

    /// <summary>
    /// Gets or sets the metrics collection time.
    /// </summary>
    public DateTime CollectionTime { get; set; }
}

/// <summary>
/// Individual plugin metric.
/// </summary>
public sealed record PluginMetric
{
    /// <summary>
    /// Gets the activation count.
    /// </summary>
    public int ActivationCount { get; init; }

    /// <summary>
    /// Gets the execution count.
    /// </summary>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime
        => ExecutionCount > 0 ? TimeSpan.FromTicks(TotalExecutionTime.Ticks / ExecutionCount) : TimeSpan.Zero;
}

#endregion