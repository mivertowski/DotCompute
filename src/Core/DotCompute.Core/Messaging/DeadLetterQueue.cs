// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Messaging;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - DeadLetterQueue is intentional

/// <summary>
/// Thread-safe implementation of the dead letter queue.
/// </summary>
public sealed partial class DeadLetterQueue : IDeadLetterQueue
{
    private readonly ConcurrentDictionary<Guid, DeadLetterEntry> _entries = new();
    private readonly ConcurrentQueue<Guid> _orderedEntryIds = new();
    private readonly DeadLetterQueueOptions _options;
    private readonly ILogger<DeadLetterQueue> _logger;
    private readonly Timer? _cleanupTimer;
    private readonly object _lock = new();
    private long _totalDeadLettered;
    private bool _disposed;

    // Event IDs: 9500-9599 for DeadLetterQueue
    [LoggerMessage(EventId = 9500, Level = LogLevel.Warning,
        Message = "Message {MessageId} dead-lettered: {Reason} - {ErrorMessage}")]
    private static partial void LogMessageDeadLettered(ILogger logger, Guid messageId, DeadLetterReason reason, string? errorMessage);

    [LoggerMessage(EventId = 9501, Level = LogLevel.Debug,
        Message = "Dead letter queue '{Name}' cleanup: removed {RemovedCount} expired entries")]
    private static partial void LogCleanupCompleted(ILogger logger, string name, int removedCount);

    [LoggerMessage(EventId = 9502, Level = LogLevel.Debug,
        Message = "Dead letter queue '{Name}' capacity reached, removing oldest entry")]
    private static partial void LogCapacityReached(ILogger logger, string name);

    [LoggerMessage(EventId = 9503, Level = LogLevel.Information,
        Message = "Dead letter queue '{Name}' cleared: {RemovedCount} entries removed")]
    private static partial void LogQueueCleared(ILogger logger, string name, int removedCount);

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public int Count => _entries.Count;

    /// <inheritdoc />
    public int MaxCapacity => _options.MaxCapacity;

    /// <inheritdoc />
    public long TotalDeadLettered => Interlocked.Read(ref _totalDeadLettered);

    /// <inheritdoc />
    public event EventHandler<DeadLetterEventArgs>? MessageDeadLettered;

