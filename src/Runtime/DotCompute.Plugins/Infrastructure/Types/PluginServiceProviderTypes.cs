// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;

namespace DotCompute.Plugins.Infrastructure.Types;

/// <summary>
/// Configuration options for the plugin service provider.
/// </summary>
/// <remarks>
/// <para>
/// Defines behavior for service resolution, fallback strategies, validation,
/// and resource limits for the plugin dependency injection container.
/// </para>
/// </remarks>
public sealed class PluginServiceProviderOptions
{
    /// <summary>
    /// Gets or sets whether to allow fallback to host application services
    /// when a service is not found in the plugin container.
    /// </summary>
    /// <value>
    /// True to enable fallback (default); false to isolate plugin services completely.
    /// </value>
    /// <remarks>
    /// Disabling fallback provides stronger isolation but may prevent plugins
    /// from accessing host infrastructure services.
    /// </remarks>
    public bool AllowHostServiceFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent plugin scopes.
    /// </summary>
    /// <value>Maximum allowed scopes; default is 100.</value>
    /// <remarks>
    /// Limits resource usage by preventing unbounded scope creation.
    /// Adjust based on expected plugin concurrency.
    /// </remarks>
    public int MaxPluginScopes { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to enable service registration validation.
    /// </summary>
    /// <value>True to validate service registrations (default); false to skip validation.</value>
    /// <remarks>
    /// Validation checks for common registration errors at startup.
    /// Disable in production for faster startup after thorough testing.
    /// </remarks>
    public bool EnableServiceValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for service resolution operations.
    /// </summary>
    /// <value>Timeout duration; default is 30 seconds.</value>
    /// <remarks>
    /// Prevents deadlocks and infinite loops during service construction.
    /// </remarks>
    public TimeSpan ServiceResolutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Health information and diagnostics for the plugin service provider.
/// </summary>
/// <remarks>
/// <para>
/// Provides snapshot of service provider state including service counts,
/// active scopes, memory usage, and error status.
/// </para>
/// <para>
/// Use for monitoring, diagnostics, and operational health checks.
/// </para>
/// </remarks>
public sealed class PluginServiceProviderHealth
{
    /// <summary>
    /// Gets or sets whether the service provider is in a healthy state.
    /// </summary>
    /// <value>True if healthy; false if errors detected.</value>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Gets or sets the total number of services registered in the container.
    /// </summary>
    /// <value>Total service registration count.</value>
    public int TotalServicesRegistered { get; set; }

    /// <summary>
    /// Gets or sets the number of currently active service scopes.
    /// </summary>
    /// <value>Active scope count.</value>
    public int ActiveScopeCount { get; set; }

    /// <summary>
    /// Gets or sets the number of singleton services in the cache.
    /// </summary>
    /// <value>Singleton service count.</value>
    public int SingletonServiceCount { get; set; }

    /// <summary>
    /// Gets or sets the approximate memory usage in bytes.
    /// </summary>
    /// <value>Estimated memory usage.</value>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets when this health check was performed.
    /// </summary>
    /// <value>Health check timestamp.</value>
    public DateTime LastHealthCheck { get; set; }

    /// <summary>
    /// Gets or sets the list of registered plugin assembly names.
    /// </summary>
    /// <value>Assembly names of loaded plugins.</value>
    public IList<string> RegisteredAssemblies { get; init; } = [];

    /// <summary>
    /// Gets or sets the error message if the provider is unhealthy.
    /// </summary>
    /// <value>Error description, or null if healthy.</value>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Scoped service provider wrapper for plugin execution contexts.
/// </summary>
/// <remarks>
/// Internal implementation that associates service resolution with a specific plugin assembly.
/// </remarks>
internal sealed class PluginScopedServiceProvider(IServiceScope scope, Assembly pluginAssembly, ILogger logger) : IServiceProvider
{
    private readonly IServiceScope _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    private readonly Assembly _pluginAssembly = pluginAssembly ?? throw new ArgumentNullException(nameof(pluginAssembly));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Resolves a service from the scoped container.
    /// </summary>
    /// <param name="serviceType">The service type to resolve.</param>
    /// <returns>The service instance, or null if not found.</returns>
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
/// Plugin-specific service scope wrapper implementing IServiceScope.
/// </summary>
/// <remarks>
/// Wraps an inner scope to provide plugin-specific service resolution and disposal semantics.
/// </remarks>
internal sealed class PluginServiceScope(IServiceProvider serviceProvider, IServiceScope innerScope) : IServiceScope
{
    private readonly IServiceScope _innerScope = innerScope ?? throw new ArgumentNullException(nameof(innerScope));
    private bool _disposed;

    /// <summary>
    /// Gets the service provider for this scope.
    /// </summary>
    /// <value>The scoped service provider.</value>
    public IServiceProvider ServiceProvider { get; } = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Disposes the scope and releases associated resources.
    /// </summary>
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
