// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Statistics;

/// <summary>
/// Kernel validation result
/// </summary>
public class KernelValidationResult
{
    /// <summary>
    /// Gets whether the kernel is valid
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets validation errors
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets validation warnings
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets estimated resource requirements
    /// </summary>
    public KernelResourceRequirements? ResourceRequirements { get; init; }

    /// <summary>
    /// Gets performance predictions
    /// </summary>
    public Dictionary<string, double> PerformancePredictions { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <param name="resourceRequirements">Resource requirements</param>
    /// <param name="performancePredictions">Performance predictions</param>
    /// <returns>A successful validation result</returns>
    public static KernelValidationResult Success(
        KernelResourceRequirements? resourceRequirements = null,
        Dictionary<string, double>? performancePredictions = null)
        => new()
        {
            IsValid = true,
            ResourceRequirements = resourceRequirements,
            PerformancePredictions = performancePredictions ?? []
        };

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    /// <param name="errors">Validation errors</param>
    /// <param name="warnings">Validation warnings</param>
    /// <returns>A failed validation result</returns>
    public static KernelValidationResult Failure(
        IEnumerable<string> errors,
        IEnumerable<string>? warnings = null)
        => new()
        {
            IsValid = false,
            Errors = errors.ToList(),
            Warnings = warnings?.ToList() ?? []
        };
}