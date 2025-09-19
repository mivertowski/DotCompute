// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Abstractions.Interfaces.Recovery;

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