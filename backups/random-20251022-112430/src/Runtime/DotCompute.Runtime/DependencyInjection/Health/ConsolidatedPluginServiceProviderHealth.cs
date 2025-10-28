// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Health;

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
    public IList<string> RegisteredAssemblies { get; init; } = [];

    /// <summary>
    /// Gets or sets the error message if unhealthy.
    /// </summary>
    public string? ErrorMessage { get; set; }
}