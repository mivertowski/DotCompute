// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.ErrorHandling.Exceptions;
using DotCompute.Backends.Metal.Telemetry;

namespace DotCompute.Backends.Metal.ErrorHandling;

/// <summary>
/// Provides error recovery strategies and hints for Metal operations.
/// </summary>
public sealed class MetalErrorRecovery
{
    private readonly ILogger _logger;
    private readonly MetalTelemetryManager? _telemetry;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalErrorRecovery"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="telemetry">Optional telemetry manager.</param>
    public MetalErrorRecovery(ILogger logger, MetalTelemetryManager? telemetry = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry;
    }

    /// <summary>
    /// Analyzes an exception and provides recovery hints.
    /// </summary>
    /// <param name="exception">The exception to analyze.</param>
    /// <returns>Recovery hints and suggested actions.</returns>
    public RecoveryHints AnalyzeException(Exception exception)
    {
        var hints = exception switch
        {
            MetalCompilationException compilationEx => AnalyzeCompilationError(compilationEx),
            Exceptions.MetalDeviceException deviceEx => AnalyzeDeviceError(deviceEx),
            Exceptions.MetalOperationException operationEx => AnalyzeOperationError(operationEx),
            CpuFallbackRequiredException fallbackEx => AnalyzeFallbackRequired(fallbackEx),
            _ => CreateGenericHints(exception)
        };

        // Record telemetry
        _telemetry?.RecordErrorEvent(
            DetermineErrorCode(exception),
            exception.GetType().Name,
            new Dictionary<string, object>
            {
                ["exception_type"] = exception.GetType().Name,
                ["recoverable"] = hints.IsRecoverable,
                ["fallback_available"] = hints.FallbackAvailable
            });

        _logger.LogDebug(
            "Error analysis: Type={Type}, Recoverable={Recoverable}, Fallback={Fallback}",
            exception.GetType().Name, hints.IsRecoverable, hints.FallbackAvailable);

        return hints;
    }

