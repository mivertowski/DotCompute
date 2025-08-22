// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Compilation.Validation;

/// <summary>
/// Represents the result of expression validation.
/// </summary>
/// <remarks>
/// This class contains the outcome of validating a LINQ expression for GPU compilation,
/// including success status, error messages, and detailed error information.
/// </remarks>
public class ValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">Whether the expression is valid for compilation.</param>
    /// <param name="message">An optional validation message.</param>
    /// <param name="errors">A collection of validation errors.</param>
    public ValidationResult(bool isValid, string? message = null, IEnumerable<ValidationError>? errors = null)
    {
        IsValid = isValid;
        Message = message;
        Errors = errors?.ToList() ?? [];
    }

    /// <summary>
    /// Gets a value indicating whether the expression is valid for compilation.
    /// </summary>
    /// <value>
    /// <c>true</c> if the expression can be compiled for GPU execution; otherwise, <c>false</c>.
    /// </value>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the validation message.
    /// </summary>
    /// <value>
    /// A human-readable message describing the validation result, or <c>null</c> if no message is provided.
    /// </value>
    public string? Message { get; }

    /// <summary>
    /// Gets the collection of validation errors.
    /// </summary>
    /// <value>
    /// A read-only list of validation errors encountered during validation.
    /// Empty if validation was successful.
    /// </value>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="message">An optional success message.</param>
    /// <returns>A validation result indicating success.</returns>
    public static ValidationResult Success(string? message = null) =>
        new(true, message);

    /// <summary>
    /// Creates a failed validation result with a single error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static ValidationResult Failure(string message) =>
        new(false, message);

    /// <summary>
    /// Creates a failed validation result with multiple errors.
    /// </summary>
    /// <param name="errors">The collection of validation errors.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static ValidationResult Failure(IEnumerable<ValidationError> errors) =>
        new(false, errors: errors);
}