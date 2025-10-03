// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

// KernelExecutionResult is already defined in IKernelDebugService.cs as a record

/// <summary>
/// Represents the result of cross-validation between multiple accelerators.
/// </summary>
public sealed class CrossValidationResult
{
    /// <summary>
    /// Gets the name of the kernel that was validated.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the cross-validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the execution results from each accelerator.
    /// </summary>
    public IReadOnlyList<KernelExecutionResult> ExecutionResults { get; init; } = [];

    /// <summary>
    /// Gets validation issues found during cross-validation.
    /// </summary>
    public IReadOnlyList<DebugValidationIssue> ValidationIssues { get; init; } = [];

    /// <summary>
    /// Gets when the validation was performed.
    /// </summary>
    public DateTime ValidationTime { get; init; }

    /// <summary>
    /// Gets the tolerance used for output comparison.
    /// </summary>
    public double Tolerance { get; init; } = 1e-6;

    /// <summary>
    /// Gets the maximum difference found between outputs.
    /// </summary>
    public double MaxDifference { get; init; }

    /// <summary>
    /// Gets additional validation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
