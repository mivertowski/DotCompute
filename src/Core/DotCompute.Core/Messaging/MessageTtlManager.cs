// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Manages message TTL enforcement and expiration handling.
/// </summary>
public sealed partial class MessageTtlManager : IMessageTtlManager
{
    private readonly ConcurrentDictionary<Guid, TrackedMessage> _trackedMessages = new();
    private readonly Timer _expirationCheckTimer;
    private readonly ILogger<MessageTtlManager> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _defaultTtl;
    private long _expiredCount;
    private bool _disposed;

    // Event IDs: 9600-9699 for MessageTtlManager
    [LoggerMessage(EventId = 9600, Level = LogLevel.Debug,
        Message = "Message {MessageId} tracking started with TTL {TtlMs}ms")]
    private static partial void LogTrackingStarted(ILogger logger, Guid messageId, double ttlMs);

    [LoggerMessage(EventId = 9601, Level = LogLevel.Debug,
        Message = "Message {MessageId} tracking cancelled")]
    private static partial void LogTrackingCancelled(ILogger logger, Guid messageId);

    [LoggerMessage(EventId = 9602, Level = LogLevel.Warning,
        Message = "Message {MessageId} expired after {TtlMs}ms")]
    private static partial void LogMessageExpired(ILogger logger, Guid messageId, double ttlMs);

    [LoggerMessage(EventId = 9603, Level = LogLevel.Debug,
        Message = "TTL check completed: {TrackedCount} tracked, {ExpiredCount} expired")]
    private static partial void LogExpirationCheck(ILogger logger, int trackedCount, int expiredCount);

    [LoggerMessage(EventId = 9604, Level = LogLevel.Error,
        Message = "Error invoking expiration callback for message {MessageId}")]
    private static partial void LogCallbackError(ILogger logger, Guid messageId, Exception ex);

    /// <inheritdoc />
    public int TrackedCount => _trackedMessages.Count;

    /// <inheritdoc />
    public long ExpiredCount => Interlocked.Read(ref _expiredCount);

    /// <inheritdoc />
    public event EventHandler<MessageExpiredEventArgs>? MessageExpired;

    /// <summary>
    /// Creates a new message TTL manager.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="checkInterval">How often to check for expired messages. Default: 1 second.</param>
    /// <param name="defaultTtl">Default TTL for messages without explicit TTL. Default: 30 seconds.</param>
    public MessageTtlManager(
        ILogger<MessageTtlManager> logger,
        TimeSpan? checkInterval = null,
        TimeSpan? defaultTtl = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(1);
        _defaultTtl = defaultTtl ?? TimeSpan.FromSeconds(30);

        _expirationCheckTimer = new Timer(
            CheckExpiredMessages,
            null,
            _checkInterval,
            _checkInterval);
    }

    /// <inheritdoc />
    public bool IsExpired<TMessage>(TMessage message) where TMessage : IRingKernelMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        if (message is IExpirableMessage expirable)
        {
            return expirable.IsExpired;
        }

        // Non-expirable messages never expire
        return false;
    }

    /// <inheritdoc />
    public TimeSpan? GetRemainingTtl<TMessage>(TMessage message) where TMessage : IRingKernelMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        if (message is IExpirableMessage expirable)
        {
            if (expirable.TimeToLive == Timeout.InfiniteTimeSpan)
            {
                return null;
            }

            var expiresAt = expirable.CreatedAt + expirable.TimeToLive;
            var remaining = expiresAt - DateTimeOffset.UtcNow;

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        // Non-expirable messages have no TTL
        return null;
    }

    /// <inheritdoc />
    public Task<Guid> TrackAsync<TMessage>(
        TMessage message,
        Func<TMessage, Task> onExpired,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(onExpired);

        cancellationToken.ThrowIfCancellationRequested();

        var trackingId = Guid.NewGuid();
        DateTimeOffset createdAt;
        TimeSpan ttl;

        if (message is IExpirableMessage expirable)
        {
            createdAt = expirable.CreatedAt;
            ttl = expirable.TimeToLive;
        }
        else
        {
            createdAt = DateTimeOffset.UtcNow;
            ttl = _defaultTtl;
        }

        // If TTL is infinite, don't track
        if (ttl == Timeout.InfiniteTimeSpan)
        {
            return Task.FromResult(Guid.Empty);
        }

        var tracked = new TrackedMessage(
            trackingId,
            message.MessageId,
            message.MessageType,
            createdAt,
            ttl,
            async () => await onExpired(message).ConfigureAwait(false),
            cancellationToken);

        _trackedMessages[trackingId] = tracked;
        LogTrackingStarted(_logger, message.MessageId, ttl.TotalMilliseconds);

        return Task.FromResult(trackingId);
    }

    /// <inheritdoc />
    public bool CancelTracking(Guid trackingId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_trackedMessages.TryRemove(trackingId, out var tracked))
        {
            LogTrackingCancelled(_logger, tracked.MessageId);
            tracked.Dispose();
            return true;
        }

        return false;
    }

    private void CheckExpiredMessages(object? state)
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var expiredCount = 0;

        foreach (var (trackingId, tracked) in _trackedMessages.ToArray())
        {
            if (tracked.CancellationToken.IsCancellationRequested)
            {
                _trackedMessages.TryRemove(trackingId, out _);
                tracked.Dispose();
                continue;
            }

            var expiresAt = tracked.CreatedAt + tracked.TimeToLive;
            if (now >= expiresAt)
            {
                if (_trackedMessages.TryRemove(trackingId, out _))
                {
                    expiredCount++;
                    Interlocked.Increment(ref _expiredCount);
                    HandleExpiredMessage(tracked);
                }
            }
        }

        if (expiredCount > 0)
        {
            LogExpirationCheck(_logger, _trackedMessages.Count, expiredCount);
        }
    }

    private void HandleExpiredMessage(TrackedMessage tracked)
    {
        LogMessageExpired(_logger, tracked.MessageId, tracked.TimeToLive.TotalMilliseconds);

        // Raise event
        MessageExpired?.Invoke(this, new MessageExpiredEventArgs
        {
            MessageId = tracked.MessageId,
            MessageType = tracked.MessageType,
            CreatedAt = tracked.CreatedAt,
            TimeToLive = tracked.TimeToLive,
            ExpiredAt = DateTimeOffset.UtcNow
        });

        // Invoke callback asynchronously
        _ = InvokeCallbackAsync(tracked);
    }

    private async Task InvokeCallbackAsync(TrackedMessage tracked)
    {
        try
        {
            await tracked.OnExpiredCallback().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCallbackError(_logger, tracked.MessageId, ex);
        }
        finally
        {
            tracked.Dispose();
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
        _expirationCheckTimer.Dispose();

        foreach (var tracked in _trackedMessages.Values)
        {
            tracked.Dispose();
        }

        _trackedMessages.Clear();

        return ValueTask.CompletedTask;
    }

    private sealed class TrackedMessage : IDisposable
    {
        public Guid TrackingId { get; }
        public Guid MessageId { get; }
        public string MessageType { get; }
        public DateTimeOffset CreatedAt { get; }
        public TimeSpan TimeToLive { get; }
        public Func<Task> OnExpiredCallback { get; }
        public CancellationToken CancellationToken { get; }

        public TrackedMessage(
            Guid trackingId,
            Guid messageId,
            string messageType,
            DateTimeOffset createdAt,
            TimeSpan timeToLive,
            Func<Task> onExpiredCallback,
            CancellationToken cancellationToken)
        {
            TrackingId = trackingId;
            MessageId = messageId;
            MessageType = messageType;
            CreatedAt = createdAt;
            TimeToLive = timeToLive;
            OnExpiredCallback = onExpiredCallback;
            CancellationToken = cancellationToken;
        }

        public void Dispose()
        {
            // No unmanaged resources to release
        }
    }
}

