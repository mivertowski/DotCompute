// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Linq.Expressions;

namespace DotCompute.Linq.Compilation.Validation;

/// <summary>
/// Represents a validation error encountered during expression analysis.
/// </summary>
/// <remarks>
/// This class provides detailed information about validation failures,
/// including error codes, messages, and the specific expression that caused the error.
/// </remarks>
public class ValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="code">The error code identifying the type of validation error.</param>
    /// <param name="message">A human-readable description of the error.</param>
    /// <param name="expression">The expression that caused the error.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="code"/> or <paramref name="message"/> is null.
    /// </exception>
    public ValidationError(string code, string message, Expression? expression = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Expression = expression;
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    /// <value>
    /// A string identifier for the specific type of validation error.
    /// </value>
    /// <remarks>
    /// Error codes provide a programmatic way to identify and handle
    /// specific types of validation failures.
    /// </remarks>
    public string Code { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    /// <value>
    /// A human-readable description of what went wrong during validation.
    /// </value>
    public string Message { get; }

    /// <summary>
    /// Gets the expression that caused the error.
    /// </summary>
    /// <value>
    /// The specific expression node that failed validation, or <c>null</c> if not available.
    /// </value>
    /// <remarks>
    /// This property can be used to provide precise error location information
    /// and to implement expression-specific error handling.
    /// </remarks>
    public Expression? Expression { get; }

    /// <summary>
    /// Returns a string representation of the validation error.
    /// </summary>
    /// <returns>A formatted string containing the error code and message.</returns>
    public override string ToString() => $"[{Code}] {Message}";
}