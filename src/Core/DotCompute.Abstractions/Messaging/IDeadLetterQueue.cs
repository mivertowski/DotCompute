// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace DotCompute.Abstractions.Messaging;

/// <summary>
/// Represents a message that supports Time-To-Live (TTL) expiration.
/// </summary>
/// <remarks>
/// Messages implementing this interface can specify their own expiration time,
/// overriding any queue-level timeout settings.
/// </remarks>
public interface IExpirableMessage : IRingKernelMessage
{
    /// <summary>
    /// Gets or sets the time when this message was created.
    /// </summary>
    /// <remarks>
    /// Used in conjunction with <see cref="TimeToLive"/> to determine
    /// when the message expires. Default: <see cref="DateTimeOffset.UtcNow"/>
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the message's time-to-live duration.
    /// </summary>
    /// <remarks>
    /// The message expires when <see cref="CreatedAt"/> + <see cref="TimeToLive"/>
    /// is less than the current time.
    ///
    /// Set to <see cref="Timeout.InfiniteTimeSpan"/> for no expiration.
    /// Default: 30 seconds
    /// </remarks>
    public TimeSpan TimeToLive { get; set; }

    /// <summary>
    /// Gets a value indicating whether the message has expired.
    /// </summary>
    public bool IsExpired => TimeToLive != Timeout.InfiniteTimeSpan &&
                            DateTimeOffset.UtcNow > CreatedAt + TimeToLive;

    /// <summary>
    /// Gets the absolute expiration time of this message.
    /// </summary>
    public DateTimeOffset? ExpiresAt => TimeToLive == Timeout.InfiniteTimeSpan
        ? null
        : CreatedAt + TimeToLive;
}

