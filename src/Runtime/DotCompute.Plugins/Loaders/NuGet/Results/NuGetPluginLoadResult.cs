// <copyright file="NuGetPluginLoadResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Plugins.Loaders.NuGet.Types;

namespace DotCompute.Plugins.Loaders.NuGet.Results;

/// <summary>
/// Result of loading a NuGet plugin.
/// Contains the outcome and details of a plugin load operation.
/// </summary>
public class NuGetPluginLoadResult
{
    /// <summary>
    /// Gets or sets whether the plugin was loaded successfully.
    /// True if the plugin is ready for use.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets or sets the loaded plugin information.
    /// Contains plugin instance and metadata when successfully loaded.
    /// </summary>
    public LoadedNuGetPlugin? Plugin { get; set; }

    /// <summary>
    /// Gets or sets the error message if loading failed.
    /// Primary error description for failed loads.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the load result type.
    /// Categorizes the specific outcome of the load operation.
    /// </summary>
    public PluginLoadResultType ResultType { get; set; }

    /// <summary>
    /// Gets or sets additional error details.
    /// Detailed error information for troubleshooting.
    /// </summary>
    public IList<string> ErrorDetails { get; set; } = [];

    /// <summary>
    /// Creates a successful load result.
    /// </summary>
    /// <param name="plugin">The successfully loaded plugin.</param>
    /// <returns>A success result containing the loaded plugin.</returns>
    public static NuGetPluginLoadResult Success(LoadedNuGetPlugin plugin) => new()
    {
        IsLoaded = true,
        Plugin = plugin,
        ResultType = PluginLoadResultType.Success
    };

    /// <summary>
    /// Creates a result indicating the plugin is already loaded.
    /// </summary>
    /// <param name="pluginId">The ID of the already loaded plugin.</param>
    /// <returns>An already loaded result.</returns>
    public static NuGetPluginLoadResult AlreadyLoaded(string pluginId) => new()
    {
        IsLoaded = true,
        ResultType = PluginLoadResultType.AlreadyLoaded,
        ErrorMessage = $"Plugin {pluginId} is already loaded"
    };

    /// <summary>
    /// Creates a result indicating validation failed.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin that failed validation.</param>
    /// <param name="errors">The validation errors encountered.</param>
    /// <returns>A validation failed result.</returns>
    public static NuGetPluginLoadResult ValidationFailed(string pluginId, IEnumerable<string> errors) => new()
    {
        IsLoaded = false,
        ResultType = PluginLoadResultType.ValidationFailed,
        ErrorMessage = $"Validation failed for plugin {pluginId}",
        ErrorDetails = [.. errors]
    };

    /// <summary>
    /// Creates a result indicating dependency resolution failed.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin with dependency issues.</param>
    /// <param name="errors">The dependency resolution errors.</param>
    /// <returns>A dependency resolution failed result.</returns>
    public static NuGetPluginLoadResult DependencyResolutionFailed(string pluginId, IEnumerable<string> errors) => new()
    {
        IsLoaded = false,
        ResultType = PluginLoadResultType.DependencyResolutionFailed,
        ErrorMessage = $"Dependency resolution failed for plugin {pluginId}",
        ErrorDetails = [.. errors]
    };

    /// <summary>
    /// Creates a result indicating a load error occurred.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin that failed to load.</param>
    /// <param name="error">The error message.</param>
    /// <returns>A load error result.</returns>
    public static NuGetPluginLoadResult LoadError(string pluginId, string error) => new()
    {
        IsLoaded = false,
        ResultType = PluginLoadResultType.LoadError,
        ErrorMessage = $"Failed to load plugin {pluginId}: {error}"
    };
}
