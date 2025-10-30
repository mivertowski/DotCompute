// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions.Models.Pipelines;

/// <summary>
/// Pipeline validation result.
/// </summary>
public sealed class PipelineValidationResult
{
    /// <summary>
    /// Gets whether the pipeline is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Gets validation errors.
    /// </summary>
    public IReadOnlyList<ValidationIssue>? Errors { get; init; }

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public IReadOnlyList<ValidationWarning>? Warnings { get; init; }
}