/// <summary>
/// Extension methods for message TTL and dead letter operations.
/// </summary>
public static class MessageTtlExtensions
{
    /// <summary>
    /// Creates a message envelope with TTL for any message.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to wrap.</param>
    /// <param name="timeToLive">The TTL duration.</param>
    /// <returns>A message envelope with TTL.</returns>
    public static MessageEnvelope<TMessage> WithTtl<TMessage>(
        this TMessage message,
        TimeSpan timeToLive)
        where TMessage : IRingKernelMessage
    {
        return new MessageEnvelope<TMessage>(message, timeToLive);
    }

    /// <summary>
    /// Creates a message envelope with infinite TTL (no expiration).
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to wrap.</param>
    /// <returns>A message envelope that never expires.</returns>
    public static MessageEnvelope<TMessage> WithNoExpiration<TMessage>(
        this TMessage message)
        where TMessage : IRingKernelMessage
    {
        return new MessageEnvelope<TMessage>(message, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Sends a message to the dead letter queue if it has expired.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="dlq">The dead letter queue.</param>
    /// <param name="message">The message to check and potentially dead-letter.</param>
    /// <param name="sourceQueue">Optional source queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dead letter entry if the message expired, or null.</returns>
    public static async Task<DeadLetterEntry?> DeadLetterIfExpiredAsync<TMessage>(
        this IDeadLetterQueue dlq,
        TMessage message,
        string? sourceQueue = null,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage
    {
        ArgumentNullException.ThrowIfNull(dlq);
        ArgumentNullException.ThrowIfNull(message);

        if (message is IExpirableMessage expirable && expirable.IsExpired)
        {
            return await dlq.EnqueueAsync(
                message,
                DeadLetterReason.Expired,
                $"Message expired at {expirable.ExpiresAt:O}",
                sourceQueue: sourceQueue,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Sends a message to the dead letter queue after a processing error.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="dlq">The dead letter queue.</param>
    /// <param name="message">The failed message.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="attemptCount">Number of processing attempts.</param>
    /// <param name="sourceQueue">Optional source queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dead letter entry.</returns>
    public static Task<DeadLetterEntry> DeadLetterOnErrorAsync<TMessage>(
        this IDeadLetterQueue dlq,
        TMessage message,
        Exception exception,
        int attemptCount = 1,
        string? sourceQueue = null,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage
    {
        ArgumentNullException.ThrowIfNull(dlq);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(exception);

        return dlq.EnqueueAsync(
            message,
            DeadLetterReason.ProcessingError,
            exception.Message,
            exception,
            attemptCount,
            sourceQueue,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sends a message to the dead letter queue after max retries exceeded.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="dlq">The dead letter queue.</param>
    /// <param name="message">The failed message.</param>
    /// <param name="maxRetries">Maximum retry count that was exceeded.</param>
    /// <param name="lastException">The last exception that occurred.</param>
    /// <param name="sourceQueue">Optional source queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dead letter entry.</returns>
    public static Task<DeadLetterEntry> DeadLetterOnMaxRetriesAsync<TMessage>(
        this IDeadLetterQueue dlq,
        TMessage message,
        int maxRetries,
        Exception? lastException = null,
        string? sourceQueue = null,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage
    {
        ArgumentNullException.ThrowIfNull(dlq);
        ArgumentNullException.ThrowIfNull(message);

        return dlq.EnqueueAsync(
            message,
            DeadLetterReason.MaxRetriesExceeded,
            $"Max retries ({maxRetries}) exceeded",
            lastException,
            maxRetries,
            sourceQueue,
            cancellationToken: cancellationToken);
    }
}
