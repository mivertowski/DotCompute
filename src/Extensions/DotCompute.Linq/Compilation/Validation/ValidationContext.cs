// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Linq.Compilation.Validation;

/// <summary>
/// Context for query compilation validation.
/// </summary>
public sealed class ValidationContext
{
    /// <summary>
    /// Gets the current validation errors.
    /// </summary>
    public List<DotCompute.Abstractions.Validation.ValidationIssue> Errors { get; } = new();

    /// <summary>
    /// Gets the current validation warnings.
    /// </summary>
    public List<DotCompute.Abstractions.Validation.ValidationIssue> Warnings { get; } = new();

    /// <summary>
    /// Gets whether validation has passed.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Adds an error to the validation context.
    /// </summary>
    public void AddError(string code, string message)
    {
        Errors.Add(ValidationIssue.Error(code, message));
    }

    /// <summary>
    /// Adds a warning to the validation context.
    /// </summary>
    public void AddWarning(string code, string message)
    {
        Warnings.Add(ValidationIssue.Warning(code, message));
    }

    /// <summary>
    /// Creates a validation result from this context.
    /// </summary>
    public UnifiedValidationResult ToResult()
    {
        if (!IsValid)
        {
            return UnifiedValidationResult.Failure(Errors.ToArray());
        }
        
        if (Warnings.Count > 0)
        {
            return UnifiedValidationResult.WithWarnings(Warnings.ToArray());
        }

        return UnifiedValidationResult.Success();
    }
}