// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Runtime.DependencyInjection.Attributes;

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