// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.DependencyInjection.Scopes;

/// <summary>
/// Provides isolated service resolution for plugins with controlled fallback to parent services.
/// This ensures plugins have their own service instances while optionally allowing access to host services.
/// </summary>
/// <remarks>
/// <para>Service resolution follows this order:</para>
/// <list type="number">
/// <item>Plugin's own service collection (highest priority)</item>
/// <item>Parent service provider (if <see cref="AllowParentFallback"/> is true)</item>
/// </list>
/// <para>
/// This isolation prevents service leakage between plugins and ensures each plugin
/// can have its own singleton instances without affecting other plugins.
/// </para>
/// </remarks>
public sealed class IsolatedPluginServiceProvider : IServiceProvider, IDisposable, IAsyncDisposable
{
    private readonly IServiceProvider _pluginServiceProvider;
    private readonly IServiceProvider _parentServiceProvider;
    private readonly IServiceCollection _pluginServices;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<Type, bool> _serviceLocationCache;
    private readonly HashSet<Type> _pluginRegisteredTypes;
    private readonly string _pluginId;
    private bool _disposed;

    /// <summary>
    /// Gets whether fallback to parent service provider is allowed.
    /// </summary>
    public bool AllowParentFallback { get; }

    /// <summary>
    /// Gets the set of service types that are explicitly blocked from parent fallback.
    /// </summary>
    public IReadOnlySet<Type> BlockedParentServices { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IsolatedPluginServiceProvider"/> class.
    /// </summary>
    /// <param name="pluginId">The unique identifier for the plugin.</param>
    /// <param name="pluginServices">The plugin's service collection.</param>
    /// <param name="parentServiceProvider">The parent service provider for fallback resolution.</param>
    /// <param name="allowParentFallback">Whether to allow fallback to parent services.</param>
    /// <param name="blockedParentServices">Service types that should not be resolved from parent.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when pluginId, pluginServices, or parentServiceProvider is null.</exception>
    /// <exception cref="ArgumentException">Thrown when pluginId is empty or whitespace.</exception>
    public IsolatedPluginServiceProvider(
        string pluginId,
        IServiceCollection pluginServices,
        IServiceProvider parentServiceProvider,
        bool allowParentFallback = true,
        IEnumerable<Type>? blockedParentServices = null,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(pluginServices);
        ArgumentNullException.ThrowIfNull(parentServiceProvider);

        _pluginId = pluginId;
        _pluginServices = pluginServices;
        _parentServiceProvider = parentServiceProvider;
        _logger = logger;
        AllowParentFallback = allowParentFallback;
        BlockedParentServices = blockedParentServices?.ToHashSet() ?? new HashSet<Type>();

        // Build the plugin's own service provider
        _pluginServiceProvider = pluginServices.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Cache the types registered in the plugin's service collection for fast lookup
        _pluginRegisteredTypes = new HashSet<Type>(pluginServices.Select(sd => sd.ServiceType));
        _serviceLocationCache = new ConcurrentDictionary<Type, bool>();

        _logger?.LogDebug(
            "Created isolated service provider for plugin '{PluginId}' with {ServiceCount} services, AllowParentFallback={AllowParentFallback}",
            _pluginId, _pluginRegisteredTypes.Count, allowParentFallback);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(serviceType);

        // First, try to resolve from plugin's own services
        if (IsPluginRegisteredService(serviceType))
        {
            var service = _pluginServiceProvider.GetService(serviceType);
            if (service != null)
            {
                _logger?.LogTrace(
                    "Resolved service '{ServiceType}' from plugin '{PluginId}' isolated container",
                    serviceType.Name, _pluginId);
                return service;
            }
        }

        // Fall back to parent if allowed and not blocked
        if (AllowParentFallback && !IsBlockedFromParent(serviceType))
        {
            var service = _parentServiceProvider.GetService(serviceType);
            if (service != null)
            {
                _logger?.LogTrace(
                    "Resolved service '{ServiceType}' from parent provider for plugin '{PluginId}'",
                    serviceType.Name, _pluginId);
                return service;
            }
        }

        _logger?.LogDebug(
            "Service '{ServiceType}' not found for plugin '{PluginId}'",
            serviceType.Name, _pluginId);
        return null;
    }

    /// <summary>
    /// Determines whether a service type is registered in the plugin's service collection.
    /// </summary>
    /// <param name="serviceType">The service type to check.</param>
    /// <returns>True if the service is registered in the plugin's collection.</returns>
    public bool IsPluginRegisteredService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        // Use cache for performance on repeated lookups
        return _serviceLocationCache.GetOrAdd(serviceType, type =>
        {
            // Direct registration check
            if (_pluginRegisteredTypes.Contains(type))
            {
                return true;
            }

            // Check for generic type definitions (e.g., IOptions<T>)
            if (type.IsGenericType)
            {
                var genericDefinition = type.GetGenericTypeDefinition();
                if (_pluginRegisteredTypes.Any(t =>
                    t.IsGenericTypeDefinition && t == genericDefinition))
                {
                    return true;
                }
            }

            // Check for assignable types (interfaces implemented by concrete types)
            return _pluginRegisteredTypes.Any(t =>
                type.IsAssignableFrom(t) || (t.IsGenericTypeDefinition && type.IsGenericType));
        });
    }

