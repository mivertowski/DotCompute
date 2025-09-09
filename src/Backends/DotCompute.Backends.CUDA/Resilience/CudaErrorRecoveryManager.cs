// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace DotCompute.Backends.CUDA.Resilience
{
    /// <summary>
    /// Manages error recovery and retry logic for transient CUDA failures.
    /// Implements exponential backoff, circuit breaker, and context recovery strategies.
    /// </summary>
    public sealed class CudaErrorRecoveryManager : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly CudaContextStateManager _stateManager;
        private readonly ConcurrentDictionary<string, ErrorStatistics> _errorStats;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly IAsyncPolicy _combinedPolicy;
        private readonly SemaphoreSlim _recoveryLock;
        private bool _disposed;

        // Recovery configuration
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const int CIRCUIT_BREAKER_THRESHOLD = 5;
        private const int CIRCUIT_BREAKER_DURATION_SECONDS = 30;
        private static readonly TimeSpan[] RetryDelays =

        [
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(2)
        ];

        // Error counters
        private long _totalErrors;
        private long _recoveredErrors;
        private long _permanentFailures;

        public CudaErrorRecoveryManager(CudaContext context, ILogger logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stateManager = new CudaContextStateManager(logger);
            _errorStats = new ConcurrentDictionary<string, ErrorStatistics>();
            _recoveryLock = new SemaphoreSlim(1, 1);

            // Setup Polly retry policy with exponential backoff
            _retryPolicy = Policy
                .Handle<CudaException>(ex => IsTransientError(ex.Error))
                .WaitAndRetryAsync(
                    RetryDelays,
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        var cudaEx = exception as CudaException;
                        _logger.LogWarning(
                            "CUDA operation failed with {Error}, retrying attempt {RetryCount}/{MaxRetries} after {Delay}ms",
                            cudaEx?.Error, retryCount, MAX_RETRY_ATTEMPTS, timeSpan.TotalMilliseconds);
                    });

            // Setup circuit breaker
            _circuitBreakerPolicy = Policy
                .Handle<CudaException>()
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5,
                    samplingDuration: TimeSpan.FromSeconds(60),
                    minimumThroughput: CIRCUIT_BREAKER_THRESHOLD,
                    durationOfBreak: TimeSpan.FromSeconds(CIRCUIT_BREAKER_DURATION_SECONDS),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError(
                            "Circuit breaker opened due to excessive failures. Breaking for {Duration} seconds",
                            duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInfoMessage("Circuit breaker reset, resuming operations");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInfoMessage("Circuit breaker half-open, testing with next operation");
                    });

            // Combine policies
            _combinedPolicy = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);

            _logger.LogInfoMessage($"CUDA error recovery manager initialized with {MAX_RETRY_ATTEMPTS} retries and circuit breaker");
        }

        /// <summary>
        /// Gets the total number of errors encountered.
        /// </summary>
        public long TotalErrors => _totalErrors;

        /// <summary>
        /// Gets the number of successfully recovered errors.
        /// </summary>
        public long RecoveredErrors => _recoveredErrors;

        /// <summary>
        /// Gets the number of permanent failures.
        /// </summary>
        public long PermanentFailures => _permanentFailures;

        /// <summary>
        /// Gets the recovery success rate.
        /// </summary>
        public double RecoverySuccessRate => _totalErrors > 0 ? (double)_recoveredErrors / _totalErrors : 0;

        /// <summary>
        /// Executes a CUDA operation with automatic retry and recovery.
        /// </summary>
        public async Task<T> ExecuteWithRecoveryAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            RecoveryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            options ??= RecoveryOptions.Default;
            var startTime = DateTime.UtcNow;

            try
            {
                return await _combinedPolicy.ExecuteAsync(async (ct) =>
                {
                    try
                    {
                        return await operation();
                    }
                    catch (CudaException ex)
                    {
                        _ = Interlocked.Increment(ref _totalErrors);
                        RecordError(operationName, ex.Error);

                        // Attempt recovery based on error type
                        if (options.EnableContextRecovery && RequiresContextRecovery(ex.Error))
                        {
                            await AttemptContextRecoveryAsync(ct);
                        }

                        throw;
                    }
                }, cancellationToken);
            }
            catch (BrokenCircuitException)
            {
                _ = Interlocked.Increment(ref _permanentFailures);
                throw new CudaException(CudaError.Unknown,

                    $"Circuit breaker is open for operation '{operationName}'. Too many failures detected.");
            }
            catch (CudaException ex)
            {
                _ = Interlocked.Increment(ref _permanentFailures);
                _logger.LogErrorMessage(ex, $"Permanent failure for operation '{operationName}' after all retry attempts");
                throw;
            }
            finally
            {
                var duration = DateTime.UtcNow - startTime;
                if (duration.TotalSeconds > 1)
                {
                    _logger.LogWarningMessage($"Operation '{operationName}' took {duration.TotalSeconds} seconds with recovery");
                }
            }
        }

        /// <summary>
        /// Executes a CUDA operation with automatic retry (void return).
        /// </summary>
        public async Task ExecuteWithRecoveryAsync(
            Func<Task> operation,
            string operationName,
            RecoveryOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _ = await ExecuteWithRecoveryAsync(async () =>
            {
                await operation();
                return true;
            }, operationName, options, cancellationToken);
        }

        /// <summary>
        /// Attempts to recover the CUDA context after critical errors.
        /// </summary>
        private async Task AttemptContextRecoveryAsync(CancellationToken cancellationToken)
        {
            await _recoveryLock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogWarningMessage("Attempting CUDA context recovery");

                // Create state snapshot before recovery
                var snapshot = await _stateManager.CreateSnapshotAsync(cancellationToken);
                _logger.LogInfoMessage("Created pre-recovery snapshot {snapshot.SnapshotId}");

                // Try progressive recovery first
                var lastError = _errorStats.Values
                    .Where(s => s.LastError != CudaError.Success)
                    .OrderByDescending(s => s.LastErrorTime)
                    .FirstOrDefault()?.LastError ?? CudaError.Unknown;

                var recoveryResult = await _stateManager.PerformProgressiveRecoveryAsync(
                    lastError, 1, cancellationToken);

                if (recoveryResult.Success)
                {
                    _logger.LogInfoMessage("Progressive recovery successful: {recoveryResult.Message}");
                    _ = Interlocked.Increment(ref _recoveredErrors);
                    return;
                }

                // Progressive recovery failed, attempt full device reset
                _logger.LogWarningMessage("Progressive recovery failed, attempting full device reset");

                // Prepare for recovery by cleaning up resources
                await _stateManager.PrepareForRecoveryAsync(cancellationToken);

                // Synchronize to ensure all operations complete
                var syncResult = CudaRuntime.cudaDeviceSynchronize();
                if (syncResult != CudaError.Success)
                {
                    _logger.LogWarningMessage("Device synchronization failed during recovery: {syncResult}");
                }

                // Reset device
                var resetResult = CudaRuntime.cudaDeviceReset();
                if (resetResult == CudaError.Success)
                {
                    _logger.LogInfoMessage("CUDA device reset successful");

                    // Re-initialize context

                    _context.Reinitialize();

                    // Restore state from snapshot

                    var restoreResult = await _stateManager.RestoreFromSnapshotAsync(snapshot, cancellationToken);
                    if (restoreResult.Success)
                    {
                        _logger.LogInfoMessage($"Context state restored: {restoreResult.Message}. Restored {restoreResult.RestoredStreams} streams, {restoreResult.RestoredKernels} kernels");
                    }
                    else
                    {
                        _logger.LogWarningMessage("Context state restoration partial: {restoreResult.Message}");
                    }

                    _ = Interlocked.Increment(ref _recoveredErrors);
                    _logger.LogInfoMessage("CUDA context recovery completed successfully");
                }
                else
                {
                    _logger.LogError("CUDA device reset failed: {Error}", resetResult);
                    throw new CudaException(resetResult, "Failed to recover CUDA context");
                }
            }
            finally
            {
                _ = _recoveryLock.Release();
            }
        }

        /// <summary>
        /// Determines if an error is transient and can be retried.
        /// </summary>
        private static bool IsTransientError(CudaError error)
        {
            return error switch
            {
                CudaError.MemoryAllocation => true,
                CudaError.LaunchTimeout => true,
                CudaError.LaunchOutOfResources => true,
                CudaError.NotReady => true,
                CudaError.IllegalAddress => false, // Usually indicates a bug
                CudaError.InvalidValue => false,   // Programming error
                CudaError.EccUncorrectable => false, // Hardware failure
                _ => false
            };
        }

        /// <summary>
        /// Determines if an error requires context recovery.
        /// </summary>
        private static bool RequiresContextRecovery(CudaError error)
        {
            return error switch
            {
                CudaError.IllegalAddress => true,
                CudaError.IllegalInstruction => true,
                CudaError.InvalidPtx => true,
                CudaError.LaunchFailure => true,
                _ => false
            };
        }

        /// <summary>
        /// Records error statistics for analysis.
        /// </summary>
        private void RecordError(string operationName, CudaError error)
        {
            var stats = _errorStats.GetOrAdd(operationName, _ => new ErrorStatistics());
            stats.RecordError(error);
        }

        /// <summary>
        /// Gets error statistics for all operations.
        /// </summary>
        public ErrorRecoveryStatistics GetStatistics()
        {
            var operationStats = new Dictionary<string, OperationErrorStatistics>();


            foreach (var kvp in _errorStats)
            {
                var stats = kvp.Value;
                operationStats[kvp.Key] = new OperationErrorStatistics
                {
                    TotalErrors = stats.TotalErrors,
                    LastError = stats.LastError,
                    LastErrorTime = stats.LastErrorTime,
                    MostCommonError = stats.GetMostCommonError()
                };
            }

            var resourceStats = _stateManager.GetStatistics();

            return new ErrorRecoveryStatistics
            {
                TotalErrors = _totalErrors,
                RecoveredErrors = _recoveredErrors,
                PermanentFailures = _permanentFailures,
                RecoverySuccessRate = RecoverySuccessRate,
                OperationStatistics = operationStats,
                ResourceStatistics = resourceStats
            };
        }

        /// <summary>
        /// Resets error statistics.
        /// </summary>
        public void ResetStatistics()
        {
            _errorStats.Clear();
            _ = Interlocked.Exchange(ref _totalErrors, 0);
            _ = Interlocked.Exchange(ref _recoveredErrors, 0);
            _ = Interlocked.Exchange(ref _permanentFailures, 0);


            _logger.LogInfoMessage("Error recovery statistics reset");
        }

        /// <summary>
        /// Registers a memory allocation with the state manager.
        /// </summary>
        public void RegisterMemoryAllocation(IntPtr ptr, ulong size, MemoryType type, string? tag = null)
        {
            _stateManager.RegisterMemoryAllocation(ptr, size, type, tag);
        }

        /// <summary>
        /// Unregisters a memory allocation from the state manager.
        /// </summary>
        public void UnregisterMemoryAllocation(IntPtr ptr)
        {
            _stateManager.UnregisterMemoryAllocation(ptr);
        }

        /// <summary>
        /// Registers a CUDA stream with the state manager.
        /// </summary>
        public void RegisterStream(IntPtr stream, StreamPriority priority = StreamPriority.Default)
        {
            _stateManager.RegisterStream(stream, priority);
        }

        /// <summary>
        /// Unregisters a CUDA stream from the state manager.
        /// </summary>
        public void UnregisterStream(IntPtr stream)
        {
            _stateManager.UnregisterStream(stream);
        }

        /// <summary>
        /// Registers a compiled kernel with the state manager.
        /// </summary>
        public void RegisterKernel(string name, byte[] ptxCode, byte[]? cubinCode = null)
        {
            _stateManager.RegisterKernel(name, ptxCode, cubinCode);
        }

        /// <summary>
        /// Creates a snapshot of the current context state.
        /// </summary>
        public async Task<ContextSnapshot> CreateContextSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return await _stateManager.CreateSnapshotAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _stateManager?.Dispose();
            _recoveryLock?.Dispose();
            _disposed = true;

            var resourceStats = _stateManager?.GetStatistics();
            _logger.LogInformation(
                "Disposed error recovery manager. Total: {Total}, Recovered: {Recovered}, Failed: {Failed}, " +
                "Recovery Count: {RecoveryCount}",
                _totalErrors, _recoveredErrors, _permanentFailures,

                resourceStats?.RecoveryCount ?? 0);
        }

        private sealed class ErrorStatistics
        {
            private readonly ConcurrentDictionary<CudaError, long> _errorCounts;
            private long _totalErrors;


            public long TotalErrors => _totalErrors;
            public CudaError LastError { get; private set; }
            public DateTime LastErrorTime { get; private set; }

            public ErrorStatistics()
            {
                _errorCounts = new ConcurrentDictionary<CudaError, long>();
            }

            public void RecordError(CudaError error)
            {
                _ = Interlocked.Increment(ref _totalErrors);
                _ = _errorCounts.AddOrUpdate(error, 1, (_, count) => count + 1);
                LastError = error;
                LastErrorTime = DateTime.UtcNow;
            }

            public CudaError GetMostCommonError()
            {
                var mostCommon = CudaError.Success;
                long maxCount = 0;

                foreach (var kvp in _errorCounts)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        mostCommon = kvp.Key;
                    }
                }

                return mostCommon;
            }
        }
    }

    /// <summary>
    /// Options for error recovery behavior.
    /// </summary>
    public sealed class RecoveryOptions
    {
        /// <summary>
        /// Gets or sets whether to attempt context recovery for critical errors.
        /// </summary>
        public bool EnableContextRecovery { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to use circuit breaker pattern.
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Gets the default recovery options.
        /// </summary>
        public static RecoveryOptions Default => new();

        /// <summary>
        /// Gets recovery options for critical operations (no circuit breaker).
        /// </summary>
        public static RecoveryOptions Critical => new()
        {
            EnableContextRecovery = true,
            EnableCircuitBreaker = false,
            MaxRetries = 5
        };

        /// <summary>
        /// Gets recovery options for non-critical operations (fail fast).
        /// </summary>
        public static RecoveryOptions FastFail => new()
        {
            EnableContextRecovery = false,
            EnableCircuitBreaker = true,
            MaxRetries = 1
        };
    }

    /// <summary>
    /// Statistics for error recovery operations.
    /// </summary>
    public sealed class ErrorRecoveryStatistics
    {
        public long TotalErrors { get; init; }
        public long RecoveredErrors { get; init; }
        public long PermanentFailures { get; init; }
        public double RecoverySuccessRate { get; init; }
        public Dictionary<string, OperationErrorStatistics> OperationStatistics { get; init; } = [];
        public ResourceStatistics? ResourceStatistics { get; init; }
    }

    /// <summary>
    /// Error statistics for a specific operation.
    /// </summary>
    public sealed class OperationErrorStatistics
    {
        public long TotalErrors { get; init; }
        public CudaError LastError { get; init; }
        public DateTime LastErrorTime { get; init; }
        public CudaError MostCommonError { get; init; }
    }

    /// <summary>
    /// Custom CUDA exception with error code.
    /// </summary>
    public class CudaException : Exception
    {
        public CudaError Error { get; }

        public CudaException(CudaError error, string message) : base(message)
        {
            Error = error;
        }

        public CudaException(CudaError error, string message, Exception innerException)

            : base(message, innerException)
        {
            Error = error;
        }
    }
}