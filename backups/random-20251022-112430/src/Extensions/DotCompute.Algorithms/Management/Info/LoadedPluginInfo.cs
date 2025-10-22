#nullable enable

// <copyright file="LoadedPluginInfo.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Reflection;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;

namespace DotCompute.Algorithms.Management.Info;

/// <summary>
/// Detailed information about a loaded plugin including runtime state.
/// Provides comprehensive runtime metrics and status information for monitoring and management.
/// </summary>
public sealed class LoadedPluginInfo
{
    /// <summary>
    /// Gets the unique identifier for the plugin.
    /// </summary>
    public string Id => Plugin.Id;

    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public string Name => Plugin.Name;

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    public string Version => Plugin.Version.ToString();

    /// <summary>
    /// Gets the assembly name.
    /// </summary>
    public string AssemblyName => Metadata.Name;

    /// <summary>
    /// Gets or sets the plugin instance.
    /// The actual algorithm plugin implementation.
    /// </summary>
    public required IAlgorithmPlugin Plugin { get; set; }

    /// <summary>
    /// Gets or sets the plugin metadata.
    /// Static information about the plugin from its manifest or assembly.
    /// </summary>
    public required PluginMetadata Metadata { get; set; }

    /// <summary>
    /// Gets or sets the plugin assembly load context.
    /// Isolation context where the plugin is loaded.
    /// </summary>
    public required PluginAssemblyLoadContext LoadContext { get; set; }

    /// <summary>
    /// Gets or sets the plugin assembly.
    /// The actual assembly containing the plugin implementation.
    /// </summary>
    public required Assembly Assembly { get; set; }

    /// <summary>
    /// Gets or sets the current state of the plugin.
    /// Indicates the plugin's lifecycle stage.
    /// </summary>
    public PluginState State { get; set; }

    /// <summary>
    /// Gets or sets the health status of the plugin.
    /// Indicates whether the plugin is functioning correctly.
    /// </summary>
    public PluginHealth Health { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the plugin was loaded.
    /// Used for uptime calculations and diagnostics.
    /// </summary>
    public DateTime LoadTime { get; set; }

    /// <summary>
    /// Gets or sets the total number of executions.
    /// Counts how many times the plugin's algorithm has been invoked.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last execution.
    /// Used for monitoring plugin activity and idle time.
    /// </summary>
    public DateTime LastExecution { get; set; }

    /// <summary>
    /// Gets or sets the cumulative execution time.
    /// Total time spent executing the plugin's algorithm across all invocations.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the last error encountered by the plugin.
    /// Null if no errors have occurred or if the last error has been cleared.
    /// </summary>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Gets or sets the file system location of the plugin assembly.
    /// Used for debugging and hot-reload operations.
    /// </summary>
    public required string AssemblyLocation { get; set; }

    /// <summary>
    /// Gets or sets the name of the assembly load context.
    /// Identifies the isolation context where the plugin is loaded.
    /// </summary>
    public required string LoadContextName { get; set; }

    /// <summary>
    /// Gets the average execution time per invocation.
    /// Calculated from total execution time and execution count.
    /// </summary>
    public TimeSpan AverageExecutionTime
        => ExecutionCount > 0 ? TimeSpan.FromTicks(TotalExecutionTime.Ticks / ExecutionCount) : TimeSpan.Zero;

    /// <summary>
    /// Gets the plugin uptime since loading.
    /// The duration the plugin has been loaded in memory.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - LoadTime;

    /// <summary>
    /// Gets whether the plugin is currently active.
    /// True if the plugin is initialized and not in a failed or unloaded state.
    /// </summary>
    public bool IsActive
        => State is PluginState.Loaded or PluginState.Running;
}