// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Integration.Components.Enums;
using DotCompute.Backends.CUDA.Integration.Components.Policies;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Integration.Components.ErrorHandling;

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
        _ = Interlocked.Increment(ref _totalErrors);
        _ = Interlocked.Increment(ref _recentErrors);

        var severity = DetermineErrorSeverity(error);
        if (severity == CudaErrorSeverity.Critical)
        {
            _ = Interlocked.Increment(ref _criticalErrors);
        }
        else if (severity != CudaErrorSeverity.None)
        {
            _ = Interlocked.Increment(ref _recoverableErrors);
        }

        lock (_lock)
        {
            _errorCounts[error] = _errorCounts.GetValueOrDefault(error) + 1;
        }
    }

    public void RecordRecoverySuccess()
    {
        _ = Interlocked.Increment(ref _recoveryAttempts);
        _ = Interlocked.Increment(ref _recoverySuccesses);
    }

    public void RecordRecoveryFailure()
    {
        _ = Interlocked.Increment(ref _recoveryAttempts);
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