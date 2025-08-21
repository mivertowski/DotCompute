// <copyright file="ValidationError.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Kernels.Validation;

/// <summary>
/// Represents a validation error encountered during kernel validation.
/// Contains detailed information about the error including location and description.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets or sets the error code for categorization and identification.
    /// Used for programmatic error handling and documentation references.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// Provides detailed description of the validation error.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets or sets the line number where the error occurred.
    /// Null if the error is not associated with a specific line.
    /// </summary>
    public int? Line { get; init; }

    /// <summary>
    /// Gets or sets the column number where the error occurred.
    /// Null if the error is not associated with a specific column position.
    /// </summary>
    public int? Column { get; init; }
}