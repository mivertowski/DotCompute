// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Unified base recovery strategy that consolidates common error recovery patterns
/// across all backend implementations (CUDA, Metal, OpenCL, CPU, Memory, GPU).
/// Eliminates over 1,800 lines of duplicate recovery code.
/// </summary>
/// <typeparam name="TContext">The specific context type for this recovery strategy</typeparam>
public abstract class BaseRecoveryStrategy<TContext> : IRecoveryStrategy<TContext>, IDisposable
    where TContext : class
{
    protected readonly ILogger Logger;
    private readonly ConcurrentDictionary<string, RecoveryAttemptHistory> _recoveryHistory;
    private readonly SemaphoreSlim _recoveryLock;
    private readonly Timer? _cleanupTimer;

    // Performance and metrics tracking
    private long _totalRecoveryAttempts;
    private long _successfulRecoveries;
    private long _failedRecoveries;
    private readonly ConcurrentQueue<RecoveryMetric> _recentMetrics;

    private volatile bool _disposed;

    protected BaseRecoveryStrategy(ILogger logger, TimeSpan? cleanupInterval = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _recoveryHistory = new ConcurrentDictionary<string, RecoveryAttemptHistory>();
        _recoveryLock = new SemaphoreSlim(1, 1);
        _recentMetrics = new ConcurrentQueue<RecoveryMetric>();

        // Start cleanup timer if specified
        if (cleanupInterval.HasValue && cleanupInterval.Value > TimeSpan.Zero)
        {
            _cleanupTimer = new Timer(PerformCleanup, null, cleanupInterval.Value, cleanupInterval.Value);
        }

        Logger.LogDebug("Recovery strategy {StrategyType} initialized", GetType().Name);
    }

    #region Abstract Properties and Methods

    /// <summary>
    /// Gets the recovery capability of this strategy.
    /// </summary>
    public abstract RecoveryCapability Capability { get; }

    /// <summary>
    /// Gets the priority of this recovery strategy (higher values = higher priority).
    /// </summary>
    public abstract int Priority { get; }

    /// <summary>
    /// Determines if this strategy can handle the given error and context.
    /// </summary>
    public abstract bool CanHandle(Exception error, TContext context);

    /// <summary>
    /// Performs the actual recovery operation specific to the implementing strategy.
    /// </summary>
    public abstract Task<RecoveryResult> RecoverAsync(
        Exception error,
        TContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken = default);

    #endregion

    #region Core Recovery Logic

    /// <summary>
    /// Attempts recovery with comprehensive error handling, retry logic, and metrics tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // Ensure proper stack traces for debugging
    public async Task<RecoveryResult> AttemptRecoveryAsync(
        Exception error,
        TContext context,
        RecoveryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (error == null)
        {
            throw new ArgumentNullException(nameof(error));
        }


        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }


        var recoveryOptions = options ?? RecoveryOptions.Default;
        var stopwatch = Stopwatch.StartNew();
        var contextKey = GetContextKey(context);

        _ = Interlocked.Increment(ref _totalRecoveryAttempts);

        Logger.LogInformation("Starting recovery attempt for error: {ErrorType} with strategy: {StrategyType}",
            error.GetType().Name, GetType().Name);

        await _recoveryLock.WaitAsync(cancellationToken);

        try
        {
            // Check if we should attempt recovery based on history
            if (!ShouldAttemptRecovery(error, context, recoveryOptions))
            {
                var result = CreateFailureResult("Recovery skipped due to rate limiting or consecutive failures", error);
                RecordRecoveryAttempt(contextKey, result, stopwatch.Elapsed);
                return result;
            }

            // Perform pre-recovery validation and preparation
            await PrepareForRecoveryAsync(error, context, recoveryOptions, cancellationToken);

            // Execute the actual recovery
            var recoveryResult = await ExecuteRecoveryWithRetriesAsync(error, context, recoveryOptions, cancellationToken);

            // Post-recovery validation and cleanup
            await PostRecoveryValidationAsync(recoveryResult, context, cancellationToken);

            stopwatch.Stop();
            recoveryResult.Duration = stopwatch.Elapsed;

            // Record the attempt
            RecordRecoveryAttempt(contextKey, recoveryResult, stopwatch.Elapsed);

            if (recoveryResult.Success)
            {
                _ = Interlocked.Increment(ref _successfulRecoveries);
                Logger.LogInformation("Recovery successful for {ErrorType} in {Duration}ms",
                    error.GetType().Name, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _ = Interlocked.Increment(ref _failedRecoveries);
                Logger.LogWarning("Recovery failed for {ErrorType}: {Message}",
                    error.GetType().Name, recoveryResult.Message);
            }

            return recoveryResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var result = CreateFailureResult("Recovery cancelled", error);
            result.Duration = stopwatch.Elapsed;
            RecordRecoveryAttempt(contextKey, result, stopwatch.Elapsed);
            _ = Interlocked.Increment(ref _failedRecoveries);
            return result;
        }
        catch (Exception recoveryException)
        {
            stopwatch.Stop();
            Logger.LogError(recoveryException, "Exception during recovery of {ErrorType}", error.GetType().Name);

            var result = CreateFailureResult($"Recovery failed with exception: {recoveryException.Message}",
                error, recoveryException);
            result.Duration = stopwatch.Elapsed;
            RecordRecoveryAttempt(contextKey, result, stopwatch.Elapsed);
            _ = Interlocked.Increment(ref _failedRecoveries);
            return result;
        }
        finally
        {
            _ = _recoveryLock.Release();
        }
    }

    /// <summary>
    /// Executes recovery with configurable retry logic and exponential backoff.
    /// </summary>
    protected virtual async Task<RecoveryResult> ExecuteRecoveryWithRetriesAsync(
        Exception error,
        TContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken)
    {
        var maxRetries = options.MaxRetryAttempts;
        var baseDelay = options.RetryDelay;
        var lastException = error;

        for (var attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                Logger.LogDebug("Recovery attempt {Attempt}/{MaxAttempts} for {ErrorType}",
                    attempt, maxRetries + 1, error.GetType().Name);

                var result = await RecoverAsync(error, context, options, cancellationToken);

                if (result.Success || attempt > maxRetries)
                {
                    return result;
                }

                lastException = result.Exception ?? error;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt > maxRetries)
                {
                    Logger.LogError(ex, "Recovery failed after {MaxRetries} attempts", maxRetries);
                    throw;
                }
            }

            // Apply exponential backoff if we're going to retry
            if (attempt <= maxRetries)
            {
                var delay = CalculateRetryDelay(baseDelay, attempt, options.UseExponentialBackoff);
                Logger.LogDebug("Waiting {Delay}ms before retry attempt {NextAttempt}",
                    delay.TotalMilliseconds, attempt + 1);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // If we get here, all retries failed
        return CreateFailureResult(
            $"Recovery failed after {maxRetries} retries. Last error: {lastException.Message}",
            error, lastException);
    }

    #endregion

    #region Recovery Decision Logic

    /// <summary>
    /// Determines if recovery should be attempted based on history and options.
    /// </summary>
    protected virtual bool ShouldAttemptRecovery(Exception error, TContext context, RecoveryOptions options)
    {
        var contextKey = GetContextKey(context);

        if (!_recoveryHistory.TryGetValue(contextKey, out var history))
        {

            return true; // No history, allow recovery
        }

        // Check rate limiting

        if (IsRateLimited(history, options))
        {
            Logger.LogDebug("Recovery rate limited for context {ContextKey}", contextKey);
            return false;
        }

        // Check consecutive failure threshold
        if (history.ConsecutiveFailures >= options.MaxConsecutiveFailures)
        {
            Logger.LogWarning("Maximum consecutive failures ({MaxFailures}) reached for context {ContextKey}",
                options.MaxConsecutiveFailures, contextKey);
            return false;
        }

        // Check error-specific conditions
        return ShouldAttemptRecoveryForError(error, history, options);
    }

    /// <summary>
    /// Checks if recovery attempts are rate limited.
    /// </summary>
    protected virtual bool IsRateLimited(RecoveryAttemptHistory history, RecoveryOptions options)
    {
        if (options.RateLimitWindow <= TimeSpan.Zero)
        {

            return false;
        }


        var cutoff = DateTimeOffset.UtcNow - options.RateLimitWindow;
        var recentAttempts = history.RecentAttempts.Count(a => a.Timestamp > cutoff);

        return recentAttempts >= options.MaxAttemptsPerWindow;
    }

    /// <summary>
    /// Override for error-specific recovery decision logic.
    /// </summary>
    protected virtual bool ShouldAttemptRecoveryForError(Exception error, RecoveryAttemptHistory history, RecoveryOptions options)
        // Base implementation allows recovery for all errors
        // Override in derived classes for specific error handling
        => true;

    #endregion

    #region Recovery Lifecycle Hooks

    /// <summary>
    /// Prepares for recovery - override for strategy-specific preparation.
    /// </summary>
    protected virtual Task PrepareForRecoveryAsync(
        Exception error,
        TContext context,
        RecoveryOptions options,
        CancellationToken cancellationToken)
        // Base implementation does nothing
        // Override in derived classes for preparation logic
        => Task.CompletedTask;

    /// <summary>
    /// Validates recovery result and performs cleanup - override for strategy-specific validation.
    /// </summary>
    protected virtual Task PostRecoveryValidationAsync(
        RecoveryResult result,
        TContext context,
        CancellationToken cancellationToken)
        // Base implementation does nothing
        // Override in derived classes for validation logic
        => Task.CompletedTask;

    #endregion

    #region Metrics and Statistics

    /// <summary>
    /// Gets comprehensive recovery statistics for this strategy.
    /// </summary>
    public virtual RecoveryStatistics GetStatistics()
    {
        var totalAttempts = Interlocked.Read(ref _totalRecoveryAttempts);
        var successful = Interlocked.Read(ref _successfulRecoveries);
        var failed = Interlocked.Read(ref _failedRecoveries);

        return new RecoveryStatistics
        {
            StrategyType = GetType().Name,
            TotalAttempts = totalAttempts,
            SuccessfulRecoveries = successful,
            FailedRecoveries = failed,
            SuccessRate = totalAttempts > 0 ? (double)successful / totalAttempts : 0.0,
            AverageRecoveryTime = CalculateAverageRecoveryTime(),
            ActiveContexts = _recoveryHistory.Count,
            RecentMetricsCount = _recentMetrics.Count
        };
    }

    /// <summary>
    /// Gets recovery history for a specific context.
    /// </summary>
    public virtual RecoveryAttemptHistory? GetRecoveryHistory(TContext context)
    {
        var contextKey = GetContextKey(context);
        return _recoveryHistory.TryGetValue(contextKey, out var history) ? history : null;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a context key for tracking recovery history.
    /// Override for context-specific key generation.
    /// </summary>
    protected virtual string GetContextKey(TContext context) => context.GetHashCode().ToString();

    /// <summary>
    /// Creates a failure result with consistent formatting.
    /// </summary>
    protected virtual RecoveryResult CreateFailureResult(string message, Exception originalError, Exception? recoveryException = null)
    {
        return new RecoveryResult
        {
            Success = false,
            Message = message,
            Exception = recoveryException ?? originalError,
            Strategy = GetType().Name,
            RequiresManualIntervention = IsManualInterventionRequired(originalError, recoveryException)
        };
    }

    /// <summary>
    /// Creates a success result with consistent formatting.
    /// </summary>
    protected virtual RecoveryResult CreateSuccessResult(string message, TimeSpan duration)
    {
        return new RecoveryResult
        {
            Success = true,
            Message = message,
            Strategy = GetType().Name,
            Duration = duration
        };
    }

    /// <summary>
    /// Convenience method to create a successful recovery result.
    /// </summary>
    /// <param name="message">The success message.</param>
    /// <param name="duration">The duration of the recovery operation.</param>
    /// <returns>A successful recovery result.</returns>
    protected RecoveryResult Success(string message, TimeSpan duration) => CreateSuccessResult(message, duration);

    /// <summary>
    /// Convenience method to create a failed recovery result.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <returns>A failed recovery result.</returns>
    protected RecoveryResult Failure(string message) => CreateFailureResult(message, new InvalidOperationException(message));

    /// <summary>
    /// Convenience method to create a failed recovery result with an exception.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed recovery result.</returns>
    protected RecoveryResult Failure(string message, Exception exception) => CreateFailureResult(message, exception);

    /// <summary>
    /// Convenience method to create a failed recovery result with an exception and duration.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="duration">The duration before the failure occurred.</param>
    /// <returns>A failed recovery result.</returns>
    protected RecoveryResult Failure(string message, Exception exception, TimeSpan duration)
    {
        var result = CreateFailureResult(message, exception);
        result.Duration = duration;
        return result;
    }

    /// <summary>
    /// Determines if manual intervention is required based on the error types.
    /// </summary>
    protected virtual bool IsManualInterventionRequired(Exception originalError, Exception? recoveryException)
        // Override in derived classes for specific logic
        => recoveryException != null || originalError is SystemException;

    /// <summary>
    /// Calculates retry delay with optional exponential backoff.
    /// </summary>
    protected virtual TimeSpan CalculateRetryDelay(TimeSpan baseDelay, int attemptNumber, bool useExponentialBackoff)
    {
        if (!useExponentialBackoff)
        {

            return baseDelay;
        }


        var multiplier = Math.Pow(2, attemptNumber - 1);
        var delayMs = baseDelay.TotalMilliseconds * multiplier;

        // Add jitter to prevent thundering herd
        var jitter = Random.Shared.NextDouble() * 0.1; // ±10% jitter
        delayMs *= (1.0 + jitter);

        // Cap at reasonable maximum
        var maxDelayMs = Math.Min(delayMs, 30000); // 30 seconds max

        return TimeSpan.FromMilliseconds(maxDelayMs);
    }

    /// <summary>
    /// Records a recovery attempt in the history.
    /// </summary>
    protected virtual void RecordRecoveryAttempt(string contextKey, RecoveryResult result, TimeSpan duration)
    {
        var attempt = new RecoveryAttempt
        {
            Timestamp = DateTimeOffset.UtcNow,
            Success = result.Success,
            Duration = duration,
            Message = result.Message,
            Strategy = GetType().Name
        };

        var history = _recoveryHistory.GetOrAdd(contextKey, _ => new RecoveryAttemptHistory(contextKey));
        history.AddAttempt(attempt);

        // Record metric for performance tracking
        var metric = new RecoveryMetric
        {
            Timestamp = attempt.Timestamp,
            StrategyType = GetType().Name,
            Success = result.Success,
            Duration = duration,
            ContextKey = contextKey
        };

        _recentMetrics.Enqueue(metric);

        // Limit recent metrics queue size
        while (_recentMetrics.Count > 1000)
        {
            _ = _recentMetrics.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Calculates average recovery time from recent metrics.
    /// </summary>
    protected virtual TimeSpan CalculateAverageRecoveryTime()
    {
        var recentMetrics = _recentMetrics.ToArray();
        if (recentMetrics.Length == 0)
        {

            return TimeSpan.Zero;
        }


        var averageTicks = recentMetrics.Average(m => m.Duration.Ticks);
        return TimeSpan.FromTicks((long)averageTicks);
    }

    /// <summary>
    /// Performs periodic cleanup of old recovery history and metrics.
    /// </summary>
    protected virtual void PerformCleanup(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-24); // Keep 24 hours of history

            // Clean up old recovery history
            var keysToRemove = _recoveryHistory
                .Where(kvp => kvp.Value.LastAttempt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _ = _recoveryHistory.TryRemove(key, out _);
            }

            // Clean up old metrics
            var metricsToKeep = new List<RecoveryMetric>();
            while (_recentMetrics.TryDequeue(out var metric))
            {
                if (metric.Timestamp > cutoff)
                {
                    metricsToKeep.Add(metric);
                }
            }

            foreach (var metric in metricsToKeep)
            {
                _recentMetrics.Enqueue(metric);
            }

            if (keysToRemove.Count > 0)
            {
                Logger.LogDebug("Cleanup completed: removed {Count} old recovery contexts", keysToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during recovery strategy cleanup");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if this instance has been disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(GetType().Name);
        }

    }

    #endregion

    #region Disposal

    /// <summary>
    /// Disposes the recovery strategy and releases all resources.
    /// </summary>
    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Log final statistics
            var stats = GetStatistics();
            Logger.LogInformation(
                "Recovery strategy {StrategyType} disposed - Attempts: {Attempts}, Success Rate: {SuccessRate:P2}",
                stats.StrategyType, stats.TotalAttempts, stats.SuccessRate);

            // Dispose resources
            _cleanupTimer?.Dispose();
            _recoveryLock?.Dispose();

            // Clear collections
            _recoveryHistory.Clear();
            while (_recentMetrics.TryDequeue(out _)) { }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Asynchronous disposal method.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            // Await any pending recovery operations
            _ = await _recoveryLock.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                Dispose();
            }
            finally
            {
                _ = _recoveryLock.Release();
            }
        }
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Tracks recovery attempt history for a specific context.
/// </summary>
public sealed class RecoveryAttemptHistory(string contextKey)
{
    private readonly object _lock = new();
    private readonly Queue<RecoveryAttempt> _attempts = new();
    private const int MaxHistorySize = 100;
    /// <summary>
    /// Gets or sets the context key.
    /// </summary>
    /// <value>The context key.</value>

    public string ContextKey { get; } = contextKey;
    /// <summary>
    /// Gets or sets the last attempt.
    /// </summary>
    /// <value>The last attempt.</value>
    public DateTimeOffset LastAttempt { get; private set; }
    /// <summary>
    /// Gets or sets the consecutive failures.
    /// </summary>
    /// <value>The consecutive failures.</value>
    public int ConsecutiveFailures { get; private set; }
    /// <summary>
    /// Gets or sets the total attempts.
    /// </summary>
    /// <value>The total attempts.</value>
    public int TotalAttempts { get; private set; }
    /// <summary>
    /// Gets or sets the total successes.
    /// </summary>
    /// <value>The total successes.</value>
    public int TotalSuccesses { get; private set; }
    /// <summary>
    /// Gets or sets the recent attempts.
    /// </summary>
    /// <value>The recent attempts.</value>

    public IEnumerable<RecoveryAttempt> RecentAttempts
    {
        get
        {
            lock (_lock)
            {
                return _attempts.ToArray();
            }
        }
    }
    /// <summary>
    /// Performs add attempt.
    /// </summary>
    /// <param name="attempt">The attempt.</param>

    public void AddAttempt(RecoveryAttempt attempt)
    {
        lock (_lock)
        {
            _attempts.Enqueue(attempt);
            LastAttempt = attempt.Timestamp;
            TotalAttempts++;

            if (attempt.Success)
            {
                ConsecutiveFailures = 0;
                TotalSuccesses++;
            }
            else
            {
                ConsecutiveFailures++;
            }

            // Limit history size
            while (_attempts.Count > MaxHistorySize)
            {
                _ = _attempts.Dequeue();
            }
        }
    }
    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    /// <value>The success rate.</value>

    public double SuccessRate => TotalAttempts > 0 ? (double)TotalSuccesses / TotalAttempts : 0.0;
}

/// <summary>
/// Represents a single recovery attempt.
/// </summary>
public sealed class RecoveryAttempt
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public required DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public required bool Success { get; init; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public required TimeSpan Duration { get; init; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public required string Message { get; init; }
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public required string Strategy { get; init; }
}

/// <summary>
/// Performance metric for recovery operations.
/// </summary>
internal sealed class RecoveryMetric
{
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public required DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the strategy type.
    /// </summary>
    /// <value>The strategy type.</value>
    public required string StrategyType { get; init; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public required bool Success { get; init; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public required TimeSpan Duration { get; init; }
    /// <summary>
    /// Gets or sets the context key.
    /// </summary>
    /// <value>The context key.</value>
    public required string ContextKey { get; init; }
}

/// <summary>
/// Comprehensive recovery statistics.
/// </summary>
public sealed class RecoveryStatistics
{
    /// <summary>
    /// Gets or sets the strategy type.
    /// </summary>
    /// <value>The strategy type.</value>
    public required string StrategyType { get; init; }
    /// <summary>
    /// Gets or sets the total attempts.
    /// </summary>
    /// <value>The total attempts.</value>
    public required long TotalAttempts { get; init; }
    /// <summary>
    /// Gets or sets the successful recoveries.
    /// </summary>
    /// <value>The successful recoveries.</value>
    public required long SuccessfulRecoveries { get; init; }
    /// <summary>
    /// Gets or sets the failed recoveries.
    /// </summary>
    /// <value>The failed recoveries.</value>
    public required long FailedRecoveries { get; init; }
    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    /// <value>The success rate.</value>
    public required double SuccessRate { get; init; }
    /// <summary>
    /// Gets or sets the average recovery time.
    /// </summary>
    /// <value>The average recovery time.</value>
    public required TimeSpan AverageRecoveryTime { get; init; }
    /// <summary>
    /// Gets or sets the active contexts.
    /// </summary>
    /// <value>The active contexts.</value>
    public required int ActiveContexts { get; init; }
    /// <summary>
    /// Gets or sets the recent metrics count.
    /// </summary>
    /// <value>The recent metrics count.</value>
    public required int RecentMetricsCount { get; init; }
}

/// <summary>
/// Recovery options configuration.
/// </summary>
public sealed class RecoveryOptions
{
    public static readonly RecoveryOptions Default = new();
    /// <summary>
    /// Gets or sets the max retry attempts.
    /// </summary>
    /// <value>The max retry attempts.</value>

    public int MaxRetryAttempts { get; init; } = 3;
    /// <summary>
    /// Gets or sets the retry delay.
    /// </summary>
    /// <value>The retry delay.</value>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Gets or sets the use exponential backoff.
    /// </summary>
    /// <value>The use exponential backoff.</value>
    public bool UseExponentialBackoff { get; init; } = true;
    /// <summary>
    /// Gets or sets the max consecutive failures.
    /// </summary>
    /// <value>The max consecutive failures.</value>
    public int MaxConsecutiveFailures { get; init; } = 5;
    /// <summary>
    /// Gets or sets the rate limit window.
    /// </summary>
    /// <value>The rate limit window.</value>
    public TimeSpan RateLimitWindow { get; init; } = TimeSpan.FromMinutes(1);
    /// <summary>
    /// Gets or sets the max attempts per window.
    /// </summary>
    /// <value>The max attempts per window.</value>
    public int MaxAttemptsPerWindow { get; init; } = 10;
    /// <summary>
    /// Gets or sets the allow concurrent recovery.
    /// </summary>
    /// <value>The allow concurrent recovery.</value>
    public bool AllowConcurrentRecovery { get; init; } = false;
    /// <summary>
    /// Gets or sets the recovery timeout.
    /// </summary>
    /// <value>The recovery timeout.</value>
    public TimeSpan RecoveryTimeout { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Result of a recovery operation.
/// </summary>
public sealed class RecoveryResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public required bool Success { get; init; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public required string Message { get; init; }
    /// <summary>
    /// Gets or sets the exception.
    /// </summary>
    /// <value>The exception.</value>
    public Exception? Exception { get; init; }
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public required string Strategy { get; init; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan Duration { get; set; }
    /// <summary>
    /// Gets or sets the requires manual intervention.
    /// </summary>
    /// <value>The requires manual intervention.</value>
    public bool RequiresManualIntervention { get; init; }
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
/// <summary>
/// An recovery capability enumeration.
/// </summary>

/// <summary>
/// Recovery capability enumeration.
/// </summary>
[Flags]
public enum RecoveryCapability
{
    None = 0,
    MemoryErrors = 1 << 0,
    DeviceErrors = 1 << 1,
    KernelErrors = 1 << 2,
    NetworkErrors = 1 << 3,
    TimeoutErrors = 1 << 4,
    ResourceExhaustion = 1 << 5,
    All = MemoryErrors | DeviceErrors | KernelErrors | NetworkErrors | TimeoutErrors | ResourceExhaustion
}

/// <summary>
/// Interface for recovery strategies.
/// </summary>
public interface IRecoveryStrategy<in TContext> where TContext : class
{
    /// <summary>
    /// Gets or sets the capability.
    /// </summary>
    /// <value>The capability.</value>
    public RecoveryCapability Capability { get; }
    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    /// <value>The priority.</value>
    public int Priority { get; }
    /// <summary>
    /// Determines whether handle.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="context">The context.</param>
    /// <returns>true if the condition is met; otherwise, false.</returns>
    public bool CanHandle(Exception error, TContext context);
    /// <summary>
    /// Gets attempt recovery asynchronously.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="context">The context.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public Task<RecoveryResult> AttemptRecoveryAsync(
        Exception error,
        TContext context,
        RecoveryOptions? options = null,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets the statistics.
    /// </summary>
    /// <returns>The statistics.</returns>
    public RecoveryStatistics GetStatistics();
}

#endregion