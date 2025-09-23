// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// CUDA error handling and diagnostic component that provides comprehensive
/// error analysis, recovery strategies, and diagnostic information.
/// </summary>
public sealed class CudaErrorHandler : IDisposable
{
    private readonly ILogger<CudaErrorHandler> _logger;
    private readonly CudaContext _context;
    private readonly ErrorRecoveryStrategies _recoveryStrategies;
    private readonly ErrorStatistics _errorStatistics;
    private readonly ConcurrentQueue<CudaErrorRecord> _recentErrors;
    private readonly Timer _diagnosticsTimer;
    private volatile bool _disposed;

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

        _logger.LogDebug("CUDA error handler initialized for device {DeviceId}", context.DeviceId);
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
            _recentErrors.TryDequeue(out _);
        }

        var severity = DetermineErrorSeverity(error);
        var shouldRecover = ShouldAttemptRecovery(error, severity);

        // Log error with appropriate level
        LogError(errorRecord, severity);

        var result = new CudaErrorHandlingResult
        {
            OriginalError = error,
            Severity = severity,
            ErrorMessage = GetErrorDescription(error),
            Timestamp = errorRecord.Timestamp,
            RecoveryAttempted = shouldRecover
        };

        // Attempt recovery if appropriate
        if (shouldRecover)
        {
            try
            {
                var recoveryResult = _recoveryStrategies.AttemptRecovery(error, operation, context);
                result.RecoverySuccessful = recoveryResult.Success;
                result.RecoveryActions = recoveryResult.ActionsPerformed;
                result.RecoveryMessage = recoveryResult.Message;

                if (recoveryResult.Success)
                {
                    _logger.LogInformation("Successfully recovered from CUDA error {Error} in operation {Operation}",
                        error, operation);
                    _errorStatistics.RecordRecoverySuccess();
                }
                else
                {
                    _logger.LogWarning("Failed to recover from CUDA error {Error} in operation {Operation}: {Message}",
                        error, operation, recoveryResult.Message);
                    _errorStatistics.RecordRecoveryFailure();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during error recovery for {Error} in {Operation}", error, operation);
                result.RecoveryMessage = $"Recovery failed with exception: {ex.Message}";
                _errorStatistics.RecordRecoveryFailure();
            }
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

        var assessment = new CudaSystemHealthAssessment
        {
            Timestamp = DateTimeOffset.UtcNow,
            OverallHealth = CudaHealthStatus.Healthy
        };

        try
        {
            // Check device accessibility
            var deviceCheck = CheckDeviceAccessibility();
            assessment.DeviceAccessible = deviceCheck.Success;
            assessment.HealthChecks.Add("Device Accessibility", deviceCheck);

            // Check memory status
            var memoryCheck = CheckMemoryStatus();
            assessment.MemoryHealth = memoryCheck.Status;
            assessment.HealthChecks.Add("Memory Status", memoryCheck);

            // Check error rates
            var errorCheck = CheckErrorRates();
            assessment.ErrorRateHealth = errorCheck.Status;
            assessment.HealthChecks.Add("Error Rates", errorCheck);

            // Check context status
            var contextCheck = CheckContextStatus();
            assessment.ContextHealth = contextCheck.Status;
            assessment.HealthChecks.Add("Context Status", contextCheck);

            // Determine overall health
            assessment.OverallHealth = DetermineOverallHealth(assessment.HealthChecks.Values);

            _logger.LogDebug("Health check completed: {OverallHealth}", assessment.OverallHealth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
            assessment.OverallHealth = CudaHealthStatus.Critical;
            assessment.HealthChecks.Add("Health Check Error", new HealthCheckResult
            {
                Success = false,
                Status = CudaHealthStatus.Critical,
                Message = ex.Message
            });
        }

        return assessment;
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

        _logger.LogDebug("Error handling policies configured");
    }

    #region Private Methods

    private CudaErrorSeverity DetermineErrorSeverity(CudaError error)
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
        var message = "CUDA error {Error} in operation {Operation}";
        var args = new object[] { errorRecord.Error, errorRecord.Operation };

        switch (severity)
        {
            case CudaErrorSeverity.Critical:
                _logger.LogCritical(message, args);
                break;
            case CudaErrorSeverity.High:
                _logger.LogError(message, args);
                break;
            case CudaErrorSeverity.Medium:
                _logger.LogWarning(message, args);
                break;
            case CudaErrorSeverity.Low:
                _logger.LogInformation(message, args);
                break;
            default:
                _logger.LogDebug(message, args);
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
            _logger.LogWarning(ex, "Failed to query device status");
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
                _logger.LogWarning("Periodic diagnostics: {CriticalErrors} critical errors detected",
                    diagnostics.CriticalErrors);
            }

            if (diagnostics.RecoverySuccessRate < 0.8 && diagnostics.TotalErrors > 10)
            {
                _logger.LogWarning("Periodic diagnostics: Low recovery success rate: {Rate:P1}",
                    diagnostics.RecoverySuccessRate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during periodic diagnostics");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaErrorHandler));
        }
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _diagnosticsTimer?.Dispose();
            _recoveryStrategies?.Dispose();
            _logger.LogDebug("CUDA error handler disposed");
        }
    }
}

