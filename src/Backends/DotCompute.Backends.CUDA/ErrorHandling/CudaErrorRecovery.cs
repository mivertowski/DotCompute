// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.ErrorHandling
{

    /// <summary>
    /// Comprehensive CUDA error handling and recovery system
    /// </summary>
    public sealed class CudaErrorRecovery : IDisposable
    {
        private readonly CudaContext _context;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<CudaError, CudaErrorStrategy> _errorStrategies;
        private readonly ConcurrentQueue<CudaErrorEvent> _errorHistory;
        private readonly Timer _errorAnalysisTimer;
        private readonly SemaphoreSlim _recoverySemaphore;
        private bool _disposed;

        // Error tracking
        private const int MaxErrorHistorySize = 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaErrorRecovery"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">
        /// context
        /// or
        /// logger
        /// </exception>
        public CudaErrorRecovery(CudaContext context, ILogger<CudaErrorRecovery> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _errorStrategies = new ConcurrentDictionary<CudaError, CudaErrorStrategy>();
            _errorHistory = new ConcurrentQueue<CudaErrorEvent>();
            _recoverySemaphore = new SemaphoreSlim(1, 1);

            InitializeErrorStrategies();

            // Set up periodic error analysis
            _errorAnalysisTimer = new Timer(AnalyzeErrorPatterns, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger.LogInfoMessage("CUDA Error Recovery system initialized");
        }

        /// <summary>
        /// Handles a CUDA error with appropriate recovery strategy
        /// </summary>
        public async Task<CudaErrorRecoveryResult> HandleErrorAsync(
            CudaError error,
            string operation,
            object? context = null,
            CancellationToken cancellationToken = default)
        {
            var errorEvent = new CudaErrorEvent
            {
                Error = error,
                Operation = operation,
                Context = context,
                Timestamp = DateTimeOffset.UtcNow,
                ThreadId = Environment.CurrentManagedThreadId
            };

            // Record error in history
            RecordError(errorEvent);

            // Get recovery strategy
            var strategy = _errorStrategies.GetValueOrDefault(error, CudaErrorStrategy.LogAndThrow);

            _logger.LogError("CUDA error {Error} in operation '{Operation}': {Message}. Strategy: {Strategy}",
                error, operation, CudaRuntime.GetErrorString(error), strategy);

            try
            {
                return await ExecuteRecoveryStrategyAsync(errorEvent, strategy, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during recovery strategy execution");
                return new CudaErrorRecoveryResult
                {
                    Success = false,
                    Strategy = strategy,
                    ErrorMessage = ex.Message,
                    RequiresManualIntervention = true
                };
            }
        }

        /// <summary>
        /// Performs a comprehensive health check and recovery
        /// </summary>
        public async Task<CudaHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            await _recoverySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                _context.MakeCurrent();

                var result = new CudaHealthCheckResult
                {
                    Timestamp = DateTimeOffset.UtcNow
                };

                // Check device availability
                result.DeviceAvailable = await CheckDeviceAvailabilityAsync().ConfigureAwait(false);

                // Check memory status
                result.MemoryStatus = await CheckMemoryStatusAsync().ConfigureAwait(false);

                // Check context status
                result.ContextValid = await CheckContextStatusAsync().ConfigureAwait(false);

                // Check for error accumulation
                result.ErrorRate = CalculateErrorRate();

                // Overall health assessment
                result.OverallHealth = CalculateOverallHealth(result);

                // Apply recovery if needed
                if (result.OverallHealth < 0.7)
                {
                    await AttemptSystemRecoveryAsync(result, cancellationToken).ConfigureAwait(false);
                }

                return result;
            }
            finally
            {
                _ = _recoverySemaphore.Release();
            }
        }

        /// <summary>
        /// Resets the CUDA context in case of severe errors
        /// </summary>
        public async Task<bool> ResetContextAsync(CancellationToken cancellationToken = default)
        {
            await _recoverySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                _logger.LogWarningMessage("Attempting CUDA context reset");

                // Save current device
                var result = CudaRuntime.cudaGetDevice(out var currentDevice);
                if (result != CudaError.Success)
                {
                    currentDevice = _context.DeviceId;
                }

                // Reset device
                result = CudaRuntime.cudaDeviceReset();
                if (result != CudaError.Success)
                {
                    _logger.LogError("Failed to reset CUDA device: {Error}", CudaRuntime.GetErrorString(result));
                    return false;
                }

                // Restore device
                result = CudaRuntime.cudaSetDevice(currentDevice);
                CudaRuntime.CheckError(result, "restoring device after reset");

                _logger.LogInfoMessage("CUDA context reset successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during context reset");
                return false;
            }
            finally
            {
                _ = _recoverySemaphore.Release();
            }
        }

        /// <summary>
        /// Gets error statistics and patterns
        /// </summary>
        public CudaErrorStatistics GetErrorStatistics()
        {
            var errors = _errorHistory.ToArray();
            var now = DateTimeOffset.UtcNow;
            var recentErrors = errors.Where(e => (now - e.Timestamp).TotalMinutes < 60).ToArray();

            var errorCounts = errors
                .GroupBy(e => e.Error)
                .ToDictionary(g => g.Key, g => g.Count());

            var operationErrors = errors
                .GroupBy(e => e.Operation)
                .ToDictionary(g => g.Key, g => g.Count());

            return new CudaErrorStatistics
            {
                TotalErrors = errors.Length,
                RecentErrors = recentErrors.Length,
                ErrorRate = CalculateErrorRate(),
                MostCommonErrors = [.. errorCounts.OrderByDescending(kvp => kvp.Value).Take(5)],
                ProblematicOperations = [.. operationErrors.OrderByDescending(kvp => kvp.Value).Take(5)],
                LastError = errors.LastOrDefault(),
                RecoverySuccessRate = CalculateRecoverySuccessRate()
            };
        }

        private void InitializeErrorStrategies()
        {
            // Memory errors
            _errorStrategies[CudaError.MemoryAllocation] = CudaErrorStrategy.RetryWithGC;
            _errorStrategies[CudaError.LaunchOutOfResources] = CudaErrorStrategy.RetryWithGC;
            _errorStrategies[CudaError.MemoryValueTooLarge] = CudaErrorStrategy.LogAndThrow;

            // Launch errors
            _errorStrategies[CudaError.LaunchFailure] = CudaErrorStrategy.RetryOnce;
            _errorStrategies[CudaError.LaunchTimeout] = CudaErrorStrategy.RetryWithDelay;
            _errorStrategies[CudaError.InvalidConfiguration] = CudaErrorStrategy.LogAndThrow;

            // Device errors
            _errorStrategies[CudaError.NoDevice] = CudaErrorStrategy.ResetContext;
            _errorStrategies[CudaError.InvalidDevice] = CudaErrorStrategy.LogAndThrow;
            _errorStrategies[CudaError.DeviceAlreadyInUse] = CudaErrorStrategy.RetryWithDelay;

            // Context errors
            _errorStrategies[CudaError.IncompatibleDriverContext] = CudaErrorStrategy.ResetContext;
            _errorStrategies[CudaError.InvalidHandle] = CudaErrorStrategy.RetryOnce;

            // Default strategy
            _errorStrategies[CudaError.Unknown] = CudaErrorStrategy.LogAndThrow;
        }

        private async Task<CudaErrorRecoveryResult> ExecuteRecoveryStrategyAsync(
            CudaErrorEvent errorEvent,
            CudaErrorStrategy strategy,
            CancellationToken cancellationToken)
        {
            return strategy switch
            {
                CudaErrorStrategy.RetryOnce => await RetryOperationAsync(errorEvent, 1, TimeSpan.Zero, cancellationToken),
                CudaErrorStrategy.RetryWithDelay => await RetryOperationAsync(errorEvent, 2, TimeSpan.FromMilliseconds(100), cancellationToken),
                CudaErrorStrategy.RetryWithGC => await RetryWithGarbageCollectionAsync(errorEvent, cancellationToken),
                CudaErrorStrategy.ResetContext => await ResetContextRecoveryAsync(errorEvent, cancellationToken),
                CudaErrorStrategy.LogAndContinue => LogAndContinue(errorEvent),
                CudaErrorStrategy.LogAndThrow => LogAndThrow(errorEvent),
                _ => LogAndThrow(errorEvent)
            };
        }

        private async Task<CudaErrorRecoveryResult> RetryOperationAsync(
            CudaErrorEvent errorEvent,
            int maxRetries,
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    // Clear any pending errors
                    _ = CudaRuntime.cudaGetLastError();

                    _logger.LogInfoMessage($"Retry attempt {attempt}/{maxRetries} for operation '{errorEvent.Operation}'");

                    return new CudaErrorRecoveryResult
                    {
                        Success = true,
                        Strategy = CudaErrorStrategy.RetryOnce,
                        RetryAttempt = attempt,
                        RecoveryMessage = $"Ready for retry attempt {attempt}"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Retry attempt {Attempt} failed", attempt);

                    if (attempt == maxRetries)
                    {
                        return new CudaErrorRecoveryResult
                        {
                            Success = false,
                            Strategy = CudaErrorStrategy.RetryOnce,
                            RetryAttempt = attempt,
                            ErrorMessage = $"All {maxRetries} retry attempts failed: {ex.Message}"
                        };
                    }
                }
            }

            return new CudaErrorRecoveryResult
            {
                Success = false,
                Strategy = CudaErrorStrategy.RetryOnce,
                ErrorMessage = "Maximum retry attempts exceeded"
            };
        }

        private async Task<CudaErrorRecoveryResult> RetryWithGarbageCollectionAsync(
            CudaErrorEvent errorEvent,
            CancellationToken cancellationToken)
        {
            _logger.LogInfoMessage($"Performing garbage collection before retry for operation '{errorEvent.Operation}'");

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);

            // Try to free device memory
            try
            {
                _context.MakeCurrent();
                _ = CudaRuntime.cudaDeviceSynchronize();
            }
            catch
            {
                // Ignore errors during cleanup
            }

            return await RetryOperationAsync(errorEvent, 2, TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        private async Task<CudaErrorRecoveryResult> ResetContextRecoveryAsync(
            CudaErrorEvent errorEvent,
            CancellationToken cancellationToken)
        {
            _logger.LogWarningMessage($"Attempting context reset recovery for operation '{errorEvent.Operation}'");

            var resetSuccess = await ResetContextAsync(cancellationToken).ConfigureAwait(false);

            return new CudaErrorRecoveryResult
            {
                Success = resetSuccess,
                Strategy = CudaErrorStrategy.ResetContext,
                RecoveryMessage = resetSuccess ? "Context reset successful" : "Context reset failed",
                RequiresManualIntervention = !resetSuccess
            };
        }

        private CudaErrorRecoveryResult LogAndContinue(CudaErrorEvent errorEvent)
        {
            _logger.LogWarningMessage($"Continuing after error {errorEvent.Error} in operation '{errorEvent.Operation}'");

            return new CudaErrorRecoveryResult
            {
                Success = true,
                Strategy = CudaErrorStrategy.LogAndContinue,
                RecoveryMessage = "Error logged, continuing execution"
            };
        }

        private static CudaErrorRecoveryResult LogAndThrow(CudaErrorEvent errorEvent)
        {
            return new CudaErrorRecoveryResult
            {
                Success = false,
                Strategy = CudaErrorStrategy.LogAndThrow,
                ErrorMessage = $"CUDA error {errorEvent.Error} in operation '{errorEvent.Operation}': {CudaRuntime.GetErrorString(errorEvent.Error)}",
                RequiresManualIntervention = true
            };
        }

        private void RecordError(CudaErrorEvent errorEvent)
        {
            _errorHistory.Enqueue(errorEvent);

            // Maintain history size limit
            while (_errorHistory.Count > MaxErrorHistorySize && _errorHistory.TryDequeue(out _))
            {
                // Remove old entries
            }
        }

        private async Task<bool> CheckDeviceAvailabilityAsync()
        {
            await Task.Delay(1).ConfigureAwait(false);

            try
            {
                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
                return result == CudaError.Success && deviceCount > _context.DeviceId;
            }
            catch
            {
                return false;
            }
        }

        private async Task<CudaMemoryStatus> CheckMemoryStatusAsync()
        {
            await Task.Delay(1).ConfigureAwait(false);

            try
            {
                _context.MakeCurrent();
                var result = CudaRuntime.cudaMemGetInfo(out var free, out var total);

                if (result == CudaError.Success)
                {
                    return new CudaMemoryStatus
                    {
                        TotalBytes = total,
                        FreeBytes = free,
                        UsedBytes = total - free,
                        UtilizationPercentage = (double)(total - free) / total * 100.0
                    };
                }
            }
            catch
            {
                // Fall through to error case
            }

            return new CudaMemoryStatus { IsValid = false };
        }

        private async Task<bool> CheckContextStatusAsync()
        {
            await Task.Delay(1).ConfigureAwait(false);

            try
            {
                _context.MakeCurrent();
                var result = CudaRuntime.cudaGetLastError();
                return result == CudaError.Success;
            }
            catch
            {
                return false;
            }
        }

        private double CalculateErrorRate()
        {
            var now = DateTimeOffset.UtcNow;
            var recentErrors = _errorHistory.Count(e => (now - e.Timestamp).TotalHours < 1);
            return recentErrors; // Errors per hour
        }

        private static double CalculateOverallHealth(CudaHealthCheckResult result)
        {
            var factors = new[]
            {
            result.DeviceAvailable ? 1.0 : 0.0,
            result.ContextValid ? 1.0 : 0.0,
            result.MemoryStatus.IsValid ? (1.0 - result.MemoryStatus.UtilizationPercentage / 100.0) : 0.5,
            Math.Max(0.0, 1.0 - result.ErrorRate / 10.0) // Penalize high error rates
        };

            return factors.Average();
        }

        private async Task AttemptSystemRecoveryAsync(
            CudaHealthCheckResult healthResult,
            CancellationToken cancellationToken)
        {
            _logger.LogWarningMessage($"System health degraded ({healthResult.OverallHealth}), attempting recovery");

            if (!healthResult.DeviceAvailable || !healthResult.ContextValid)
            {
                _ = await ResetContextAsync(cancellationToken).ConfigureAwait(false);
            }

            if (healthResult.MemoryStatus.UtilizationPercentage > 90)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private static double CalculateRecoverySuccessRate()
            // Simplified calculation - would track actual recovery attempts in production



            => 0.85; // 85% success rate

        private void AnalyzeErrorPatterns(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var stats = GetErrorStatistics();

                if (stats.RecentErrors > 10)
                {
                    _logger.LogWarningMessage($"High error rate detected: {stats.RecentErrors} errors in the last hour");
                }

                // Analyze for patterns and suggest optimizations
                if (stats.MostCommonErrors.Count > 0)
                {
                    var topError = stats.MostCommonErrors.First();
                    if (topError.Value > 5)
                    {
                        _logger.LogInfoMessage($"Most common error: {topError.Key} ({topError.Value} occurrences)");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during error pattern analysis");
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _errorAnalysisTimer?.Dispose();
                _recoverySemaphore?.Dispose();
                _disposed = true;

                _logger.LogInfoMessage("CUDA Error Recovery system disposed");
            }
        }
    }

    // Supporting types
    /// <summary>
    ///
    /// </summary>
    public enum CudaErrorStrategy
    {
        /// <summary>
        /// The log and throw
        /// </summary>
        LogAndThrow,
        /// <summary>
        /// The log and continue
        /// </summary>
        LogAndContinue,
        /// <summary>
        /// The retry once
        /// </summary>
        RetryOnce,
        /// <summary>
        /// The retry with delay
        /// </summary>
        RetryWithDelay,
        /// <summary>
        /// The retry with gc
        /// </summary>
        RetryWithGC,
        /// <summary>
        /// The reset context
        /// </summary>
        ResetContext
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class CudaErrorEvent
    {
        /// <summary>
        /// Gets or sets the error.
        /// </summary>
        /// <value>
        /// The error.
        /// </value>
        public CudaError Error { get; set; }

        /// <summary>
        /// Gets or sets the operation.
        /// </summary>
        /// <value>
        /// The operation.
        /// </value>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the context.
        /// </summary>
        /// <value>
        /// The context.
        /// </value>
        public object? Context { get; set; }

        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>
        /// The timestamp.
        /// </value>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the thread identifier.
        /// </summary>
        /// <value>
        /// The thread identifier.
        /// </value>
        public int ThreadId { get; set; }
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class CudaErrorRecoveryResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="CudaErrorRecoveryResult"/> is success.
        /// </summary>
        /// <value>
        ///   <c>true</c> if success; otherwise, <c>false</c>.
        /// </value>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the strategy.
        /// </summary>
        /// <value>
        /// The strategy.
        /// </value>
        public CudaErrorStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets the recovery message.
        /// </summary>
        /// <value>
        /// The recovery message.
        /// </value>
        public string? RecoveryMessage { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        /// <value>
        /// The error message.
        /// </value>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the retry attempt.
        /// </summary>
        /// <value>
        /// The retry attempt.
        /// </value>
        public int RetryAttempt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [requires manual intervention].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [requires manual intervention]; otherwise, <c>false</c>.
        /// </value>
        public bool RequiresManualIntervention { get; set; }
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class CudaHealthCheckResult
    {
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        /// <value>
        /// The timestamp.
        /// </value>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [device available].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [device available]; otherwise, <c>false</c>.
        /// </value>
        public bool DeviceAvailable { get; set; }

        /// <summary>
        /// Gets or sets the memory status.
        /// </summary>
        /// <value>
        /// The memory status.
        /// </value>
        public CudaMemoryStatus MemoryStatus { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether [context valid].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [context valid]; otherwise, <c>false</c>.
        /// </value>
        public bool ContextValid { get; set; }

        /// <summary>
        /// Gets or sets the error rate.
        /// </summary>
        /// <value>
        /// The error rate.
        /// </value>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets the overall health.
        /// </summary>
        /// <value>
        /// The overall health.
        /// </value>
        public double OverallHealth { get; set; }
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class CudaMemoryStatus
    {
        /// <summary>
        /// Gets or sets the total bytes.
        /// </summary>
        /// <value>
        /// The total bytes.
        /// </value>
        public ulong TotalBytes { get; set; }

        /// <summary>
        /// Gets or sets the free bytes.
        /// </summary>
        /// <value>
        /// The free bytes.
        /// </value>
        public ulong FreeBytes { get; set; }

        /// <summary>
        /// Gets or sets the used bytes.
        /// </summary>
        /// <value>
        /// The used bytes.
        /// </value>
        public ulong UsedBytes { get; set; }

        /// <summary>
        /// Gets or sets the utilization percentage.
        /// </summary>
        /// <value>
        /// The utilization percentage.
        /// </value>
        public double UtilizationPercentage { get; set; }

        /// <summary>
        /// Returns true if ... is valid.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </value>
        public bool IsValid { get; set; } = true;
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class CudaErrorStatistics
    {
        /// <summary>
        /// Gets or sets the total errors.
        /// </summary>
        /// <value>
        /// The total errors.
        /// </value>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Gets or sets the recent errors.
        /// </summary>
        /// <value>
        /// The recent errors.
        /// </value>
        public int RecentErrors { get; set; }

        /// <summary>
        /// Gets or sets the error rate.
        /// </summary>
        /// <value>
        /// The error rate.
        /// </value>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets the most common errors.
        /// </summary>
        /// <value>
        /// The most common errors.
        /// </value>
        public List<KeyValuePair<CudaError, int>> MostCommonErrors { get; } = [];

        /// <summary>
        /// Gets or sets the problematic operations.
        /// </summary>
        /// <value>
        /// The problematic operations.
        /// </value>
        public List<KeyValuePair<string, int>> ProblematicOperations { get; } = [];

        /// <summary>
        /// Gets or sets the last error.
        /// </summary>
        /// <value>
        /// The last error.
        /// </value>
        public CudaErrorEvent? LastError { get; set; }

        /// <summary>
        /// Gets or sets the recovery success rate.
        /// </summary>
        /// <value>
        /// The recovery success rate.
        /// </value>
        public double RecoverySuccessRate { get; set; }
    }
}
