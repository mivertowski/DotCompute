// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Factory for creating message queue instances based on configuration options.
/// </summary>
/// <remarks>
/// Provides a centralized way to create message queues with appropriate implementations
/// based on the configuration. Selects between lock-free ring buffer (<see cref="MessageQueue{T}"/>)
/// and priority-based heap (<see cref="PriorityMessageQueue{T}"/>) implementations.
/// </remarks>
public static class MessageQueueFactory
{
    /// <summary>
    /// Creates a message queue instance based on the provided options.
    /// </summary>
    /// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="options">Configuration options for the queue.</param>
    /// <returns>
    /// A message queue instance. Returns <see cref="PriorityMessageQueue{T}"/> if
    /// <see cref="MessageQueueOptions.EnablePriorityQueue"/> is true; otherwise, returns
    /// <see cref="MessageQueue{T}"/> for optimal performance.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when options are invalid.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Implementation Selection:</b>
    /// - <see cref="MessageQueue{T}"/>: Lock-free FIFO queue with atomic operations (~50ns per operation)
    /// - <see cref="PriorityMessageQueue{T}"/>: Binary heap with priority ordering (O(log n) per operation)
    /// </para>
    ///
    /// <para>
    /// <b>Usage:</b>
    /// <code>
    /// var options = new MessageQueueOptions
    /// {
    ///     Capacity = 2048,
    ///     EnablePriorityQueue = true,
    ///     BackpressureStrategy = BackpressureStrategy.DropOldest
    /// };
    ///
    /// using var queue = MessageQueueFactory.Create&lt;MyMessage&gt;(options);
    /// </code>
    /// </para>
    /// </remarks>
    public static IMessageQueue<T> Create<T>(MessageQueueOptions options)
        where T : IRingKernelMessage
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.EnablePriorityQueue
            ? new PriorityMessageQueue<T>(options)
            : new MessageQueue<T>(options);
    }

    /// <summary>
    /// Creates a message queue instance with default options.
    /// </summary>
    /// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <returns>
    /// A message queue instance with default configuration (capacity: 1024, FIFO ordering,
    /// deduplication enabled, Block backpressure strategy).
    /// </returns>
    /// <remarks>
    /// Creates a lock-free <see cref="MessageQueue{T}"/> with default settings suitable
    /// for most use cases. For custom configuration, use <see cref="Create{T}(MessageQueueOptions)"/>.
    /// </remarks>
    public static IMessageQueue<T> CreateDefault<T>()
        where T : IRingKernelMessage
        => new MessageQueue<T>(new MessageQueueOptions());

    /// <summary>
    /// Creates a priority-based message queue with default options.
    /// </summary>
    /// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <returns>
    /// A priority message queue instance with default configuration (capacity: 1024,
    /// priority ordering enabled, deduplication enabled, Block backpressure strategy).
    /// </returns>
    /// <remarks>
    /// Creates a <see cref="PriorityMessageQueue{T}"/> with default settings for
    /// scenarios where message priority is important. Messages are dequeued in order
    /// of highest priority first.
    /// </remarks>
    public static IMessageQueue<T> CreatePriority<T>()
        where T : IRingKernelMessage
        => new PriorityMessageQueue<T>(new MessageQueueOptions
        {
            EnablePriorityQueue = true
        });

    /// <summary>
    /// Creates a high-throughput message queue optimized for performance.
    /// </summary>
    /// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
    /// <param name="capacity">The queue capacity (must be power of 2, will be rounded up).</param>
    /// <returns>
    /// A lock-free message queue optimized for high throughput with minimal overhead.
    /// </returns>
    /// <remarks>
    /// Creates a <see cref="MessageQueue{T}"/> with optimizations for maximum throughput:
    /// - Deduplication disabled (no dictionary overhead)
    /// - DropOldest backpressure (non-blocking)
    /// - No message timeout checking
    ///
    /// Use this for high-frequency, low-latency message passing where deduplication
    /// and message timeout are not required.
    /// </remarks>
    public static IMessageQueue<T> CreateHighThroughput<T>(int capacity = 4096)
        where T : IRingKernelMessage
        => new MessageQueue<T>(new MessageQueueOptions
        {
            Capacity = capacity,
            EnableDeduplication = false,
            BackpressureStrategy = BackpressureStrategy.DropOldest,
            MessageTimeout = TimeSpan.Zero
        });
}
