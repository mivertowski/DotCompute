// <copyright file="PluginConfigurationAttribute.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Attributes.Configuration;

/// <summary>
/// Specifies configuration options for a plugin.
/// This attribute indicates that a plugin requires or supports configuration settings.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PluginConfigurationAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the configuration section name.
    /// This identifies the section in the configuration file where plugin settings are stored.
    /// </summary>
    public string? ConfigurationSection { get; set; }

    /// <summary>
    /// Gets or sets whether configuration is required.
    /// When true, the plugin will not load without valid configuration.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema for configuration validation.
    /// Provides a JSON Schema document that defines valid configuration structure.
    /// </summary>
    public string? JsonSchema { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin supports configuration hot reload.
    /// When true, configuration changes can be applied without restarting the plugin.
    /// </summary>
    public bool SupportsHotReload { get; set; } = true;
}