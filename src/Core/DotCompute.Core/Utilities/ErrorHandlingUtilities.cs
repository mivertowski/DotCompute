// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Utilities.ErrorHandling.Enums;
using DotCompute.Core.Utilities.ErrorHandling.Models;

namespace DotCompute.Core.Utilities;

/// <summary>
/// Unified error handling utilities that consolidate common error handling patterns
/// across all backend implementations. Provides consistent error classification,
/// logging, and recovery strategies with production-grade reliability.
/// </summary>
public static class ErrorHandlingUtilities
{
    private static readonly ConcurrentDictionary<Type, ErrorClassification> ErrorClassificationCache = new();
    private static readonly ConcurrentDictionary<string, ErrorPattern> ErrorPatternCache = new();

    /// <summary>
    /// Classifies an exception into predefined categories for appropriate handling.
    /// </summary>
    public static ErrorClassification ClassifyError(Exception exception)
    {
        if (exception == null)
        {
            return ErrorClassification.Unknown;
        }

        // Check cache first for performance

        var exceptionType = exception.GetType();
        if (ErrorClassificationCache.TryGetValue(exceptionType, out var cached))
        {
            return cached;
        }

        var classification = ClassifyErrorInternal(exception);
        ErrorClassificationCache.TryAdd(exceptionType, classification);
        return classification;
    }

    /// <summary>
    /// Creates a comprehensive error context with all relevant information.
    /// </summary>
    public static ErrorContext CreateErrorContext(
        Exception exception,
        string operation,
        string? backendType = null,
        Dictionary<string, object>? additionalContext = null)
    {
        var classification = ClassifyError(exception);
        var severity = DetermineSeverity(exception, classification);
        var errorId = GenerateErrorId(exception);

        var context = new ErrorContext
        {
            ErrorId = errorId,
            Exception = exception,
            Operation = operation,
            BackendType = backendType ?? "Unknown",
            Classification = classification,
            Severity = severity,
            Timestamp = DateTimeOffset.UtcNow,
            StackTrace = exception.StackTrace ?? string.Empty,
            InnerExceptions = CollectInnerExceptions(exception),
            AdditionalContext = additionalContext ?? []
        };

        // Add runtime context
        context.AdditionalContext["ProcessId"] = Environment.ProcessId;
        context.AdditionalContext["ThreadId"] = Environment.CurrentManagedThreadId;
        context.AdditionalContext["MachineName"] = Environment.MachineName;
        context.AdditionalContext["OSVersion"] = Environment.OSVersion.ToString();

        return context;
    }

