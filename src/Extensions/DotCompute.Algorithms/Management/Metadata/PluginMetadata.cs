// <copyright file="PluginMetadata.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Algorithms.Management.Metadata;

/// <summary>
/// Metadata about a plugin loaded from manifest or assembly.
/// Contains essential information for plugin identification, versioning, and dependency management.
/// </summary>
public sealed class PluginMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for the plugin.
    /// Used as the primary key for plugin registration and lookup.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name of the plugin.
    /// Displayed in user interfaces and diagnostic messages.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the version string of the plugin.
    /// Used for compatibility checking and update management.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the detailed description of the plugin's functionality.
    /// Provides users with information about what the plugin does.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the author or organization that created the plugin.
    /// Used for attribution and support contact information.
    /// </summary>
    public required string Author { get; set; }

    /// <summary>
    /// Gets or sets the file system path to the plugin assembly.
    /// Used for loading and hot-reload operations.
    /// </summary>
    public required string AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the plugin was loaded.
    /// Used for monitoring and diagnostic purposes.
    /// </summary>
    public DateTime LoadTime { get; set; }

    /// <summary>
    /// Gets or sets the required .NET framework version.
    /// Ensures compatibility with the host application runtime.
    /// </summary>
    public string? RequiredFrameworkVersion { get; set; }

    /// <summary>
    /// Gets the list of plugin dependencies.
    /// Other plugins or libraries required for this plugin to function.
    /// </summary>
    public List<string> Dependencies { get; } = [];

    /// <summary>
    /// Gets additional metadata as key-value pairs.
    /// Custom metadata that doesn't fit into predefined fields.
    /// </summary>
    public Dictionary<string, object> AdditionalMetadata { get; } = [];
}