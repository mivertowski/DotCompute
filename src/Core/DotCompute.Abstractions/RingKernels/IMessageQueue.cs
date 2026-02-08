// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.RingKernels;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - IMessageQueue is intentional

/// <summary>
/// Non-generic base interface for GPU-resident message queues (used for reflection).
/// </summary>
/// <remarks>
/// This interface provides non-generic access to message queue properties
/// and operations for scenarios requiring reflection or dynamic typing in ring kernel runtimes.
/// </remarks>
public interface IMessageQueue : IAsyncDisposable
{
    /// <summary>
    /// Gets the queue capacity (maximum number of messages).
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets whether the queue is empty.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Gets whether the queue is full.
    /// </summary>
    public bool IsFull { get; }

    /// <summary>
    /// Gets the approximate current size of the queue.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the memory buffer handle for GPU access.
    /// </summary>
    public IUnifiedMemoryBuffer GetBuffer();

    /// <summary>
    /// Gets the head pointer buffer (atomic counter for dequeue position).
    /// </summary>
    public IUnifiedMemoryBuffer GetHeadPtr();

    /// <summary>
    /// Gets the tail pointer buffer (atomic counter for enqueue position).
    /// </summary>
    public IUnifiedMemoryBuffer GetTailPtr();

    /// <summary>
    /// Initializes the message queue and allocates GPU buffers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the initialization operation.</returns>
    public Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all messages from the queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the clear operation.</returns>
    public Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queue statistics for monitoring.
    /// </summary>
    /// <returns>Queue statistics including message counts and throughput.</returns>
    public Task<MessageQueueStatistics> GetStatisticsAsync();
}

/// <summary>
/// Represents a GPU-resident lock-free message queue for inter-kernel communication.
/// </summary>
/// <typeparam name="T">
/// The message payload type. Must be an unmanaged type for GPU transfer.
/// </typeparam>
/// <remarks>
/// MessageQueue implements a lock-free ring buffer using atomic operations
/// for concurrent access from multiple GPU threads. The implementation supports:
/// - Multiple concurrent producers (enqueue)
/// - Multiple concurrent consumers (dequeue)
/// - GPU-resident buffers to avoid CPU-GPU transfers
/// - Atomic head/tail indices for thread safety
///
/// Queue capacity should be a power of 2 for optimal modulo operations.
/// </remarks>
public interface IMessageQueue<T> : IMessageQueue where T : unmanaged
{
    /// <summary>
    /// Attempts to enqueue a message (non-blocking).
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was enqueued; false if the queue was full.</returns>
    /// <remarks>
    /// This is a non-blocking operation that returns false if the queue is full.
    /// Use BlockingEnqueue if you need guaranteed delivery with backpressure.
    /// </remarks>
    public Task<bool> TryEnqueueAsync(KernelMessage<T> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to dequeue a message (non-blocking).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The dequeued message if available; null if the queue was empty.
    /// </returns>
    /// <remarks>
    /// This is a non-blocking operation that returns null if the queue is empty.
    /// Use BlockingDequeue if you need to wait for messages.
    /// </remarks>
    public Task<KernelMessage<T>?> TryDequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a message, blocking until space is available.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="timeout">Maximum wait time (default: infinite).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the message is enqueued.</returns>
    /// <exception cref="TimeoutException">
    /// Thrown if the timeout expires before space becomes available.
    /// </exception>
    public Task EnqueueAsync(KernelMessage<T> message, TimeSpan timeout = default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues a message, blocking until one is available.
    /// </summary>
    /// <param name="timeout">Maximum wait time (default: infinite).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dequeued message.</returns>
    /// <exception cref="TimeoutException">
    /// Thrown if the timeout expires before a message arrives.
    /// </exception>
    public Task<KernelMessage<T>> DequeueAsync(TimeSpan timeout = default,
        CancellationToken cancellationToken = default);
}

#pragma warning restore CA1711

/// <summary>
/// Statistics for monitoring message queue performance.
/// </summary>
public struct MessageQueueStatistics : IEquatable<MessageQueueStatistics>
{
    /// <summary>
    /// Total messages enqueued since initialization.
    /// </summary>
    public long TotalEnqueued { get; init; }

    /// <summary>
    /// Total messages dequeued since initialization.
    /// </summary>
    public long TotalDequeued { get; init; }

    /// <summary>
    /// Total messages dropped due to full queue.
    /// </summary>
    public long TotalDropped { get; init; }

    /// <summary>
    /// Current queue utilization (0.0 to 1.0).
    /// </summary>
    public double Utilization { get; init; }

    /// <summary>
    /// Average enqueue throughput (messages per second).
    /// </summary>
    public double EnqueueThroughput { get; init; }

    /// <summary>
    /// Average dequeue throughput (messages per second).
    /// </summary>
    public double DequeueThroughput { get; init; }

    /// <summary>
    /// Average message latency (microseconds from enqueue to dequeue).
    /// </summary>
    public double AverageLatencyUs { get; init; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is MessageQueueStatistics other && Equals(other);
    /// <inheritdoc/>
    public bool Equals(MessageQueueStatistics other) =>
        TotalEnqueued == other.TotalEnqueued &&
        TotalDequeued == other.TotalDequeued &&
        TotalDropped == other.TotalDropped &&
        Utilization == other.Utilization &&
        EnqueueThroughput == other.EnqueueThroughput &&
        DequeueThroughput == other.DequeueThroughput &&
        AverageLatencyUs == other.AverageLatencyUs;
    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(TotalEnqueued, TotalDequeued, TotalDropped, Utilization,
            EnqueueThroughput, DequeueThroughput, AverageLatencyUs);
    /// <summary>
    /// Determines whether two instances are equal.
    /// </summary>
    public static bool operator ==(MessageQueueStatistics left, MessageQueueStatistics right) =>
        left.Equals(right);
    /// <summary>
    /// Determines whether two instances are not equal.
    /// </summary>
    public static bool operator !=(MessageQueueStatistics left, MessageQueueStatistics right) =>
        !left.Equals(right);
}
