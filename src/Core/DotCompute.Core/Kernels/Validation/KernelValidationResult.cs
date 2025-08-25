// <copyright file="KernelValidationResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Kernels.Validation;

/// <summary>
/// Represents the result of kernel validation.
/// Contains validation status, errors, warnings, and resource usage estimates.
/// </summary>
public sealed class KernelValidationResult
{
    /// <summary>
    /// Gets whether the kernel is valid and can be compiled.
    /// True if no errors were found during validation.
    /// </summary>
    public bool IsValid { get; init; }

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
    /// Gets resource usage estimates for the kernel.
    /// Provides information about memory, register usage, and performance characteristics.
    /// </summary>
    public ResourceUsageEstimate? ResourceUsage { get; init; }
}