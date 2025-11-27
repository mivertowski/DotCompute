// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace DotCompute.Abstractions.FaultTolerance;

/// <summary>
/// Categorizes errors for appropriate handling strategies.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Unknown or unclassified error.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Transient error that may succeed on retry.
    /// Examples: Network timeout, temporary resource unavailable.
    /// </summary>
    Transient,

    /// <summary>
    /// Permanent error that will not succeed on retry.
    /// Examples: Invalid input, permission denied, not found.
    /// </summary>
    Permanent,

    /// <summary>
    /// Resource exhaustion error (memory, connections, etc.).
    /// May succeed after resource cleanup or cooldown.
    /// </summary>
    ResourceExhaustion,

    /// <summary>
    /// Configuration or setup error.
    /// Requires system reconfiguration to resolve.
    /// </summary>
    Configuration,

    /// <summary>
    /// Hardware or infrastructure error.
    /// May require failover to different hardware.
    /// </summary>
    Hardware,

    /// <summary>
    /// Timeout or deadline exceeded error.
    /// May succeed with longer timeout or faster execution.
    /// </summary>
    Timeout,

    /// <summary>
    /// Concurrency or synchronization error.
    /// May succeed on retry with different timing.
    /// </summary>
    Concurrency,

    /// <summary>
    /// Data corruption or integrity error.
    /// May require data recovery or rollback.
    /// </summary>
    DataIntegrity,

    /// <summary>
    /// Security or authentication error.
    /// Requires credential refresh or authorization.
    /// </summary>
    Security,

    /// <summary>
    /// External service or dependency error.
    /// Depends on external system recovery.
    /// </summary>
    ExternalDependency,

    /// <summary>
    /// GPU-specific error (memory, kernel failure, etc.).
    /// May require GPU reset or reconfiguration.
    /// </summary>
    GpuError
}

/// <summary>
/// Indicates the severity of an error.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational - logged but not critical.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning - may indicate potential issues.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - operation failed but system stable.
    /// </summary>
    Error,

    /// <summary>
    /// Critical - system stability may be affected.
    /// </summary>
    Critical,

    /// <summary>
    /// Fatal - system cannot continue operating.
    /// </summary>
    Fatal
}

/// <summary>
/// Suggested recovery action for an error.
/// </summary>
public enum RecoveryAction
{
    /// <summary>
    /// No specific action recommended.
    /// </summary>
    None = 0,

    /// <summary>
    /// Retry the operation immediately.
    /// </summary>
    RetryImmediate,

    /// <summary>
    /// Retry after a delay (exponential backoff recommended).
    /// </summary>
    RetryWithBackoff,

    /// <summary>
    /// Skip this operation and continue.
    /// </summary>
    Skip,

    /// <summary>
    /// Fail fast and propagate the error.
    /// </summary>
    FailFast,

    /// <summary>
    /// Use a fallback or alternative approach.
    /// </summary>
    Fallback,

    /// <summary>
    /// Restore from checkpoint and retry.
    /// </summary>
    RestoreAndRetry,

    /// <summary>
    /// Restart the affected component.
    /// </summary>
    RestartComponent,

    /// <summary>
    /// Alert an operator for manual intervention.
    /// </summary>
    AlertOperator,

    /// <summary>
    /// Reconfigure the system before retry.
    /// </summary>
    Reconfigure,

    /// <summary>
    /// Failover to an alternative resource.
    /// </summary>
    Failover
}

/// <summary>
/// Classification result for an error.
/// </summary>
public sealed class ErrorClassification
{
    /// <summary>
    /// Gets the error category.
    /// </summary>
    public ErrorCategory Category { get; init; }

    /// <summary>
    /// Gets the error severity.
    /// </summary>
    public ErrorSeverity Severity { get; init; }

    /// <summary>
    /// Gets the recommended recovery action.
    /// </summary>
    public RecoveryAction RecommendedAction { get; init; }

    /// <summary>
    /// Gets whether the error is retryable.
    /// </summary>
    public bool IsRetryable { get; init; }

    /// <summary>
    /// Gets the recommended retry delay if retryable.
    /// </summary>
    public TimeSpan? RecommendedRetryDelay { get; init; }

    /// <summary>
    /// Gets the maximum recommended retry attempts.
    /// </summary>
    public int? MaxRetryAttempts { get; init; }

    /// <summary>
    /// Gets a human-readable description of the error.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets suggested resolution steps.
    /// </summary>
    public IReadOnlyList<string> ResolutionSteps { get; init; } = [];

    /// <summary>
    /// Gets related error codes or identifiers.
    /// </summary>
    public IReadOnlyList<string> RelatedCodes { get; init; } = [];

    /// <summary>
    /// Gets additional metadata about the classification.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Gets when the classification was performed.
    /// </summary>
    public DateTimeOffset ClassifiedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates an unknown classification.
    /// </summary>
    public static ErrorClassification Unknown(string description) => new()
    {
        Category = ErrorCategory.Unknown,
        Severity = ErrorSeverity.Error,
        RecommendedAction = RecoveryAction.FailFast,
        IsRetryable = false,
        Description = description
    };

    /// <summary>
    /// Creates a transient error classification.
    /// </summary>
    public static ErrorClassification Transient(
        string description,
        TimeSpan? retryDelay = null,
        int maxRetries = 3) => new()
    {
        Category = ErrorCategory.Transient,
        Severity = ErrorSeverity.Warning,
        RecommendedAction = RecoveryAction.RetryWithBackoff,
        IsRetryable = true,
        RecommendedRetryDelay = retryDelay ?? TimeSpan.FromSeconds(1),
        MaxRetryAttempts = maxRetries,
        Description = description
    };

