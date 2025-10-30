// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Runtime.DependencyInjection.Attributes;

/// <summary>
/// Attribute to mark a class for automatic service registration.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginServiceAttribute"/> class.
/// </remarks>
/// <param name="lifetime">The service lifetime.</param>
/// <param name="serviceType">The service type to register as.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PluginServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped, Type? serviceType = null) : Attribute
{

    /// <summary>
    /// Gets the service lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the service type to register as.
    /// </summary>
    public Type? ServiceType { get; } = serviceType;
}
