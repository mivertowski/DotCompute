// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Utilities.ErrorHandling.Enums;

namespace DotCompute.Core.Utilities.ErrorHandling.Models;

/// <summary>
/// Error pattern for pattern matching and automated error handling.
/// Defines rules for recognizing and responding to specific error conditions.
/// </summary>
public sealed class ErrorPattern
{
    /// <summary>
    /// Gets or sets the regex pattern for matching error messages.
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// Gets or sets the classification to assign to matching errors.
    /// </summary>
    public required ErrorClassification Classification { get; init; }

    /// <summary>
    /// Gets or sets whether errors matching this pattern are transient.
    /// </summary>
    public required bool IsTransient { get; init; }

    /// <summary>
    /// Gets or sets the recommended action for this error pattern.
    /// </summary>
    public required string RecommendedAction { get; init; }

    /// <summary>
    /// Gets or sets the retry delay for transient errors.
    /// </summary>
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int? MaxRetries { get; init; }

    /// <summary>
    /// Returns a string representation of the error pattern.
    /// </summary>
    public override string ToString()
        => $"{Classification} pattern: {Pattern} (Transient: {IsTransient})";
}
