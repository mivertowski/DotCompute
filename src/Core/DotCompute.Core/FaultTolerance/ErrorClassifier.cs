// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DotCompute.Abstractions.FaultTolerance;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.FaultTolerance;

/// <summary>
/// Default implementation of error classification.
/// </summary>
/// <remarks>
/// Classifies exceptions into categories based on type and characteristics
/// to enable appropriate recovery strategies.
/// </remarks>
public sealed partial class ErrorClassifier : IErrorClassifier
{
    private readonly ILogger<ErrorClassifier> _logger;
    private readonly ErrorClassifierOptions _options;

    // Event IDs: 9900-9999 for ErrorClassifier
    [LoggerMessage(EventId = 9900, Level = LogLevel.Debug,
        Message = "Classified exception {ExceptionType} as {Category}/{Severity}")]
    private static partial void LogClassified(
        ILogger logger, string exceptionType, ErrorCategory category, ErrorSeverity severity);

    [LoggerMessage(EventId = 9901, Level = LogLevel.Debug,
        Message = "Created diagnostics for {ExceptionType} in component {ComponentId}")]
    private static partial void LogDiagnosticsCreated(
        ILogger logger, string exceptionType, string? componentId);

    /// <summary>
    /// Creates a new error classifier.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Classification options.</param>
    public ErrorClassifier(ILogger<ErrorClassifier> logger, ErrorClassifierOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ErrorClassifierOptions();
    }

    /// <inheritdoc />
    public ErrorClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var classification = ClassifyInternal(exception);

        LogClassified(_logger, exception.GetType().Name, classification.Category, classification.Severity);

