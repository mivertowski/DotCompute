// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Types.Abstractions
{

/// <summary>
/// Interface for algorithm plugins that can be dynamically loaded and executed.
/// </summary>
public interface IAlgorithmPlugin : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of the plugin.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the description of what this plugin does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the accelerator types supported by this plugin.
    /// </summary>
    IEnumerable<AcceleratorType> SupportedAccelerators { get; }

    /// <summary>
    /// Gets the input types that this plugin can process.
    /// </summary>
    IEnumerable<Type> InputTypes { get; }

    /// <summary>
    /// Initializes the plugin with the given accelerator and logger.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for computations.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A task representing the async initialization operation.</returns>
    ValueTask InitializeAsync(IAccelerator accelerator, ILogger? logger = null);

    /// <summary>
    /// Executes the algorithm with the provided input data.
    /// </summary>
    /// <param name="input">The input data for the algorithm.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the algorithm execution.</returns>
    ValueTask<object> ExecuteAsync(object input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the provided input is compatible with this plugin.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <returns>True if the input is valid for this plugin, false otherwise.</returns>
    bool ValidateInput(object input);

    /// <summary>
    /// Gets performance characteristics of this plugin.
    /// </summary>
    /// <returns>Performance profile information.</returns>
    AlgorithmPerformanceProfile GetPerformanceProfile();
}

/// <summary>
/// Compute complexity levels for algorithms.
/// </summary>
public enum ComputeComplexity
{
    /// <summary>
    /// Low computational complexity - O(1) or O(log n).
    /// </summary>
    Low,

    /// <summary>
    /// Medium computational complexity - O(n) or O(n log n).
    /// </summary>
    Medium,

    /// <summary>
    /// High computational complexity - O(n²) or higher.
    /// </summary>
    High,

    /// <summary>
    /// Very high computational complexity - exponential or factorial.
    /// </summary>
    VeryHigh
}}
