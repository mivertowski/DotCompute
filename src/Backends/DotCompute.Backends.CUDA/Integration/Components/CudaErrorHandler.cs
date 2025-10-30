// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Integration.Components.Enums;
using DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;
using DotCompute.Backends.CUDA.Integration.Components.Exceptions;
using DotCompute.Backends.CUDA.Integration.Components.Health;
using DotCompute.Backends.CUDA.Integration.Components.Policies;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// CUDA error handling and diagnostic component that provides comprehensive
/// error analysis, recovery strategies, and diagnostic information.
/// </summary>
public sealed partial class CudaErrorHandler : IDisposable
{
    private readonly ILogger<CudaErrorHandler> _logger;
    private readonly CudaContext _context;
    private readonly ErrorRecoveryStrategies _recoveryStrategies;
    private readonly ErrorStatistics _errorStatistics;
    private readonly ConcurrentQueue<CudaErrorRecord> _recentErrors;
    private readonly Timer _diagnosticsTimer;
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaErrorHandler class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

    public CudaErrorHandler(CudaContext context, ILogger<CudaErrorHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _recoveryStrategies = new ErrorRecoveryStrategies(context, logger);
        _errorStatistics = new ErrorStatistics();
        _recentErrors = new ConcurrentQueue<CudaErrorRecord>();

        // Set up periodic diagnostics
        _diagnosticsTimer = new Timer(PerformPeriodicDiagnostics, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

        Log.LogErrorHandlerInitialized(_logger, context.DeviceId);
    }

    /// <summary>
    /// Handles a CUDA error with appropriate logging, analysis, and recovery.
    /// </summary>
    /// <param name="error">CUDA error code.</param>
    /// <param name="operation">Operation that caused the error.</param>
    /// <param name="context">Additional context information.</param>
    /// <returns>Error handling result with recovery actions.</returns>
    public CudaErrorHandlingResult HandleError(
        CudaError error,
        string operation,
        string? context = null)
    {
        ThrowIfDisposed();

        var errorRecord = new CudaErrorRecord
        {
            Error = error,
            Operation = operation,
            Context = context ?? string.Empty,
            Timestamp = DateTimeOffset.UtcNow,
            ThreadId = Environment.CurrentManagedThreadId,
            StackTrace = _errorStatistics.CaptureDetailedDiagnostics ? Environment.StackTrace : null
        };

        // Record error statistics
        _errorStatistics.RecordError(error);
        _recentErrors.Enqueue(errorRecord);

        // Keep only recent errors in memory
        while (_recentErrors.Count > 100)
        {
            _ = _recentErrors.TryDequeue(out _);
        }

        var severity = DetermineErrorSeverity(error);
        var shouldRecover = ShouldAttemptRecovery(error, severity);

        // Log error with appropriate level
        LogError(errorRecord, severity);

        // Attempt recovery if appropriate
        CudaErrorHandlingResult result;
        if (shouldRecover)
        {
            try
            {
                var recoveryResult = _recoveryStrategies.AttemptRecovery(error, operation, context);

                if (recoveryResult.Success)
                {
                    Log.LogRecoverySuccess(_logger, error, operation);
                    _errorStatistics.RecordRecoverySuccess();
                }
                else
                {
                    Log.LogRecoveryFailure(_logger, error, operation, recoveryResult.Message);
                    _errorStatistics.RecordRecoveryFailure();
                }

                result = new CudaErrorHandlingResult
                {
                    OriginalError = error,
                    Severity = severity,
                    ErrorMessage = GetErrorDescription(error),
                    Timestamp = errorRecord.Timestamp,
                    RecoveryAttempted = true,
                    RecoverySuccessful = recoveryResult.Success,
                    RecoveryActions = recoveryResult.ActionsPerformed,
                    RecoveryMessage = recoveryResult.Message
                };
            }
            catch (Exception ex)
            {
                Log.LogRecoveryException(_logger, ex, error, operation);
                _errorStatistics.RecordRecoveryFailure();

                result = new CudaErrorHandlingResult
                {
                    OriginalError = error,
                    Severity = severity,
                    ErrorMessage = GetErrorDescription(error),
                    Timestamp = errorRecord.Timestamp,
                    RecoveryAttempted = true,
                    RecoverySuccessful = false,
                    RecoveryMessage = $"Recovery failed with exception: {ex.Message}"
                };
            }
        }
        else
        {
            result = new CudaErrorHandlingResult
            {
                OriginalError = error,
                Severity = severity,
                ErrorMessage = GetErrorDescription(error),
                Timestamp = errorRecord.Timestamp,
                RecoveryAttempted = false
            };
        }

        return result;
    }

    /// <summary>
    /// Validates CUDA operation results and handles any errors.
    /// </summary>
    /// <param name="result">CUDA operation result.</param>
    /// <param name="operation">Operation description.</param>
    /// <param name="throwOnError">Whether to throw exception on error.</param>
    /// <returns>True if operation was successful, false otherwise.</returns>
    public bool ValidateResult(CudaError result, string operation, bool throwOnError = true)
    {
        ThrowIfDisposed();

        if (result == CudaError.Success)
        {
            return true;
        }

        var handlingResult = HandleError(result, operation);

        if (throwOnError && !handlingResult.RecoverySuccessful)
        {
            throw new CudaException(
                $"CUDA operation '{operation}' failed: {handlingResult.ErrorMessage}",
                result,
                handlingResult);
        }

        return handlingResult.RecoverySuccessful;
    }

    /// <summary>
    /// Gets comprehensive error statistics and diagnostics.
    /// </summary>
    /// <returns>Error statistics and diagnostic information.</returns>
    public CudaErrorDiagnostics GetDiagnostics()
    {
        ThrowIfDisposed();

        var recentErrorsList = _recentErrors.ToArray();
        var deviceStatus = QueryDeviceStatus();

        return new CudaErrorDiagnostics
        {
            TotalErrors = _errorStatistics.TotalErrors,
            CriticalErrors = _errorStatistics.CriticalErrors,
            RecoverableErrors = _errorStatistics.RecoverableErrors,
            RecoverySuccessRate = _errorStatistics.RecoverySuccessRate,
            RecentErrors = recentErrorsList,
            MostCommonErrors = _errorStatistics.GetMostCommonErrors(),
            ErrorTrends = _errorStatistics.GetErrorTrends(),
            DeviceStatus = deviceStatus,
            LastDiagnosticsUpdate = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Performs comprehensive system health check.
    /// </summary>
    /// <returns>System health assessment.</returns>
    public CudaSystemHealthAssessment PerformHealthCheck()
    {
        ThrowIfDisposed();

        var healthChecks = new Dictionary<string, HealthCheckResult>();
        var deviceAccessible = false;
        var memoryHealth = CudaHealthStatus.Critical;
        var errorRateHealth = CudaHealthStatus.Critical;
        var contextHealth = CudaHealthStatus.Critical;
        CudaHealthStatus overallHealth;

        try
        {
            // Check device accessibility
            var deviceCheck = CheckDeviceAccessibility();
            deviceAccessible = deviceCheck.Success;
            healthChecks.Add("Device Accessibility", deviceCheck);

            // Check memory status
            var memoryCheck = CheckMemoryStatus();
            memoryHealth = memoryCheck.Status;
            healthChecks.Add("Memory Status", memoryCheck);

            // Check error rates
            var errorCheck = CheckErrorRates();
            errorRateHealth = errorCheck.Status;
            healthChecks.Add("Error Rates", errorCheck);

            // Check context status
            var contextCheck = CheckContextStatus();
            contextHealth = contextCheck.Status;
            healthChecks.Add("Context Status", contextCheck);

            // Determine overall health
            overallHealth = DetermineOverallHealth(healthChecks.Values);

            Log.LogHealthCheckCompleted(_logger, overallHealth);
        }
        catch (Exception ex)
        {
            Log.LogHealthCheckError(_logger, ex);
            overallHealth = CudaHealthStatus.Critical;
            healthChecks.Add("Health Check Error", new HealthCheckResult
            {
                Success = false,
                Status = CudaHealthStatus.Critical,
                Message = ex.Message
            });
        }

        return new CudaSystemHealthAssessment
        {
            Timestamp = DateTimeOffset.UtcNow,
            OverallHealth = overallHealth,
            DeviceAccessible = deviceAccessible,
            MemoryHealth = memoryHealth,
            ErrorRateHealth = errorRateHealth,
            ContextHealth = contextHealth,
            HealthChecks = healthChecks
        };
    }

    /// <summary>
    /// Configures error handling policies and thresholds.
    /// </summary>
    /// <param name="policies">Error handling policies.</param>
    public void ConfigurePolicies(CudaErrorHandlingPolicies policies)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(policies);

        _errorStatistics.Configure(policies.StatisticsPolicy);
        ErrorRecoveryStrategies.Configure(policies.RecoveryPolicy);

        Log.LogPoliciesConfigured(_logger);
    }

    #region Private Methods

    private static CudaErrorSeverity DetermineErrorSeverity(CudaError error)
    {
        return error switch
        {
            CudaError.Success => CudaErrorSeverity.None,

            // Critical errors that usually require restart
            CudaError.InvalidDevice or
            CudaError.InvalidContext or
            CudaError.ContextIsDestroyed or
            CudaError.SystemDriverMismatch or
            CudaError.SystemNotReady => CudaErrorSeverity.Critical,

            // High severity errors that may be recoverable
            CudaError.OutOfMemory or
            CudaError.LaunchOutOfResources or
            CudaError.TooManyPeers or
            CudaError.HostMemoryAlreadyRegistered => CudaErrorSeverity.High,

            // Medium severity errors that are often recoverable
            CudaError.InvalidValue or
            CudaError.InvalidResourceHandle or
            CudaError.NotReady or
            CudaError.TimeoutExpired => CudaErrorSeverity.Medium,

            // Low severity errors that are usually recoverable
            CudaError.NotSupported or
            CudaError.InsufficientDriver or
            CudaError.MissingConfiguration => CudaErrorSeverity.Low,

            _ => CudaErrorSeverity.Unknown
        };
    }

    private bool ShouldAttemptRecovery(CudaError error, CudaErrorSeverity severity)
    {
        // Don't attempt recovery for critical errors or if too many recent failures
        if (severity == CudaErrorSeverity.Critical)
        {
            return false;
        }

        // Check recent recovery failure rate
        if (_errorStatistics.RecentRecoveryFailureRate > 0.8)
        {
            return false;
        }

        return severity switch
        {
            CudaErrorSeverity.High => true,
            CudaErrorSeverity.Medium => true,
            CudaErrorSeverity.Low => true,
            _ => false
        };
    }

    private void LogError(CudaErrorRecord errorRecord, CudaErrorSeverity severity)
    {
        switch (severity)
        {
            case CudaErrorSeverity.Critical:
                Log.LogCriticalError(_logger, errorRecord.Error, errorRecord.Operation);
                break;
            case CudaErrorSeverity.High:
                Log.LogErrorError(_logger, errorRecord.Error, errorRecord.Operation);
                break;
            case CudaErrorSeverity.Medium:
                Log.LogWarningError(_logger, errorRecord.Error, errorRecord.Operation);
                break;
            case CudaErrorSeverity.Low:
                Log.LogInformationError(_logger, errorRecord.Error, errorRecord.Operation);
                break;
            default:
                Log.LogDebugError(_logger, errorRecord.Error, errorRecord.Operation);
                break;
        }
    }

    private static string GetErrorDescription(CudaError error)
    {
        return error switch
        {
            CudaError.Success => "No error",
            CudaError.InvalidValue => "Invalid value or parameter",
            CudaError.OutOfMemory => "Out of memory",
            CudaError.NotInitialized => "CUDA not initialized",
            CudaError.Deinitialized => "CUDA deinitialized",
            CudaError.InvalidDevice => "Invalid device ordinal",
            CudaError.InvalidContext => "Invalid context",
            CudaError.ContextIsDestroyed => "Context is destroyed",
            CudaError.LaunchOutOfResources => "Launch out of resources",
            CudaError.SystemDriverMismatch => "System driver mismatch",
            CudaError.SystemNotReady => "System not ready",
            _ => $"CUDA error {(int)error}: {error}"
        };
    }

    private CudaDeviceStatus QueryDeviceStatus()
    {
        try
        {
            var (freeMemory, totalMemory) = QueryMemoryInfo();

            return new CudaDeviceStatus
            {
                DeviceId = _context.DeviceId,
                IsAccessible = IsDeviceAccessible(),
                TotalMemory = totalMemory,
                FreeMemory = freeMemory,
                MemoryUtilization = totalMemory > 0 ? (double)(totalMemory - freeMemory) / totalMemory : 0.0,
                LastQueried = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            Log.LogDeviceStatusQueryFailed(_logger, ex);
            return new CudaDeviceStatus
            {
                DeviceId = _context.DeviceId,
                IsAccessible = false,
                LastQueried = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    private (long freeMemory, long totalMemory) QueryMemoryInfo()
    {
        var result = CudaRuntime.cudaSetDevice(_context.DeviceId);
        if (result != CudaError.Success)
        {
            return (0, 0);
        }

        result = CudaRuntime.cudaMemGetInfo(out var free, out var total);
        return result == CudaError.Success ? ((long)free, (long)total) : (0, 0);
    }

    private bool IsDeviceAccessible()
    {
        try
        {
            var result = CudaRuntime.cudaSetDevice(_context.DeviceId);
            return result == CudaError.Success;
        }
        catch
        {
            return false;
        }
    }

    private HealthCheckResult CheckDeviceAccessibility()
    {
        try
        {
            var accessible = IsDeviceAccessible();
            return new HealthCheckResult
            {
                Success = accessible,
                Status = accessible ? CudaHealthStatus.Healthy : CudaHealthStatus.Critical,
                Message = accessible ? "Device accessible" : "Device not accessible"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Success = false,
                Status = CudaHealthStatus.Critical,
                Message = $"Device accessibility check failed: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckMemoryStatus()
    {
        try
        {
            var (freeMemory, totalMemory) = QueryMemoryInfo();

            if (totalMemory == 0)
            {
                return new HealthCheckResult
                {
                    Success = false,
                    Status = CudaHealthStatus.Critical,
                    Message = "Cannot query memory information"
                };
            }

            var utilizationRatio = (double)(totalMemory - freeMemory) / totalMemory;
            var status = utilizationRatio switch
            {
                < 0.8 => CudaHealthStatus.Healthy,
                < 0.95 => CudaHealthStatus.Warning,
                _ => CudaHealthStatus.Critical
            };

            return new HealthCheckResult
            {
                Success = true,
                Status = status,
                Message = $"Memory utilization: {utilizationRatio:P1} ({totalMemory - freeMemory:N0}/{totalMemory:N0} bytes)"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Success = false,
                Status = CudaHealthStatus.Critical,
                Message = $"Memory status check failed: {ex.Message}"
            };
        }
    }

    private HealthCheckResult CheckErrorRates()
    {
        var errorRate = _errorStatistics.RecentErrorRate;
        var status = errorRate switch
        {
            < 0.01 => CudaHealthStatus.Healthy,    // Less than 1% error rate
            < 0.05 => CudaHealthStatus.Warning,    // Less than 5% error rate
            _ => CudaHealthStatus.Critical         // 5% or higher error rate
        };

        return new HealthCheckResult
        {
            Success = status != CudaHealthStatus.Critical,
            Status = status,
            Message = $"Error rate: {errorRate:P2} ({_errorStatistics.RecentErrors}/{_errorStatistics.RecentOperations} operations)"
        };
    }

    private HealthCheckResult CheckContextStatus()
    {
        try
        {
            _context.MakeCurrent();
            return new HealthCheckResult
            {
                Success = true,
                Status = CudaHealthStatus.Healthy,
                Message = "Context is valid and current"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Success = false,
                Status = CudaHealthStatus.Critical,
                Message = $"Context check failed: {ex.Message}"
            };
        }
    }

    private static CudaHealthStatus DetermineOverallHealth(IEnumerable<HealthCheckResult> checks)
    {
        var results = checks.ToArray();

        if (results.Any(c => c.Status == CudaHealthStatus.Critical))
        {
            return CudaHealthStatus.Critical;
        }

        if (results.Any(c => c.Status == CudaHealthStatus.Warning))
        {
            return CudaHealthStatus.Warning;
        }

        return CudaHealthStatus.Healthy;
    }

    private void PerformPeriodicDiagnostics(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var diagnostics = GetDiagnostics();

            if (diagnostics.CriticalErrors > 0)
            {
                Log.LogPeriodicDiagnosticsCriticalErrors(_logger, (int)diagnostics.CriticalErrors);
            }

            if (diagnostics.RecoverySuccessRate < 0.8 && diagnostics.TotalErrors > 10)
            {
                Log.LogPeriodicDiagnosticsLowRecoveryRate(_logger, diagnostics.RecoverySuccessRate);
            }
        }
        catch (Exception ex)
        {
            Log.LogPeriodicDiagnosticsError(_logger, ex);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _diagnosticsTimer?.Dispose();
            _recoveryStrategies?.Dispose();
            Log.LogErrorHandlerDisposed(_logger);
        }
    }
}