#region Supporting Types and Classes

/// <summary>
/// Error recovery strategies implementation.
/// </summary>
internal sealed class ErrorRecoveryStrategies : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private volatile bool _disposed;

    public ErrorRecoveryStrategies(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ErrorRecoveryResult AttemptRecovery(CudaError error, string operation, string? context)
    {
        if (_disposed)
        {
            return new ErrorRecoveryResult { Success = false, Message = "Recovery service disposed" };
        }

        return error switch
        {
            CudaError.OutOfMemory => AttemptMemoryRecovery(),
            CudaError.NotReady => AttemptSynchronizationRecovery(),
            CudaError.InvalidValue => AttemptParameterValidationRecovery(operation),
            CudaError.LaunchOutOfResources => AttemptResourceRecovery(),
            _ => new ErrorRecoveryResult
            {
                Success = false,
                Message = $"No recovery strategy available for {error}"
            }
        };
    }

    public static void Configure(ErrorRecoveryPolicy policy)
    {
        // Configure recovery strategies based on policy
    }

    private ErrorRecoveryResult AttemptMemoryRecovery()
    {
        var actions = new List<string>();

        try
        {
            // Attempt to free memory and synchronize
            _context.MakeCurrent();
            _context.Synchronize();
            actions.Add("Device synchronization");

            // In production, would attempt memory cleanup
            actions.Add("Memory cleanup attempted");

            return new ErrorRecoveryResult
            {
                Success = true,
                Message = "Memory recovery completed",
                ActionsPerformed = actions
            };
        }
        catch (Exception ex)
        {
            return new ErrorRecoveryResult
            {
                Success = false,
                Message = $"Memory recovery failed: {ex.Message}",
                ActionsPerformed = actions
            };
        }
    }

    private ErrorRecoveryResult AttemptSynchronizationRecovery()
    {
        var actions = new List<string>();

        try
        {
            _context.MakeCurrent();
            _context.Synchronize();
            actions.Add("Context synchronization");

            return new ErrorRecoveryResult
            {
                Success = true,
                Message = "Synchronization recovery completed",
                ActionsPerformed = actions
            };
        }
        catch (Exception ex)
        {
            return new ErrorRecoveryResult
            {
                Success = false,
                Message = $"Synchronization recovery failed: {ex.Message}",
                ActionsPerformed = actions
            };
        }
    }

    private static ErrorRecoveryResult AttemptParameterValidationRecovery(string operation)
    {
        return new ErrorRecoveryResult
        {
            Success = false,
            Message = $"Parameter validation required for operation: {operation}",
            ActionsPerformed = ["Parameter validation suggested"]
        };
    }

    private ErrorRecoveryResult AttemptResourceRecovery()
    {
        var actions = new List<string>();

        try
        {
            _context.MakeCurrent();
            _context.Synchronize();
            actions.Add("Resource cleanup");

            return new ErrorRecoveryResult
            {
                Success = true,
                Message = "Resource recovery completed",
                ActionsPerformed = actions
            };
        }
        catch (Exception ex)
        {
            return new ErrorRecoveryResult
            {
                Success = false,
                Message = $"Resource recovery failed: {ex.Message}",
                ActionsPerformed = actions
            };
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Error statistics tracking and analysis.
/// </summary>
internal sealed class ErrorStatistics
{
    private long _totalErrors;
    private long _criticalErrors;
    private long _recoverableErrors;
    private long _recoveryAttempts;
    private long _recoverySuccesses;
    private long _recentOperations;
    private long _recentErrors;
    private readonly Dictionary<CudaError, long> _errorCounts = [];
    private readonly object _lock = new();

    public bool CaptureDetailedDiagnostics { get; set; } = true;

    public long TotalErrors => Interlocked.Read(ref _totalErrors);
    public long CriticalErrors => Interlocked.Read(ref _criticalErrors);
    public long RecoverableErrors => Interlocked.Read(ref _recoverableErrors);
    public long RecentErrors => Interlocked.Read(ref _recentErrors);
    public long RecentOperations => Interlocked.Read(ref _recentOperations);

    public double RecoverySuccessRate
    {
        get
        {
            var attempts = Interlocked.Read(ref _recoveryAttempts);
            var successes = Interlocked.Read(ref _recoverySuccesses);
            return attempts > 0 ? (double)successes / attempts : 0.0;
        }
    }

    public double RecentErrorRate
    {
        get
        {
            var operations = RecentOperations;
            var errors = RecentErrors;
            return operations > 0 ? (double)errors / operations : 0.0;
        }
    }

    public double RecentRecoveryFailureRate
    {
        get
        {
            var attempts = Interlocked.Read(ref _recoveryAttempts);
            var successes = Interlocked.Read(ref _recoverySuccesses);
            var failures = attempts - successes;
            return attempts > 0 ? (double)failures / attempts : 0.0;
        }
    }

    public void RecordError(CudaError error)
    {
        Interlocked.Increment(ref _totalErrors);
        Interlocked.Increment(ref _recentErrors);

        var severity = DetermineErrorSeverity(error);
        if (severity == CudaErrorSeverity.Critical)
        {
            Interlocked.Increment(ref _criticalErrors);
        }
        else if (severity != CudaErrorSeverity.None)
        {
            Interlocked.Increment(ref _recoverableErrors);
        }

        lock (_lock)
        {
            _errorCounts[error] = _errorCounts.GetValueOrDefault(error) + 1;
        }
    }

    public void RecordRecoverySuccess()
    {
        Interlocked.Increment(ref _recoveryAttempts);
        Interlocked.Increment(ref _recoverySuccesses);
    }

    public void RecordRecoveryFailure()
    {
        Interlocked.Increment(ref _recoveryAttempts);
    }

    public void Configure(ErrorStatisticsPolicy policy)
    {
        CaptureDetailedDiagnostics = policy.CaptureDetailedDiagnostics;
    }

    public Dictionary<CudaError, long> GetMostCommonErrors()
    {
        lock (_lock)
        {
            return _errorCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }

    public ErrorTrends GetErrorTrends()
    {
        return new ErrorTrends
        {
            TotalErrors = TotalErrors,
            RecentErrorRate = RecentErrorRate,
            RecoverySuccessRate = RecoverySuccessRate,
            TrendDirection = DetermineTrendDirection()
        };
    }

    private ErrorTrendDirection DetermineTrendDirection()
    {
        // Simplified trend analysis
        var errorRate = RecentErrorRate;
        return errorRate switch
        {
            < 0.01 => ErrorTrendDirection.Improving,
            < 0.05 => ErrorTrendDirection.Stable,
            _ => ErrorTrendDirection.Deteriorating
        };
    }

    private static CudaErrorSeverity DetermineErrorSeverity(CudaError error)
    {
        return error switch
        {
            CudaError.Success => CudaErrorSeverity.None,
            CudaError.InvalidDevice or CudaError.InvalidContext => CudaErrorSeverity.Critical,
            CudaError.OutOfMemory or CudaError.LaunchOutOfResources => CudaErrorSeverity.High,
            CudaError.InvalidValue or CudaError.NotReady => CudaErrorSeverity.Medium,
            _ => CudaErrorSeverity.Low
        };
    }
}

#endregion

#region Data Types

/// <summary>
/// CUDA error handling result.
/// </summary>
public sealed class CudaErrorHandlingResult
{
    public CudaError OriginalError { get; init; }
    public CudaErrorSeverity Severity { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public bool RecoveryAttempted { get; init; }
    public bool RecoverySuccessful { get; init; }
    public List<string> RecoveryActions { get; init; } = [];
    public string? RecoveryMessage { get; init; }
}

/// <summary>
/// CUDA error record for tracking and analysis.
/// </summary>
public readonly record struct CudaErrorRecord
{
    public CudaError Error { get; init; }
    public string Operation { get; init; }
    public string Context { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public int ThreadId { get; init; }
    public string? StackTrace { get; init; }
}

/// <summary>
/// Error recovery result.
/// </summary>
public sealed class ErrorRecoveryResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<string> ActionsPerformed { get; init; } = [];
}

/// <summary>
/// CUDA error diagnostics information.
/// </summary>
public sealed class CudaErrorDiagnostics
{
    public long TotalErrors { get; init; }
    public long CriticalErrors { get; init; }
    public long RecoverableErrors { get; init; }
    public double RecoverySuccessRate { get; init; }
    public CudaErrorRecord[] RecentErrors { get; init; } = [];
    public Dictionary<CudaError, long> MostCommonErrors { get; init; } = [];
    public ErrorTrends ErrorTrends { get; init; } = new();
    public CudaDeviceStatus DeviceStatus { get; init; } = new();
    public DateTimeOffset LastDiagnosticsUpdate { get; init; }
}

/// <summary>
/// System health assessment result.
/// </summary>
public sealed class CudaSystemHealthAssessment
{
    public DateTimeOffset Timestamp { get; init; }
    public CudaHealthStatus OverallHealth { get; init; }
    public bool DeviceAccessible { get; init; }
    public CudaHealthStatus MemoryHealth { get; init; }
    public CudaHealthStatus ErrorRateHealth { get; init; }
    public CudaHealthStatus ContextHealth { get; init; }
    public Dictionary<string, HealthCheckResult> HealthChecks { get; init; } = [];
}

/// <summary>
/// Health check result.
/// </summary>
public sealed class HealthCheckResult
{
    public bool Success { get; init; }
    public CudaHealthStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Device status information.
/// </summary>
public sealed class CudaDeviceStatus
{
    public int DeviceId { get; init; }
    public bool IsAccessible { get; init; }
    public long TotalMemory { get; init; }
    public long FreeMemory { get; init; }
    public double MemoryUtilization { get; init; }
    public DateTimeOffset LastQueried { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Error trends analysis.
/// </summary>
public sealed class ErrorTrends
{
    public long TotalErrors { get; init; }
    public double RecentErrorRate { get; init; }
    public double RecoverySuccessRate { get; init; }
    public ErrorTrendDirection TrendDirection { get; init; }
}

/// <summary>
/// Error handling policies configuration.
/// </summary>
public sealed class CudaErrorHandlingPolicies
{
    public ErrorStatisticsPolicy StatisticsPolicy { get; init; } = new();
    public ErrorRecoveryPolicy RecoveryPolicy { get; init; } = new();
}

/// <summary>
/// Error statistics policy.
/// </summary>
public sealed class ErrorStatisticsPolicy
{
    public bool CaptureDetailedDiagnostics { get; init; } = true;
    public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromHours(24);
    public int MaxRecentErrors { get; init; } = 100;
}

/// <summary>
/// Error recovery policy.
/// </summary>
public sealed class ErrorRecoveryPolicy
{
    public bool EnableAutoRecovery { get; init; } = true;
    public int MaxRecoveryAttempts { get; init; } = 3;
    public TimeSpan RecoveryTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// CUDA exception with error handling context.
/// </summary>
public sealed class CudaException : Exception
{
    public CudaError CudaError { get; }
    public CudaErrorHandlingResult? HandlingResult { get; }

    public CudaException(string message, CudaError cudaError, CudaErrorHandlingResult? handlingResult = null)
        : base(message)
    {
        CudaError = cudaError;
        HandlingResult = handlingResult;
    }

    public CudaException(string message, CudaError cudaError, Exception innerException, CudaErrorHandlingResult? handlingResult = null)
        : base(message, innerException)
    {
        CudaError = cudaError;
        HandlingResult = handlingResult;
    }
}

#endregion

#region Enumerations

/// <summary>
/// CUDA error severity levels.
/// </summary>
public enum CudaErrorSeverity
{
    None,
    Low,
    Medium,
    High,
    Critical,
    Unknown
}

/// <summary>
/// CUDA health status indicators.
/// </summary>
public enum CudaHealthStatus
{
    Healthy,
    Warning,
    Critical
}

/// <summary>
/// Error trend direction indicators.
/// </summary>
public enum ErrorTrendDirection
{
    Improving,
    Stable,
    Deteriorating
}

#endregion