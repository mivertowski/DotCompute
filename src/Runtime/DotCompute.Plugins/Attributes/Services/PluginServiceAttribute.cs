// <copyright file="PluginServiceAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Services;

/// <summary>
/// Marks a type as a plugin service that should be registered in the dependency injection container.
/// This allows the type to be resolved and injected into other components.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class PluginServiceAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the service lifetime.
    /// Determines how instances of the service are created and managed.
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Gets or sets the service type to register as.
    /// If null, the type will be registered as itself.
    /// Use this to register a concrete type as an interface or base class.
    /// </summary>
    public Type? ServiceType { get; set; }

    /// <summary>
    /// Gets or sets whether to register all implemented interfaces.
    /// When true, the type will be registered for all interfaces it implements.
    /// </summary>
    public bool RegisterInterfaces { get; set; } = true;

    /// <summary>
    /// Gets or sets the registration priority.
    /// Higher values are registered first, which can affect resolution order for multiple implementations.
    /// </summary>
    public int Priority { get; set; }
}
