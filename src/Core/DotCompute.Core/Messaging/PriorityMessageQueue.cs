// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Messaging;

namespace DotCompute.Core.Messaging;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - PriorityMessageQueue is intentional

/// <summary>
/// Priority-based message queue using a binary heap for message ordering by priority.
/// </summary>
/// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// Messages are dequeued in priority order (highest priority first). Uses a
/// <see cref="PriorityQueue{TElement, TPriority}"/> internally with reader-writer locking
/// for thread safety.
///
/// <para>
/// <b>Performance:</b>
/// - Enqueue: O(log n) with write lock
/// - Dequeue: O(log n) with write lock
/// - Slower than <see cref="MessageQueue{T}"/> but ensures priority ordering
/// - Recommended for systems where message priority is critical
/// </para>
///
/// <para>
/// <b>Thread Safety:</b> Uses <see cref="ReaderWriterLockSlim"/> for thread-safe
/// operations. Multiple readers can peek concurrently, but enqueue/dequeue are
/// serialized with write locks.
/// </para>
/// </remarks>
public sealed class PriorityMessageQueue<T> : IMessageQueue<T>
    where T : IRingKernelMessage
{
    private readonly PriorityQueue<T, byte> _heap;
    private readonly ReaderWriterLockSlim _lock;
    private readonly MessageQueueOptions _options;
    private readonly ConcurrentDictionary<Guid, byte>? _seenMessages;
    private readonly SemaphoreSlim? _semaphore;
    private readonly DateTime _createdAt;

    private int _count;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PriorityMessageQueue{T}"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the queue.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when options are invalid.
    /// </exception>
    public PriorityMessageQueue(MessageQueueOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();
        _options = options;

        // Initialize priority queue (max-heap by inverting priority: 255 - priority)
        _heap = new PriorityQueue<T, byte>(options.Capacity);
        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _createdAt = DateTime.UtcNow;

        // Initialize deduplication tracking if enabled
        if (options.EnableDeduplication)
        {
            _seenMessages = new ConcurrentDictionary<Guid, byte>();
        }

        // Initialize semaphore for blocking backpressure strategy
        if (options.BackpressureStrategy == BackpressureStrategy.Block)
        {
            _semaphore = new SemaphoreSlim(options.Capacity, options.Capacity);
        }
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc/>
    public int Capacity => _options.Capacity;

    /// <inheritdoc/>
    public bool IsFull => Count >= Capacity;

    /// <inheritdoc/>
    public bool IsEmpty => Count == 0;

    /// <inheritdoc/>
    public bool TryEnqueue(T message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        // Check for duplicates if deduplication is enabled
        if (_seenMessages is not null)
        {
            if (!_seenMessages.TryAdd(message.MessageId, 0))
            {
                // Duplicate message - silently drop and return true (already processed)
                return true;
            }

            // Limit deduplication window size
            if (_seenMessages.Count > _options.DeduplicationWindowSize)
            {
                // Remove oldest entries (simple strategy: clear half)
                var toRemove = _seenMessages.Keys.Take(_options.DeduplicationWindowSize / 2).ToList();
                foreach (var key in toRemove)
                {
                    _ = _seenMessages.TryRemove(key, out _);
                }
            }
        }

        // Handle backpressure based on strategy
        if (IsFull)
        {
            switch (_options.BackpressureStrategy)
            {
                case BackpressureStrategy.Block:
                    // Wait for space to become available
                    if (_semaphore is null)
                    {
                        throw new InvalidOperationException("Semaphore not initialized for Block strategy");
                    }

                    try
                    {
                        if (!_semaphore.Wait(_options.BlockTimeout, cancellationToken))
                        {
                            // Timeout waiting for space
                            RemoveFromDeduplication(message.MessageId);
                            return false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        RemoveFromDeduplication(message.MessageId);
                        throw;
                    }
                    break;

                case BackpressureStrategy.DropOldest:
                    // Dequeue lowest priority message to make space
                    _lock.EnterWriteLock();
                    try
                    {
                        if (_heap.TryPeek(out _, out var lowestPriority))
                        {
                            // Only drop if new message has higher priority
                            if (message.Priority > lowestPriority)
                            {
                                if (_heap.TryDequeue(out var dropped, out _))
                                {
                                    RemoveFromDeduplication(dropped.MessageId);
                                    _count--;
                                }
                            }
                            else
                            {
                                // New message has lower/equal priority - drop it instead
                                RemoveFromDeduplication(message.MessageId);
                                return true;
                            }
                        }
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                    break;

                case BackpressureStrategy.Reject:
                    // Return false to reject the message
                    RemoveFromDeduplication(message.MessageId);
                    return false;

                case BackpressureStrategy.DropNew:
                    // Drop the new message silently
                    RemoveFromDeduplication(message.MessageId);
                    return true; // Return true despite dropping (fire-and-forget semantics)
            }
        }

        // Enqueue with priority (invert priority for max-heap: higher byte value = higher priority)
        _lock.EnterWriteLock();
        try
        {
            _heap.Enqueue(message, (byte)(255 - message.Priority));
            _count++;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return true;
    }

    /// <inheritdoc/>
    public bool TryDequeue(out T? message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        message = default;

        _lock.EnterWriteLock();
        try
        {
            if (!_heap.TryDequeue(out message, out _))
            {
                return false; // Queue is empty
            }

            _count--;

            // Check message timeout
            if (message is not null && _options.MessageTimeout != TimeSpan.Zero)
            {
                var age = DateTime.UtcNow - _createdAt;
                if (age > _options.MessageTimeout)
                {
                    // Message expired - remove from deduplication and return null
                    RemoveFromDeduplication(message.MessageId);
                    message = default;
                    return false;
                }
            }

            // Remove from deduplication tracking
            if (message is not null)
            {
                RemoveFromDeduplication(message.MessageId);
            }

            // Release semaphore if using blocking strategy
            _semaphore?.Release();

            return message is not null;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public bool TryPeek(out T? message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        message = default;

        _lock.EnterReadLock();
        try
        {
            return _heap.TryPeek(out message, out _);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _lock.EnterWriteLock();
        try
        {
            _heap.Clear();
            _count = 0;

            // Clear deduplication tracking
            _seenMessages?.Clear();

            // Reset semaphore to full capacity
            if (_semaphore is not null && _options.BackpressureStrategy == BackpressureStrategy.Block)
            {
                // Drain semaphore completely
                while (_semaphore.CurrentCount > 0)
                {
                    _semaphore.Wait(0);
                }

                // Release to full capacity
                _semaphore.Release(_options.Capacity);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _lock.Dispose();
        _semaphore?.Dispose();
        _seenMessages?.Clear();

        _disposed = true;
    }

    private void RemoveFromDeduplication(Guid messageId)
    {
        _seenMessages?.TryRemove(messageId, out _);
    }
}

#pragma warning restore CA1711
