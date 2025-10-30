
// <copyright file="AlgorithmPluginInfo.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Algorithms.Types.Abstractions;
using DotCompute.Algorithms.Types.Enums;

namespace DotCompute.Algorithms.Management.Info;

/// <summary>
/// Contains comprehensive information about an algorithm plugin.
/// Provides metadata, capabilities, and performance characteristics of a loaded plugin.
/// </summary>
public sealed class AlgorithmPluginInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the plugin.
    /// Used to reference the plugin throughout its lifecycle.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the human-readable name of the plugin.
    /// Displayed in user interfaces and logs.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the version of the plugin.
    /// Used for compatibility checking and update management.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    /// Gets or sets the detailed description of the plugin.
    /// Explains the plugin's purpose and capabilities.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or sets the accelerator types supported by this plugin.
    /// Determines which hardware devices can execute this algorithm.
    /// </summary>
    public required IReadOnlyList<AcceleratorType> SupportedAccelerators { get; init; }

    /// <summary>
    /// Gets or sets the type names of input data accepted by the plugin.
    /// Used for type validation and compatibility checking.
    /// </summary>
    public required IReadOnlyList<string> InputTypes { get; init; }

    /// <summary>
    /// Gets or sets the type name of output data produced by the plugin.
    /// Used for type validation and pipeline composition.
    /// </summary>
    public required string OutputType { get; init; }

    /// <summary>
    /// Gets or sets the performance profile of the algorithm.
    /// Contains performance metrics and resource requirements.
    /// </summary>
    public required AlgorithmPerformanceProfile PerformanceProfile { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the plugin was loaded.
    /// Used for uptime calculations and diagnostics.
    /// </summary>
    public DateTime LoadTime { get; set; }

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
    /// Gets or sets the last error message encountered by the plugin.
    /// Null if no errors have occurred or if the last error has been cleared.
    /// </summary>
    public string? LastError { get; set; }
}
