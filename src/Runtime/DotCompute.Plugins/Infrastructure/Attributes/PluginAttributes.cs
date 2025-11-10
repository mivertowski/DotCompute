// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Plugins.Infrastructure.Attributes;

/// <summary>
/// Attribute to mark a class for automatic service registration in the plugin dependency injection container.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to plugin service classes to enable automatic discovery and registration
/// during plugin loading. The attribute supports customizing the service lifetime and the
/// interface type to register.
/// </para>
/// <para>
/// Multiple attributes can be applied to register the same implementation under different service types.
/// </para>
/// <example>
/// <code>
/// [PluginService(ServiceLifetime.Singleton, typeof(IMyService))]
/// public class MyServiceImplementation : IMyService
/// {
///     // Implementation...
/// }
/// </code>
/// </example>
/// </remarks>
/// <param name="lifetime">The service lifetime (Singleton, Scoped, or Transient).</param>
/// <param name="serviceType">The service type to register as (interface or base class). If null, registers as self.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PluginServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Scoped, Type? serviceType = null) : Attribute
{
    /// <summary>
    /// Gets the service lifetime for this registration.
    /// </summary>
    /// <value>The DI service lifetime (Singleton, Scoped, or Transient).</value>
    public ServiceLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Gets the service type to register as.
    /// </summary>
    /// <value>The service interface or base class type, or null to register as self.</value>
    public Type? ServiceType { get; } = serviceType;
}

/// <summary>
/// Attribute to mark properties for automatic dependency injection in plugin classes.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to properties in plugin classes to enable property injection.
/// The plugin service provider will automatically resolve and inject these dependencies
/// when creating plugin instances.
/// </para>
/// <para>
/// Property injection is useful for optional dependencies or when constructor injection
/// is not feasible. However, constructor injection is generally preferred for required dependencies.
/// </para>
/// <example>
/// <code>
/// public class MyPlugin
/// {
///     [PluginInject(required: true)]
///     public ILogger&lt;MyPlugin&gt; Logger { get; set; } = null!;
///
///     [PluginInject(required: false)]
///     public IOptionalService? OptionalService { get; set; }
/// }
/// </code>
/// </example>
/// </remarks>
/// <param name="required">Whether the dependency is required (true) or optional (false).</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PluginInjectAttribute(bool required = true) : Attribute
{
    /// <summary>
    /// Gets whether the dependency is required.
    /// </summary>
    /// <value>
    /// True if the dependency must be resolved (throws exception if not available);
    /// false if the dependency is optional (property remains null if not available).
    /// </value>
    public bool Required { get; } = required;
}
