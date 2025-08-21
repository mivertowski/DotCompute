// <copyright file="PluginDependencyAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Dependency;

/// <summary>
/// Specifies a dependency on another plugin.
/// This attribute declares that a plugin requires another plugin to be loaded and available.
/// </summary>
/// <remarks>
/// Multiple PluginDependencyAttribute instances can be applied to a single plugin class
/// to specify multiple dependencies. The plugin system will ensure all required dependencies
/// are loaded before the dependent plugin.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PluginDependencyAttribute : Attribute
{
    /// <summary>
    /// Gets the ID of the required plugin.
    /// This must match the ID of an available plugin in the system.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets or sets the minimum version required.
    /// Uses semantic versioning. If null, any version is acceptable.
    /// </summary>
    public string? MinimumVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum version allowed.
    /// Uses semantic versioning. If null, no upper limit is enforced.
    /// </summary>
    public string? MaximumVersion { get; set; }

    /// <summary>
    /// Gets or sets whether the dependency is optional.
    /// When true, the plugin will load even if this dependency is not available.
    /// When false (default), missing dependencies will prevent plugin loading.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginDependencyAttribute"/> class.
    /// </summary>
    /// <param name="pluginId">The ID of the required plugin.</param>
    /// <exception cref="ArgumentNullException">Thrown when pluginId is null.</exception>
    public PluginDependencyAttribute(string pluginId)
    {
        PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
    }
}