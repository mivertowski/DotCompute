// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Algorithms.Abstractions
{

/// <summary>
/// Defines the contract for algorithm plugins that can be dynamically loaded and executed.
/// </summary>
public interface IAlgorithmPlugin
{
    /// <summary>
    /// Gets the unique identifier for this plugin.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the name of the algorithm plugin.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the version of the plugin.
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// Gets the description of what this algorithm does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the supported accelerator types for this algorithm.
    /// </summary>
    public AcceleratorType[] SupportedAccelerators { get; }

    /// <summary>
    /// Gets the input data types supported by this algorithm.
    /// </summary>
    public Type[] InputTypes { get; }

    /// <summary>
    /// Gets the output data type produced by this algorithm.
    /// </summary>
    public Type OutputType { get; }

    /// <summary>
    /// Initializes the plugin with the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to use for computations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public Task InitializeAsync(IAccelerator accelerator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the algorithm with the provided input data.
    /// </summary>
    /// <param name="inputs">The input data for the algorithm.</param>
    /// <param name="parameters">Optional parameters for the algorithm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the algorithm execution.</returns>
    public Task<object> ExecuteAsync(object[] inputs, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the input data before execution.
    /// </summary>
    /// <param name="inputs">The input data to validate.</param>
    /// <returns>True if the input is valid; otherwise, false.</returns>
    public bool ValidateInputs(object[] inputs);

    /// <summary>
    /// Gets the estimated memory requirements for the given input size.
    /// </summary>
    /// <param name="inputSizes">The sizes of the input data.</param>
    /// <returns>The estimated memory requirement in bytes.</returns>
    public long EstimateMemoryRequirement(int[] inputSizes);

    /// <summary>
    /// Gets the performance characteristics of this algorithm.
    /// </summary>
    /// <returns>Performance metrics and characteristics.</returns>
    public AlgorithmPerformanceProfile GetPerformanceProfile();

    /// <summary>
    /// Disposes of any resources used by the plugin.
    /// </summary>
    public ValueTask DisposeAsync();
}

/// <summary>
/// Represents the performance characteristics of an algorithm.
/// </summary>
public sealed class AlgorithmPerformanceProfile
{
    /// <summary>
    /// Gets or sets the computational complexity (e.g., O(n), O(n^2), O(n log n)).
    /// </summary>
    public required string Complexity { get; init; }

    /// <summary>
    /// Gets or sets whether this algorithm benefits from parallelization.
    /// </summary>
    public required bool IsParallelizable { get; init; }

    /// <summary>
    /// Gets or sets the optimal thread/work-item count for parallel execution.
    /// </summary>
    public int OptimalParallelism { get; init; }

    /// <summary>
    /// Gets or sets whether this algorithm is memory-bound.
    /// </summary>
    public bool IsMemoryBound { get; init; }

    /// <summary>
    /// Gets or sets whether this algorithm is compute-bound.
    /// </summary>
    public bool IsComputeBound { get; init; }

    /// <summary>
    /// Gets or sets the estimated FLOPS (floating-point operations per second) for this algorithm.
    /// </summary>
    public long EstimatedFlops { get; init; }

    /// <summary>
    /// Gets or sets additional performance metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}}
