// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents detailed analysis of an error that occurred during kernel execution.
/// </summary>
public sealed class ErrorAnalysis
{
    /// <summary>
    /// Gets the type of error that occurred.
    /// </summary>
    public string ErrorType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the error is likely transient (temporary).
    /// </summary>
    public bool IsTransient { get; init; }

    /// <summary>
    /// Gets the severity of the error.
    /// </summary>
    public ValidationSeverity Severity { get; init; }

    /// <summary>
    /// Gets suggested actions to resolve the error.
    /// </summary>
    public IReadOnlyList<string> SuggestedActions { get; init; } = [];

    /// <summary>
    /// Gets the stack trace if available.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets inner exception information if available.
    /// </summary>
    public string? InnerException { get; init; }

    /// <summary>
    /// Gets error context information.
    /// </summary>
    public Dictionary<string, object> Context { get; init; } = [];

    /// <summary>
    /// Gets potential root causes for the error.
    /// </summary>
    public IReadOnlyList<string> PotentialCauses { get; init; } = [];

    /// <summary>
    /// Gets related documentation or help links.
    /// </summary>
    public IReadOnlyList<string> HelpLinks { get; init; } = [];
}
