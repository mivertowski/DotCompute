// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Base interface for all recovery strategies in DotCompute
/// </summary>
public interface IRecoveryStrategy<TContext>
{
    /// <summary>
    /// The type of errors this strategy can handle
    /// </summary>
    RecoveryCapability Capability { get; }

    /// <summary>
    /// Priority of this strategy (higher values = higher priority)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines if this strategy can handle the given error
    /// </summary>
    bool CanHandle(Exception error, TContext context);

    /// <summary>
    /// Attempts to recover from the error
    /// </summary>
    Task<RecoveryResult> RecoverAsync(Exception error, TContext context, RecoveryOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base recovery strategy implementation with common functionality
/// </summary>
public abstract class BaseRecoveryStrategy<TContext> : IRecoveryStrategy<TContext>
{
    protected readonly ILogger Logger;
    protected readonly RecoveryMetrics Metrics;

    protected BaseRecoveryStrategy(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Metrics = new RecoveryMetrics();
    }

    public abstract RecoveryCapability Capability { get; }
    public abstract int Priority { get; }
    public abstract bool CanHandle(Exception error, TContext context);
    public abstract Task<RecoveryResult> RecoverAsync(Exception error, TContext context, RecoveryOptions options, CancellationToken cancellationToken = default);

    protected RecoveryResult Success(string message, TimeSpan duration)
    {
        Metrics.RecordSuccess(duration);
        return new RecoveryResult
        {
            Success = true,
            Message = message,
            Duration = duration,
            Strategy = GetType().Name
        };
    }

    protected RecoveryResult Failure(string message, Exception? exception = null, TimeSpan duration = default)
    {
        Metrics.RecordFailure(duration, exception);
        return new RecoveryResult
        {
            Success = false,
            Message = message,
            Exception = exception,
            Duration = duration,
            Strategy = GetType().Name
        };
    }
}

/// <summary>
/// Recovery capabilities that strategies can handle
/// </summary>
[Flags]
public enum RecoveryCapability
{
    None = 0,
    GpuErrors = 1 << 0,
    MemoryErrors = 1 << 1,
    CompilationErrors = 1 << 2,
    NetworkErrors = 1 << 3,
    PluginErrors = 1 << 4,
    TimeoutErrors = 1 << 5,
    ResourceExhaustion = 1 << 6,
    DataCorruption = 1 << 7,
    All = int.MaxValue
}

/// <summary>
/// Options for recovery operations
/// </summary>
public class RecoveryOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public bool AllowFallback { get; set; } = true;
    public bool ForceGarbageCollection { get; set; } = false;
    public TimeSpan MaxRecoveryTime { get; set; } = TimeSpan.FromSeconds(30);
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Result of a recovery operation
/// </summary>
public class RecoveryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public TimeSpan Duration { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public int RetryAttempt { get; set; }
    public bool RequiresManualIntervention { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
}

/// <summary>
/// Tracks metrics for recovery operations
/// </summary>
public class RecoveryMetrics
{
    private long _totalAttempts;
    private long _successfulRecoveries;
    private long _failedRecoveries;
    private readonly List<TimeSpan> _recoveryTimes = new();
    private readonly List<Exception?> _recentExceptions = new();
    private readonly object _lock = new();

    public long TotalAttempts => _totalAttempts;
    public long SuccessfulRecoveries => _successfulRecoveries;
    public long FailedRecoveries => _failedRecoveries;
    public double SuccessRate => _totalAttempts == 0 ? 0.0 : (double)_successfulRecoveries / _totalAttempts;
    public TimeSpan AverageRecoveryTime
    {
        get
        {
            lock (_lock)
            {
                return _recoveryTimes.Count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(_recoveryTimes.Sum(t => t.Ticks) / _recoveryTimes.Count);
            }
        }
    }

    public void RecordSuccess(TimeSpan duration)
    {
        lock (_lock)
        {
            Interlocked.Increment(ref _totalAttempts);
            Interlocked.Increment(ref _successfulRecoveries);
            _recoveryTimes.Add(duration);
            
            // Keep only recent times (last 100)
            if (_recoveryTimes.Count > 100)
            {
                _recoveryTimes.RemoveAt(0);
            }
        }
    }

    public void RecordFailure(TimeSpan duration, Exception? exception)
    {
        lock (_lock)
        {
            Interlocked.Increment(ref _totalAttempts);
            Interlocked.Increment(ref _failedRecoveries);
            _recoveryTimes.Add(duration);
            _recentExceptions.Add(exception);
            
            // Keep only recent data
            if (_recoveryTimes.Count > 100)
            {
                _recoveryTimes.RemoveAt(0);
            }
            if (_recentExceptions.Count > 50)
            {
                _recentExceptions.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<Exception?> GetRecentExceptions()
    {
        lock (_lock)
        {
            return _recentExceptions.ToList().AsReadOnly();
        }
    }
}