// <copyright file="LoadedNuGetPlugin.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Reflection;
using DotCompute.Plugins.Interfaces;

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// Information about a loaded NuGet plugin.
/// Contains the plugin instance and all associated metadata.
/// </summary>
public class LoadedNuGetPlugin
{
    /// <summary>
    /// Gets or sets the plugin manifest.
    /// Complete metadata and configuration for the plugin.
    /// </summary>
    public required NuGetPluginManifest Manifest { get; set; }

    /// <summary>
    /// Gets or sets the loaded plugin instance.
    /// The actual plugin implementation object.
    /// </summary>
    public IBackendPlugin? Plugin { get; set; }

    /// <summary>
    /// Gets or sets the assembly load context.
    /// Isolation context for the plugin's assemblies.
    /// </summary>
    public NuGetPluginLoadContext? LoadContext { get; set; }

    /// <summary>
    /// Gets or sets the main assembly.
    /// The primary assembly containing the plugin implementation.
    /// </summary>
    public Assembly? Assembly { get; set; }

    /// <summary>
    /// Gets or sets the resolved dependency graph.
    /// Complete dependency tree for the plugin.
    /// </summary>
    public DependencyGraph DependencyGraph { get; set; } = new();

    /// <summary>
    /// Gets or sets when the plugin was loaded.
    /// Timestamp of successful plugin loading.
    /// </summary>
    public DateTimeOffset LoadedAt { get; set; }

    /// <summary>
    /// Gets or sets the load duration.
    /// Time taken to load and initialize the plugin.
    /// </summary>
    public TimeSpan LoadDuration { get; set; }

    /// <summary>
    /// Gets or sets the runtime metrics for the plugin.
    /// Performance and resource usage statistics.
    /// </summary>
    public PluginRuntimeMetrics Metrics { get; set; } = new();
}
