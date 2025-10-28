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
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649 // ref type
    private long _recentOperations;
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore CS0649

    private long _recentErrors;
    private readonly Dictionary<CudaError, long> _errorCounts = [];
    private readonly object _lock = new();
    /// <summary>
    /// Gets or sets the capture detailed diagnostics.
    /// </summary>
    /// <value>The capture detailed diagnostics.</value>

    public bool CaptureDetailedDiagnostics { get; set; } = true;
    /// <summary>
    /// Gets or sets the total errors.
    /// </summary>
    /// <value>The total errors.</value>

    public long TotalErrors => Interlocked.Read(ref _totalErrors);
    /// <summary>
    /// Gets or sets the critical errors.
    /// </summary>
    /// <value>The critical errors.</value>
    public long CriticalErrors => Interlocked.Read(ref _criticalErrors);
    /// <summary>
    /// Gets or sets the recoverable errors.
    /// </summary>
    /// <value>The recoverable errors.</value>
    public long RecoverableErrors => Interlocked.Read(ref _recoverableErrors);
    /// <summary>
    /// Gets or sets the recent errors.
    /// </summary>
    /// <value>The recent errors.</value>
    public long RecentErrors => Interlocked.Read(ref _recentErrors);
    /// <summary>
    /// Gets or sets the recent operations.
    /// </summary>
    /// <value>The recent operations.</value>
    public long RecentOperations => Interlocked.Read(ref _recentOperations);
    /// <summary>
    /// Gets or sets the recovery success rate.
    /// </summary>
    /// <value>The recovery success rate.</value>

    public double RecoverySuccessRate
    {
        get
        {
            var attempts = Interlocked.Read(ref _recoveryAttempts);
            var successes = Interlocked.Read(ref _recoverySuccesses);
            return attempts > 0 ? (double)successes / attempts : 0.0;
        }
    }
    /// <summary>
    /// Gets or sets the recent error rate.
    /// </summary>
    /// <value>The recent error rate.</value>

    public double RecentErrorRate
    {
        get
        {
            var operations = RecentOperations;
            var errors = RecentErrors;
            return operations > 0 ? (double)errors / operations : 0.0;
        }
    }
    /// <summary>
    /// Gets or sets the recent recovery failure rate.
    /// </summary>
    /// <value>The recent recovery failure rate.</value>

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
    /// <summary>
    /// Performs record error.
    /// </summary>
    /// <param name="error">The error.</param>

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
    /// <summary>
    /// Performs record recovery success.
    /// </summary>

    public void RecordRecoverySuccess()
    {
        _ = Interlocked.Increment(ref _recoveryAttempts);
        _ = Interlocked.Increment(ref _recoverySuccesses);
    }
    /// <summary>
    /// Performs record recovery failure.
    /// </summary>

    public void RecordRecoveryFailure() => _ = Interlocked.Increment(ref _recoveryAttempts);
    /// <summary>
    /// Performs configure.
    /// </summary>
    /// <param name="policy">The policy.</param>

    public void Configure(ErrorStatisticsPolicy policy) => CaptureDetailedDiagnostics = policy.CaptureDetailedDiagnostics;
    /// <summary>
    /// Gets the most common errors.
    /// </summary>
    /// <returns>The most common errors.</returns>

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
    /// <summary>
    /// Gets the error trends.
    /// </summary>
    /// <returns>The error trends.</returns>

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