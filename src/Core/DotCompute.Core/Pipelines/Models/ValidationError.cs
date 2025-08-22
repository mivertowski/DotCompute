// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Models;

/// <summary>
/// Represents a validation error that occurred during pipeline validation
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets or sets the validation rule that failed
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message describing the validation failure
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the property or field that failed validation
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the attempted value that failed validation
    /// </summary>
    public object? AttemptedValue { get; set; }

    /// <summary>
    /// Gets or sets the invalid value that caused the validation error
    /// </summary>
    public object? InvalidValue { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the validation error
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    /// <summary>
    /// Gets or sets the error code for programmatic handling
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the validation error code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the source that caused the validation error
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional context information
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the validation error occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets suggested fixes for the validation error
    /// </summary>
    public List<string> SuggestedFixes { get; set; } = new();
}

/// <summary>
/// Represents the severity level of a validation error
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Information about validation status
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't prevent execution but may cause issues
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents successful validation
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that indicates a serious validation failure
    /// </summary>
    Critical
}