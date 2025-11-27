// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Aggregates responses from multiple requests into a single result.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <typeparam name="TResult">The aggregated result type.</typeparam>
public sealed partial class MessageAggregator<TResponse, TResult> : IMessageAggregator<TResult>
    where TResponse : IResponseMessage
{
    private readonly int _expectedCount;
    private readonly Func<IReadOnlyList<TResponse>, TResult> _aggregateFunc;
    private readonly AggregationOptions _options;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource<TResult> _tcs;
    private readonly List<TResponse> _responses = new();
    private readonly Dictionary<Guid, Exception> _failures = new();
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private readonly Timer? _timeoutTimer;
    private readonly object _lock = new();
    private bool _completed;
    private bool _disposed;

    // Event IDs: 9400-9499 for MessageAggregator
    [LoggerMessage(EventId = 9400, Level = LogLevel.Debug,
        Message = "Response received: {ReceivedCount}/{ExpectedCount}")]
    private static partial void LogResponseReceived(ILogger logger, int receivedCount, int expectedCount);

    [LoggerMessage(EventId = 9401, Level = LogLevel.Debug,
        Message = "Aggregation completed with {SuccessCount} successes and {FailureCount} failures")]
    private static partial void LogAggregationCompleted(ILogger logger, int successCount, int failureCount);

    [LoggerMessage(EventId = 9402, Level = LogLevel.Warning,
        Message = "Aggregation timed out after {TimeoutMs}ms with {ReceivedCount}/{ExpectedCount} responses")]
    private static partial void LogAggregationTimeout(ILogger logger, double timeoutMs, int receivedCount, int expectedCount);

    [LoggerMessage(EventId = 9403, Level = LogLevel.Warning,
        Message = "Request {CorrelationId} failed: {Error}")]
    private static partial void LogRequestFailed(ILogger logger, Guid correlationId, string error);

    /// <inheritdoc />
    public int ExpectedCount => _expectedCount;

    /// <inheritdoc />
    public int ReceivedCount
    {
        get
        {
            lock (_lock)
            {
                return _responses.Count + _failures.Count;
            }
        }
    }

    /// <inheritdoc />
    public bool IsComplete
    {
        get
        {
            lock (_lock)
            {
                return _completed;
            }
        }
    }

    /// <inheritdoc />
    public Task<TResult> AggregatedResult => _tcs.Task;

    /// <summary>
    /// Creates a new message aggregator.
    /// </summary>
    /// <param name="expectedCount">Number of expected responses.</param>
    /// <param name="aggregateFunc">Function to aggregate responses.</param>
    /// <param name="options">Aggregation options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public MessageAggregator(
        int expectedCount,
        Func<IReadOnlyList<TResponse>, TResult> aggregateFunc,
        AggregationOptions options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (expectedCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCount), "Expected count must be positive");
        }

        _expectedCount = expectedCount;
        _aggregateFunc = aggregateFunc ?? throw new ArgumentNullException(nameof(aggregateFunc));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Register cancellation
        if (cancellationToken.CanBeCanceled)
        {
            _cancellationRegistration = cancellationToken.Register(() =>
            {
                lock (_lock)
                {
                    if (!_completed)
                    {
                        _completed = true;
                        _tcs.TrySetCanceled(cancellationToken);
                    }
                }
            });
        }

        // Setup timeout timer
        if (_options.DefaultTimeout != Timeout.InfiniteTimeSpan && _options.DefaultTimeout > TimeSpan.Zero)
        {
            _timeoutTimer = new Timer(
                OnTimeoutElapsed,
                null,
                _options.DefaultTimeout,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <inheritdoc />
    public Task<bool> AddResponseAsync<TResponseInput>(TResponseInput response, CancellationToken cancellationToken = default)
        where TResponseInput : IResponseMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(response);

        if (response is not TResponse typedResponse)
        {
            return Task.FromResult(false);
        }

        lock (_lock)
        {
            if (_completed)
            {
                return Task.FromResult(false);
            }

            _responses.Add(typedResponse);
            LogResponseReceived(_logger, _responses.Count + _failures.Count, _expectedCount);

            // Check if we should complete
            var totalReceived = _responses.Count + _failures.Count;
            var shouldComplete = false;

            if (totalReceived >= _expectedCount)
            {
                shouldComplete = true;
            }
            else if (_options.FailFastOnError && !typedResponse.IsSuccess)
            {
                shouldComplete = true;
            }

            if (shouldComplete)
            {
                CompleteAggregation();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public void MarkFailed(Guid correlationId, Exception exception)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(exception);

        lock (_lock)
        {
            if (_completed)
            {
                return;
            }

            _failures[correlationId] = exception;
            LogRequestFailed(_logger, correlationId, exception.Message);

            var totalReceived = _responses.Count + _failures.Count;
            if (totalReceived >= _expectedCount || _options.FailFastOnError)
            {
                CompleteAggregation();
            }
        }
    }

    /// <inheritdoc />
    public void Cancel()
    {
        lock (_lock)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            if (_options.IncludePartialResults && _responses.Count > 0)
            {
                try
                {
                    var result = _aggregateFunc(_responses);
                    _tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }
            else
            {
                _tcs.TrySetCanceled();
            }
        }
    }

    private void OnTimeoutElapsed(object? state)
    {
        lock (_lock)
        {
            if (_completed)
            {
                return;
            }

            LogAggregationTimeout(_logger, _options.DefaultTimeout.TotalMilliseconds, ReceivedCount, _expectedCount);

            if (_options.IncludePartialResults && _responses.Count > 0)
            {
                CompleteAggregation();
            }
            else
            {
                _completed = true;
                _tcs.TrySetException(new TimeoutException(
                    $"Aggregation timed out after {_options.DefaultTimeout.TotalMilliseconds}ms. " +
                    $"Received {ReceivedCount}/{_expectedCount} responses."));
            }
        }
    }

    private void CompleteAggregation()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _timeoutTimer?.Dispose();

        LogAggregationCompleted(_logger, _responses.Count, _failures.Count);

        try
        {
            var result = _aggregateFunc(_responses);
            _tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            _tcs.TrySetException(ex);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _timeoutTimer?.Dispose();
        _cancellationRegistration.Dispose();

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Factory for creating message aggregators.
/// </summary>
public sealed partial class MessageAggregatorFactory : IMessageAggregatorFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly AggregationOptions _defaultOptions;

    /// <summary>
    /// Creates a new message aggregator factory.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    /// <param name="defaultOptions">Default aggregation options.</param>
    public MessageAggregatorFactory(ILoggerFactory loggerFactory, AggregationOptions? defaultOptions = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _defaultOptions = defaultOptions ?? AggregationOptions.Default;
    }

    /// <inheritdoc />
    public IMessageAggregator<TResult> CreateAggregator<TResponse, TResult>(
        int expectedCount,
        Func<IReadOnlyList<TResponse>, TResult> aggregateFunc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : IResponseMessage
    {
        var options = _defaultOptions with { DefaultTimeout = timeout };
        var logger = _loggerFactory.CreateLogger<MessageAggregator<TResponse, TResult>>();
        return new MessageAggregator<TResponse, TResult>(
            expectedCount,
            aggregateFunc,
            options,
            logger,
            cancellationToken);
    }

    /// <inheritdoc />
    public IMessageAggregator<IReadOnlyList<TResponse>> CreateWaitAll<TResponse>(
        int expectedCount,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : IResponseMessage
    {
        return CreateAggregator<TResponse, IReadOnlyList<TResponse>>(
            expectedCount,
            responses => responses.ToList(),
            timeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public IMessageAggregator<TResponse?> CreateFirstSuccess<TResponse>(
        int expectedCount,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : class, IResponseMessage
    {
        var options = _defaultOptions with { DefaultTimeout = timeout, FailFastOnError = false };
        var logger = _loggerFactory.CreateLogger<MessageAggregator<TResponse, TResponse?>>();

        return new MessageAggregator<TResponse, TResponse?>(
            expectedCount,
            responses => responses.FirstOrDefault(r => r.IsSuccess),
            options,
            logger,
            cancellationToken);
    }
}
