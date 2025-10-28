// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Plugins.Configuration;

/// <summary>
/// Configuration for a specific plugin.
/// Provides settings and metadata for plugin loading and initialization.
/// </summary>
/// <remarks>
/// This class contains all necessary information to configure and load a plugin,
/// including assembly path, type information, and plugin-specific settings.
/// </remarks>
public class PluginConfig
{
    private string _assemblyPath = "";
    private string _typeName = "";

    /// <summary>
    /// Gets or sets the plugin assembly path.
    /// </summary>
    /// <value>
    /// The file system path to the plugin assembly. Cannot be null.
    /// </value>
    /// <remarks>
    /// This path is used to locate and load the plugin assembly.
    /// The path should point to a valid .NET assembly file.
    /// </remarks>
    public string AssemblyPath
    {
        get => _assemblyPath;
        set => _assemblyPath = value ?? "";
    }

    /// <summary>
    /// Gets or sets the plugin type name.
    /// </summary>
    /// <value>
    /// The fully qualified type name of the plugin class. Cannot be null.
    /// </value>
    /// <remarks>
    /// This should be the full type name including namespace of the class
    /// that implements the plugin interface.
    /// </remarks>
    public string TypeName
    {
        get => _typeName;
        set => _typeName = value ?? "";
    }

    /// <summary>
    /// Gets or sets whether the plugin is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the plugin should be loaded and activated; otherwise, <c>false</c>.
    /// Default value is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Disabled plugins are ignored during the plugin discovery and loading process.
    /// This allows for temporary disabling of plugins without removing their configuration.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets plugin-specific settings.
    /// </summary>
    /// <value>
    /// A dictionary containing key-value pairs of plugin configuration settings.
    /// Cannot be null.
    /// </value>
    /// <remarks>
    /// This dictionary contains arbitrary settings that are specific to the plugin.
    /// The plugin implementation can access these settings during initialization
    /// to customize its behavior.
    /// </remarks>
    public Dictionary<string, object> Settings { get; internal set; } = [];
}