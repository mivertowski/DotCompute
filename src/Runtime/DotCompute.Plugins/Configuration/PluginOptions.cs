// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;

namespace DotCompute.Plugins.Configuration;

/// <summary>
/// Options for the plugin system.
/// Provides configuration settings for plugin loading, discovery, and management.
/// </summary>
/// <remarks>
/// This class centralizes all configuration options for the plugin system,
/// including directory scanning, plugin isolation, concurrency limits, and timeouts.
/// </remarks>
public class PluginOptions
{
    /// <summary>
    /// Gets or sets the directory to scan for plugins.
    /// </summary>
    /// <value>
    /// The file system path to scan for plugin assemblies, or <c>null</c> if no directory is configured.
    /// </value>
    /// <remarks>
    /// This directory is scanned recursively for plugin assemblies during plugin discovery.
    /// If null, only explicitly configured plugins will be loaded.
    /// </remarks>
    public string? PluginsDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether to enable hot reload.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable hot reload functionality; otherwise, <c>false</c>.
    /// Default value is <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, the plugin system will monitor plugin files for changes
    /// and automatically reload them. This feature is useful for development scenarios.
    /// </remarks>
    public bool EnableHotReload { get; set; }

    /// <summary>
    /// Gets or sets whether to load plugins in isolation.
    /// </summary>
    /// <value>
    /// <c>true</c> to load plugins in isolated contexts; otherwise, <c>false</c>.
    /// Default value is <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, plugins are loaded in separate AssemblyLoadContext instances,
    /// providing better isolation and security. Disabling this may improve performance
    /// but reduces security and isolation between plugins.
    /// </remarks>
    public bool IsolatePlugins { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of shared assemblies.
    /// </summary>
    /// <value>
    /// A collection of assembly names that should be shared across plugin contexts.
    /// Cannot be null.
    /// </value>
    /// <remarks>
    /// These assemblies are loaded in the main application context and shared
    /// with all plugin contexts, enabling communication between plugins and the host.
    /// </remarks>
    public Collection<string> SharedAssemblies { get; internal set; } =
    [
        "DotCompute.Core",
        "DotCompute.Plugins",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "Microsoft.Extensions.Configuration.Abstractions"
    ];

    /// <summary>
    /// Gets or sets configured plugins.
    /// </summary>
    /// <value>
    /// A dictionary mapping plugin identifiers to their configuration.
    /// Cannot be null.
    /// </value>
    /// <remarks>
    /// This dictionary contains explicitly configured plugins with their settings.
    /// The key is typically the plugin name or identifier, and the value contains
    /// all configuration details for that plugin.
    /// </remarks>
    public Dictionary<string, PluginConfig> Plugins { get; internal set; } = [];

    /// <summary>
    /// Gets or sets the maximum number of concurrent plugin loads.
    /// </summary>
    /// <value>
    /// The maximum number of plugins that can be loaded concurrently.
    /// Must be greater than zero. Default value is 4.
    /// </value>
    /// <remarks>
    /// This setting controls the parallelism of plugin loading operations.
    /// Higher values may improve loading performance but consume more system resources.
    /// </remarks>
    public int MaxConcurrentLoads { get; set; } = 4;

    /// <summary>
    /// Gets or sets the timeout for plugin loading operations.
    /// </summary>
    /// <value>
    /// The maximum time to wait for a plugin to load.
    /// Default value is 30 seconds.
    /// </value>
    /// <remarks>
    /// If a plugin takes longer than this timeout to load, the loading operation
    /// will be cancelled and an error will be reported. This prevents hanging
    /// during plugin loading.
    /// </remarks>
    public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    private Collection<string> _pluginDirectories = [];

    /// <summary>
    /// Gets or sets the directories to scan for plugins.
    /// </summary>
    /// <value>
    /// A collection of directory paths to scan for plugin assemblies.
    /// Cannot be null.
    /// </value>
    /// <remarks>
    /// All directories in this collection will be scanned recursively for plugin assemblies
    /// during plugin discovery. This allows for distributed plugin storage across multiple
    /// directories.
    /// </remarks>
    public Collection<string> PluginDirectories
    {
        get => _pluginDirectories;
        internal set => _pluginDirectories = value ?? [];
    }

    /// <summary>
    /// Gets or sets whether the plugin system is initialized.
    /// </summary>
    /// <value>
    /// <c>true</c> if the plugin system has been initialized; otherwise, <c>false</c>.
    /// Default value is <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property is used internally to track the initialization state of the plugin system.
    /// It prevents multiple initialization attempts and ensures proper system state.
    /// </remarks>
    public bool IsInitialized { get; set; }
}

