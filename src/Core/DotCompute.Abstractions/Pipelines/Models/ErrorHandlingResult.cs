// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Result of error handling operations in pipeline execution.
/// Provides information about how errors were handled and their resolution.
/// </summary>
public sealed class ErrorHandlingResult
{
    /// <summary>
    /// Gets or sets whether the error was successfully handled.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the strategy that was used to handle the error.
    /// </summary>
    public ErrorHandlingStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets the original exception that was handled.
    /// </summary>
    public Exception? OriginalException { get; set; }

    /// <summary>
    /// Gets or sets the action that was taken to handle the error.
    /// </summary>
    public ErrorHandlingAction Action { get; set; }

    /// <summary>
    /// Gets or sets the result value after error handling, if applicable.
    /// </summary>
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets whether the operation should continue after error handling.
    /// </summary>
    public bool ShouldContinue { get; set; }

    /// <summary>
    /// Gets or sets whether the operation should be retried.
    /// </summary>
    public bool ShouldRetry { get; set; }

    /// <summary>
    /// Gets or sets the number of retries attempted.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries allowed.
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets the delay before the next retry.
    /// </summary>
    public TimeSpan? RetryDelay { get; set; }

    /// <summary>
    /// Gets or sets additional error information or context.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets error recovery suggestions.
    /// </summary>
    public IList<string> RecoverySuggestions { get; set; } = [];

    /// <summary>
    /// Gets or sets metadata associated with the error handling.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the timestamp when error handling was performed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the duration of error handling operations.
    /// </summary>
    public TimeSpan HandlingDuration { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the handled error.
    /// </summary>
    public ErrorSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets the impact of the error on pipeline execution.
    /// </summary>
    public ErrorImpact Impact { get; set; }

    /// <summary>
    /// Gets or sets whether the error is recoverable.
    /// </summary>
    public bool IsRecoverable { get; set; }

    /// <summary>
    /// Gets or sets the error category for classification.
    /// </summary>
    public ErrorCategory Category { get; set; }

    /// <summary>
    /// Creates a successful error handling result.
    /// </summary>
    /// <param name="strategy">The strategy used to handle the error</param>
    /// <param name="action">The action taken</param>
    /// <param name="result">The result after handling</param>
    /// <returns>A successful error handling result</returns>
    public static ErrorHandlingResult CreateSuccess(ErrorHandlingStrategy strategy, ErrorHandlingAction action, object? result = null)
    {
        return new ErrorHandlingResult
        {
            Success = true,
            Strategy = strategy,
            Action = action,
            Result = result,
            ShouldContinue = true
        };
    }

    /// <summary>
    /// Creates a failed error handling result.
    /// </summary>
    /// <param name="originalException">The original exception</param>
    /// <param name="errorMessage">Additional error message</param>
    /// <returns>A failed error handling result</returns>
    public static ErrorHandlingResult Failure(Exception originalException, string? errorMessage = null)
    {
        return new ErrorHandlingResult
        {
            Success = false,
            OriginalException = originalException,
            ErrorMessage = errorMessage ?? originalException.Message,
            Action = ErrorHandlingAction.Failed,
            ShouldContinue = false
        };
    }

    /// <summary>
    /// Creates a retry error handling result.
    /// </summary>
    /// <param name="originalException">The original exception</param>
    /// <param name="retryCount">Current retry count</param>
    /// <param name="maxRetries">Maximum retries allowed</param>
    /// <param name="retryDelay">Delay before next retry</param>
    /// <returns>A retry error handling result</returns>
    public static ErrorHandlingResult Retry(Exception originalException, int retryCount, int maxRetries, TimeSpan? retryDelay = null)
    {
        return new ErrorHandlingResult
        {
            Success = false,
            Strategy = ErrorHandlingStrategy.Retry,
            OriginalException = originalException,
            Action = ErrorHandlingAction.Retry,
            ShouldRetry = retryCount < maxRetries,
            ShouldContinue = retryCount < maxRetries,
            RetryCount = retryCount,
            MaxRetries = maxRetries,
            RetryDelay = retryDelay
        };
    }

    /// <summary>
    /// Creates a fallback error handling result.
    /// </summary>
    /// <param name="originalException">The original exception</param>
    /// <param name="fallbackResult">The fallback result to use</param>
    /// <returns>A fallback error handling result</returns>
    public static ErrorHandlingResult Fallback(Exception originalException, object? fallbackResult)
    {
        return new ErrorHandlingResult
        {
            Success = true,
            Strategy = ErrorHandlingStrategy.Fallback,
            OriginalException = originalException,
            Action = ErrorHandlingAction.UsedFallback,
            Result = fallbackResult,
            ShouldContinue = true
        };
    }

    /// <summary>
    /// Adds a recovery suggestion to the result.
    /// </summary>
    /// <param name="suggestion">The recovery suggestion to add</param>
    public void AddRecoverySuggestion(string suggestion)
    {
        RecoverySuggestions.Add(suggestion);
    }

    /// <summary>
    /// Adds metadata to the error handling result.
    /// </summary>
    /// <param name="key">The metadata key</param>
    /// <param name="value">The metadata value</param>
    public void AddMetadata(string key, object value)
    {
        Metadata[key] = value;
    }
}

/// <summary>
/// Actions that can be taken during error handling.
/// </summary>
public enum ErrorHandlingAction
{
    /// <summary>
    /// No action taken.
    /// </summary>
    None,