    /// <summary>
    /// Attempts to recover from an error using the provided hints.
    /// </summary>
    /// <param name="exception">The exception to recover from.</param>
    /// <param name="hints">The recovery hints.</param>
    /// <param name="recoveryAction">The recovery action to execute.</param>
    /// <returns>True if recovery was successful; otherwise, false.</returns>
    public bool TryRecover(Exception exception, RecoveryHints hints, Func<bool> recoveryAction)
    {
        if (!hints.IsRecoverable)
        {
            _logger.LogWarning("Error is not recoverable: {Message}", exception.Message);
            return false;
        }

        try
        {
            _logger.LogInformation("Attempting recovery: {Strategy}", hints.RecoveryStrategy);
            var success = recoveryAction();

            if (success)
            {
                _logger.LogInformation("Recovery successful for {ExceptionType}", exception.GetType().Name);
                _telemetry?.RecordKernelExecution("error_recovery", TimeSpan.FromMilliseconds(1), 0, true);
            }
            else
            {
                _logger.LogWarning("Recovery attempt failed for {ExceptionType}", exception.GetType().Name);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recovery attempt threw exception");
            return false;
        }
    }

    private RecoveryHints AnalyzeCompilationError(MetalCompilationException exception)
    {
        var hints = new RecoveryHints
        {
            ErrorCategory = ErrorCategory.Compilation,
            IsRecoverable = true,
            FallbackAvailable = true,
            RecoveryStrategy = RecoveryStrategy.UseCpuFallback,
            SuggestedActions =
            [
                "Verify Metal Shading Language syntax",
                "Check for unsupported features on target GPU family",
                "Use CPU fallback for unsupported operations",
                "Review compiler output for specific error details"
            ]
        };

        if (exception.CompilerOutput?.Contains("unsupported", StringComparison.OrdinalIgnoreCase) == true)
        {
            hints.SuggestedActions.Add("This feature may not be supported on the current GPU family");
            hints.SuggestedActions.Add("Consider using a different algorithm or CPU fallback");
        }

        return hints;
    }

    private RecoveryHints AnalyzeDeviceError(Exceptions.MetalDeviceException exception)
    {
        var hints = new RecoveryHints
        {
            ErrorCategory = ErrorCategory.Device,
            IsRecoverable = false,
            FallbackAvailable = true,
            RecoveryStrategy = RecoveryStrategy.UseCpuFallback,
            SuggestedActions =
            [
                "Verify Metal device is available and not in use",
                "Check if GPU is overheating or experiencing hardware issues",
                "Restart application to reinitialize Metal device",
                "Use CPU backend as fallback"
            ]
        };

        return hints;
    }

    private RecoveryHints AnalyzeOperationError(Exceptions.MetalOperationException exception)
    {
        var hints = new RecoveryHints
        {
            ErrorCategory = ErrorCategory.Operation,
            IsRecoverable = true,
            FallbackAvailable = true,
            RecoveryStrategy = RecoveryStrategy.RetryOperation,
            SuggestedActions =
            [
                "Retry the operation with backoff",
                "Check memory availability and reduce allocation size",
                "Synchronize pending operations before retrying",
                "Use CPU fallback if retries fail"
            ]
        };

        if (exception.Message.Contains("memory", StringComparison.OrdinalIgnoreCase))
        {
            hints.SuggestedActions.Insert(0, "Reduce memory allocation size or free unused buffers");
        }

        return hints;
    }

    private RecoveryHints AnalyzeFallbackRequired(CpuFallbackRequiredException exception)
    {
        var hints = new RecoveryHints
        {
            ErrorCategory = ErrorCategory.UnsupportedFeature,
            IsRecoverable = true,
            FallbackAvailable = true,
            RecoveryStrategy = RecoveryStrategy.UseCpuFallback,
            SuggestedActions =
            [
                $"Metal backend does not support this operation: {exception.Reason}",
                "Automatically falling back to CPU backend",
                "Consider using a different algorithm that is GPU-compatible"
            ]
        };

        return hints;
    }

    private RecoveryHints CreateGenericHints(Exception exception)
    {
        return new RecoveryHints
        {
            ErrorCategory = ErrorCategory.Unknown,
            IsRecoverable = false,
            FallbackAvailable = true,
            RecoveryStrategy = RecoveryStrategy.UseCpuFallback,
            SuggestedActions =
            [
                "An unexpected error occurred",
                "Check application logs for details",
                "Consider using CPU fallback",
                "Report this issue if it persists"
            ]
        };
    }

    private Execution.MetalError DetermineErrorCode(Exception exception)
    {
        return exception switch
        {
            MetalCompilationException => Execution.MetalError.CompilationError,
            Exceptions.MetalDeviceException => Execution.MetalError.DeviceNotFound,
            Exceptions.MetalOperationException => Execution.MetalError.InvalidOperation,
            CpuFallbackRequiredException => Execution.MetalError.UnsupportedFeature,
            _ => Execution.MetalError.Unknown
        };
    }
}

/// <summary>
/// Recovery hints for error handling.
/// </summary>
public sealed class RecoveryHints
{
    /// <summary>Gets or sets the error category.</summary>
    public ErrorCategory ErrorCategory { get; set; }

    /// <summary>Gets or sets whether the error is recoverable.</summary>
    public bool IsRecoverable { get; set; }

    /// <summary>Gets or sets whether CPU fallback is available.</summary>
    public bool FallbackAvailable { get; set; }

    /// <summary>Gets or sets the recovery strategy.</summary>
    public RecoveryStrategy RecoveryStrategy { get; set; }

    /// <summary>Gets or sets suggested actions.</summary>
    public List<string> SuggestedActions { get; set; } = [];
}

/// <summary>
/// Error category enumeration.
/// </summary>
public enum ErrorCategory
{
    /// <summary>Unknown error category.</summary>
    Unknown,
    /// <summary>Compilation error.</summary>
    Compilation,
    /// <summary>Device error.</summary>
    Device,
    /// <summary>Operation error.</summary>
    Operation,
    /// <summary>Unsupported feature.</summary>
    UnsupportedFeature
}

/// <summary>
/// Recovery strategy enumeration.
/// </summary>
public enum RecoveryStrategy
{
    /// <summary>No recovery possible.</summary>
    None,
    /// <summary>Retry the operation.</summary>
    RetryOperation,
    /// <summary>Use CPU fallback.</summary>
    UseCpuFallback,
    /// <summary>Reinitialize device.</summary>
    ReinitializeDevice
}
