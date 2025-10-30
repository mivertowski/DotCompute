// <copyright file="NuGetPluginLoaderOptions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Plugins.Security;

namespace DotCompute.Plugins.Loaders.NuGet.Configuration;

/// <summary>
/// Configuration options for the NuGet plugin loader.
/// Controls how plugins are discovered, loaded, and managed.
/// </summary>
public class NuGetPluginLoaderOptions
{
    /// <summary>
    /// Gets or sets the directories to search for plugins.
    /// Local file system paths where plugin packages are stored.
    /// </summary>
    public IList<string> PluginDirectories { get; init; } = [];

    /// <summary>
    /// Gets or sets the NuGet package sources to use.
    /// Remote repositories for downloading plugin packages.
    /// </summary>
    public IList<string> PackageSources { get; init; } = [];

    /// <summary>
    /// Gets or sets the security policy for plugin loading.
    /// Rules and restrictions for plugin security validation.
    /// </summary>
    public SecurityPolicy? SecurityPolicy { get; set; }

    /// <summary>
    /// Gets or sets the dependency resolution settings.
    /// Configuration for handling plugin dependencies.
    /// </summary>
    public DependencyResolutionSettings DependencyResolution { get; set; } = new();

    /// <summary>
    /// Gets or sets the compatibility checking settings.
    /// Rules for verifying plugin compatibility.
    /// </summary>
    public CompatibilitySettings CompatibilitySettings { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to enable parallel plugin loading.
    /// Allows multiple plugins to be loaded simultaneously.
    /// </summary>
    public bool EnableParallelLoading { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent plugin loads.
    /// Limits parallel loading to prevent resource exhaustion.
    /// </summary>
    public int MaxConcurrentLoads { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the plugin load timeout.
    /// Maximum time allowed for loading a single plugin.
    /// </summary>
    public TimeSpan LoadTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to cache resolved dependencies.
    /// Improves performance by reusing dependency resolution results.
    /// </summary>
    public bool CacheDependencies { get; set; } = true;

    /// <summary>
    /// Gets or sets the dependency cache expiration time.
    /// How long cached dependencies remain valid.
    /// </summary>
    public TimeSpan DependencyCacheExpiration { get; set; } = TimeSpan.FromHours(1);
}
