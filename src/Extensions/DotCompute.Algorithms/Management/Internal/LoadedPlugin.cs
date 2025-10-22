
// <copyright file="LoadedPlugin.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System.Reflection;
using DotCompute.Algorithms.Management.Loading;
using DotCompute.Algorithms.Management.Metadata;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;

namespace DotCompute.Algorithms.Management.Internal;

/// <summary>
/// Represents a loaded plugin with its context and metadata.
/// Internal class used by AlgorithmPluginManager to track plugin state and lifecycle.
/// </summary>
internal sealed class LoadedPlugin
{
    /// <summary>
    /// Gets or initializes the plugin instance.
    /// The actual algorithm implementation.
    /// </summary>
    public required IAlgorithmPlugin Plugin { get; init; }

    /// <summary>
    /// Gets or initializes the assembly load context.
    /// Provides isolation for the plugin's dependencies.
    /// </summary>
    public required PluginAssemblyLoadContext LoadContext { get; init; }

    /// <summary>
    /// Gets or initializes the plugin assembly.
    /// The compiled .NET assembly containing the plugin code.
    /// </summary>
    public required Assembly Assembly { get; init; }

    /// <summary>
    /// Gets or initializes the plugin metadata.
    /// Static information about the plugin.
    /// </summary>
    public required PluginMetadata Metadata { get; init; }

    /// <summary>
    /// Gets or initializes the load timestamp.
    /// When the plugin was loaded into memory.
    /// </summary>
    public required DateTime LoadTime { get; init; }

    /// <summary>
    /// Gets or sets the current plugin state.
    /// Tracks the plugin's lifecycle stage.
    /// </summary>
    public PluginState State { get; set; } = PluginState.Loaded;

    /// <summary>
    /// Gets or sets the plugin health status.
    /// Indicates whether the plugin is functioning correctly.
    /// </summary>
    public PluginHealth Health { get; set; } = PluginHealth.Unknown;

    /// <summary>
    /// Gets or sets the execution count.
    /// Number of times the plugin has been executed.
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the last execution timestamp.
    /// When the plugin was last executed.
    /// </summary>
    public DateTime LastExecution { get; set; }

    /// <summary>
    /// Gets or sets the total execution time.
    /// Cumulative time spent executing the plugin.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the last error encountered.
    /// The most recent exception thrown by the plugin.
    /// </summary>
    public Exception? LastError { get; set; }
}