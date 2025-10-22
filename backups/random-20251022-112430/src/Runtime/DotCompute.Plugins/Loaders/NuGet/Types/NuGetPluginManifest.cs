// <copyright file="NuGetPluginManifest.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// Manifest for NuGet-based plugins with comprehensive metadata.
/// Contains all necessary information to load and configure a plugin from a NuGet package.
/// </summary>
public class NuGetPluginManifest
{
    /// <summary>
    /// Gets or sets the unique plugin identifier.
    /// Corresponds to the NuGet package ID.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the plugin.
    /// Human-readable name for UI display.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the plugin version.
    /// Semantic version of the plugin package.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the plugin description.
    /// Detailed description of plugin functionality.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the plugin author.
    /// Name or organization that created the plugin.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Gets or sets the path to the main assembly.
    /// Relative path within the package to the plugin DLL.
    /// </summary>
    public required string AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the entry point type name.
    /// Fully qualified name of the plugin's main class.
    /// </summary>
    public string? EntryPointType { get; set; }

    /// <summary>
    /// Gets or sets the minimum framework version required.
    /// Minimum .NET framework version needed by the plugin.
    /// </summary>
    public string? MinFrameworkVersion { get; set; }

    /// <summary>
    /// Gets or sets the maximum framework version supported.
    /// Maximum .NET framework version tested with the plugin.
    /// </summary>
    public string? MaxFrameworkVersion { get; set; }

    /// <summary>
    /// Gets or sets the list of dependencies.
    /// Other packages required by this plugin.
    /// </summary>
    public IList<NuGetPackageDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the supported platforms.
    /// List of operating systems and architectures supported.
    /// </summary>
    public IList<string> SupportedPlatforms { get; } = [];

    /// <summary>
    /// Gets or sets the supported accelerators.
    /// List of compute device types the plugin can utilize.
    /// </summary>
    public IList<string> SupportedAccelerators { get; } = [];

    /// <summary>
    /// Gets or sets the configuration settings.
    /// Default configuration for the plugin.
    /// </summary>
    public Dictionary<string, object> Configuration { get; } = [];

    /// <summary>
    /// Gets or sets the tags for categorization.
    /// Keywords for plugin discovery and filtering.
    /// </summary>
    public IList<string> Tags { get; } = [];

    /// <summary>
    /// Gets or sets the license information.
    /// Plugin licensing details.
    /// </summary>
    public PluginLicense? License { get; set; }

    /// <summary>
    /// Gets or sets the project URL.
    /// Link to the plugin's project page or repository.
    /// </summary>
    public string? ProjectUrl { get; set; }

    /// <summary>
    /// Gets or sets the icon URL.
    /// Link to the plugin's icon image.
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets the release notes.
    /// Changes and updates in this version.
    /// </summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Gets or sets the signature information.
    /// Digital signature for security verification.
    /// </summary>
    public PluginSignature? Signature { get; set; }
}