    /// <summary>
    /// Determines whether a service type is blocked from parent provider fallback.
    /// </summary>
    /// <param name="serviceType">The service type to check.</param>
    /// <returns>True if the service is blocked from parent fallback.</returns>
    public bool IsBlockedFromParent(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (BlockedParentServices.Contains(serviceType))
        {
            return true;
        }

        // Also check generic type definitions
        if (serviceType.IsGenericType)
        {
            var genericDefinition = serviceType.GetGenericTypeDefinition();
            return BlockedParentServices.Contains(genericDefinition);
        }

        return false;
    }

    /// <summary>
    /// Gets all services of a specified type from both plugin and parent providers.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>All services of the specified type.</returns>
    public IEnumerable<T> GetServices<T>()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var pluginServices = _pluginServiceProvider.GetServices<T>();

        if (AllowParentFallback)
        {
            var parentServices = _parentServiceProvider.GetServices<T>();
            // Return plugin services first, then parent services (plugin takes precedence)
            return pluginServices.Concat(parentServices.Where(ps =>
                !pluginServices.Any(s => s?.GetType() == ps?.GetType())));
        }

        return pluginServices;
    }

    /// <summary>
    /// Gets a required service, throwing if not found.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the service is not registered.</exception>
    public T GetRequiredService<T>() where T : notnull
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var service = GetService(typeof(T));
        if (service is T typedService)
        {
            return typedService;
        }

        throw new InvalidOperationException(
            $"Service of type '{typeof(T).Name}' is not registered for plugin '{_pluginId}'.");
    }

    /// <summary>
    /// Gets statistics about the isolated provider.
    /// </summary>
    /// <returns>Provider statistics.</returns>
    public IsolatedProviderStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return new IsolatedProviderStatistics
        {
            PluginId = _pluginId,
            RegisteredServiceCount = _pluginRegisteredTypes.Count,
            CachedLookupCount = _serviceLocationCache.Count,
            BlockedServiceCount = BlockedParentServices.Count,
            AllowsParentFallback = AllowParentFallback
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger?.LogDebug("Disposing isolated service provider for plugin '{PluginId}'", _pluginId);

        if (_pluginServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _serviceLocationCache.Clear();
        _disposed = true;

        _logger?.LogDebug("Disposed isolated service provider for plugin '{PluginId}'", _pluginId);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger?.LogDebug("Async disposing isolated service provider for plugin '{PluginId}'", _pluginId);

        if (_pluginServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_pluginServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _serviceLocationCache.Clear();
        _disposed = true;

        _logger?.LogDebug("Async disposed isolated service provider for plugin '{PluginId}'", _pluginId);
    }
}

/// <summary>
/// Statistics for an isolated plugin service provider.
/// </summary>
public sealed class IsolatedProviderStatistics
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Gets or sets the number of services registered in the plugin's collection.
    /// </summary>
    public int RegisteredServiceCount { get; init; }

    /// <summary>
    /// Gets or sets the number of cached service location lookups.
    /// </summary>
    public int CachedLookupCount { get; init; }

    /// <summary>
    /// Gets or sets the number of blocked parent services.
    /// </summary>
    public int BlockedServiceCount { get; init; }

    /// <summary>
    /// Gets or sets whether parent fallback is allowed.
    /// </summary>
    public bool AllowsParentFallback { get; init; }
}
