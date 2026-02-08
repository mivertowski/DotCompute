// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Manages correlation between request and response messages.
/// </summary>
public sealed partial class CorrelationManager : ICorrelationManager
{
    private readonly ILogger<CorrelationManager> _logger;
    private readonly ConcurrentDictionary<Guid, PendingRequest> _pendingRequests = new();
    private bool _disposed;

    // Event IDs: 9200-9299 for CorrelationManager
    [LoggerMessage(EventId = 9200, Level = LogLevel.Debug,
        Message = "Registered request with correlation ID {CorrelationId}, timeout {TimeoutMs}ms")]
    private static partial void LogRequestRegistered(ILogger logger, Guid correlationId, double timeoutMs);

    [LoggerMessage(EventId = 9201, Level = LogLevel.Debug,
        Message = "Completed request with correlation ID {CorrelationId} in {ElapsedMs:F2}ms")]
    private static partial void LogRequestCompleted(ILogger logger, Guid correlationId, double elapsedMs);

    [LoggerMessage(EventId = 9202, Level = LogLevel.Warning,
        Message = "Request with correlation ID {CorrelationId} timed out after {TimeoutMs}ms")]
    private static partial void LogRequestTimedOut(ILogger logger, Guid correlationId, double timeoutMs);

    [LoggerMessage(EventId = 9203, Level = LogLevel.Debug,
        Message = "Cancelled request with correlation ID {CorrelationId}")]
    private static partial void LogRequestCancelled(ILogger logger, Guid correlationId);

    [LoggerMessage(EventId = 9204, Level = LogLevel.Warning,
        Message = "Received response with correlation ID {CorrelationId} but no matching request found")]
    private static partial void LogOrphanedResponse(ILogger logger, Guid correlationId);

    /// <inheritdoc />
    public int PendingRequestCount => _pendingRequests.Count;

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> PendingCorrelationIds => _pendingRequests.Keys.ToList();

    /// <inheritdoc />
    public event EventHandler<CorrelationTimeoutEventArgs>? RequestTimedOut;

    /// <summary>
    /// Creates a new correlation manager.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public CorrelationManager(ILogger<CorrelationManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public CorrelationContext<TResponse> RegisterRequest<TResponse>(
        IRequestMessage<TResponse> request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        where TResponse : IResponseMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        // Ensure the message has a correlation ID
        if (request.CorrelationId == null || request.CorrelationId == Guid.Empty)
        {
            request.CorrelationId = request.MessageId;
        }

        var correlationId = request.CorrelationId.Value;

        var context = new CorrelationContext<TResponse>(
            correlationId,
            timeout,
            cancellationToken,
            onTimeout: OnRequestTimeout);

        var pendingRequest = new PendingRequest(
            correlationId,
            typeof(TResponse),
            context,
            DateTimeOffset.UtcNow);

        if (!_pendingRequests.TryAdd(correlationId, pendingRequest))
        {
            throw new InvalidOperationException(
                $"A request with correlation ID {correlationId} is already pending");
        }

        LogRequestRegistered(_logger, correlationId, timeout.TotalMilliseconds);

        return context;
    }

    /// <inheritdoc />
    public bool TryCompleteRequest(IResponseMessage response)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(response);

        if (response.CorrelationId == null || response.CorrelationId == Guid.Empty)
        {
            return false;
        }

        var correlationId = response.CorrelationId.Value;

        if (!_pendingRequests.TryRemove(correlationId, out var pendingRequest))
        {
            LogOrphanedResponse(_logger, correlationId);
            return false;
        }

        var elapsed = DateTimeOffset.UtcNow - pendingRequest.CreatedAt;

        // Use interface method to complete the request (Native AOT compatible)
        var result = pendingRequest.Context.TrySetResultUntyped(response);

        if (result)
        {
            LogRequestCompleted(_logger, correlationId, elapsed.TotalMilliseconds);
        }

        return result;
    }

    /// <inheritdoc />
    public bool CancelRequest(Guid correlationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_pendingRequests.TryRemove(correlationId, out var pendingRequest))
        {
            return false;
        }

        pendingRequest.Context.TrySetCanceled();
        LogRequestCancelled(_logger, correlationId);

        return true;
    }

    private void OnRequestTimeout(Guid correlationId)
    {
        if (_pendingRequests.TryRemove(correlationId, out var pendingRequest))
        {
            LogRequestTimedOut(_logger, correlationId, pendingRequest.Context.Timeout.TotalMilliseconds);

            RequestTimedOut?.Invoke(this, new CorrelationTimeoutEventArgs
            {
                CorrelationId = correlationId,
                RequestCreatedAt = pendingRequest.CreatedAt,
                TimeoutDuration = pendingRequest.Context.Timeout
            });
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Cancel all pending requests
        foreach (var kvp in _pendingRequests)
        {
            kvp.Value.Context.TrySetCanceled();
        }

        _pendingRequests.Clear();
    }

    private sealed class PendingRequest
    {
        public Guid CorrelationId { get; }
        public Type ResponseType { get; }
        public ICorrelationContextBase Context { get; }
        public DateTimeOffset CreatedAt { get; }

        public PendingRequest(Guid correlationId, Type responseType, ICorrelationContextBase context, DateTimeOffset createdAt)
        {
            CorrelationId = correlationId;
            ResponseType = responseType;
            Context = context;
            CreatedAt = createdAt;
        }
    }
}
