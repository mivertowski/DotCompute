// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Plugin information
/// </summary>
public class PluginInfo
{
    /// <summary>
    /// Gets the plugin ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the plugin name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the plugin version
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Gets the plugin description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the plugin author
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the plugin assembly path
    /// </summary>
    public string? AssemblyPath { get; init; }

    /// <summary>
    /// Gets the plugin load time
    /// </summary>
    public DateTime LoadTime { get; init; }

    /// <summary>
    /// Gets whether the plugin is currently loaded
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// Gets the supported interfaces
    /// </summary>
    public IReadOnlyList<Type> SupportedInterfaces { get; init; } = Array.Empty<Type>();

    /// <summary>
    /// Gets plugin-specific metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
