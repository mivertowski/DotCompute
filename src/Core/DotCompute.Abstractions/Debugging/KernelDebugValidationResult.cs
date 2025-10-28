// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;
using DotCompute.Abstractions.Interfaces.Kernels;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents the result of kernel validation across multiple backends.
/// </summary>
public sealed class KernelValidationResult
{
    /// <summary>
    /// Gets the name of the kernel that was validated.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the kernel validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the list of backends that were tested.
    /// </summary>
    public IReadOnlyList<string> BackendsTested { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the execution results from each backend.
    /// </summary>
    public IReadOnlyList<KernelExecutionResult> Results { get; init; } = Array.Empty<KernelExecutionResult>();

    /// <summary>
    /// Gets the comparisons between backend results.
    /// </summary>
    public IReadOnlyList<ResultComparison> Comparisons { get; init; } = Array.Empty<ResultComparison>();

    /// <summary>
    /// Gets the validation issues found.
    /// </summary>
    public Collection<DebugValidationIssue> Issues { get; init; } = [];

    /// <summary>
    /// Gets the total execution time for validation.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets when the validation was performed.
    /// </summary>
    public DateTime ValidationTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets additional validation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>
    /// Gets the total validation time (same as ExecutionTime for compatibility).
    /// </summary>
    public TimeSpan TotalValidationTime => ExecutionTime;

    /// <summary>
    /// Gets the maximum difference found between backend results.
    /// </summary>
    public float MaxDifference { get; init; }

    /// <summary>
    /// Gets the recommended backend based on validation results.
    /// </summary>
    public string? RecommendedBackend { get; init; }

    /// <summary>
    /// Gets performance recommendations based on validation.
    /// </summary>
    public IList<string> Recommendations { get; init; } = new List<string>();

    /// <summary>
    /// Gets errors found during validation (filtered from Issues).
    /// </summary>
    public IEnumerable<DebugValidationIssue> Errors => Issues.Where(i => i.Severity == Validation.ValidationSeverity.Error);

    /// <summary>
    /// Gets warnings found during validation (filtered from Issues).
    /// </summary>
    public IEnumerable<DebugValidationIssue> Warnings => Issues.Where(i => i.Severity == Validation.ValidationSeverity.Warning);

    /// <summary>
    /// Gets resource usage information during validation.
    /// </summary>
    public Dictionary<string, object> ResourceUsage { get; init; } = [];
}

/// <summary>
/// Represents a comparison between two backend execution results.
/// </summary>
public sealed class ResultComparison
{
    /// <summary>
    /// Gets the first backend name.
    /// </summary>
    public string Backend1 { get; init; } = string.Empty;

    /// <summary>
    /// Gets the second backend name.
    /// </summary>
    public string Backend2 { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the results match within tolerance.
    /// </summary>
    public bool IsMatch { get; init; }

    /// <summary>
    /// Gets the numeric difference between results.
    /// </summary>
    public float Difference { get; init; }

    /// <summary>
    /// Gets additional comparison details.
    /// </summary>
    public Dictionary<string, object> Details { get; init; } = [];
}
