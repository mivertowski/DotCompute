// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Result of validation operations with detailed feedback.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the error severity level.
    /// </summary>
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Info;

    /// <summary>
    /// Gets or sets validation errors.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets validation warnings.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="message">Optional success message</param>
    /// <returns>Successful validation result</returns>
    public static ValidationResult Success(string? message = null)
    {
        return new ValidationResult
        {
            IsValid = true,
            Message = message ?? "Validation successful",
            Severity = ErrorSeverity.Info
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="severity">Error severity</param>
    /// <returns>Failed validation result</returns>
    public static ValidationResult Failure(string message, ErrorSeverity severity = ErrorSeverity.Error)
    {
        return new ValidationResult
        {
            IsValid = false,
            Message = message,
            Severity = severity,
            Errors = { message }
        };
    }
}
