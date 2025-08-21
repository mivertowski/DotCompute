// <copyright file="ValidationWarning.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Kernels.Validation;

/// <summary>
/// Represents a validation warning encountered during kernel validation.
/// Warnings indicate potential issues that don't prevent compilation but may affect performance or correctness.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Gets or sets the warning code for categorization and identification.
    /// Used for programmatic warning handling and suppression.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the human-readable warning message.
    /// Provides detailed description of the potential issue.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the line number where the warning occurred.
    /// Null if the warning is not associated with a specific line.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets or sets the column number where the warning occurred.
    /// Null if the warning is not associated with a specific column position.
    /// </summary>
    public int? Column { get; init; }

    /// <summary>
    /// Gets or sets the severity level of the warning.
    /// Indicates the importance and potential impact of the warning.
    /// </summary>
    public WarningSeverity Severity { get; init; } = WarningSeverity.Warning;
}