/// <summary>
/// Wraps a message with TTL metadata without requiring the message to implement <see cref="IExpirableMessage"/>.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <remarks>
/// Use this wrapper to add TTL semantics to messages that don't natively support it.
/// </remarks>
public sealed class MessageEnvelope<TMessage> : IExpirableMessage, IDisposable
    where TMessage : IRingKernelMessage
{
    private bool _disposed;
    private readonly byte[] _serializedData;

    /// <summary>
    /// Gets the wrapped message.
    /// </summary>
    public TMessage Message { get; }

    /// <inheritdoc />
    public Guid MessageId
    {
        get => Message.MessageId;
        set => Message.MessageId = value;
    }

    /// <inheritdoc />
    public string MessageType => $"Envelope<{Message.MessageType}>";

    /// <inheritdoc />
    public byte Priority
    {
        get => Message.Priority;
        set => Message.Priority = value;
    }

    /// <inheritdoc />
    public Guid? CorrelationId
    {
        get => Message.CorrelationId;
        set => Message.CorrelationId = value;
    }

    /// <inheritdoc />
    public int PayloadSize => Message.PayloadSize + 24; // + CreatedAt(8) + TTL(8) + header(8)

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets a value indicating whether the message has expired.
    /// </summary>
    public bool IsExpired => TimeToLive != Timeout.InfiniteTimeSpan &&
                            DateTimeOffset.UtcNow > CreatedAt + TimeToLive;

    /// <summary>
    /// Gets the absolute expiration time of this message.
    /// </summary>
    public DateTimeOffset? ExpiresAt => TimeToLive == Timeout.InfiniteTimeSpan
        ? null
        : CreatedAt + TimeToLive;

    /// <summary>
    /// Creates a new message envelope.
    /// </summary>
    /// <param name="message">The message to wrap.</param>
    /// <param name="timeToLive">Optional TTL override. Default: 30 seconds.</param>
    public MessageEnvelope(TMessage message, TimeSpan? timeToLive = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        if (timeToLive.HasValue)
        {
            TimeToLive = timeToLive.Value;
        }

        _serializedData = new byte[PayloadSize];
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> Serialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var innerData = Message.Serialize();
        var offset = 0;

        // Write envelope header
        BitConverter.TryWriteBytes(_serializedData.AsSpan(offset), CreatedAt.ToUnixTimeMilliseconds());
        offset += 8;
        BitConverter.TryWriteBytes(_serializedData.AsSpan(offset), TimeToLive.Ticks);
        offset += 8;
        BitConverter.TryWriteBytes(_serializedData.AsSpan(offset), innerData.Length);
        offset += 8;

        // Write inner message
        innerData.CopyTo(_serializedData.AsSpan(offset));

        return _serializedData;
    }

    /// <inheritdoc />
    public void Deserialize(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (data.Length < 24)
        {
            throw new ArgumentException("Data too short for envelope deserialization.", nameof(data));
        }

        var offset = 0;
        CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(BitConverter.ToInt64(data.Slice(offset, 8)));
        offset += 8;
        TimeToLive = TimeSpan.FromTicks(BitConverter.ToInt64(data.Slice(offset, 8)));
        offset += 8;
        var innerLength = BitConverter.ToInt32(data.Slice(offset, 8));
        offset += 8;

        Message.Deserialize(data.Slice(offset, innerLength));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// Reason why a message was sent to the dead letter queue.
/// </summary>
public enum DeadLetterReason
{
    /// <summary>
    /// Message exceeded its time-to-live and expired.
    /// </summary>
    Expired = 0,

    /// <summary>
    /// Message processing failed after maximum retries.
    /// </summary>
    MaxRetriesExceeded = 1,

    /// <summary>
    /// Message processing threw an unhandled exception.
    /// </summary>
    ProcessingError = 2,

    /// <summary>
    /// Message was rejected due to validation failure.
    /// </summary>
    ValidationFailed = 3,

    /// <summary>
    /// Message format was invalid or corrupted.
    /// </summary>
    MalformedMessage = 4,

    /// <summary>
    /// Target handler or processor was not found.
    /// </summary>
    NoHandler = 5,

    /// <summary>
    /// Message was explicitly rejected by the consumer.
    /// </summary>
    Rejected = 6,

    /// <summary>
    /// Queue was full and message was dropped.
    /// </summary>
    QueueOverflow = 7,

    /// <summary>
    /// Message was manually moved to DLQ for investigation.
    /// </summary>
    Manual = 8,

    /// <summary>
    /// Unknown or unspecified reason.
    /// </summary>
    Unknown = 99
}

/// <summary>
/// Represents an entry in the dead letter queue.
/// </summary>
/// <remarks>
/// Contains the original message along with metadata about why
/// it was moved to the dead letter queue.
/// </remarks>
public sealed class DeadLetterEntry : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the unique identifier for this dead letter entry.
    /// </summary>
    public Guid EntryId { get; }

    /// <summary>
    /// Gets the original message ID.
    /// </summary>
    public Guid OriginalMessageId { get; }

    /// <summary>
    /// Gets the original message type.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gets the serialized message payload.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    /// <summary>
    /// Gets the reason this message was dead-lettered.
    /// </summary>
    public DeadLetterReason Reason { get; }

    /// <summary>
    /// Gets the detailed error message if available.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets when the message was originally created.
    /// </summary>
    public DateTimeOffset OriginalCreatedAt { get; }

    /// <summary>
    /// Gets when the message was moved to the dead letter queue.
    /// </summary>
    public DateTimeOffset DeadLetteredAt { get; }

    /// <summary>
    /// Gets the number of times processing was attempted.
    /// </summary>
    public int AttemptCount { get; }

    /// <summary>
    /// Gets the source queue name if available.
    /// </summary>
    public string? SourceQueue { get; }

    /// <summary>
    /// Gets optional custom metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    /// <summary>
    /// Creates a new dead letter entry.
    /// </summary>
    public DeadLetterEntry(
        Guid originalMessageId,
        string messageType,
        ReadOnlyMemory<byte> payload,
        DeadLetterReason reason,
        DateTimeOffset originalCreatedAt,
        string? errorMessage = null,
        Exception? exception = null,
        int attemptCount = 1,
        string? sourceQueue = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        EntryId = Guid.NewGuid();
        OriginalMessageId = originalMessageId;
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
        Payload = payload;
        Reason = reason;
        OriginalCreatedAt = originalCreatedAt;
        DeadLetteredAt = DateTimeOffset.UtcNow;
        ErrorMessage = errorMessage;
        Exception = exception;
        AttemptCount = attemptCount;
        SourceQueue = sourceQueue;
        Metadata = metadata;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Payload memory is managed by caller
    }
}

/// <summary>
/// Event arguments for dead letter events.
/// </summary>
public sealed class DeadLetterEventArgs : EventArgs
{
    /// <summary>
    /// Gets the dead letter entry.
    /// </summary>
    public required DeadLetterEntry Entry { get; init; }

    /// <summary>
    /// Gets the dead letter queue name.
    /// </summary>
    public required string QueueName { get; init; }
}

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - IDeadLetterQueue is intentional

/// <summary>
/// Stores messages that could not be processed successfully.
/// </summary>
/// <remarks>
/// <para>
/// Dead letter queues capture messages that have failed processing due to:
/// </para>
/// <list type="bullet">
/// <item>TTL expiration</item>
/// <item>Maximum retry count exceeded</item>
/// <item>Processing errors</item>
/// <item>Validation failures</item>
/// <item>Missing handlers</item>
/// </list>
/// <para>
/// Messages in the DLQ can be inspected, reprocessed, or archived.
/// </para>
/// </remarks>
public interface IDeadLetterQueue : IDisposable
{
    /// <summary>
    /// Gets the queue name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the current number of entries in the dead letter queue.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the maximum capacity of the dead letter queue.
    /// </summary>
    public int MaxCapacity { get; }

    /// <summary>
    /// Gets the total number of messages ever dead-lettered.
    /// </summary>
    public long TotalDeadLettered { get; }

    /// <summary>
    /// Adds a message to the dead letter queue.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The failed message.</param>
    /// <param name="reason">Why the message was dead-lettered.</param>
    /// <param name="errorMessage">Optional error description.</param>
    /// <param name="exception">Optional exception that caused the failure.</param>
    /// <param name="attemptCount">Number of processing attempts.</param>
    /// <param name="sourceQueue">Name of the source queue.</param>
    /// <param name="metadata">Optional custom metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created dead letter entry.</returns>
    public Task<DeadLetterEntry> EnqueueAsync<TMessage>(
        TMessage message,
        DeadLetterReason reason,
        string? errorMessage = null,
        Exception? exception = null,
        int attemptCount = 1,
        string? sourceQueue = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage;

    /// <summary>
    /// Attempts to dequeue an entry from the dead letter queue.
    /// </summary>
    /// <param name="entry">The dequeued entry, or null if empty.</param>
    /// <returns>True if an entry was dequeued.</returns>
    public bool TryDequeue(out DeadLetterEntry? entry);

    /// <summary>
    /// Peeks at the next entry without removing it.
    /// </summary>
    /// <param name="entry">The peeked entry, or null if empty.</param>
    /// <returns>True if an entry exists.</returns>
    public bool TryPeek(out DeadLetterEntry? entry);

    /// <summary>
    /// Gets all entries matching the specified filter.
    /// </summary>
    /// <param name="predicate">Optional filter predicate.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <returns>Matching dead letter entries.</returns>
    public IReadOnlyList<DeadLetterEntry> GetEntries(
        Func<DeadLetterEntry, bool>? predicate = null,
        int maxResults = 100);

    /// <summary>
    /// Gets entries by dead letter reason.
    /// </summary>
    /// <param name="reason">The reason to filter by.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <returns>Matching dead letter entries.</returns>
    public IReadOnlyList<DeadLetterEntry> GetEntriesByReason(
        DeadLetterReason reason,
        int maxResults = 100);

    /// <summary>
    /// Gets entries within a time range.
    /// </summary>
    /// <param name="startTime">Start time (inclusive).</param>
    /// <param name="endTime">End time (inclusive).</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <returns>Matching dead letter entries.</returns>
    public IReadOnlyList<DeadLetterEntry> GetEntriesByTimeRange(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int maxResults = 100);

    /// <summary>
    /// Removes an entry by its ID.
    /// </summary>
    /// <param name="entryId">The entry ID to remove.</param>
    /// <returns>True if the entry was found and removed.</returns>
    public bool Remove(Guid entryId);

    /// <summary>
    /// Removes all entries matching the predicate.
    /// </summary>
    /// <param name="predicate">Filter for entries to remove.</param>
    /// <returns>Number of entries removed.</returns>
    public int RemoveWhere(Func<DeadLetterEntry, bool> predicate);

    /// <summary>
    /// Clears all entries from the dead letter queue.
    /// </summary>
    /// <returns>Number of entries removed.</returns>
    public int Clear();

    /// <summary>
    /// Gets statistics about the dead letter queue.
    /// </summary>
    /// <returns>Dead letter queue statistics.</returns>
    public DeadLetterStatistics GetStatistics();

    /// <summary>
    /// Occurs when a message is added to the dead letter queue.
    /// </summary>
    public event EventHandler<DeadLetterEventArgs>? MessageDeadLettered;
}

#pragma warning restore CA1711

/// <summary>
/// Statistics for a dead letter queue.
/// </summary>
public sealed record DeadLetterStatistics
{
    /// <summary>
    /// Gets the queue name.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Gets the current entry count.
    /// </summary>
    public required int CurrentCount { get; init; }

    /// <summary>
    /// Gets the maximum capacity.
    /// </summary>
    public required int MaxCapacity { get; init; }

    /// <summary>
    /// Gets the total messages ever dead-lettered.
    /// </summary>
    public required long TotalDeadLettered { get; init; }

    /// <summary>
    /// Gets the count by reason.
    /// </summary>
    public required IReadOnlyDictionary<DeadLetterReason, int> CountByReason { get; init; }

    /// <summary>
    /// Gets when the oldest entry was dead-lettered.
    /// </summary>
    public DateTimeOffset? OldestEntryTime { get; init; }

    /// <summary>
    /// Gets when the newest entry was dead-lettered.
    /// </summary>
    public DateTimeOffset? NewestEntryTime { get; init; }

    /// <summary>
    /// Gets when the statistics were captured.
    /// </summary>
    public required DateTimeOffset CapturedAt { get; init; }
}

/// <summary>
/// Configuration options for the dead letter queue.
/// </summary>
public sealed class DeadLetterQueueOptions
{
    /// <summary>
    /// Gets or sets the maximum capacity of the dead letter queue.
    /// </summary>
    /// <remarks>
    /// When the DLQ is full, oldest entries are automatically removed.
    /// Default: 10,000 entries.
    /// </remarks>
    public int MaxCapacity { get; init; } = 10_000;

    /// <summary>
    /// Gets or sets the retention period for dead letter entries.
    /// </summary>
    /// <remarks>
    /// Entries older than this are automatically cleaned up.
    /// Default: 7 days.
    /// </remarks>
    public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets whether to store exception details.
    /// </summary>
    /// <remarks>
    /// When false, exceptions are summarized to reduce memory usage.
    /// Default: true.
    /// </remarks>
    public bool StoreExceptionDetails { get; init; } = true;

    /// <summary>
    /// Gets or sets the cleanup interval for expired entries.
    /// </summary>
    /// <remarks>
    /// How often to run the cleanup task for entries past retention.
    /// Default: 1 hour.
    /// </remarks>
    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets whether to enable automatic cleanup.
    /// </summary>
    /// <remarks>
    /// Default: true.
    /// </remarks>
    public bool EnableAutoCleanup { get; init; } = true;

    /// <summary>
    /// Default dead letter queue options.
    /// </summary>
    public static DeadLetterQueueOptions Default { get; } = new();
}

/// <summary>
/// Manages message TTL enforcement and expiration handling.
/// </summary>
public interface IMessageTtlManager : IAsyncDisposable
{
    /// <summary>
    /// Checks if a message has expired.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to check.</param>
    /// <returns>True if the message has expired.</returns>
    public bool IsExpired<TMessage>(TMessage message) where TMessage : IRingKernelMessage;

    /// <summary>
    /// Gets the remaining TTL for a message.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to check.</param>
    /// <returns>Remaining time, or TimeSpan.Zero if expired, or null if no TTL.</returns>
    public TimeSpan? GetRemainingTtl<TMessage>(TMessage message) where TMessage : IRingKernelMessage;

    /// <summary>
    /// Tracks a message for TTL enforcement.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="message">The message to track.</param>
    /// <param name="onExpired">Callback when the message expires.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tracking ID that can be used to cancel tracking.</returns>
    public Task<Guid> TrackAsync<TMessage>(
        TMessage message,
        Func<TMessage, Task> onExpired,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage;

    /// <summary>
    /// Stops tracking a message.
    /// </summary>
    /// <param name="trackingId">The tracking ID from <see cref="TrackAsync{TMessage}"/>.</param>
    /// <returns>True if tracking was cancelled.</returns>
    public bool CancelTracking(Guid trackingId);

    /// <summary>
    /// Gets the number of messages currently being tracked.
    /// </summary>
    public int TrackedCount { get; }

    /// <summary>
    /// Gets the number of messages that have expired while being tracked.
    /// </summary>
    public long ExpiredCount { get; }

    /// <summary>
    /// Occurs when a tracked message expires.
    /// </summary>
    public event EventHandler<MessageExpiredEventArgs>? MessageExpired;
}

/// <summary>
/// Event arguments for message expiration events.
/// </summary>
public sealed class MessageExpiredEventArgs : EventArgs
{
    /// <summary>
    /// Gets the message ID of the expired message.
    /// </summary>
    public required Guid MessageId { get; init; }

    /// <summary>
    /// Gets the message type.
    /// </summary>
    public required string MessageType { get; init; }

    /// <summary>
    /// Gets when the message was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the configured TTL.
    /// </summary>
    public required TimeSpan TimeToLive { get; init; }

    /// <summary>
    /// Gets when the message expired.
    /// </summary>
    public required DateTimeOffset ExpiredAt { get; init; }
}

/// <summary>
/// Factory for creating dead letter queues.
/// </summary>
public interface IDeadLetterQueueFactory
{
    /// <summary>
    /// Creates a new dead letter queue with the specified name.
    /// </summary>
    /// <param name="name">The queue name.</param>
    /// <param name="options">Optional configuration.</param>
    /// <returns>A new dead letter queue instance.</returns>
    public IDeadLetterQueue Create(string name, DeadLetterQueueOptions? options = null);

    /// <summary>
    /// Gets or creates a dead letter queue for the specified source queue.
    /// </summary>
    /// <param name="sourceQueueName">The source queue name.</param>
    /// <param name="options">Optional configuration.</param>
    /// <returns>A dead letter queue instance.</returns>
    public IDeadLetterQueue GetOrCreate(string sourceQueueName, DeadLetterQueueOptions? options = null);
}
