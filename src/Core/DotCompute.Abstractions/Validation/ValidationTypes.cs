// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Validation;

/// <summary>
/// Represents a validation warning.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Gets the warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the warning code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the severity level.
    /// </summary>
    public required WarningSeverity Severity { get; init; }

    /// <summary>
    /// Gets the location where the warning occurred.
    /// </summary>
    public string? Location { get; init; }
}

/// <summary>
/// Warning severity levels.
/// </summary>
public enum WarningSeverity
{
    /// <summary>
    /// Low severity warning.
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity warning.
    /// </summary>
    Medium,

    /// <summary>
    /// High severity warning.
    /// </summary>
    High
}