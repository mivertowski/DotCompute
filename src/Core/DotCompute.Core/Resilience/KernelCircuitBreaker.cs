// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Resilience;
using DotCompute.Core.Logging;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Resilience;

/// <summary>
/// Circuit breaker implementation specifically designed for GPU kernel execution.
/// Provides per-kernel and per-accelerator failure tracking with GPU-aware policies.
/// </summary>
/// <remarks>
/// This implementation extends the core circuit breaker pattern with:
/// - Per-kernel failure tracking and state management
/// - Per-accelerator aggregate failure monitoring
/// - GPU-specific error categorization and smart recovery
/// - Adaptive timeout calculation based on execution history
/// - Integration with DotCompute's error classification system
/// </remarks>
public sealed partial class KernelCircuitBreaker : IKernelCircuitBreaker, IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 14001, Level = MsLogLevel.Warning, Message = "Kernel {KernelName} circuit opened on {AcceleratorType}: {Reason}")]
    private static partial void LogCircuitOpened(ILogger logger, string kernelName, string acceleratorType, string reason);

    [LoggerMessage(EventId = 14002, Level = MsLogLevel.Information, Message = "Kernel {KernelName} circuit transitioned to HalfOpen on {AcceleratorType}")]
    private static partial void LogCircuitHalfOpen(ILogger logger, string kernelName, string acceleratorType);

    [LoggerMessage(EventId = 14003, Level = MsLogLevel.Information, Message = "Kernel {KernelName} circuit closed on {AcceleratorType} after successful execution")]
    private static partial void LogCircuitClosed(ILogger logger, string kernelName, string acceleratorType);

    [LoggerMessage(EventId = 14004, Level = MsLogLevel.Warning, Message = "Kernel {KernelName} execution failed on {AcceleratorType}: {ErrorMessage}")]
    private static partial void LogKernelExecutionFailed(ILogger logger, string kernelName, string acceleratorType, string errorMessage, Exception ex);

    [LoggerMessage(EventId = 14005, Level = MsLogLevel.Debug, Message = "Kernel {KernelName} executed successfully on {AcceleratorType} in {ElapsedMs}ms")]
    private static partial void LogKernelExecutionSuccess(ILogger logger, string kernelName, string acceleratorType, double elapsedMs);

    #endregion

    private readonly ILogger<KernelCircuitBreaker> _logger;
    private readonly KernelCircuitBreakerPolicy _defaultPolicy;
    private readonly ConcurrentDictionary<string, KernelCircuitState> _kernelStates;
    private readonly ConcurrentDictionary<string, KernelCircuitState> _acceleratorStates;
    private readonly Timer _healthCheckTimer;
    private readonly Lock _stateLock = new();
    private bool _disposed;

    // Global state
    private CircuitBreakerState _globalState = CircuitBreakerState.Closed;
    private long _totalExecutions;
    private long _totalFailedExecutions;
    private DateTimeOffset _lastGlobalStateChange = DateTimeOffset.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelCircuitBreaker"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="defaultPolicy">Optional default policy.</param>
    public KernelCircuitBreaker(
        ILogger<KernelCircuitBreaker> logger,
        KernelCircuitBreakerPolicy? defaultPolicy = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultPolicy = defaultPolicy ?? KernelCircuitBreakerPolicy.Default;
        _kernelStates = new ConcurrentDictionary<string, KernelCircuitState>();
        _acceleratorStates = new ConcurrentDictionary<string, KernelCircuitState>();

        // Health check timer for state transitions
        _healthCheckTimer = new Timer(
            PerformHealthCheck,
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10));

        _logger.LogInfoMessage($"KernelCircuitBreaker initialized with policy: " +
            $"FailureThreshold={_defaultPolicy.FailureThresholdPercentage}%, " +
            $"ConsecutiveFailures={_defaultPolicy.ConsecutiveFailureThreshold}");
    }

    /// <inheritdoc />
    public bool CanExecute(string kernelName, string acceleratorType)
    {
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(acceleratorType);

        // Check global state first
        if (_globalState == CircuitBreakerState.Open)
        {
            return false;
        }

        // Check kernel-specific state
        var kernelState = GetOrCreateKernelState(kernelName);
        if (!CanExecuteInState(kernelState))
        {
            return false;
        }

        // Check accelerator-specific state
        var acceleratorState = GetOrCreateAcceleratorState(acceleratorType);
        return CanExecuteInState(acceleratorState);
    }

    /// <inheritdoc />
    public async Task<KernelExecutionResult> ExecuteKernelAsync(
        string kernelName,
        string acceleratorType,
        Func<CancellationToken, Task<KernelExecutionResult>> operation,
        KernelCircuitBreakerPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(acceleratorType);
        ArgumentNullException.ThrowIfNull(operation);

        var effectivePolicy = policy ?? _defaultPolicy;

        // Check if circuit allows execution
        if (!CanExecute(kernelName, acceleratorType))
        {
            var kernelState = GetOrCreateKernelState(kernelName);
            var exception = new KernelCircuitOpenException(kernelName, acceleratorType, kernelState);
            RecordFailure(kernelName, acceleratorType, exception, KernelErrorCategory.Transient);
            throw exception;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Execute with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var timeout = effectivePolicy.UseAdaptiveTimeout
                ? CalculateAdaptiveTimeout(kernelName, effectivePolicy)
                : effectivePolicy.OperationTimeout;

            timeoutCts.CancelAfter(timeout);

            var result = await operation(timeoutCts.Token);

            stopwatch.Stop();

            if (result.Success)
            {
                RecordSuccess(kernelName, acceleratorType, stopwatch.Elapsed);
                LogKernelExecutionSuccess(_logger, kernelName, acceleratorType, stopwatch.Elapsed.TotalMilliseconds);
            }
            else
            {
                var category = ClassifyError(result.Error);
                RecordFailure(kernelName, acceleratorType, result.Error ?? new Exception(result.ErrorMessage ?? "Unknown error"), category);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User-requested cancellation - don't count as failure
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Timeout - count as failure
            stopwatch.Stop();
            RecordFailure(kernelName, acceleratorType, ex, KernelErrorCategory.Timeout);
            LogKernelExecutionFailed(_logger, kernelName, acceleratorType, "Execution timeout", ex);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var category = ClassifyError(ex);
            RecordFailure(kernelName, acceleratorType, ex, category);
            LogKernelExecutionFailed(_logger, kernelName, acceleratorType, ex.Message, ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<KernelExecutionResult> ExecuteKernelWithRetryAsync(
        string kernelName,
        string acceleratorType,
        Func<CancellationToken, Task<KernelExecutionResult>> operation,
        KernelCircuitBreakerPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        var effectivePolicy = policy ?? _defaultPolicy;
        Exception? lastException = null;

        for (var attempt = 1; attempt <= effectivePolicy.MaxRetries; attempt++)
        {
            try
            {
                return await ExecuteKernelAsync(kernelName, acceleratorType, operation, policy, cancellationToken);
            }
            catch (KernelCircuitOpenException)
            {
                // Don't retry if circuit is open
                throw;
            }
            catch (Exception ex) when (attempt < effectivePolicy.MaxRetries)
            {
                lastException = ex;

                // Check if error is retryable
                var category = ClassifyError(ex);
                if (effectivePolicy.CriticalErrorCategories.Contains(category))
                {
                    // Critical error - don't retry
                    throw;
                }

                // Calculate retry delay with exponential backoff
                var delay = CalculateRetryDelay(attempt, effectivePolicy);

                _logger.LogWarningMessage($"Kernel {kernelName} attempt {attempt}/{effectivePolicy.MaxRetries} " +
                    $"failed on {acceleratorType}, retrying in {delay.TotalMilliseconds}ms: {ex.Message}");

                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries exhausted
        throw lastException ?? new InvalidOperationException("All retry attempts failed");
    }

    /// <inheritdoc />
    public KernelCircuitState GetKernelState(string kernelName)
    {
        ArgumentNullException.ThrowIfNull(kernelName);
        return GetOrCreateKernelState(kernelName);
    }

    /// <inheritdoc />
    public KernelCircuitState GetAcceleratorState(string acceleratorType)
    {
        ArgumentNullException.ThrowIfNull(acceleratorType);
        return GetOrCreateAcceleratorState(acceleratorType);
    }

    /// <inheritdoc />
    public KernelCircuitBreakerStatistics GetStatistics()
    {
        var kernelStats = _kernelStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var acceleratorStats = _acceleratorStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new KernelCircuitBreakerStatistics
        {
            GlobalState = _globalState,
            TotalExecutions = _totalExecutions,
            TotalFailedExecutions = _totalFailedExecutions,
            KernelStatistics = kernelStats,
            AcceleratorStatistics = acceleratorStats,
            ActiveKernels = _kernelStates.Count,
            ActiveAccelerators = _acceleratorStates.Count,
            OpenCircuitKernels = kernelStats.Values.Count(s => s.State == CircuitBreakerState.Open),
            LastUpdated = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc />
    public void RecordSuccess(string kernelName, string acceleratorType, TimeSpan executionTime)
    {
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(acceleratorType);

        var kernelState = GetOrCreateKernelState(kernelName);
        var acceleratorState = GetOrCreateAcceleratorState(acceleratorType);

        lock (_stateLock)
        {
            Interlocked.Increment(ref _totalExecutions);

            UpdateStateOnSuccess(kernelState, executionTime);
            UpdateStateOnSuccess(acceleratorState, executionTime);

            // Transition from half-open to closed if needed
            if (kernelState.State == CircuitBreakerState.HalfOpen)
            {
                TransitionKernelState(kernelState, CircuitBreakerState.Closed, "Successful execution in half-open state");
                LogCircuitClosed(_logger, kernelName, acceleratorType);
            }

            if (_globalState == CircuitBreakerState.HalfOpen)
            {
                _globalState = CircuitBreakerState.Closed;
                _lastGlobalStateChange = DateTimeOffset.UtcNow;
            }
        }
    }

    /// <inheritdoc />
    public void RecordFailure(string kernelName, string acceleratorType, Exception exception, KernelErrorCategory? errorCategory = null)
    {
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(acceleratorType);

        var category = errorCategory ?? ClassifyError(exception);
        var kernelState = GetOrCreateKernelState(kernelName);
        var acceleratorState = GetOrCreateAcceleratorState(acceleratorType);

        lock (_stateLock)
        {
            Interlocked.Increment(ref _totalExecutions);
            Interlocked.Increment(ref _totalFailedExecutions);

            UpdateStateOnFailure(kernelState, exception, category);
            UpdateStateOnFailure(acceleratorState, exception, category);

            // Check if circuit should open
            if (ShouldOpenCircuit(kernelState, _defaultPolicy))
            {
                var reason = $"Failure rate {kernelState.FailureRate * 100:F1}% or consecutive failures {kernelState.ConsecutiveFailures}";
                TransitionKernelState(kernelState, CircuitBreakerState.Open, reason);
                LogCircuitOpened(_logger, kernelName, acceleratorType, reason);
            }

            // Check if critical error should immediately open circuit
            if (_defaultPolicy.CriticalErrorCategories.Contains(category))
            {
                TransitionKernelState(kernelState, CircuitBreakerState.Open, $"Critical error: {category}");
                LogCircuitOpened(_logger, kernelName, acceleratorType, $"Critical error: {category}");
            }
        }
    }

    /// <inheritdoc />
    public void ResetKernel(string kernelName)
    {
        ArgumentNullException.ThrowIfNull(kernelName);

        if (_kernelStates.TryGetValue(kernelName, out var state))
        {
            ResetState(state);
            _logger.LogInfoMessage($"Kernel {kernelName} circuit reset to CLOSED");
        }
    }

    /// <inheritdoc />
    public void ResetAccelerator(string acceleratorType)
    {
        ArgumentNullException.ThrowIfNull(acceleratorType);

        if (_acceleratorStates.TryGetValue(acceleratorType, out var state))
        {
            ResetState(state);
            _logger.LogInfoMessage($"Accelerator {acceleratorType} circuit reset to CLOSED");
        }
    }

    /// <inheritdoc />
    public void ResetAll()
    {
        lock (_stateLock)
        {
            foreach (var state in _kernelStates.Values)
            {
                ResetState(state);
            }

            foreach (var state in _acceleratorStates.Values)
            {
                ResetState(state);
            }

            _globalState = CircuitBreakerState.Closed;
            _totalExecutions = 0;
            _totalFailedExecutions = 0;
            _lastGlobalStateChange = DateTimeOffset.UtcNow;
        }

        _logger.LogInfoMessage("All kernel circuits reset to CLOSED");
    }

    /// <inheritdoc />
    public void ForceKernelState(string kernelName, CircuitBreakerState state)
    {
        ArgumentNullException.ThrowIfNull(kernelName);

        var kernelState = GetOrCreateKernelState(kernelName);
        TransitionKernelState(kernelState, state, "Forced by operator");

        _logger.LogWarningMessage($"Kernel {kernelName} circuit forced to {state}");
    }

    /// <inheritdoc />
    public KernelRecoveryRecommendation GetRecoveryRecommendation(string kernelName, string acceleratorType)
    {
        ArgumentNullException.ThrowIfNull(kernelName);
        ArgumentNullException.ThrowIfNull(acceleratorType);

        var kernelState = GetOrCreateKernelState(kernelName);

        // Analyze failure patterns
        if (kernelState.ConsecutiveFailures == 0)
        {
            return new KernelRecoveryRecommendation
            {
                RecommendedAction = KernelRecoveryAction.RetryImmediate,
                Confidence = 1.0,
                Reason = "No recent failures"
            };
        }

        // Check dominant error category
        var dominantCategory = kernelState.FailuresByCategory
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();

        return dominantCategory switch
        {
            KernelErrorCategory.Timeout => new KernelRecoveryRecommendation
            {
                RecommendedAction = KernelRecoveryAction.ReduceProblemSize,
                Confidence = 0.8,
                Reason = "Repeated timeouts suggest problem size is too large",
                SuggestedWaitTime = TimeSpan.FromSeconds(5)
            },
            KernelErrorCategory.MemoryError => new KernelRecoveryRecommendation
            {
                RecommendedAction = KernelRecoveryAction.ReduceProblemSize,
                Confidence = 0.9,
                Reason = "Memory errors indicate GPU memory exhaustion",
                SuggestFallbackBackend = true,
                FallbackBackendType = "CPU"
            },
            KernelErrorCategory.ResourceExhaustion => new KernelRecoveryRecommendation
            {
                RecommendedAction = KernelRecoveryAction.WaitForRecovery,
                Confidence = 0.7,
                Reason = "GPU resources exhausted, waiting may help",
                SuggestedWaitTime = _defaultPolicy.OpenCircuitDuration
            },
            KernelErrorCategory.ContextError => new KernelRecoveryRecommendation
            {
                RecommendedAction = KernelRecoveryAction.ResetContextAndRetry,
                Confidence = 0.85,
                Reason = "Context error may be resolved by reset"
            },
            KernelErrorCategory.HardwareError or KernelErrorCategory.Permanent => new KernelRecoveryRecommendation
            {
                RecommendedAction = KernelRecoveryAction.UseFallbackBackend,
                Confidence = 0.95,
                Reason = "Hardware/permanent error requires fallback",
                SuggestFallbackBackend = true,
                FallbackBackendType = "CPU"
            },
            KernelErrorCategory.DeviceNotReady => new KernelRecoveryRecommendation
            {
                RecommendedAction = KernelRecoveryAction.RetryWithBackoff,
                Confidence = 0.75,
                Reason = "Device not ready, may recover with wait",
                SuggestedWaitTime = TimeSpan.FromSeconds(2)
            },
            _ => new KernelRecoveryRecommendation
            {
                RecommendedAction = kernelState.ConsecutiveFailures >= _defaultPolicy.ConsecutiveFailureThreshold
                    ? KernelRecoveryAction.WaitForRecovery
                    : KernelRecoveryAction.RetryWithBackoff,
                Confidence = 0.5,
                Reason = "Unknown error pattern, using default recovery strategy",
                SuggestedWaitTime = CalculateRetryDelay(kernelState.ConsecutiveFailures, _defaultPolicy)
            }
        };
    }

    private KernelCircuitState GetOrCreateKernelState(string kernelName)
    {
        return _kernelStates.GetOrAdd(kernelName, name => new KernelCircuitState
        {
            KernelName = name,
            State = CircuitBreakerState.Closed,
            LastStateChange = DateTimeOffset.UtcNow
        });
    }

    private KernelCircuitState GetOrCreateAcceleratorState(string acceleratorType)
    {
        return _acceleratorStates.GetOrAdd(acceleratorType, type => new KernelCircuitState
        {
            KernelName = $"accelerator:{type}",
            AcceleratorType = type,
            State = CircuitBreakerState.Closed,
            LastStateChange = DateTimeOffset.UtcNow
        });
    }

    private static bool CanExecuteInState(KernelCircuitState state)
    {
        return state.State switch
        {
            CircuitBreakerState.Closed => true,
            CircuitBreakerState.HalfOpen => true,
            CircuitBreakerState.Open => state.NextRetryTime.HasValue && DateTimeOffset.UtcNow >= state.NextRetryTime.Value,
            _ => false
        };
    }

    private static void UpdateStateOnSuccess(KernelCircuitState state, TimeSpan executionTime)
    {
        state.TotalExecutions++;
        state.SuccessfulExecutions++;
        state.ConsecutiveFailures = 0;

        // Update execution time statistics
        if (state.MinExecutionTime > executionTime)
            state.MinExecutionTime = executionTime;
        if (state.MaxExecutionTime < executionTime)
            state.MaxExecutionTime = executionTime;

        // Update average using exponential moving average
        if (state.AverageExecutionTime == TimeSpan.Zero)
        {
            state.AverageExecutionTime = executionTime;
        }
        else
        {
            const double alpha = 0.1;
            state.AverageExecutionTime = TimeSpan.FromTicks(
                (long)(alpha * executionTime.Ticks + (1 - alpha) * state.AverageExecutionTime.Ticks));
        }
    }

    private static void UpdateStateOnFailure(KernelCircuitState state, Exception exception, KernelErrorCategory category)
    {
        state.TotalExecutions++;
        state.FailedExecutions++;
        state.ConsecutiveFailures++;
        state.LastError = exception;
        state.LastErrorCategory = category;
        state.LastErrorTime = DateTimeOffset.UtcNow;

        // Track failure by category
        state.FailuresByCategory.TryGetValue(category, out var count);
        state.FailuresByCategory[category] = count + 1;
    }

    private bool ShouldOpenCircuit(KernelCircuitState state, KernelCircuitBreakerPolicy policy)
    {
        if (state.State != CircuitBreakerState.Closed)
            return false;

        // Check minimum throughput
        if (state.TotalExecutions < policy.MinimumThroughput)
            return state.ConsecutiveFailures >= policy.ConsecutiveFailureThreshold;

        // Check failure rate
        var failureRate = state.FailureRate * 100.0;
        return failureRate >= policy.FailureThresholdPercentage ||
               state.ConsecutiveFailures >= policy.ConsecutiveFailureThreshold;
    }

    private void TransitionKernelState(KernelCircuitState state, CircuitBreakerState newState, string reason)
    {
        var previousState = state.State;
        state.State = newState;
        state.LastStateChange = DateTimeOffset.UtcNow;

        if (newState == CircuitBreakerState.Open)
        {
            state.OpenCount++;
            state.NextRetryTime = DateTimeOffset.UtcNow.Add(_defaultPolicy.OpenCircuitDuration);
        }
        else if (previousState == CircuitBreakerState.Open)
        {
            state.TotalOpenTime += DateTimeOffset.UtcNow - state.LastStateChange;
            state.NextRetryTime = null;
        }
    }

    private static void ResetState(KernelCircuitState state)
    {
        state.State = CircuitBreakerState.Closed;
        state.ConsecutiveFailures = 0;
        state.TotalExecutions = 0;
        state.SuccessfulExecutions = 0;
        state.FailedExecutions = 0;
        state.LastError = null;
        state.LastErrorCategory = null;
        state.LastErrorTime = null;
        state.NextRetryTime = null;
        state.AverageExecutionTime = TimeSpan.Zero;
        state.MinExecutionTime = TimeSpan.MaxValue;
        state.MaxExecutionTime = TimeSpan.Zero;
        state.FailuresByCategory.Clear();
        state.LastStateChange = DateTimeOffset.UtcNow;
    }

    private TimeSpan CalculateAdaptiveTimeout(string kernelName, KernelCircuitBreakerPolicy policy)
    {
        if (!_kernelStates.TryGetValue(kernelName, out var state) || state.AverageExecutionTime == TimeSpan.Zero)
        {
            return policy.OperationTimeout;
        }

        var adaptiveTimeout = TimeSpan.FromTicks((long)(state.MaxExecutionTime.Ticks * policy.AdaptiveTimeoutMultiplier));

        // Clamp to reasonable bounds
        if (adaptiveTimeout < policy.OperationTimeout)
            return policy.OperationTimeout;

        var maxTimeout = TimeSpan.FromMinutes(5);
        return adaptiveTimeout > maxTimeout ? maxTimeout : adaptiveTimeout;
    }

    private static TimeSpan CalculateRetryDelay(int attempt, KernelCircuitBreakerPolicy policy)
    {
        var baseDelay = policy.RetryBaseDelay;
        var exponentialDelay = TimeSpan.FromTicks(
            (long)(baseDelay.Ticks * Math.Pow(policy.RetryBackoffMultiplier, attempt - 1)));

        // Add jitter to prevent thundering herd
#pragma warning disable CA5394 // Random is used for retry jitter, not security
        var jitter = Random.Shared.NextDouble() * 0.1;
#pragma warning restore CA5394
        var finalDelay = TimeSpan.FromTicks((long)(exponentialDelay.Ticks * (1.0 + jitter)));

        return finalDelay > policy.MaxRetryDelay ? policy.MaxRetryDelay : finalDelay;
    }

    private static KernelErrorCategory ClassifyError(Exception? exception)
    {
        if (exception == null)
            return KernelErrorCategory.Unknown;

        var message = exception.Message.ToUpperInvariant();
        var typeName = exception.GetType().Name.ToUpperInvariant();

        // Memory-related errors
        if (message.Contains("OUT OF MEMORY") || message.Contains("ALLOCATION") ||
            message.Contains("CUDA_ERROR_OUT_OF_MEMORY") || typeName.Contains("OUTOFMEMORY"))
        {
            return KernelErrorCategory.MemoryError;
        }

        // Timeout errors
        if (exception is OperationCanceledException or TimeoutException ||
            message.Contains("TIMEOUT") || message.Contains("CUDA_ERROR_LAUNCH_TIMEOUT"))
        {
            return KernelErrorCategory.Timeout;
        }

        // Resource exhaustion
        if (message.Contains("RESOURCE") || message.Contains("CUDA_ERROR_LAUNCH_OUT_OF_RESOURCES"))
        {
            return KernelErrorCategory.ResourceExhaustion;
        }

        // Device not ready
        if (message.Contains("NOT READY") || message.Contains("CUDA_ERROR_NOT_READY") ||
            message.Contains("DEVICE") && message.Contains("UNAVAILABLE"))
        {
            return KernelErrorCategory.DeviceNotReady;
        }

        // Compilation errors
        if (message.Contains("COMPILE") || message.Contains("PTX") ||
            message.Contains("KERNEL") && message.Contains("NOT FOUND"))
        {
            return KernelErrorCategory.CompilationError;
        }

        // Invalid arguments
        if (exception is ArgumentException || message.Contains("INVALID") && message.Contains("ARGUMENT"))
        {
            return KernelErrorCategory.InvalidArguments;
        }

        // Context errors
        if (message.Contains("CONTEXT") || message.Contains("CUDA_ERROR_INVALID_CONTEXT"))
        {
            return KernelErrorCategory.ContextError;
        }

        // Hardware errors
        if (message.Contains("HARDWARE") || message.Contains("ECC") ||
            message.Contains("CUDA_ERROR_HARDWARE_STACK_ERROR"))
        {
            return KernelErrorCategory.HardwareError;
        }

        // Check if transient (common transient patterns)
        if (message.Contains("BUSY") || message.Contains("RETRY") || message.Contains("TEMPORARY"))
        {
            return KernelErrorCategory.Transient;
        }

        return KernelErrorCategory.Unknown;
    }

    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
            return;

        try
        {
            var now = DateTimeOffset.UtcNow;

            lock (_stateLock)
            {
                // Check global state transition
                if (_globalState == CircuitBreakerState.Open &&
                    (now - _lastGlobalStateChange) >= _defaultPolicy.OpenCircuitDuration)
                {
                    _globalState = CircuitBreakerState.HalfOpen;
                    _lastGlobalStateChange = now;
                }

                // Check kernel state transitions
                foreach (var kernelState in _kernelStates.Values)
                {
                    if (kernelState.State == CircuitBreakerState.Open &&
                        kernelState.NextRetryTime.HasValue &&
                        now >= kernelState.NextRetryTime.Value)
                    {
                        TransitionKernelState(kernelState, CircuitBreakerState.HalfOpen, "Timeout expired");
                        LogCircuitHalfOpen(_logger, kernelState.KernelName, kernelState.AcceleratorType ?? "unknown");
                    }
                }

                // Check accelerator state transitions
                foreach (var acceleratorState in _acceleratorStates.Values)
                {
                    if (acceleratorState.State == CircuitBreakerState.Open &&
                        acceleratorState.NextRetryTime.HasValue &&
                        now >= acceleratorState.NextRetryTime.Value)
                    {
                        TransitionKernelState(acceleratorState, CircuitBreakerState.HalfOpen, "Timeout expired");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during kernel circuit breaker health check");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer.Dispose();
            _disposed = true;
            _logger.LogInfoMessage("KernelCircuitBreaker disposed");
        }
    }
}
