// <copyright file="PluginCapabilityAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Core;

/// <summary>
/// Specifies a capability provided by the plugin.
/// Capabilities are features or services that a plugin exposes to the system and other plugins.
/// </summary>
/// <remarks>
/// Multiple PluginCapabilityAttribute instances can be applied to a single plugin class
/// to declare multiple capabilities. Other plugins can query for plugins with specific capabilities.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="PluginCapabilityAttribute"/> class.
/// </remarks>
/// <param name="name">The capability name.</param>
/// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PluginCapabilityAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the capability name.
    /// This is a unique identifier for the capability type.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets or sets the capability version.
    /// Uses semantic versioning to indicate the capability implementation version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets additional metadata for the capability.
    /// Can be used to store JSON or other structured data about the capability.
    /// </summary>
    public string? Metadata { get; set; }
}
