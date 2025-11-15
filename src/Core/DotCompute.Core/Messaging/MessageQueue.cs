// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Messaging;

namespace DotCompute.Core.Messaging;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - MessageQueue is intentional

/// <summary>
/// Lock-free concurrent message queue using a ring buffer for high-performance message passing.
/// </summary>
/// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// Uses atomic operations (Interlocked) for head/tail pointers to achieve lock-free
/// enqueue and dequeue operations. Provides configurable backpressure strategies and
/// optional message deduplication.
///
/// <para>
/// <b>Performance:</b>
/// - Target: &lt;50ns per enqueue/dequeue (lock-free path)
/// - Zero allocations after initialization
/// - Capacity must be a power of 2 for efficient modulo operations
/// </para>
///
/// <para>
/// <b>Thread Safety:</b> All operations are thread-safe and can be called concurrently
/// from multiple threads without external synchronization.
/// </para>
/// </remarks>
public sealed class MessageQueue<T> : IMessageQueue<T>
    where T : IRingKernelMessage
{
    private readonly T?[] _buffer;
    private readonly int _capacityMask; // capacity - 1, for fast modulo
    private readonly MessageQueueOptions _options;
    private readonly ConcurrentDictionary<Guid, byte>? _seenMessages;
    private readonly SemaphoreSlim? _semaphore;
    private readonly DateTime _createdAt;

    private long _head; // Write position (enqueue)
    private long _tail; // Read position (dequeue)
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageQueue{T}"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the queue.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when options are invalid.
    /// </exception>
    public MessageQueue(MessageQueueOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Validate(); // Rounds capacity to power of 2
        _options = options;

        _buffer = new T[options.Capacity];
        _capacityMask = options.Capacity - 1;
        _createdAt = DateTime.UtcNow;

        // Initialize deduplication tracking if enabled
        if (options.EnableDeduplication)
        {
            _seenMessages = new ConcurrentDictionary<Guid, byte>();
        }

        // Initialize semaphore for blocking backpressure strategy
        // Semaphore tracks number of items in queue (0 = empty, capacity = full)
        if (options.BackpressureStrategy == BackpressureStrategy.Block)
        {
            _semaphore = new SemaphoreSlim(0, options.Capacity);
        }
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            long head = Interlocked.Read(ref _head);
            long tail = Interlocked.Read(ref _tail);
            return (int)(head - tail);
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
                    // Wait for space to become available (CurrentCount < Capacity)
                    if (_semaphore is null)
                    {
                        throw new InvalidOperationException("Semaphore not initialized for Block strategy");
                    }

                    try
                    {
                        // Poll until space available or timeout
                        var startTime = DateTime.UtcNow;
                        while (_semaphore.CurrentCount >= _options.Capacity)
                        {
                            if (DateTime.UtcNow - startTime > _options.BlockTimeout)
                            {
                                // Timeout waiting for space
                                RemoveFromDeduplication(message.MessageId);
                                return false;
                            }
                            if (cancellationToken.IsCancellationRequested)
                            {
                                RemoveFromDeduplication(message.MessageId);
                                throw new OperationCanceledException();
                            }
                            Thread.Sleep(1); // Small delay to reduce CPU usage
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        RemoveFromDeduplication(message.MessageId);
                        throw;
                    }
                    break;

                case BackpressureStrategy.DropOldest:
                    // Dequeue oldest message to make space
                    if (!TryDequeue(out _))
                    {
                        // Queue became empty between check and dequeue (rare race condition)
                        // Just proceed with enqueue
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

        // Atomic enqueue operation
        long writeIndex = Interlocked.Increment(ref _head) - 1;
        int slotIndex = (int)(writeIndex & _capacityMask);

        // Write message to buffer slot
        Interlocked.Exchange(ref _buffer[slotIndex], message);

        // Signal item added (for blocking backpressure strategy)
        if (_options.BackpressureStrategy == BackpressureStrategy.Block)
        {
            _semaphore?.Release();
        }

        return true;
    }

    /// <inheritdoc/>
    public bool TryDequeue(out T? message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        message = default;

        // Check if queue is empty
        long head = Interlocked.Read(ref _head);
        long tail = Interlocked.Read(ref _tail);

        if (tail >= head)
        {
            return false; // Queue is empty
        }

        // Atomic dequeue operation
        long readIndex = Interlocked.Increment(ref _tail) - 1;

        // Double-check we didn't race past head
        if (readIndex >= Interlocked.Read(ref _head))
        {
            // Raced past head, queue was actually empty
            Interlocked.Decrement(ref _tail); // Restore tail
            return false;
        }

        int slotIndex = (int)(readIndex & _capacityMask);

        // Read and clear buffer slot atomically
        message = Interlocked.Exchange(ref _buffer[slotIndex], default);

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

            // Decrement item count if using blocking strategy (only if we successfully dequeued)
            if (_options.BackpressureStrategy == BackpressureStrategy.Block)
            {
                _semaphore?.Wait(0); // Non-blocking decrement
            }
        }

        return message is not null;
    }

    /// <inheritdoc/>
    public bool TryPeek(out T? message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        message = default;

        // Check if queue is empty
        long head = Interlocked.Read(ref _head);
        long tail = Interlocked.Read(ref _tail);

        if (tail >= head)
        {
            return false; // Queue is empty
        }

        // Read without advancing tail (non-atomic peek)
        int slotIndex = (int)(tail & _capacityMask);
        message = Interlocked.CompareExchange(ref _buffer[slotIndex], default, default);

        return message is not null;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Reset head and tail atomically (move tail to head = empty queue)
        long currentHead = Interlocked.Read(ref _head);
        Interlocked.Exchange(ref _tail, currentHead);

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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
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
