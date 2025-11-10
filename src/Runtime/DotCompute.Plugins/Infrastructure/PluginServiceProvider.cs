// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CS9113 // Parameter is unread

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DotCompute.Plugins.Infrastructure.Attributes;
using DotCompute.Plugins.Infrastructure.Services;
using DotCompute.Plugins.Infrastructure.Types;
using DotCompute.Plugins.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Infrastructure;


    /// <summary>
    /// Advanced service provider for plugin dependency injection with isolation and lifecycle management.
    /// </summary>
    public sealed class PluginServiceProvider : IServiceProvider, IDisposable
    {
        private readonly IServiceProvider _hostServiceProvider;
        private readonly IServiceCollection _pluginServices;
        private readonly IServiceProvider _pluginServiceProvider;
        private readonly ILogger<PluginServiceProvider> _logger;
        private readonly ConcurrentDictionary<Type, object> _singletonCache;
        private readonly ConcurrentDictionary<Assembly, IServiceScope> _assemblyScopes;
        private readonly PluginServiceProviderOptions _options;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginServiceProvider"/> class.
        /// </summary>
        /// <param name="hostServiceProvider">The host application's service provider.</param>
        /// <param name="logger">The logger instance.</param>
        /// <param name="options">Configuration options.</param>
        public PluginServiceProvider(
            IServiceProvider hostServiceProvider,
            ILogger<PluginServiceProvider> logger,
            PluginServiceProviderOptions? options = null)
        {
            _hostServiceProvider = hostServiceProvider ?? throw new ArgumentNullException(nameof(hostServiceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new PluginServiceProviderOptions();
            _singletonCache = new ConcurrentDictionary<Type, object>();
            _assemblyScopes = new ConcurrentDictionary<Assembly, IServiceScope>();
            _pluginServices = new ServiceCollection();

            // Configure plugin services
            ConfigurePluginServices();

            // Build the plugin service provider
            _pluginServiceProvider = _pluginServices.BuildServiceProvider();

            _logger.LogInfoMessage($"Plugin service provider initialized with {_pluginServices.Count} plugin services");
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
        /// Gets service of the specified type.
        /// </summary>
        /// <param name="serviceType">The type of service to retrieve.</param>
        /// <returns>The service instance, or null if not found.</returns>
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
        public PluginServiceProviderHealth GetHealth()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                var totalServices = _pluginServices.Count;
                var activeScopes = _assemblyScopes.Count;
                var singletonCount = _singletonCache.Count;

                // Calculate approximate memory usage
                var memoryUsage = GC.GetTotalMemory(false);

                return new PluginServiceProviderHealth
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
                return new PluginServiceProviderHealth
                {
                    IsHealthy = false,
                    ErrorMessage = ex.Message,
                    LastHealthCheck = DateTime.UtcNow
                };
            }
        }

        private void ConfigurePluginServices()
        {
            // Register core plugin infrastructure services
            _ = _pluginServices.AddSingleton(_logger);
            _ = _pluginServices.AddScoped<IPluginActivator, PluginActivator>();
            _ = _pluginServices.AddScoped<IPluginValidator, PluginValidator>();
            _ = _pluginServices.AddScoped<IPluginMetrics, PluginMetrics>();

            // Register common services that plugins might need
            _ = _pluginServices.AddSingleton(_ =>
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

        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Plugin service types are expected to have public constructors for DI.")]
        private void RegisterServicesWithAttributes(Assembly assembly)
        {
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;

                var attributes = type.GetCustomAttributes<PluginServiceAttribute>();

                foreach (var attribute in attributes)
                {
                    try
                    {
                        var serviceType = attribute.ServiceType ?? type;

                        var serviceDescriptor = CreateServiceDescriptor(serviceType, type, attribute.Lifetime);

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

        /// <summary>
        /// Creates a service descriptor with proper trim-safe annotations.
        /// </summary>
        /// <param name="serviceType">The service interface type.</param>
        /// <param name="implementationType">The implementation type with preserved constructors.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>A service descriptor for the specified types.</returns>
        private static ServiceDescriptor CreateServiceDescriptor(
            Type serviceType,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType,
            ServiceLifetime lifetime)
        {
            return lifetime switch
            {
                ServiceLifetime.Singleton => ServiceDescriptor.Singleton(serviceType, implementationType),
                ServiceLifetime.Scoped => ServiceDescriptor.Scoped(serviceType, implementationType),
                ServiceLifetime.Transient => ServiceDescriptor.Transient(serviceType, implementationType),
                _ => ServiceDescriptor.Scoped(serviceType, implementationType)
            };
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

                _disposed = true;
                _logger.LogInfoMessage("Plugin service provider disposed");
            }
        }
    }
