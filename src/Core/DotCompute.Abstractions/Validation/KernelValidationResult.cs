// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Debugging;

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
    public string[] BackendsTested { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets validation errors that prevent kernel compilation.
    /// Empty list indicates no errors found.
    /// </summary>
    public List<ValidationIssue> Errors { get; init; } = [];

    /// <summary>
    /// Gets validation warnings that don't prevent compilation.
    /// Warnings indicate potential issues or optimization opportunities.
    /// </summary>
    public List<ValidationWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Gets debug validation issues found during cross-backend validation.
    /// </summary>
    public List<DebugValidationIssue> Issues { get; init; } = [];

    /// <summary>
    /// Gets the results from different backends as key-value pairs.
    /// Key: backend name, Value: validation result data
    /// </summary>
    public Dictionary<string, object> Results { get; init; } = [];

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
    /// Gets the time spent validating this kernel.
    /// </summary>
    public TimeSpan ValidationTime { get; set; }

    /// <summary>
    /// Gets optimization recommendations for the kernel.
    /// </summary>
    public List<string> Recommendations { get; init; } = [];
}

// ValidationWarning, ValidationIssue, WarningSeverity, and ResourceUsageEstimate
// are defined in other files in this namespace