        return classification;
    }

    /// <inheritdoc />
    public ErrorDiagnostics CreateDiagnostics(
        Exception exception,
        string? componentId = null,
        string? operation = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var classification = Classify(exception);
        var stackFrames = ExtractStackFrames(exception);
        var innerErrors = ExtractInnerErrors(exception, componentId, operation, correlationId);

        var diagnostics = new ErrorDiagnostics
        {
            Classification = classification,
            Exception = exception,
            ComponentId = componentId,
            Operation = operation,
            CorrelationId = correlationId,
            StackFrames = stackFrames,
            Environment = _options.IncludeEnvironmentSnapshot ? CaptureEnvironment() : null,
            InnerErrors = innerErrors,
            Context = ExtractContext(exception)
        };

        LogDiagnosticsCreated(_logger, exception.GetType().Name, componentId);

        return diagnostics;
    }

    /// <inheritdoc />
    public bool IsRetryable(Exception exception)
    {
        return Classify(exception).IsRetryable;
    }

    /// <inheritdoc />
    public TimeSpan GetRetryDelay(Exception exception, int attemptNumber)
    {
        var classification = Classify(exception);

        if (!classification.IsRetryable)
        {
            return TimeSpan.Zero;
        }

        return classification.GetExponentialBackoff(
            attemptNumber,
            _options.BackoffMultiplier,
            _options.MaxRetryDelay);
    }

    private ErrorClassification ClassifyInternal(Exception exception)
    {
        // Check custom classifiers first
        foreach (var classifier in _options.CustomClassifiers)
        {
            var result = classifier(exception);
            if (result != null)
            {
                return result;
            }
        }

        // Classify by exception type
        // Note: More specific exception types must come before their base types
        return exception switch
        {
            // Timeout exceptions
            TimeoutException => CreateTimeoutClassification(exception),
            OperationCanceledException oce when oce.CancellationToken.IsCancellationRequested =>
                CreateCancellationClassification(),
            TaskCanceledException tce when tce.CancellationToken.IsCancellationRequested =>
                CreateCancellationClassification(),

            // Network/IO exceptions (transient)
            SocketException se => ClassifySocketException(se),
            HttpRequestException => CreateTransientNetworkClassification("HTTP request failed"),
            IOException io => ClassifyIOException(io),

            // Memory exceptions (InsufficientMemoryException derives from OutOfMemoryException, so it's covered)
            OutOfMemoryException => CreateResourceExhaustionClassification("Out of memory"),

            // Concurrency exceptions
            SynchronizationLockException => CreateConcurrencyClassification("Lock acquisition failed"),

            // Object state exceptions (permanent) - ObjectDisposedException extends InvalidOperationException, so check first
            ObjectDisposedException => CreatePermanentClassification("Object disposed", RecoveryAction.RestartComponent),

            // Argument/validation exceptions (permanent)
            // ArgumentNullException and ArgumentOutOfRangeException derive from ArgumentException
            ArgumentException => CreatePermanentClassification("Invalid argument", RecoveryAction.FailFast),
            InvalidOperationException => CreatePermanentClassification("Invalid operation state", RecoveryAction.FailFast),
            NotSupportedException => CreatePermanentClassification("Operation not supported", RecoveryAction.FailFast),
            NotImplementedException => CreatePermanentClassification("Not implemented", RecoveryAction.FailFast),

            // Security exceptions
            UnauthorizedAccessException => CreateSecurityClassification("Unauthorized access"),
            global::System.Security.SecurityException => CreateSecurityClassification("Security violation"),

            // Data exceptions
            FormatException => CreateDataIntegrityClassification("Invalid format"),
            InvalidCastException => CreateDataIntegrityClassification("Invalid type conversion"),
            ArithmeticException => CreateDataIntegrityClassification("Arithmetic error"),

            // Other object state exceptions (permanent)
            NullReferenceException => CreatePermanentClassification("Null reference", RecoveryAction.FailFast),
            IndexOutOfRangeException => CreatePermanentClassification("Index out of range", RecoveryAction.FailFast),

            // Aggregate exceptions - classify based on inner exceptions
            AggregateException ae => ClassifyAggregateException(ae),

            // Default
            _ => ClassifyByMessage(exception)
        };
    }

    private static ErrorClassification ClassifySocketException(SocketException se)
    {
        return se.SocketErrorCode switch
        {
            SocketError.ConnectionRefused => CreateTransientNetworkClassification("Connection refused"),
            SocketError.ConnectionReset => CreateTransientNetworkClassification("Connection reset"),
            SocketError.HostUnreachable => CreateTransientNetworkClassification("Host unreachable"),
            SocketError.NetworkUnreachable => CreateTransientNetworkClassification("Network unreachable"),
            SocketError.TimedOut => CreateTimeoutClassification(se),
            SocketError.ConnectionAborted => CreateTransientNetworkClassification("Connection aborted"),
            SocketError.TryAgain => CreateTransientNetworkClassification("Temporary DNS failure"),
            SocketError.AccessDenied => CreateSecurityClassification("Socket access denied"),
            SocketError.AddressAlreadyInUse => CreateResourceExhaustionClassification("Address in use"),
            SocketError.NoBufferSpaceAvailable => CreateResourceExhaustionClassification("No buffer space"),
            SocketError.TooManyOpenSockets => CreateResourceExhaustionClassification("Too many sockets"),
            _ => CreateTransientNetworkClassification($"Socket error: {se.SocketErrorCode}")
        };
    }

    private static ErrorClassification ClassifyIOException(IOException io)
    {
        var message = io.Message;

        if (message.Contains("disk full", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("no space", StringComparison.OrdinalIgnoreCase))
        {
            return CreateResourceExhaustionClassification("Disk full");
        }

        if (message.Contains("locked", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("in use", StringComparison.OrdinalIgnoreCase))
        {
            return CreateConcurrencyClassification("File locked");
        }

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return CreatePermanentClassification("File not found", RecoveryAction.FailFast);
        }

        if (message.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return CreateSecurityClassification("File access denied");
        }

        // Most IO exceptions are transient
        return ErrorClassification.Transient(
            "I/O operation failed",
            TimeSpan.FromSeconds(1),
            3);
    }

    private ErrorClassification ClassifyAggregateException(AggregateException ae)
    {
        var flattened = ae.Flatten();

        // If all inner exceptions have the same classification, use that
        var classifications = flattened.InnerExceptions
            .Select(ClassifyInternal)
            .ToList();

        // If any is permanent, treat as permanent
        if (classifications.Any(c => c.Category == ErrorCategory.Permanent))
        {
            return CreatePermanentClassification(
                "One or more permanent errors occurred",
                RecoveryAction.FailFast);
        }

        // If any is resource exhaustion, treat as resource exhaustion
        if (classifications.Any(c => c.Category == ErrorCategory.ResourceExhaustion))
        {
            return CreateResourceExhaustionClassification("One or more resource exhaustion errors");
        }

        // If all are transient, it's transient
        if (classifications.All(c => c.Category == ErrorCategory.Transient))
        {
            return ErrorClassification.Transient(
                "Multiple transient errors occurred",
                TimeSpan.FromSeconds(1),
                3);
        }

        // Mixed or unknown
        return ErrorClassification.Unknown("Multiple errors of different types occurred");
    }

    private static ErrorClassification ClassifyByMessage(Exception exception)
    {
        var message = exception.Message;

        // Check for common transient error patterns
        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return CreateTimeoutClassification(exception);
        }

        if (message.Contains("retry", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("transient", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorClassification.Transient(exception.Message);
        }

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return CreatePermanentClassification("Resource not found", RecoveryAction.FailFast);
        }

        if (message.Contains("cuda", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("gpu", StringComparison.OrdinalIgnoreCase))
        {
            return CreateGpuErrorClassification(exception.Message);
        }

        if (message.Contains("memory", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("allocation", StringComparison.OrdinalIgnoreCase))
        {
            return CreateResourceExhaustionClassification("Memory issue detected");
        }

        // Default to unknown
        return ErrorClassification.Unknown(exception.Message);
    }

    private static ErrorClassification CreateTimeoutClassification(Exception exception) => new()
    {
        Category = ErrorCategory.Timeout,
        Severity = ErrorSeverity.Warning,
        RecommendedAction = RecoveryAction.RetryWithBackoff,
        IsRetryable = true,
        RecommendedRetryDelay = TimeSpan.FromSeconds(2),
        MaxRetryAttempts = 3,
        Description = exception.Message,
        ResolutionSteps =
        [
            "Check network connectivity",
            "Verify service availability",
            "Consider increasing timeout duration",
            "Check for resource contention"
        ]
    };

    private static ErrorClassification CreateCancellationClassification() => new()
    {
        Category = ErrorCategory.Transient,
        Severity = ErrorSeverity.Info,
        RecommendedAction = RecoveryAction.Skip,
        IsRetryable = false,
        Description = "Operation was cancelled",
        ResolutionSteps = ["Operation cancelled by user or system", "No action required"]
    };

    private static ErrorClassification CreateTransientNetworkClassification(string description) => new()
    {
        Category = ErrorCategory.Transient,
        Severity = ErrorSeverity.Warning,
        RecommendedAction = RecoveryAction.RetryWithBackoff,
        IsRetryable = true,
        RecommendedRetryDelay = TimeSpan.FromSeconds(1),
        MaxRetryAttempts = 5,
        Description = description,
        ResolutionSteps =
        [
            "Check network connectivity",
            "Verify remote service is available",
            "Check firewall rules",
            "Consider circuit breaker pattern"
        ]
    };

    private static ErrorClassification CreateResourceExhaustionClassification(string description) => new()
    {
        Category = ErrorCategory.ResourceExhaustion,
        Severity = ErrorSeverity.Critical,
        RecommendedAction = RecoveryAction.RetryWithBackoff,
        IsRetryable = true,
        RecommendedRetryDelay = TimeSpan.FromSeconds(5),
        MaxRetryAttempts = 2,
        Description = description,
        ResolutionSteps =
        [
            "Free up system resources",
            "Check for memory leaks",
            "Consider reducing batch size",
            "Increase available resources"
        ]
    };

    private static ErrorClassification CreateConcurrencyClassification(string description) => new()
    {
        Category = ErrorCategory.Concurrency,
        Severity = ErrorSeverity.Warning,
        RecommendedAction = RecoveryAction.RetryWithBackoff,
        IsRetryable = true,
        RecommendedRetryDelay = TimeSpan.FromMilliseconds(100),
        MaxRetryAttempts = 3,
        Description = description,
        ResolutionSteps =
        [
            "Retry with exponential backoff",
            "Check for deadlocks",
            "Review locking strategy",
            "Consider optimistic concurrency"
        ]
    };

    private static ErrorClassification CreateSecurityClassification(string description) => new()
    {
        Category = ErrorCategory.Security,
        Severity = ErrorSeverity.Error,
        RecommendedAction = RecoveryAction.Reconfigure,
        IsRetryable = false,
        Description = description,
        ResolutionSteps =
        [
            "Check credentials",
            "Verify permissions",
            "Review security configuration",
            "Contact administrator"
        ]
    };

    private static ErrorClassification CreateDataIntegrityClassification(string description) => new()
    {
        Category = ErrorCategory.DataIntegrity,
        Severity = ErrorSeverity.Error,
        RecommendedAction = RecoveryAction.RestoreAndRetry,
        IsRetryable = false,
        Description = description,
        ResolutionSteps =
        [
            "Validate input data",
            "Check data format",
            "Restore from checkpoint if available",
            "Review data processing logic"
        ]
    };

    private static ErrorClassification CreatePermanentClassification(
        string description,
        RecoveryAction action) => new()
    {
        Category = ErrorCategory.Permanent,
        Severity = ErrorSeverity.Error,
        RecommendedAction = action,
        IsRetryable = false,
        Description = description
    };

    private static ErrorClassification CreateGpuErrorClassification(string description) => new()
    {
        Category = ErrorCategory.GpuError,
        Severity = ErrorSeverity.Error,
        RecommendedAction = RecoveryAction.Failover,
        IsRetryable = true,
        RecommendedRetryDelay = TimeSpan.FromSeconds(2),
        MaxRetryAttempts = 2,
        Description = description,
        ResolutionSteps =
        [
            "Check GPU availability",
            "Verify CUDA/OpenCL installation",
            "Check GPU memory usage",
            "Consider falling back to CPU",
            "Reset GPU context"
        ]
    };

    private static IReadOnlyList<StackFrameInfo> ExtractStackFrames(Exception exception)
    {
        var stackTrace = new StackTrace(exception, true);
        var frames = new List<StackFrameInfo>();

        foreach (var frame in stackTrace.GetFrames() ?? [])
        {
            var method = frame.GetMethod();
            if (method == null)
            {
                continue;
            }

            frames.Add(new StackFrameInfo
            {
                MethodName = method.Name,
                TypeName = method.DeclaringType?.FullName,
                FilePath = frame.GetFileName(),
                LineNumber = frame.GetFileLineNumber() > 0 ? frame.GetFileLineNumber() : null,
                ColumnNumber = frame.GetFileColumnNumber() > 0 ? frame.GetFileColumnNumber() : null,
                ILOffset = frame.GetILOffset() >= 0 ? frame.GetILOffset() : null
            });
        }

        return frames;
    }

    private IReadOnlyList<ErrorDiagnostics> ExtractInnerErrors(
        Exception exception,
        string? componentId,
        string? operation,
        string? correlationId)
    {
        var innerErrors = new List<ErrorDiagnostics>();

        if (exception.InnerException != null)
        {
            innerErrors.Add(CreateDiagnostics(
                exception.InnerException,
                componentId,
                operation,
                correlationId));
        }

        if (exception is AggregateException ae)
        {
            foreach (var inner in ae.InnerExceptions)
            {
                if (inner != exception.InnerException)
                {
                    innerErrors.Add(CreateDiagnostics(
                        inner,
                        componentId,
                        operation,
                        correlationId));
                }
            }
        }

        return innerErrors;
    }

    private static EnvironmentSnapshot CaptureEnvironment()
    {
        return new EnvironmentSnapshot();
    }

    private static IReadOnlyDictionary<string, object>? ExtractContext(Exception exception)
    {
        if (exception.Data.Count == 0)
        {
            return null;
        }

        var context = new Dictionary<string, object>();

        foreach (var key in exception.Data.Keys)
        {
            if (key != null && exception.Data[key] != null)
            {
                context[key.ToString()!] = exception.Data[key]!;
            }
        }

        return context.Count > 0 ? context : null;
    }
}

/// <summary>
/// Configuration options for the error classifier.
/// </summary>
public sealed class ErrorClassifierOptions
{
    /// <summary>
    /// Gets or sets whether to include environment snapshots in diagnostics.
    /// Default: true.
    /// </summary>
    public bool IncludeEnvironmentSnapshot { get; init; } = true;

    /// <summary>
    /// Gets or sets the backoff multiplier for retry delays.
    /// Default: 2.0.
    /// </summary>
    public double BackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum retry delay.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets custom classifiers that run before built-in classification.
    /// Return null to fall through to built-in classification.
    /// </summary>
    public IReadOnlyList<Func<Exception, ErrorClassification?>> CustomClassifiers { get; init; } = [];
}
