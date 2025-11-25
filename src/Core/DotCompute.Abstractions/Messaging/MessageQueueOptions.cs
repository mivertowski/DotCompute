// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Messaging;

/// <summary>
/// Configuration options for Ring Kernel message queues.
/// </summary>
/// <remarks>
/// Controls queue behavior including capacity, priority handling,
/// deduplication, and backpressure strategies.
/// </remarks>
public sealed class MessageQueueOptions
{
    /// <summary>
    /// Gets or sets the maximum queue capacity (number of messages).
    /// </summary>
    /// <remarks>
    /// Must be a power of 2 for efficient modulo operations in ring buffer.
    /// Values are automatically rounded up to the nearest power of 2.
    ///
    /// Default: 1024 messages
    /// Range: 16 - 1048576 (16 - 1M messages)
    /// </remarks>
    public int Capacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets a value indicating whether to use a priority queue.
    /// </summary>
    /// <remarks>
    /// When true, messages are dequeued in priority order (highest first).
    /// When false, messages are dequeued in FIFO order.
    ///
    /// Priority queues have higher overhead (~50ns vs ~20ns per operation)
    /// but ensure critical messages are processed first.
    ///
    /// Default: false (FIFO queue)
    /// </remarks>
    public bool EnablePriorityQueue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable message deduplication.
    /// </summary>
    /// <remarks>
    /// When true, duplicate messages (same MessageId) are silently dropped.
    /// Uses a concurrent set to track recently seen message IDs.
    ///
    /// Deduplication adds ~10ns overhead per enqueue operation and
    /// consumes 16 bytes per tracked message ID.
    ///
    /// Default: true
    /// </remarks>
    public bool EnableDeduplication { get; set; } = true;

    /// <summary>
    /// Gets or sets the deduplication window size (number of message IDs to track).
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="EnableDeduplication"/> is true.
    /// Older message IDs are evicted when this limit is reached.
    /// Must be between 16 and Capacity * 4 (validation enforced).
    ///
    /// Default: 256 messages (compatible with small queues)
    /// </remarks>
    public int DeduplicationWindowSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the message timeout duration.
    /// </summary>
    /// <remarks>
    /// Messages older than this timeout are automatically removed during
    /// dequeue operations. Set to <see cref="TimeSpan.Zero"/> to disable.
    ///
    /// Default: 30 seconds
    /// </remarks>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the backpressure strategy when the queue is full.
    /// </summary>
    /// <remarks>
    /// Determines behavior when attempting to enqueue to a full queue:
    /// - Block: Wait until space available (may deadlock if not careful)
    /// - Drop: Silently drop the oldest message and enqueue new one
    /// - Reject: Throw <see cref="InvalidOperationException"/>
    ///
    /// Default: <see cref="BackpressureStrategy.Block"/>
    /// </remarks>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Block;

    /// <summary>
    /// Gets or sets the block timeout when using <see cref="BackpressureStrategy.Block"/>.
    /// </summary>
    /// <remarks>
    /// Maximum time to wait for queue space before throwing exception.
    /// Set to <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
    ///
    /// Default: 5 seconds
    /// </remarks>
    public TimeSpan BlockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Validates the options and normalizes values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when options are invalid.
    /// </exception>
    public void Validate()
    {
        if (Capacity < 16 || Capacity > 1_048_576)
        {
            throw new ArgumentOutOfRangeException(nameof(Capacity), Capacity,
                "Capacity must be between 16 and 1,048,576 messages.");
        }

        // Round capacity up to nearest power of 2
        if (!IsPowerOfTwo(Capacity))
        {
            Capacity = RoundUpToPowerOfTwo(Capacity);
        }

        if (DeduplicationWindowSize < 16 || DeduplicationWindowSize > Capacity * 4)
        {
            throw new ArgumentOutOfRangeException(nameof(DeduplicationWindowSize),
                DeduplicationWindowSize,
                $"DeduplicationWindowSize must be between 16 and {Capacity * 4}.");
        }

        if (MessageTimeout < TimeSpan.Zero && MessageTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(MessageTimeout),
                MessageTimeout, "MessageTimeout must be positive or Timeout.InfiniteTimeSpan.");
        }

        if (BlockTimeout < TimeSpan.Zero && BlockTimeout != Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(BlockTimeout),
                BlockTimeout, "BlockTimeout must be positive or Timeout.InfiniteTimeSpan.");
        }
    }

    private static bool IsPowerOfTwo(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }

    private static int RoundUpToPowerOfTwo(int n)
    {
        n--;
        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        n++;
        return n;
    }
}

/// <summary>
/// Defines strategies for handling backpressure when a message queue is full.
/// </summary>
public enum BackpressureStrategy
{
    /// <summary>
    /// Block the enqueueing thread until space becomes available.
    /// </summary>
    /// <remarks>
    /// Provides flow control but risks deadlock if producer/consumer
    /// are on the same thread or circular dependencies exist.
    ///
    /// Best for: Producer can afford to wait, strong delivery guarantees needed
    /// </remarks>
    Block = 0,

    /// <summary>
    /// Drop the oldest message from the queue and enqueue the new message.
    /// </summary>
    /// <remarks>
    /// Maintains queue capacity by evicting old messages. May lose data
    /// but ensures the most recent messages are processed.
    ///
    /// Best for: Real-time systems, latest data more valuable than old data
    /// </remarks>
    DropOldest = 1,

    /// <summary>
    /// Reject the new message by returning false from TryEnqueue.
    /// </summary>
    /// <remarks>
    /// Caller is responsible for handling rejection (retry, log, dead letter, etc.).
    /// No blocking, no data loss from the existing queue.
    ///
    /// Best for: Caller has application-specific retry/fallback logic
    /// </remarks>
    Reject = 2,

    /// <summary>
    /// Drop the new message silently without enqueueing.
    /// </summary>
    /// <remarks>
    /// Similar to Reject but returns true from TryEnqueue despite dropping.
    /// May cause message loss but maintains non-blocking behavior.
    ///
    /// Best for: Fire-and-forget messaging, telemetry, non-critical data
    /// </remarks>
    DropNew = 3
}
