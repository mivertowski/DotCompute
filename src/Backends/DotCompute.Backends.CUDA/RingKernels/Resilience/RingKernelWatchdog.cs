// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.RingKernels.Resilience;

/// <summary>
/// Monitors ring kernel health and triggers recovery actions when issues are detected.
/// </summary>
/// <remarks>
/// The watchdog periodically checks kernel state and can detect:
/// - Kernel crashes (unexpected termination)
/// - Kernel stalls (no message processing activity)
/// - Heartbeat failures (kernel not responding)
///
/// When issues are detected, the watchdog triggers the configured recovery action.
/// </remarks>
public sealed class RingKernelWatchdog : IDisposable, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly RingKernelFaultRecoveryOptions _options;
    private readonly ConcurrentDictionary<string, KernelWatchdogState> _watchedKernels;
    private readonly CancellationTokenSource _watchdogCts;
    private readonly SemaphoreSlim _operationLock;
#pragma warning disable IDE0044 // Make field readonly - assigned conditionally in constructor
    private Task? _watchdogTask;
#pragma warning restore IDE0044
    private bool _disposed;

    /// <summary>
    /// Event raised when a kernel fault is detected.
    /// </summary>
    public event EventHandler<KernelFaultEventArgs>? KernelFaultDetected;

    /// <summary>
    /// Event raised when a kernel is successfully recovered.
    /// </summary>
    public event EventHandler<KernelRecoveryEventArgs>? KernelRecovered;

    /// <summary>
    /// Event raised when a kernel cannot be recovered and is marked as failed.
    /// </summary>
    public event EventHandler<KernelPermanentFailureEventArgs>? KernelPermanentlyFailed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RingKernelWatchdog"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="options">Watchdog configuration options.</param>
    public RingKernelWatchdog(ILogger logger, RingKernelFaultRecoveryOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? RingKernelFaultRecoveryOptions.Default;
        _options.Validate();

        _watchedKernels = new ConcurrentDictionary<string, KernelWatchdogState>();
        _watchdogCts = new CancellationTokenSource();
        _operationLock = new SemaphoreSlim(1, 1);

        if (_options.EnableWatchdog)
        {
            _watchdogTask = Task.Run(WatchdogLoopAsync, _watchdogCts.Token);
            _logger.LogInformation(
                "Ring kernel watchdog started with interval {Interval}, stall timeout {StallTimeout}",
                _options.WatchdogInterval, _options.KernelStallTimeout);
        }
    }

    /// <summary>
    /// Gets the current watchdog statistics.
    /// </summary>
    public WatchdogStatistics GetStatistics()
    {
        var states = _watchedKernels.Values.ToArray();
        return new WatchdogStatistics
        {
            WatchedKernelCount = states.Length,
            ActiveKernelCount = states.Count(s => s.IsActive),
            FailedKernelCount = states.Count(s => s.IsPermanentlyFailed),
            TotalRestartAttempts = states.Sum(s => s.RestartAttempts),
            TotalFaultsDetected = states.Sum(s => s.FaultsDetected),
            CircuitBreakerOpenCount = states.Count(s => s.CircuitBreakerState == CircuitBreakerState.Open),
            LastCheckTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Registers a kernel for watchdog monitoring.
    /// </summary>
    /// <param name="kernelId">Unique identifier for the kernel.</param>
    /// <param name="restartCallback">Callback to invoke when kernel restart is needed.</param>
    /// <param name="getStatusCallback">Callback to get current kernel status.</param>
    public void RegisterKernel(
        string kernelId,
        Func<CancellationToken, Task<bool>> restartCallback,
        Func<KernelHealthStatus> getStatusCallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ArgumentNullException.ThrowIfNull(restartCallback);
        ArgumentNullException.ThrowIfNull(getStatusCallback);

        var state = new KernelWatchdogState
        {
            KernelId = kernelId,
            RestartCallback = restartCallback,
            GetStatusCallback = getStatusCallback,
            LastActivityTime = DateTime.UtcNow,
            RegisteredTime = DateTime.UtcNow,
            IsActive = true,
            CircuitBreakerState = CircuitBreakerState.Closed
        };

        if (_watchedKernels.TryAdd(kernelId, state))
        {
            _logger.LogDebug("Registered kernel '{KernelId}' for watchdog monitoring", kernelId);
        }
    }

    /// <summary>
    /// Unregisters a kernel from watchdog monitoring.
    /// </summary>
    /// <param name="kernelId">The kernel identifier to unregister.</param>
    /// <returns>True if the kernel was unregistered; false if not found.</returns>
    public bool UnregisterKernel(string kernelId)
    {
        if (_watchedKernels.TryRemove(kernelId, out _))
        {
            _logger.LogDebug("Unregistered kernel '{KernelId}' from watchdog monitoring", kernelId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reports activity for a kernel, resetting the stall timeout.
    /// </summary>
    /// <param name="kernelId">The kernel identifier.</param>
    /// <param name="messagesProcessed">Number of messages processed since last report.</param>
    public void ReportActivity(string kernelId, long messagesProcessed = 0)
    {
        if (_watchedKernels.TryGetValue(kernelId, out var state))
        {
            state.LastActivityTime = DateTime.UtcNow;
            state.MessagesProcessed += messagesProcessed;

            // Reset restart count if kernel has been running successfully
            if (state.RestartAttempts > 0 &&
                state.LastRestartTime.HasValue &&
                DateTime.UtcNow - state.LastRestartTime.Value > _options.SuccessfulRunThreshold)
            {
                state.RestartAttempts = 0;
                _logger.LogDebug("Reset restart count for kernel '{KernelId}' after successful run", kernelId);
            }
        }
    }

    /// <summary>
    /// Gets the circuit breaker state for a specific kernel.
    /// </summary>
    /// <param name="kernelId">The kernel identifier.</param>
    /// <returns>The circuit breaker state, or null if kernel is not registered.</returns>
    public CircuitBreakerState? GetCircuitBreakerState(string kernelId)
    {
        return _watchedKernels.TryGetValue(kernelId, out var state) ? state.CircuitBreakerState : null;
    }

    /// <summary>
    /// Forces the circuit breaker to trip for a kernel.
    /// </summary>
    /// <param name="kernelId">The kernel identifier.</param>
    /// <param name="reason">Reason for tripping the circuit breaker.</param>
    public void TripCircuitBreaker(string kernelId, string reason)
    {
        if (_watchedKernels.TryGetValue(kernelId, out var state))
        {
            state.CircuitBreakerState = CircuitBreakerState.Open;
            state.CircuitBreakerOpenedTime = DateTime.UtcNow;
            _logger.LogWarning("Circuit breaker tripped for kernel '{KernelId}': {Reason}", kernelId, reason);
        }
    }

    /// <summary>
    /// Resets the circuit breaker for a kernel, allowing it to accept new operations.
    /// </summary>
    /// <param name="kernelId">The kernel identifier.</param>
    public void ResetCircuitBreaker(string kernelId)
    {
        if (_watchedKernels.TryGetValue(kernelId, out var state))
        {
            state.CircuitBreakerState = CircuitBreakerState.Closed;
            state.CircuitBreakerOpenedTime = null;
            state.FailuresInWindow.Clear();
            _logger.LogInformation("Circuit breaker reset for kernel '{KernelId}'", kernelId);
        }
    }

    private async Task WatchdogLoopAsync()
    {
        _logger.LogDebug("Watchdog loop started");

        while (!_watchdogCts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.WatchdogInterval, _watchdogCts.Token);
                await CheckAllKernelsAsync(_watchdogCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in watchdog loop");
                // Continue monitoring despite errors
            }
        }

        _logger.LogDebug("Watchdog loop stopped");
    }

    private async Task CheckAllKernelsAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _watchedKernels)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await CheckKernelHealthAsync(kvp.Key, kvp.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for kernel '{KernelId}'", kvp.Key);
            }
        }
    }

    private async Task CheckKernelHealthAsync(string kernelId, KernelWatchdogState state, CancellationToken cancellationToken)
    {
        // Skip permanently failed kernels
        if (state.IsPermanentlyFailed)
        {
            return;
        }

        // Handle circuit breaker state
        if (state.CircuitBreakerState == CircuitBreakerState.Open)
        {
            if (state.CircuitBreakerOpenedTime.HasValue &&
                DateTime.UtcNow - state.CircuitBreakerOpenedTime.Value > _options.CircuitBreakerOpenDuration)
            {
                state.CircuitBreakerState = CircuitBreakerState.HalfOpen;
                _logger.LogInformation("Circuit breaker half-open for kernel '{KernelId}'", kernelId);
            }
            else
            {
                return; // Skip checks while circuit is open
            }
        }

        // Clean up old failures from the tracking window
        var windowStart = DateTime.UtcNow - _options.FailureTrackingWindow;
        while (state.FailuresInWindow.TryPeek(out var oldest) && oldest < windowStart)
        {
            state.FailuresInWindow.TryDequeue(out _);
        }

        // Get current kernel status
        var status = state.GetStatusCallback();

        var fault = DetectFault(state, status);
        if (fault.HasValue)
        {
            await HandleFaultAsync(kernelId, state, fault.Value, cancellationToken);
        }
        else if (state.CircuitBreakerState == CircuitBreakerState.HalfOpen)
        {
            // Kernel is healthy, close the circuit breaker
            state.CircuitBreakerState = CircuitBreakerState.Closed;
            _logger.LogInformation("Circuit breaker closed for kernel '{KernelId}'", kernelId);
        }
    }

    private KernelFaultType? DetectFault(KernelWatchdogState state, KernelHealthStatus status)
    {
        // Check for crash
        if (!status.IsRunning && state.IsActive)
        {
            return KernelFaultType.Crash;
        }

        // Check for stall (no activity)
        if (status.IsRunning &&
            DateTime.UtcNow - state.LastActivityTime > _options.KernelStallTimeout)
        {
            return KernelFaultType.Stall;
        }

        // Check for heartbeat failure
        if (status.IsRunning &&
            status.LastHeartbeatTime.HasValue &&
            DateTime.UtcNow - status.LastHeartbeatTime.Value > _options.HeartbeatTimeout)
        {
            return KernelFaultType.HeartbeatFailure;
        }

        return null;
    }

    private async Task HandleFaultAsync(
        string kernelId,
        KernelWatchdogState state,
        KernelFaultType faultType,
        CancellationToken cancellationToken)
    {
        state.FaultsDetected++;
        state.LastFaultTime = DateTime.UtcNow;
        state.LastFaultType = faultType;
        state.FailuresInWindow.Enqueue(DateTime.UtcNow);

        _logger.LogWarning(
            "Fault detected for kernel '{KernelId}': {FaultType} (attempt {Attempt}/{Max})",
            kernelId, faultType, state.RestartAttempts + 1, _options.MaxRestartAttempts);

        // Raise fault event
        KernelFaultDetected?.Invoke(this, new KernelFaultEventArgs
        {
            KernelId = kernelId,
            FaultType = faultType,
            FaultTime = DateTime.UtcNow,
            RestartAttempt = state.RestartAttempts + 1,
            MaxRestartAttempts = _options.MaxRestartAttempts
        });

        // Check circuit breaker threshold
        if (state.FailuresInWindow.Count >= _options.CircuitBreakerFailureThreshold)
        {
            state.CircuitBreakerState = CircuitBreakerState.Open;
            state.CircuitBreakerOpenedTime = DateTime.UtcNow;
            _logger.LogWarning(
                "Circuit breaker opened for kernel '{KernelId}' after {Failures} failures",
                kernelId, state.FailuresInWindow.Count);
            return;
        }

        // Attempt restart if enabled and under limit
        if (_options.EnableAutoRestart && state.RestartAttempts < _options.MaxRestartAttempts)
        {
            await AttemptRestartAsync(kernelId, state, cancellationToken);
        }
        else if (state.RestartAttempts >= _options.MaxRestartAttempts)
        {
            MarkAsPermanentlyFailed(kernelId, state);
        }
    }

    private async Task AttemptRestartAsync(
        string kernelId,
        KernelWatchdogState state,
        CancellationToken cancellationToken)
    {
        state.RestartAttempts++;
        state.LastRestartTime = DateTime.UtcNow;

        // Calculate delay with exponential backoff
        var delay = _options.RestartDelay;
        if (_options.UseExponentialBackoff && state.RestartAttempts > 1)
        {
            delay = TimeSpan.FromMilliseconds(
                Math.Min(
                    _options.RestartDelay.TotalMilliseconds * Math.Pow(2, state.RestartAttempts - 1),
                    _options.MaxRestartDelay.TotalMilliseconds));
        }

        _logger.LogInformation(
            "Attempting restart for kernel '{KernelId}' (attempt {Attempt}/{Max}) after {Delay}ms delay",
            kernelId, state.RestartAttempts, _options.MaxRestartAttempts, delay.TotalMilliseconds);

        await Task.Delay(delay, cancellationToken);

        try
        {
            var success = await state.RestartCallback(cancellationToken);

            if (success)
            {
                state.IsActive = true;
                state.LastActivityTime = DateTime.UtcNow;

                if (_options.ResetFailuresOnSuccess)
                {
                    state.FailuresInWindow.Clear();
                }

                _logger.LogInformation("Successfully restarted kernel '{KernelId}'", kernelId);

                KernelRecovered?.Invoke(this, new KernelRecoveryEventArgs
                {
                    KernelId = kernelId,
                    RecoveryTime = DateTime.UtcNow,
                    RestartAttempts = state.RestartAttempts,
                    FaultType = state.LastFaultType ?? KernelFaultType.Unknown
                });
            }
            else
            {
                _logger.LogWarning("Restart callback returned false for kernel '{KernelId}'", kernelId);

                if (state.RestartAttempts >= _options.MaxRestartAttempts)
                {
                    MarkAsPermanentlyFailed(kernelId, state);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restart failed for kernel '{KernelId}'", kernelId);

            if (state.RestartAttempts >= _options.MaxRestartAttempts)
            {
                MarkAsPermanentlyFailed(kernelId, state);
            }
        }
    }

    private void MarkAsPermanentlyFailed(string kernelId, KernelWatchdogState state)
    {
        state.IsPermanentlyFailed = true;
        state.IsActive = false;
        state.PermanentFailureTime = DateTime.UtcNow;

        _logger.LogError(
            "Kernel '{KernelId}' permanently failed after {Attempts} restart attempts",
            kernelId, state.RestartAttempts);

        KernelPermanentlyFailed?.Invoke(this, new KernelPermanentFailureEventArgs
        {
            KernelId = kernelId,
            FailureTime = DateTime.UtcNow,
            RestartAttempts = state.RestartAttempts,
            LastFaultType = state.LastFaultType ?? KernelFaultType.Unknown,
            TotalFaultsDetected = state.FaultsDetected
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Suppress VSTHRD103 - synchronous Cancel is intentional in Dispose
#pragma warning disable VSTHRD103
            _watchdogCts.Cancel();
#pragma warning restore VSTHRD103
            _watchdogTask?.Wait(TimeSpan.FromSeconds(5));
            _watchdogCts.Dispose();
            _operationLock.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _watchdogCts.CancelAsync();
            if (_watchdogTask != null)
            {
                try
                {
                    await _watchdogTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Watchdog task did not complete within timeout");
                }
                catch (TaskCanceledException)
                {
                    // Expected when watchdog is cancelled
                }
                catch (OperationCanceledException)
                {
                    // Expected when watchdog is cancelled
                }
            }
            _watchdogCts.Dispose();
            _operationLock.Dispose();
            _disposed = true;
        }
    }

    private sealed class KernelWatchdogState
    {
        public required string KernelId { get; init; }
        public required Func<CancellationToken, Task<bool>> RestartCallback { get; init; }
        public required Func<KernelHealthStatus> GetStatusCallback { get; init; }
        public DateTime RegisteredTime { get; init; }
        public DateTime LastActivityTime { get; set; }
        public DateTime? LastRestartTime { get; set; }
        public DateTime? LastFaultTime { get; set; }
        public DateTime? PermanentFailureTime { get; set; }
        public KernelFaultType? LastFaultType { get; set; }
        public int RestartAttempts { get; set; }
        public long FaultsDetected { get; set; }
        public long MessagesProcessed { get; set; }
        public bool IsActive { get; set; }
        public bool IsPermanentlyFailed { get; set; }
        public CircuitBreakerState CircuitBreakerState { get; set; }
        public DateTime? CircuitBreakerOpenedTime { get; set; }
        public ConcurrentQueue<DateTime> FailuresInWindow { get; } = new();
    }
}

/// <summary>
/// Types of kernel faults that can be detected.
/// </summary>
public enum KernelFaultType
{
    /// <summary>
    /// Unknown fault type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Kernel crashed unexpectedly.
    /// </summary>
    Crash = 1,

    /// <summary>
    /// Kernel stalled (no activity within timeout).
    /// </summary>
    Stall = 2,

    /// <summary>
    /// Kernel failed to respond to heartbeat.
    /// </summary>
    HeartbeatFailure = 3,

    /// <summary>
    /// CUDA error occurred during kernel execution.
    /// </summary>
    CudaError = 4,

    /// <summary>
    /// Out of memory error.
    /// </summary>
    OutOfMemory = 5
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed, normal operation.
    /// </summary>
    Closed = 0,

    /// <summary>
    /// Circuit is open due to failures, blocking new operations.
    /// </summary>
    Open = 1,

    /// <summary>
    /// Circuit is half-open, testing if operations succeed.
    /// </summary>
    HalfOpen = 2
}

/// <summary>
/// Current health status of a kernel.
/// </summary>
public sealed class KernelHealthStatus
{
    /// <summary>
    /// Gets or sets whether the kernel is currently running.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Gets or sets the last heartbeat time from the kernel.
    /// </summary>
    public DateTime? LastHeartbeatTime { get; set; }

    /// <summary>
    /// Gets or sets the number of messages processed.
    /// </summary>
    public long MessagesProcessed { get; set; }

    /// <summary>
    /// Gets or sets any error code from the kernel.
    /// </summary>
    public int ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message if any.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Watchdog statistics.
/// </summary>
public sealed class WatchdogStatistics
{
    /// <summary>
    /// Gets or sets the number of kernels being watched.
    /// </summary>
    public int WatchedKernelCount { get; init; }

    /// <summary>
    /// Gets or sets the number of active kernels.
    /// </summary>
    public int ActiveKernelCount { get; init; }

    /// <summary>
    /// Gets or sets the number of permanently failed kernels.
    /// </summary>
    public int FailedKernelCount { get; init; }

    /// <summary>
    /// Gets or sets the total restart attempts across all kernels.
    /// </summary>
    public long TotalRestartAttempts { get; init; }

    /// <summary>
    /// Gets or sets the total faults detected across all kernels.
    /// </summary>
    public long TotalFaultsDetected { get; init; }

    /// <summary>
    /// Gets or sets the number of kernels with open circuit breakers.
    /// </summary>
    public int CircuitBreakerOpenCount { get; init; }

    /// <summary>
    /// Gets or sets the last check time.
    /// </summary>
    public DateTime LastCheckTime { get; init; }
}

/// <summary>
/// Event arguments for kernel fault detection.
/// </summary>
public sealed class KernelFaultEventArgs : EventArgs
{
    /// <summary>
    /// Gets the kernel identifier.
    /// </summary>
    public required string KernelId { get; init; }

    /// <summary>
    /// Gets the type of fault detected.
    /// </summary>
    public required KernelFaultType FaultType { get; init; }

    /// <summary>
    /// Gets the time the fault was detected.
    /// </summary>
    public required DateTime FaultTime { get; init; }

    /// <summary>
    /// Gets the restart attempt number.
    /// </summary>
    public required int RestartAttempt { get; init; }

    /// <summary>
    /// Gets the maximum restart attempts allowed.
    /// </summary>
    public required int MaxRestartAttempts { get; init; }
}

/// <summary>
/// Event arguments for kernel recovery.
/// </summary>
public sealed class KernelRecoveryEventArgs : EventArgs
{
    /// <summary>
    /// Gets the kernel identifier.
    /// </summary>
    public required string KernelId { get; init; }

    /// <summary>
    /// Gets the time of recovery.
    /// </summary>
    public required DateTime RecoveryTime { get; init; }

    /// <summary>
    /// Gets the number of restart attempts before success.
    /// </summary>
    public required int RestartAttempts { get; init; }

    /// <summary>
    /// Gets the fault type that triggered recovery.
    /// </summary>
    public required KernelFaultType FaultType { get; init; }
}

/// <summary>
/// Event arguments for permanent kernel failure.
/// </summary>
public sealed class KernelPermanentFailureEventArgs : EventArgs
{
    /// <summary>
    /// Gets the kernel identifier.
    /// </summary>
    public required string KernelId { get; init; }

    /// <summary>
    /// Gets the time of permanent failure.
    /// </summary>
    public required DateTime FailureTime { get; init; }

    /// <summary>
    /// Gets the number of restart attempts before giving up.
    /// </summary>
    public required int RestartAttempts { get; init; }

    /// <summary>
    /// Gets the last fault type before permanent failure.
    /// </summary>
    public required KernelFaultType LastFaultType { get; init; }

    /// <summary>
    /// Gets the total number of faults detected for this kernel.
    /// </summary>
    public required long TotalFaultsDetected { get; init; }
}
