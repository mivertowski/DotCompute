// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Messaging;

/// <summary>
/// Marker interface for request messages that expect a response.
/// </summary>
/// <typeparam name="TResponse">The expected response message type.</typeparam>
public interface IRequestMessage<TResponse> : IRingKernelMessage
    where TResponse : IResponseMessage
{
}

/// <summary>
/// Marker interface for response messages.
/// </summary>
public interface IResponseMessage : IRingKernelMessage
{
    /// <summary>
    /// Gets a value indicating whether this response indicates a successful operation.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets an optional error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; }
}

/// <summary>
/// Manages correlation between request and response messages.
/// Tracks pending requests and provides timeout handling.
/// </summary>
/// <remarks>
/// The correlation manager enables request/reply patterns across Ring Kernels:
/// <list type="bullet">
/// <item>Track pending requests with correlation IDs</item>
/// <item>Match incoming responses to pending requests</item>
/// <item>Handle request timeouts</item>
/// <item>Support cancellation of pending requests</item>
/// </list>
/// </remarks>
public interface ICorrelationManager : IDisposable
{
    /// <summary>
    /// Registers a request and returns a task that completes when the response arrives.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="request">The request message being sent.</param>
    /// <param name="timeout">The timeout for waiting for the response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A correlation context that can be used to wait for the response.
    /// </returns>
    public CorrelationContext<TResponse> RegisterRequest<TResponse>(
        IRequestMessage<TResponse> request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : IResponseMessage;

    /// <summary>
    /// Attempts to complete a pending request with the given response.
    /// </summary>
    /// <param name="response">The response message received.</param>
    /// <returns>True if a matching pending request was found and completed; otherwise, false.</returns>
    public bool TryCompleteRequest(IResponseMessage response);

    /// <summary>
    /// Cancels a pending request.
    /// </summary>
    /// <param name="correlationId">The correlation ID of the request to cancel.</param>
    /// <returns>True if the request was found and cancelled; otherwise, false.</returns>
    public bool CancelRequest(Guid correlationId);

    /// <summary>
    /// Gets the number of pending requests.
    /// </summary>
    public int PendingRequestCount { get; }

    /// <summary>
    /// Gets all pending correlation IDs.
    /// </summary>
    public IReadOnlyCollection<Guid> PendingCorrelationIds { get; }

    /// <summary>
    /// Occurs when a request times out.
    /// </summary>
    public event EventHandler<CorrelationTimeoutEventArgs>? RequestTimedOut;
}

/// <summary>
/// Non-generic base interface for correlation contexts.
/// Used internally to avoid reflection in Native AOT scenarios.
/// </summary>
public interface ICorrelationContextBase : IDisposable
{
    /// <summary>
    /// Gets the correlation ID for this request.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// Gets the timestamp when the request was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets a value indicating whether the request has completed.
    /// </summary>
    public bool IsCompleted { get; }

    /// <summary>
    /// Attempts to complete the context with a response message.
    /// </summary>
    /// <param name="response">The response message.</param>
    /// <returns>True if the context was completed; false if already completed or type mismatch.</returns>
    public bool TrySetResultUntyped(IResponseMessage response);