    /// <summary>
    /// Logs an error with consistent formatting and appropriate log level.
    /// </summary>
    public static void LogError(
        ILogger logger,
        ErrorContext errorContext,
        string? customMessage = null)
    {
        var message = customMessage ?? $"Error in {errorContext.Operation}: {errorContext.Exception.Message}";

        var logLevel = errorContext.Severity switch
        {
            ErrorSeverity.Critical => LogLevel.Critical,
            ErrorSeverity.High => LogLevel.Error,
            ErrorSeverity.Medium => LogLevel.Warning,
            ErrorSeverity.Low => LogLevel.Information,
            _ => LogLevel.Debug
        };

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["ErrorId"] = errorContext.ErrorId,
            ["Backend"] = errorContext.BackendType,
            ["Classification"] = errorContext.Classification.ToString(),
            ["Severity"] = errorContext.Severity.ToString()
        });

        logger.Log(logLevel, errorContext.Exception, message);

        // Log additional context for high severity errors
        if (errorContext.Severity >= ErrorSeverity.High)
        {
            LogDetailedErrorContext(logger, errorContext);
        }
    }

    /// <summary>
    /// Determines if an error is transient and might succeed on retry.
    /// </summary>
    public static bool IsTransientError(Exception exception)
    {
        var classification = ClassifyError(exception);

        return classification switch
        {
            ErrorClassification.MemoryExhaustion => true,
            ErrorClassification.DeviceBusy => true,
            ErrorClassification.Timeout => true,
            ErrorClassification.TemporaryFailure => true,
            ErrorClassification.ResourceContention => true,
            ErrorClassification.NetworkError => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an error requires immediate attention or manual intervention.
    /// </summary>
    public static bool RequiresImmediateAttention(Exception exception)
    {
        var classification = ClassifyError(exception);
        var severity = DetermineSeverity(exception, classification);

        return severity >= ErrorSeverity.High || classification switch
        {
            ErrorClassification.HardwareFailure => true,
            ErrorClassification.DataCorruption => true,
            ErrorClassification.SecurityViolation => true,
            ErrorClassification.SystemFailure => true,
            _ => false
        };
    }

    /// <summary>
    /// Creates a user-friendly error message from technical exception details.
    /// </summary>
    public static string CreateUserFriendlyMessage(Exception exception, string operation)
    {
        var classification = ClassifyError(exception);

        var baseMessage = classification switch
        {
            ErrorClassification.MemoryExhaustion => "Insufficient memory available",
            ErrorClassification.DeviceNotFound => "Hardware device not available",
            ErrorClassification.DeviceBusy => "Hardware device is currently busy",
            ErrorClassification.InvalidConfiguration => "Invalid configuration detected",
            ErrorClassification.HardwareFailure => "Hardware failure detected",
            ErrorClassification.Timeout => "Operation timed out",
            ErrorClassification.PermissionDenied => "Access denied",
            ErrorClassification.DataCorruption => "Data integrity issue detected",
            ErrorClassification.NetworkError => "Network connectivity issue",
            ErrorClassification.InvalidInput => "Invalid input provided",
            _ => "An unexpected error occurred"
        };

        return $"{baseMessage} during {operation}. Please try again or contact support if the problem persists.";
    }

    /// <summary>
    /// Wraps an operation with comprehensive error handling and classification.
    /// </summary>
    public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        ILogger logger,
        string? backendType = null,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation();
            stopwatch.Stop();

            logger.LogDebug("Operation {Operation} completed successfully in {Duration}ms",
                operationName, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            stopwatch.Stop();

            var errorContext = CreateErrorContext(ex, operationName, backendType, context);
            errorContext.AdditionalContext["ExecutionTime"] = stopwatch.Elapsed;

            LogError(logger, errorContext);

            // Enrich the exception with context before rethrowing
            throw EnrichException(ex, errorContext);
        }
    }

    /// <summary>
    /// Executes an operation with retry logic for transient errors.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        ILogger logger,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        string? backendType = null,
        CancellationToken cancellationToken = default)
    {
        var delay = baseDelay ?? TimeSpan.FromMilliseconds(100);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                return await ExecuteWithErrorHandlingAsync(
                    operation, operationName, logger, backendType,
                    new Dictionary<string, object> { ["Attempt"] = attempt },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt > maxRetries || !IsTransientError(ex))
                {
                    throw; // Re-throw if we've exceeded retries or error is not transient
                }

                var retryDelay = CalculateRetryDelay(delay, attempt);
                logger.LogWarning("Operation {Operation} failed on attempt {Attempt}/{MaxAttempts}, retrying in {Delay}ms. Error: {Error}",
                    operationName, attempt, maxRetries + 1, retryDelay.TotalMilliseconds, ex.Message);

                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        // This should never be reached, but throw the last exception if it somehow is
        throw lastException ?? new InvalidOperationException("Unexpected state in retry logic");
    }

    /// <summary>
    /// Aggregates multiple errors into a single exception with detailed context.
    /// </summary>
    public static Exception AggregateErrors(
        IEnumerable<Exception> errors,
        string operation,
        string? backendType = null)
    {
        var errorList = errors.ToList();

        if (errorList.Count == 0)
        {

            return new InvalidOperationException($"No errors provided for aggregation in operation: {operation}");
        }


        if (errorList.Count == 1)
        {
            var context = CreateErrorContext(errorList[0], operation, backendType);
            return EnrichException(errorList[0], context);
        }

        var message = new StringBuilder();
        message.AppendLine($"Multiple errors occurred during {operation}:");

        for (var i = 0; i < errorList.Count; i++)
        {
            var error = errorList[i];
            var classification = ClassifyError(error);
            message.AppendLine($"  {i + 1}. [{classification}] {error.Message}");
        }

        var aggregateException = new AggregateException(message.ToString(), errorList);

        // Add context to the aggregate exception
        var aggregateContext = CreateErrorContext(aggregateException, operation, backendType);
        aggregateContext.AdditionalContext["ErrorCount"] = errorList.Count;
        aggregateContext.AdditionalContext["ErrorTypes"] = errorList.Select(e => e.GetType().Name).Distinct().ToArray();

        return EnrichException(aggregateException, aggregateContext);
    }

    #region Private Implementation

    private static ErrorClassification ClassifyErrorInternal(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException => ErrorClassification.MemoryExhaustion,
            UnauthorizedAccessException => ErrorClassification.PermissionDenied,
            TimeoutException => ErrorClassification.Timeout,
            ArgumentNullException => ErrorClassification.InvalidInput,
            ArgumentOutOfRangeException => ErrorClassification.InvalidInput,
            ArgumentException => ErrorClassification.InvalidInput,
            InvalidOperationException => ErrorClassification.InvalidConfiguration,
            NotSupportedException => ErrorClassification.FeatureNotSupported,
            NotImplementedException => ErrorClassification.FeatureNotSupported,
            SystemException when exception.Message.Contains("device", StringComparison.OrdinalIgnoreCase) => ErrorClassification.DeviceNotFound,
            SystemException when exception.Message.Contains("busy", StringComparison.OrdinalIgnoreCase) => ErrorClassification.DeviceBusy,
            SystemException when exception.Message.Contains("hardware", StringComparison.OrdinalIgnoreCase) => ErrorClassification.HardwareFailure,
            SystemException when exception.Message.Contains("network", StringComparison.OrdinalIgnoreCase) => ErrorClassification.NetworkError,
            SystemException when exception.Message.Contains("corrupt", StringComparison.OrdinalIgnoreCase) => ErrorClassification.DataCorruption,
            AcceleratorException accelEx => ClassifyAcceleratorException(accelEx),
            AggregateException aggEx => ClassifyAggregateException(aggEx),
            _ => ErrorClassification.Unknown
        };
    }

    private static ErrorClassification ClassifyAcceleratorException(AcceleratorException exception)
    {
        var message = exception.Message.ToLowerInvariant();

        return message switch
        {
            var m when m.Contains("memory") => ErrorClassification.MemoryExhaustion,
            var m when m.Contains("device") && m.Contains("not") && m.Contains("found") => ErrorClassification.DeviceNotFound,
            var m when m.Contains("busy") || m.Contains("in use") => ErrorClassification.DeviceBusy,
            var m when m.Contains("timeout") => ErrorClassification.Timeout,
            var m when m.Contains("hardware") || m.Contains("driver") => ErrorClassification.HardwareFailure,
            var m when m.Contains("invalid") => ErrorClassification.InvalidInput,
            var m when m.Contains("permission") || m.Contains("access") => ErrorClassification.PermissionDenied,
            _ => ErrorClassification.ComputeError
        };
    }

    private static ErrorClassification ClassifyAggregateException(AggregateException exception)
    {
        // Classify based on the most severe inner exception
        var innerClassifications = exception.InnerExceptions.Select(ClassifyError).ToList();

        var priorityOrder = new[]
        {
            ErrorClassification.SystemFailure,
            ErrorClassification.HardwareFailure,
            ErrorClassification.DataCorruption,
            ErrorClassification.SecurityViolation,
            ErrorClassification.MemoryExhaustion,
            ErrorClassification.DeviceNotFound,
            ErrorClassification.ComputeError,
            ErrorClassification.PermissionDenied,
            ErrorClassification.Timeout,
            ErrorClassification.InvalidConfiguration,
            ErrorClassification.InvalidInput,
            ErrorClassification.FeatureNotSupported,
            ErrorClassification.TemporaryFailure,
            ErrorClassification.Unknown
        };

        foreach (var priority in priorityOrder)
        {
            if (innerClassifications.Contains(priority))
            {

                return priority;
            }

        }

        return ErrorClassification.Unknown;
    }

    private static ErrorSeverity DetermineSeverity(Exception exception, ErrorClassification classification)
    {
        return classification switch
        {
            ErrorClassification.SystemFailure => ErrorSeverity.Critical,
            ErrorClassification.HardwareFailure => ErrorSeverity.Critical,
            ErrorClassification.DataCorruption => ErrorSeverity.Critical,
            ErrorClassification.SecurityViolation => ErrorSeverity.Critical,
            ErrorClassification.MemoryExhaustion => ErrorSeverity.High,
            ErrorClassification.DeviceNotFound => ErrorSeverity.High,
            ErrorClassification.ComputeError => ErrorSeverity.High,
            ErrorClassification.PermissionDenied => ErrorSeverity.Medium,
            ErrorClassification.Timeout => ErrorSeverity.Medium,
            ErrorClassification.InvalidConfiguration => ErrorSeverity.Medium,
            ErrorClassification.DeviceBusy => ErrorSeverity.Low,
            ErrorClassification.ResourceContention => ErrorSeverity.Low,
            ErrorClassification.TemporaryFailure => ErrorSeverity.Low,
            ErrorClassification.InvalidInput => ErrorSeverity.Low,
            ErrorClassification.FeatureNotSupported => ErrorSeverity.Low,
            ErrorClassification.NetworkError => ErrorSeverity.Medium,
            _ => ErrorSeverity.Low
        };
    }

    private static string GenerateErrorId(Exception exception)
    {
        var hash = exception.GetType().Name.GetHashCode() ^
                   (exception.Message?.GetHashCode() ?? 0) ^
                   (exception.StackTrace?.GetHashCode() ?? 0);

        return $"ERR_{Math.Abs(hash):X8}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static List<Exception> CollectInnerExceptions(Exception exception)
    {
        var innerExceptions = new List<Exception>();
        var current = exception.InnerException;

        while (current != null)
        {
            innerExceptions.Add(current);
            current = current.InnerException;
        }

        if (exception is AggregateException aggEx)
        {
            innerExceptions.AddRange(aggEx.InnerExceptions);
        }

        return innerExceptions;
    }

    private static void LogDetailedErrorContext(ILogger logger, ErrorContext errorContext)
    {
        var details = new StringBuilder();
        details.AppendLine($"Detailed Error Context for {errorContext.ErrorId}:");
        details.AppendLine($"  Operation: {errorContext.Operation}");
        details.AppendLine($"  Backend: {errorContext.BackendType}");
        details.AppendLine($"  Classification: {errorContext.Classification}");
        details.AppendLine($"  Severity: {errorContext.Severity}");
        details.AppendLine($"  Timestamp: {errorContext.Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");

        if (errorContext.InnerExceptions.Count > 0)
        {
            details.AppendLine($"  Inner Exceptions ({errorContext.InnerExceptions.Count}):");
            for (var i = 0; i < errorContext.InnerExceptions.Count; i++)
            {
                var inner = errorContext.InnerExceptions[i];
                details.AppendLine($"    {i + 1}. {inner.GetType().Name}: {inner.Message}");
            }
        }

        if (errorContext.AdditionalContext.Count > 0)
        {
            details.AppendLine("  Additional Context:");
            foreach (var kvp in errorContext.AdditionalContext)
            {
                details.AppendLine($"    {kvp.Key}: {kvp.Value}");
            }
        }

        logger.LogError(details.ToString());
    }

    private static TimeSpan CalculateRetryDelay(TimeSpan baseDelay, int attemptNumber)
    {
        var multiplier = Math.Pow(2, attemptNumber - 1);
        var delayMs = baseDelay.TotalMilliseconds * multiplier;

        // Add jitter to prevent thundering herd
        var jitter = Random.Shared.NextDouble() * 0.1; // ±10% jitter
        delayMs *= (1.0 + jitter);

        // Cap at reasonable maximum
        var maxDelayMs = Math.Min(delayMs, 30000); // 30 seconds max

        return TimeSpan.FromMilliseconds(maxDelayMs);
    }

    private static Exception EnrichException(Exception exception, ErrorContext context)
    {
        // Add context to exception data without modifying the original exception
        if (exception.Data.IsReadOnly)
        {
            return exception;
        }

        exception.Data["ErrorId"] = context.ErrorId;
        exception.Data["Classification"] = context.Classification.ToString();
        exception.Data["Severity"] = context.Severity.ToString();
        exception.Data["Backend"] = context.BackendType;
        exception.Data["Operation"] = context.Operation;
        exception.Data["Timestamp"] = context.Timestamp.ToString("O");

        return exception;
    }

    #endregion
}

