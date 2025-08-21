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
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PluginCapabilityAttribute : Attribute
{
    /// <summary>
    /// Gets the capability name.
    /// This is a unique identifier for the capability type.
    /// </summary>
    public string Name { get; }

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

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginCapabilityAttribute"/> class.
    /// </summary>
    /// <param name="name">The capability name.</param>
    /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
    public PluginCapabilityAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}