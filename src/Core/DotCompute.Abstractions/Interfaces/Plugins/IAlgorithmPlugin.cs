// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces.Plugins;

/// <summary>
/// Base interface for all algorithm plugins in the DotCompute ecosystem.
/// Provides a standardized contract for algorithm implementations.
/// </summary>
public interface IAlgorithmPlugin
{
    /// <summary>
    /// Gets the unique identifier for this algorithm plugin.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the human-readable name of the algorithm.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the version of the algorithm implementation.
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// Gets the description of what this algorithm does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the supported input types for this algorithm.
    /// </summary>
    public IReadOnlyList<Type> SupportedInputTypes { get; }

    /// <summary>
    /// Gets the supported output types for this algorithm.
    /// </summary>
    public IReadOnlyList<Type> SupportedOutputTypes { get; }

    /// <summary>
    /// Executes the algorithm with the provided inputs.
    /// </summary>
    /// <param name="inputs">The input data for the algorithm.</param>
    /// <param name="parameters">Optional algorithm parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The algorithm result.</returns>
    public Task<object> ExecuteAsync(
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates input data before execution.
    /// </summary>
    /// <param name="inputs">The input data to validate.</param>
    /// <param name="parameters">Optional algorithm parameters.</param>
    /// <returns>Validation result.</returns>
    public Task<ValidationResult> ValidateAsync(
        object[] inputs,
        Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Gets algorithm metadata and capabilities.
    /// </summary>
    /// <returns>Algorithm metadata.</returns>
    public AlgorithmMetadata GetMetadata();
}

/// <summary>
/// Represents algorithm validation result.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets validation errors if any.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets validation warnings if any.
    /// </summary>
    public IList<string> Warnings { get; } = [];
}

/// <summary>
/// Represents algorithm metadata.
/// </summary>
public class AlgorithmMetadata
{
    /// <summary>
    /// Gets or sets the algorithm category.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the computational complexity.
    /// </summary>
    public string Complexity { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the algorithm is parallelizable.
    /// </summary>
    public bool IsParallelizable { get; set; }

    /// <summary>
    /// Gets or sets whether the algorithm is deterministic.
    /// </summary>
    public bool IsDeterministic { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory requirements.
    /// </summary>
    public long MemoryRequirements { get; set; }

    /// <summary>
    /// Gets or sets additional properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = [];
}