    /// <summary>
    /// Error handling failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Operation was retried.
    /// </summary>
    Retry,

    /// <summary>
    /// Operation was skipped.
    /// </summary>
    Skip,

    /// <summary>
    /// Execution was aborted.
    /// </summary>
    Abort,

    /// <summary>
    /// Fallback value was used.
    /// </summary>
    UsedFallback,

    /// <summary>
    /// Default value was substituted.
    /// </summary>
    UsedDefault,

    /// <summary>
    /// Alternative backend was used.
    /// </summary>
    UsedAlternativeBackend,

    /// <summary>
    /// Operation was degraded to simpler version.
    /// </summary>
    Degraded,

    /// <summary>
    /// Error was logged and ignored.
    /// </summary>
    Ignored,

    /// <summary>
    /// Manual intervention is required.
    /// </summary>
    RequiresIntervention
}

/// <summary>
/// Impact level of errors on pipeline execution.
/// </summary>
public enum ErrorImpact
{
    /// <summary>
    /// No impact on execution.
    /// </summary>
    None,

    /// <summary>
    /// Minor impact with degraded performance.
    /// </summary>
    Minor,

    /// <summary>
    /// Moderate impact with noticeable effects.
    /// </summary>
    Moderate,

    /// <summary>
    /// Major impact affecting functionality.
    /// </summary>
    Major,

    /// <summary>
    /// Critical impact preventing execution.
    /// </summary>
    Critical,

    /// <summary>
    /// Catastrophic failure requiring system restart.
    /// </summary>
    Catastrophic
}

/// <summary>
/// Categories of errors for classification and handling.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Unknown or unclassified error.
    /// </summary>
    Unknown,

    /// <summary>
    /// Hardware-related error.
    /// </summary>
    Hardware,

    /// <summary>
    /// Software or logic error.
    /// </summary>
    Software,

    /// <summary>
    /// Network communication error.
    /// </summary>
    Network,

    /// <summary>
    /// Memory allocation or access error.
    /// </summary>
    Memory,

    /// <summary>
    /// Input/output operation error.
    /// </summary>
    IO,

    /// <summary>
    /// Configuration or setup error.
    /// </summary>
    Configuration,

    /// <summary>
    /// Resource unavailability error.
    /// </summary>
    Resource,

    /// <summary>
    /// Timeout or deadline exceeded.
    /// </summary>
    Timeout,

    /// <summary>
    /// Permission or security error.
    /// </summary>
    Security,

    /// <summary>
    /// Data validation or format error.
    /// </summary>
    Validation,

    /// <summary>
    /// Concurrency or synchronization error.
    /// </summary>
    Concurrency,

    /// <summary>
    /// External dependency error.
    /// </summary>
    External
}