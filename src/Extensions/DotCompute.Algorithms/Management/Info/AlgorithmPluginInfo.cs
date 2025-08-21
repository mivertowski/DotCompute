// <copyright file="AlgorithmPluginInfo.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions;
using DotCompute.Algorithms.Types.Abstractions;

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
    public required AcceleratorType[] SupportedAccelerators { get; init; }

    /// <summary>
    /// Gets or sets the type names of input data accepted by the plugin.
    /// Used for type validation and compatibility checking.
    /// </summary>
    public required string[] InputTypes { get; init; }

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
}