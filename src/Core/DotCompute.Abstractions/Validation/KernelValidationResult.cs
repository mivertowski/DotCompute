// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces.Kernels;

namespace DotCompute.Abstractions.Validation;

/// <summary>
/// Represents the comprehensive result of kernel validation across multiple backends.
/// Contains validation status, errors, warnings, performance metrics, and cross-backend comparison data.
/// </summary>
public sealed class KernelValidationResult
{
    /// <summary>
    /// Gets the name of the kernel that was validated.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the kernel is valid and can be compiled.
    /// True if no errors were found during validation.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the list of backends that were tested during validation.
    /// </summary>
    public IReadOnlyList<string> BackendsTested { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets validation errors that prevent kernel compilation.
    /// Empty list indicates no errors found.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Errors { get; init; } = [];

    /// <summary>
    /// Gets validation warnings that don't prevent compilation.
    /// Warnings indicate potential issues or optimization opportunities.
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Gets debug validation issues found during cross-backend validation.
    /// </summary>
    public Collection<DebugValidationIssue> Issues { get; init; } = [];

    /// <summary>
    /// Gets the results from different backends.
    /// Contains the execution results from each backend tested.
    /// </summary>
    public IReadOnlyList<KernelExecutionResult> Results { get; init; } = [];

    /// <summary>
    /// Gets the total time spent on validation across all backends.
    /// </summary>
    public TimeSpan TotalValidationTime { get; init; }

    /// <summary>
    /// Gets the maximum difference found between backend results (for cross-validation).
    /// </summary>
    public float MaxDifference { get; init; }

    /// <summary>
    /// Gets the recommended backend based on validation results.
    /// </summary>
    public string RecommendedBackend { get; init; } = string.Empty;

    /// <summary>
    /// Gets resource usage estimates for the kernel.
    /// Provides information about memory, register usage, and performance characteristics.
    /// </summary>
    public ResourceUsageEstimate? ResourceUsage { get; init; }

    /// <summary>
    /// Gets the time spent validating this kernel (total execution time).
    /// </summary>
    public TimeSpan ValidationTime { get; set; }

    /// <summary>
    /// Gets the time spent on the actual kernel execution.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets optimization recommendations for the kernel.
    /// </summary>
    public Collection<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets the comparison results between different backends.
    /// </summary>
    public IReadOnlyList<ResultComparison> Comparisons { get; init; } = [];
}

/// <summary>
/// Represents a comparison between two backend execution results.
/// </summary>
public sealed class ResultComparison
{
    /// <summary>
    /// Gets or sets the first backend in the comparison.
    /// </summary>
    public string Backend1 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the second backend in the comparison.
    /// </summary>
    public string Backend2 { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the results match within tolerance.
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Gets or sets the measured difference between the results.
    /// </summary>
    public float Difference { get; set; }
}

// ValidationWarning, ValidationIssue, WarningSeverity, and ResourceUsageEstimate
// are defined in other files in this namespace
