// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Utilities.ErrorHandling.Enums;

namespace DotCompute.Core.Utilities.ErrorHandling.Models;

/// <summary>
/// Comprehensive error context information for detailed error analysis.
/// Contains all relevant metadata for debugging and error resolution.
/// </summary>
public sealed class ErrorContext
{
    /// <summary>
    /// Gets or sets the unique error identifier.
    /// </summary>
    public required string ErrorId { get; init; }

    /// <summary>
    /// Gets or sets the exception that occurred.
    /// </summary>
    public required Exception Exception { get; init; }

    /// <summary>
    /// Gets or sets the operation being performed when the error occurred.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets or sets the backend type where the error occurred.
    /// </summary>
    public required string BackendType { get; init; }

    /// <summary>
    /// Gets or sets the error classification.
    /// </summary>
    public required ErrorClassification Classification { get; init; }

    /// <summary>
    /// Gets or sets the error severity level.
    /// </summary>
    public required ErrorSeverity Severity { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the stack trace at the time of error.
    /// </summary>
    public required string StackTrace { get; init; }

    /// <summary>
    /// Gets or sets the collection of inner exceptions.
    /// </summary>
    public required IList<Exception> InnerExceptions { get; init; }

    /// <summary>
    /// Gets or sets additional contextual information.
    /// </summary>
    public required Dictionary<string, object> AdditionalContext { get; init; }

    /// <summary>
    /// Gets whether this error requires immediate attention.
    /// </summary>
    public bool RequiresImmediateAttention => Severity >= ErrorSeverity.High;

    /// <summary>
    /// Returns a string representation of the error context.
    /// </summary>
    public override string ToString()
        => $"[{ErrorId}] {Classification} - {Exception.Message} in {Operation}";
}
