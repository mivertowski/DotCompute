// <copyright file="PluginInjectAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Core;

/// <summary>
/// Marks a property or field for plugin dependency injection.
/// The plugin system will automatically inject the specified plugin or service into the marked member.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class PluginInjectAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the plugin ID to inject.
    /// If specified, a specific plugin instance will be injected.
    /// </summary>
    public string? PluginId { get; set; }

    /// <summary>
    /// Gets or sets the service type to inject.
    /// If specified, a service of this type will be resolved and injected.
    /// </summary>
    public Type? ServiceType { get; set; }

    /// <summary>
    /// Gets or sets whether the injection is optional.
    /// When true, the plugin will load even if the injection cannot be satisfied.
    /// When false, missing dependencies will prevent plugin loading.
    /// </summary>
    public bool IsOptional { get; set; }
}