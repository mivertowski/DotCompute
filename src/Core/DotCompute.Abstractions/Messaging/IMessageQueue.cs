// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Messaging;

#pragma warning disable CA1711 // Identifiers should not have incorrect suffix - IMessageQueue is intentional

/// <summary>
/// Non-generic base interface for message queues (used for reflection).
/// </summary>
/// <remarks>
/// This interface provides non-generic access to message queue properties
/// and operations for scenarios requiring reflection or dynamic typing.
/// </remarks>
public interface IMessageQueue : IDisposable
{
    /// <summary>
    /// Gets the current number of messages in the queue.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets the maximum capacity of the queue.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets a value indicating whether the queue is full.
    /// </summary>
    public bool IsFull { get; }

    /// <summary>
    /// Gets a value indicating whether the queue is empty.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Removes all messages from the queue.
    /// </summary>
    public void Clear();
}

/// <summary>
/// Thread-safe message queue for Ring Kernel actor communication.
/// </summary>
/// <typeparam name="T">The message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// Provides lock-free or low-contention message passing between Ring Kernels
/// with configurable backpressure, priority ordering, and deduplication.
///
/// <para>
/// <b>Thread Safety:</b> All operations are thread-safe and can be called
/// concurrently from multiple threads. Lock-free implementations use atomic
/// operations for optimal performance.
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
///
/// // Producer
/// var message = new MyMessage { Value = 42 };
/// if (queue.TryEnqueue(message))
/// {
///     Console.WriteLine($"Enqueued: {message.MessageId}");
/// }
///
/// // Consumer
/// if (queue.TryDequeue(out var received))
/// {
///     Console.WriteLine($"Received: {received.Value}");
/// }
/// </code>
/// </para>
/// </remarks>
public interface IMessageQueue<T> : IMessageQueue
    where T : IRingKernelMessage
{
    /// <summary>
    /// Attempts to enqueue a message to the queue.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="cancellationToken">Cancellation token for blocking operations.</param>
    /// <returns>
    /// True if the message was successfully enqueued; false if the queue is full
    /// and the backpressure strategy is <see cref="BackpressureStrategy.Reject"/>.
    /// </returns>
    /// <remarks>
    /// Behavior depends on the configured <see cref="MessageQueueOptions.BackpressureStrategy"/>:
    /// - <b>Block</b>: Waits until space is available (respects cancellation token)
    /// - <b>DropOldest</b>: Drops oldest message and enqueues new one (returns true)
    /// - <b>Reject</b>: Returns false immediately if queue is full
    /// - <b>DropNew</b>: Drops new message and returns true
    ///
    /// <para>
    /// If deduplication is enabled, duplicate messages (same <see cref="IRingKernelMessage.MessageId"/>)
    /// are silently dropped and true is returned.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="message"/> is null.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is cancelled via <paramref name="cancellationToken"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the queue has been disposed.
    /// </exception>
    public bool TryEnqueue(T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to dequeue a message from the queue.
    /// </summary>
    /// <param name="message">
    /// When this method returns true, contains the dequeued message;
    /// otherwise, null.
    /// </param>
    /// <returns>
    /// True if a message was successfully dequeued; false if the queue is empty.
    /// </returns>
    /// <remarks>
    /// This is a non-blocking operation. If the queue is empty, the method
    /// returns immediately with false.
    ///
    /// <para>
    /// For priority queues, returns the highest priority message.
    /// For FIFO queues, returns the oldest message.
    /// </para>
    ///
    /// <para>
    /// Expired messages (older than <see cref="MessageQueueOptions.MessageTimeout"/>)
    /// are automatically removed and not returned.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the queue has been disposed.
    /// </exception>
    public bool TryDequeue(out T? message);

    /// <summary>
    /// Attempts to peek at the next message without removing it from the queue.
    /// </summary>
    /// <param name="message">
    /// When this method returns true, contains the next message;
    /// otherwise, null.
    /// </param>
    /// <returns>
    /// True if a message is available; false if the queue is empty.
    /// </returns>
    /// <remarks>
    /// This is a non-blocking operation. The message remains in the queue
    /// after peeking and will be returned by subsequent Dequeue operations.
    ///
    /// <para>
    /// Thread safety: Another thread may dequeue the peeked message before
    /// you attempt to dequeue it. Use TryDequeue for atomic read-and-remove.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the queue has been disposed.
    /// </exception>
    public bool TryPeek(out T? message);
}

#pragma warning restore CA1711
