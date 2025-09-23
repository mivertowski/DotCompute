// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions.Validation;

/// <summary>
/// Represents a validation issue (error or warning).
/// </summary>
public sealed class ValidationIssue
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the severity level.
    /// </summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>
    /// Gets the source location if applicable.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Gets the line number if applicable.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets the column number if applicable.
    /// </summary>
    public int? Column { get; init; }

    /// <summary>
    /// Creates a new validation issue.
    /// </summary>
    public ValidationIssue(string code, string message, ValidationSeverity severity = ValidationSeverity.Error)
    {
        Code = code;
        Message = message;
        Severity = severity;
    }

    /// <summary>
    /// Creates an error validation issue.
    /// </summary>
    public static ValidationIssue Error(string code, string message) => new(code, message, ValidationSeverity.Error);

    /// <summary>
    /// Creates a warning validation issue.
    /// </summary>
    public static ValidationIssue Warning(string code, string message) => new(code, message, ValidationSeverity.Warning);
}

