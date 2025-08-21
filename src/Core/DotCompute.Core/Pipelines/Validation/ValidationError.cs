// <copyright file="ValidationError.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Pipelines.Validation;

/// <summary>
/// Represents a validation error for pipeline configuration.
/// Provides detailed information about configuration issues that prevent pipeline execution.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets the validation error code.
    /// Used for categorizing and documenting validation rules.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the error message describing the validation failure.
    /// Provides clear explanation of what validation rule was violated.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the path to the invalid configuration element.
    /// Uses dot notation to identify the specific configuration property.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the invalid value that caused the validation failure.
    /// Null if the value cannot be represented or is not available.
    /// </summary>
    public object? InvalidValue { get; init; }

    /// <summary>
    /// Gets the expected value or constraint description.
    /// Describes what value or format would pass validation.
    /// </summary>
    public string? ExpectedValue { get; init; }
}