    /// <summary>
    /// Cancels the pending request.
    /// </summary>
    /// <returns>True if the context was cancelled; false if already completed.</returns>
    public bool TrySetCanceled();
}

/// <summary>
/// Represents a pending correlation context for a request awaiting a response.
/// </summary>
/// <typeparam name="TResponse">The expected response type.</typeparam>
public sealed class CorrelationContext<TResponse> : ICorrelationContextBase
    where TResponse : IResponseMessage
{
    private readonly TaskCompletionSource<TResponse> _taskCompletionSource;
    private readonly CancellationTokenRegistration _cancellationRegistration;
    private readonly Timer? _timeoutTimer;
    private bool _completed;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the correlation ID for this request.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// Gets the timestamp when the request was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets a task that completes when the response arrives or times out.
    /// </summary>
    public Task<TResponse> ResponseTask => _taskCompletionSource.Task;

    /// <summary>
    /// Gets a value indicating whether the request has completed.
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            lock (_lock)
            {
                return _completed;
            }
        }
    }

    /// <summary>
    /// Creates a new correlation context.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="onTimeout">Callback when timeout occurs.</param>
    public CorrelationContext(
        Guid correlationId,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<Guid>? onTimeout = null)
    {
        CorrelationId = correlationId;
        CreatedAt = DateTimeOffset.UtcNow;
        Timeout = timeout;
        _taskCompletionSource = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                        _taskCompletionSource.TrySetCanceled(cancellationToken);
                    }
                }
            });
        }

        // Setup timeout timer
        if (timeout != System.Threading.Timeout.InfiniteTimeSpan && timeout > TimeSpan.Zero)
        {
            _timeoutTimer = new Timer(state =>
            {
                lock (_lock)
                {
                    if (!_completed)
                    {
                        _completed = true;
                        _taskCompletionSource.TrySetException(
                            new CorrelationTimeoutException(CorrelationId, timeout));
                        onTimeout?.Invoke(CorrelationId);
                    }
                }
            }, null, timeout, System.Threading.Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Completes the context with a response.
    /// </summary>
    /// <param name="response">The response message.</param>
    /// <returns>True if the context was completed; false if already completed.</returns>
    public bool TrySetResult(TResponse response)
    {
        lock (_lock)
        {
            if (_completed)
            {
                return false;
            }

            _completed = true;
            _timeoutTimer?.Dispose();
            _cancellationRegistration.Dispose();
            return _taskCompletionSource.TrySetResult(response);
        }
    }

    /// <inheritdoc />
    public bool TrySetResultUntyped(IResponseMessage response)
    {
        if (response is TResponse typedResponse)
        {
            return TrySetResult(typedResponse);
        }

        return false;
    }

    /// <summary>
    /// Completes the context with an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>True if the context was completed; false if already completed.</returns>
    public bool TrySetException(Exception exception)
    {
        lock (_lock)
        {
            if (_completed)
            {
                return false;
            }

            _completed = true;
            _timeoutTimer?.Dispose();
            _cancellationRegistration.Dispose();
            return _taskCompletionSource.TrySetException(exception);
        }
    }

    /// <summary>
    /// Cancels the pending request.
    /// </summary>
    /// <returns>True if the context was cancelled; false if already completed.</returns>
    public bool TrySetCanceled()
    {
        lock (_lock)
        {
            if (_completed)
            {
                return false;
            }

            _completed = true;
            _timeoutTimer?.Dispose();
            _cancellationRegistration.Dispose();
            return _taskCompletionSource.TrySetCanceled();
        }
    }

    /// <summary>
    /// Disposes the correlation context and its resources.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timeoutTimer?.Dispose();
            _cancellationRegistration.Dispose();
        }
    }
}

/// <summary>
/// Exception thrown when a correlated request times out.
/// </summary>
public sealed class CorrelationTimeoutException : TimeoutException
{
    /// <summary>
    /// Gets the correlation ID of the timed-out request.
    /// </summary>
    public Guid CorrelationId { get; }

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan TimeoutDuration { get; }

    /// <summary>
    /// Creates a new correlation timeout exception.
    /// </summary>
    public CorrelationTimeoutException()
        : base("Request timed out")
    {
    }

    /// <summary>
    /// Creates a new correlation timeout exception with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public CorrelationTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new correlation timeout exception with a message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CorrelationTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new correlation timeout exception with correlation details.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <param name="timeout">The timeout duration.</param>
    public CorrelationTimeoutException(Guid correlationId, TimeSpan timeout)
        : base($"Request with correlation ID {correlationId} timed out after {timeout.TotalMilliseconds:F0}ms")
    {
        CorrelationId = correlationId;
        TimeoutDuration = timeout;
    }
}

/// <summary>
/// Event arguments for request timeout events.
/// </summary>
public sealed class CorrelationTimeoutEventArgs : EventArgs
{
    /// <summary>
    /// Gets the correlation ID of the timed-out request.
    /// </summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Gets when the request was created.
    /// </summary>
    public required DateTimeOffset RequestCreatedAt { get; init; }

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public required TimeSpan TimeoutDuration { get; init; }
}