    /// <summary>
    /// Creates a new dead letter queue.
    /// </summary>
    /// <param name="name">The queue name.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public DeadLetterQueue(string name, DeadLetterQueueOptions options, ILogger<DeadLetterQueue> logger)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_options.EnableAutoCleanup && _options.CleanupInterval > TimeSpan.Zero)
        {
            _cleanupTimer = new Timer(
                CleanupCallback,
                null,
                _options.CleanupInterval,
                _options.CleanupInterval);
        }
    }

    /// <inheritdoc />
    public Task<DeadLetterEntry> EnqueueAsync<TMessage>(
        TMessage message,
        DeadLetterReason reason,
        string? errorMessage = null,
        Exception? exception = null,
        int attemptCount = 1,
        string? sourceQueue = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        cancellationToken.ThrowIfCancellationRequested();

        // Get original creation time from message if it implements IExpirableMessage
        var originalCreatedAt = message is IExpirableMessage expirable
            ? expirable.CreatedAt
            : DateTimeOffset.UtcNow;

        // Serialize the message
        var payload = message.Serialize().ToArray();

        // Create the dead letter entry
        var storedError = _options.StoreExceptionDetails
            ? errorMessage
            : errorMessage?.Length > 500 ? errorMessage[..500] + "..." : errorMessage;

        var storedException = _options.StoreExceptionDetails ? exception : null;

        var entry = new DeadLetterEntry(
            message.MessageId,
            message.MessageType,
            payload,
            reason,
            originalCreatedAt,
            storedError,
            storedException,
            attemptCount,
            sourceQueue,
            metadata);

        // Ensure capacity
        EnsureCapacity();

        // Add to the queue
        _entries[entry.EntryId] = entry;
        _orderedEntryIds.Enqueue(entry.EntryId);
        Interlocked.Increment(ref _totalDeadLettered);

        LogMessageDeadLettered(_logger, message.MessageId, reason, errorMessage);

        // Raise event
        MessageDeadLettered?.Invoke(this, new DeadLetterEventArgs
        {
            Entry = entry,
            QueueName = Name
        });

        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public bool TryDequeue(out DeadLetterEntry? entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        while (_orderedEntryIds.TryDequeue(out var entryId))
        {
            if (_entries.TryRemove(entryId, out entry))
            {
                return true;
            }
        }

        entry = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryPeek(out DeadLetterEntry? entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_orderedEntryIds.TryPeek(out var entryId) &&
            _entries.TryGetValue(entryId, out entry))
        {
            return true;
        }

        entry = null;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<DeadLetterEntry> GetEntries(
        Func<DeadLetterEntry, bool>? predicate = null,
        int maxResults = 100)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var query = _entries.Values.AsEnumerable();
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return query.Take(maxResults).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<DeadLetterEntry> GetEntriesByReason(
        DeadLetterReason reason,
        int maxResults = 100)
    {
        return GetEntries(e => e.Reason == reason, maxResults);
    }

    /// <inheritdoc />
    public IReadOnlyList<DeadLetterEntry> GetEntriesByTimeRange(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int maxResults = 100)
    {
        return GetEntries(e => e.DeadLetteredAt >= startTime && e.DeadLetteredAt <= endTime, maxResults);
    }

    /// <inheritdoc />
    public bool Remove(Guid entryId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_entries.TryRemove(entryId, out var entry))
        {
            entry.Dispose();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public int RemoveWhere(Func<DeadLetterEntry, bool> predicate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(predicate);

        var toRemove = _entries.Values.Where(predicate).Select(e => e.EntryId).ToList();

        var removed = 0;
        foreach (var entryId in toRemove)
        {
            if (_entries.TryRemove(entryId, out var entry))
            {
                entry.Dispose();
                removed++;
            }
        }

        return removed;
    }

    /// <inheritdoc />
    public int Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            var count = _entries.Count;

            foreach (var entry in _entries.Values)
            {
                entry.Dispose();
            }

            _entries.Clear();

            // Clear the ordered queue
            while (_orderedEntryIds.TryDequeue(out _))
            {
                // Intentionally empty - draining the queue
            }

            LogQueueCleared(_logger, Name, count);
            return count;
        }
    }

    /// <inheritdoc />
    public DeadLetterStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var entries = _entries.Values.ToList();
        var countByReason = entries
            .GroupBy(e => e.Reason)
            .ToDictionary(g => g.Key, g => g.Count());

        var orderedByTime = entries.OrderBy(e => e.DeadLetteredAt).ToList();

        return new DeadLetterStatistics
        {
            QueueName = Name,
            CurrentCount = entries.Count,
            MaxCapacity = _options.MaxCapacity,
            TotalDeadLettered = TotalDeadLettered,
            CountByReason = countByReason,
            OldestEntryTime = orderedByTime.FirstOrDefault()?.DeadLetteredAt,
            NewestEntryTime = orderedByTime.LastOrDefault()?.DeadLetteredAt,
            CapturedAt = DateTimeOffset.UtcNow
        };
    }

    private void EnsureCapacity()
    {
        while (_entries.Count >= _options.MaxCapacity)
        {
            LogCapacityReached(_logger, Name);

            if (_orderedEntryIds.TryDequeue(out var oldestId) &&
                _entries.TryRemove(oldestId, out var oldest))
            {
                oldest.Dispose();
            }
        }
    }

    private void CleanupCallback(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var cutoff = DateTimeOffset.UtcNow - _options.RetentionPeriod;
            var removed = RemoveWhere(e => e.DeadLetteredAt < cutoff);

            if (removed > 0)
            {
                LogCleanupCompleted(_logger, Name, removed);
            }
        }
        catch (ObjectDisposedException)
        {
            // Queue was disposed during cleanup
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
        _cleanupTimer?.Dispose();

        foreach (var entry in _entries.Values)
        {
            entry.Dispose();
        }

        _entries.Clear();
    }
}

/// <summary>
/// Factory for creating dead letter queues.
/// </summary>
public sealed class DeadLetterQueueFactory : IDeadLetterQueueFactory
{
    private readonly ConcurrentDictionary<string, IDeadLetterQueue> _queues = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly DeadLetterQueueOptions _defaultOptions;

    /// <summary>
    /// Creates a new dead letter queue factory.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    /// <param name="defaultOptions">Default configuration options.</param>
    public DeadLetterQueueFactory(ILoggerFactory loggerFactory, DeadLetterQueueOptions? defaultOptions = null)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _defaultOptions = defaultOptions ?? DeadLetterQueueOptions.Default;
    }

    /// <inheritdoc />
    public IDeadLetterQueue Create(string name, DeadLetterQueueOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        var logger = _loggerFactory.CreateLogger<DeadLetterQueue>();
        return new DeadLetterQueue(name, options ?? _defaultOptions, logger);
    }

    /// <inheritdoc />
    public IDeadLetterQueue GetOrCreate(string sourceQueueName, DeadLetterQueueOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(sourceQueueName);

        var dlqName = $"{sourceQueueName}.dlq";
        return _queues.GetOrAdd(dlqName, name => Create(name, options));
    }
}