    /// <summary>
    /// Creates a permanent error classification.
    /// </summary>
    public static ErrorClassification Permanent(
        string description,
        RecoveryAction action = RecoveryAction.FailFast) => new()
    {
        Category = ErrorCategory.Permanent,
        Severity = ErrorSeverity.Error,
        RecommendedAction = action,
        IsRetryable = false,
        Description = description
    };
}

/// <summary>
/// Detailed diagnostic information for an error.
/// </summary>
public sealed class ErrorDiagnostics
{
    /// <summary>
    /// Gets the error classification.
    /// </summary>
    public required ErrorClassification Classification { get; init; }

    /// <summary>
    /// Gets the original exception.
    /// </summary>
    public required Exception Exception { get; init; }

    /// <summary>
    /// Gets the error timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the component where the error occurred.
    /// </summary>
    public string? ComponentId { get; init; }

    /// <summary>
    /// Gets the operation that was being performed.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the stack frames with source information.
    /// </summary>
    public IReadOnlyList<StackFrameInfo> StackFrames { get; init; } = [];

    /// <summary>
    /// Gets environment information at error time.
    /// </summary>
    public EnvironmentSnapshot? Environment { get; init; }

    /// <summary>
    /// Gets any inner errors.
    /// </summary>
    public IReadOnlyList<ErrorDiagnostics> InnerErrors { get; init; } = [];

    /// <summary>
    /// Gets custom context data.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }
}

/// <summary>
/// Information about a stack frame.
/// </summary>
public sealed record StackFrameInfo
{
    /// <summary>
    /// Gets the method name.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// Gets the declaring type name.
    /// </summary>
    public string? TypeName { get; init; }

    /// <summary>
    /// Gets the file path if available.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the line number if available.
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// Gets the column number if available.
    /// </summary>
    public int? ColumnNumber { get; init; }

    /// <summary>
    /// Gets the IL offset.
    /// </summary>
    public int? ILOffset { get; init; }
}

/// <summary>
/// Snapshot of the environment at error time.
/// </summary>
public sealed record EnvironmentSnapshot
{
    /// <summary>
    /// Gets the machine name.
    /// </summary>
    public string MachineName { get; init; } = Environment.MachineName;

    /// <summary>
    /// Gets the process ID.
    /// </summary>
    public int ProcessId { get; init; } = Environment.ProcessId;

    /// <summary>
    /// Gets the managed thread ID.
    /// </summary>
    public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;

    /// <summary>
    /// Gets the available memory in bytes.
    /// </summary>
    public long? AvailableMemory { get; init; }

    /// <summary>
    /// Gets the GC total memory.
    /// </summary>
    public long GcTotalMemory { get; init; } = GC.GetTotalMemory(false);

    /// <summary>
    /// Gets the current GC generation counts.
    /// </summary>
    public IReadOnlyDictionary<int, int> GcCollectionCounts { get; init; } = new Dictionary<int, int>
    {
        [0] = GC.CollectionCount(0),
        [1] = GC.CollectionCount(1),
        [2] = GC.CollectionCount(2)
    };

    /// <summary>
    /// Gets CPU-related information.
    /// </summary>
    public int ProcessorCount { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets when the snapshot was captured.
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Classifies exceptions into categories for appropriate handling.
/// </summary>
public interface IErrorClassifier
{
    /// <summary>
    /// Classifies an exception.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>The classification result.</returns>
    public ErrorClassification Classify(Exception exception);

    /// <summary>
    /// Creates detailed diagnostics for an exception.
    /// </summary>
    /// <param name="exception">The exception to diagnose.</param>
    /// <param name="componentId">Optional component identifier.</param>
    /// <param name="operation">Optional operation description.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    /// <returns>Detailed diagnostic information.</returns>
    public ErrorDiagnostics CreateDiagnostics(
        Exception exception,
        string? componentId = null,
        string? operation = null,
        string? correlationId = null);

    /// <summary>
    /// Determines if an exception is retryable.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is retryable.</returns>
    public bool IsRetryable(Exception exception);

    /// <summary>
    /// Gets the recommended retry delay for an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="attemptNumber">The current attempt number (1-based).</param>
    /// <returns>The recommended delay before retry.</returns>
    public TimeSpan GetRetryDelay(Exception exception, int attemptNumber);
}

/// <summary>
/// Provides error classification extensions.
/// </summary>
public static class ErrorClassifierExtensions
{
    /// <summary>
    /// Classifies and creates diagnostics in one call.
    /// </summary>
    public static ErrorDiagnostics ClassifyAndDiagnose(
        this IErrorClassifier classifier,
        Exception exception,
        string? componentId = null,
        string? operation = null)
    {
        return classifier.CreateDiagnostics(exception, componentId, operation);
    }

    /// <summary>
    /// Determines if an error should be retried based on classification and attempt count.
    /// </summary>
    public static bool ShouldRetry(
        this ErrorClassification classification,
        int attemptNumber)
    {
        if (!classification.IsRetryable)
        {
            return false;
        }

        if (classification.MaxRetryAttempts.HasValue &&
            attemptNumber >= classification.MaxRetryAttempts.Value)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the exponential backoff delay for a retry attempt.
    /// </summary>
    public static TimeSpan GetExponentialBackoff(
        this ErrorClassification classification,
        int attemptNumber,
        double multiplier = 2.0,
        TimeSpan? maxDelay = null)
    {
        var baseDelay = classification.RecommendedRetryDelay ?? TimeSpan.FromSeconds(1);
        var delay = TimeSpan.FromTicks((long)(baseDelay.Ticks * Math.Pow(multiplier, attemptNumber - 1)));

        if (maxDelay.HasValue && delay > maxDelay.Value)
        {
            delay = maxDelay.Value;
        }

        return delay;
    }
}
