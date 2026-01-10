// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Messaging;

/// <summary>
/// Represents a peer-to-peer message queue for multi-GPU communication.
/// </summary>
/// <remarks>
/// <para>
/// P2P message queues enable efficient message passing between GPU devices
/// using direct P2P memory transfers when available, with automatic fallback
/// to host-mediated transfers when P2P is not supported.
/// </para>
/// <para>
/// <b>Transfer Modes:</b>
/// <list type="bullet">
/// <item><description>DirectP2P: Uses NVLink or PCIe P2P for lowest latency</description></item>
/// <item><description>HostStaged: Uses pinned host memory for cross-topology transfers</description></item>
/// <item><description>Adaptive: Automatically selects optimal transfer mode</description></item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="T">The message type.</typeparam>
public interface IP2PMessageQueue<T> : IAsyncDisposable
    where T : unmanaged
{
    /// <summary>
    /// Gets the source device identifier.
    /// </summary>
    public string SourceDeviceId { get; }

    /// <summary>
    /// Gets the destination device identifier.
    /// </summary>
    public string DestinationDeviceId { get; }

    /// <summary>
    /// Gets a value indicating whether direct P2P is available for this queue.
    /// </summary>
    public bool IsDirectP2PAvailable { get; }

    /// <summary>
    /// Gets the current transfer mode being used.
    /// </summary>
    public P2PTransferMode CurrentTransferMode { get; }

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
    /// Sends a message to the destination device.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the send operation.</returns>
    public ValueTask<P2PSendResult> SendAsync(T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends multiple messages to the destination device in a batch.
    /// </summary>
    /// <param name="messages">The messages to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the batch send operation.</returns>
    public ValueTask<P2PBatchSendResult> SendBatchAsync(
        ReadOnlyMemory<T> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives a message from the source device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The received message, or null if the queue is empty.</returns>
    public ValueTask<P2PReceiveResult<T>> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Receives multiple messages from the source device.
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to receive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The received messages.</returns>
    public ValueTask<P2PBatchReceiveResult<T>> ReceiveBatchAsync(
        int maxMessages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to peek at the next message without removing it.
    /// </summary>
    /// <param name="message">The peeked message if available.</param>
    /// <returns>True if a message is available; otherwise, false.</returns>
    public bool TryPeek(out T message);

    /// <summary>
    /// Clears all messages from the queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets queue statistics.
    /// </summary>
    public P2PQueueStatistics GetStatistics();
}

/// <summary>
/// Transfer modes for P2P message queues.
/// </summary>
public enum P2PTransferMode
{
    /// <summary>
    /// Automatically select the optimal transfer mode.
    /// </summary>
    Adaptive = 0,

    /// <summary>
    /// Direct GPU-to-GPU transfer using P2P (NVLink/PCIe).
    /// </summary>
    DirectP2P = 1,

    /// <summary>
    /// Transfer via pinned host memory staging buffer.
    /// </summary>
    HostStaged = 2,

    /// <summary>
    /// RDMA-based transfer for remote GPU clusters.
    /// </summary>
    RDMA = 3
}

/// <summary>
/// Result of a P2P send operation.
/// </summary>
public readonly struct P2PSendResult
{
    /// <summary>Gets a value indicating whether the send was successful.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Gets the error message if the send failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the transfer latency in microseconds.</summary>
    public double LatencyMicroseconds { get; init; }

    /// <summary>Gets the transfer mode used.</summary>
    public P2PTransferMode TransferMode { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static P2PSendResult Success(double latencyUs, P2PTransferMode mode)
        => new() { IsSuccess = true, LatencyMicroseconds = latencyUs, TransferMode = mode };

    /// <summary>Creates a failed result.</summary>
    public static P2PSendResult Failure(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Result of a P2P batch send operation.
/// </summary>
public readonly struct P2PBatchSendResult
{
    /// <summary>Gets a value indicating whether the batch send was successful.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Gets the error message if the send failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the number of messages sent.</summary>
    public int MessagesSent { get; init; }

    /// <summary>Gets the total transfer latency in microseconds.</summary>
    public double TotalLatencyMicroseconds { get; init; }

    /// <summary>Gets the throughput in messages per second.</summary>
    public double MessagesPerSecond { get; init; }

    /// <summary>Gets the transfer mode used.</summary>
    public P2PTransferMode TransferMode { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static P2PBatchSendResult Success(int count, double latencyUs, P2PTransferMode mode)
        => new()
        {
            IsSuccess = true,
            MessagesSent = count,
            TotalLatencyMicroseconds = latencyUs,
            MessagesPerSecond = latencyUs > 0 ? count / (latencyUs / 1_000_000.0) : 0,
            TransferMode = mode
        };

    /// <summary>Creates a failed result.</summary>
    public static P2PBatchSendResult Failure(string errorMessage, int messagesSent = 0)
        => new() { IsSuccess = false, ErrorMessage = errorMessage, MessagesSent = messagesSent };
}

/// <summary>
/// Result of a P2P receive operation.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
public readonly struct P2PReceiveResult<T> where T : unmanaged
{
    /// <summary>Gets a value indicating whether a message was received.</summary>
    public bool HasMessage { get; init; }

    /// <summary>Gets the received message.</summary>
    public T Message { get; init; }

    /// <summary>Gets the error message if the receive failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the receive latency in microseconds.</summary>
    public double LatencyMicroseconds { get; init; }

    /// <summary>Creates a result with a message.</summary>
    public static P2PReceiveResult<T> WithMessage(T message, double latencyUs)
        => new() { HasMessage = true, Message = message, LatencyMicroseconds = latencyUs };

    /// <summary>Creates an empty result (queue was empty).</summary>
    public static P2PReceiveResult<T> Empty()
        => new() { HasMessage = false };

    /// <summary>Creates a failed result.</summary>
    public static P2PReceiveResult<T> Failure(string errorMessage)
        => new() { HasMessage = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Result of a P2P batch receive operation.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
public readonly struct P2PBatchReceiveResult<T> where T : unmanaged
{
    /// <summary>Gets the received messages.</summary>
    public required ReadOnlyMemory<T> Messages { get; init; }

    /// <summary>Gets the error message if the receive failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the receive latency in microseconds.</summary>
    public double LatencyMicroseconds { get; init; }

    /// <summary>Gets the number of messages received.</summary>
    public int Count => Messages.Length;

    /// <summary>Creates a result with messages.</summary>
    public static P2PBatchReceiveResult<T> WithMessages(ReadOnlyMemory<T> messages, double latencyUs)
        => new() { Messages = messages, LatencyMicroseconds = latencyUs };

    /// <summary>Creates an empty result.</summary>
    public static P2PBatchReceiveResult<T> Empty()
        => new() { Messages = ReadOnlyMemory<T>.Empty };

    /// <summary>Creates a failed result.</summary>
    public static P2PBatchReceiveResult<T> Failure(string errorMessage)
        => new() { Messages = ReadOnlyMemory<T>.Empty, ErrorMessage = errorMessage };
}

/// <summary>
/// Statistics for a P2P message queue.
/// </summary>
public sealed class P2PQueueStatistics
{
    /// <summary>Gets the total number of messages sent.</summary>
    public long TotalMessagesSent { get; init; }

    /// <summary>Gets the total number of messages received.</summary>
    public long TotalMessagesReceived { get; init; }

    /// <summary>Gets the total bytes transferred.</summary>
    public long TotalBytesTransferred { get; init; }

    /// <summary>Gets the number of direct P2P transfers.</summary>
    public long DirectP2PTransfers { get; init; }

    /// <summary>Gets the number of host-staged transfers.</summary>
    public long HostStagedTransfers { get; init; }

    /// <summary>Gets the average send latency in microseconds.</summary>
    public double AverageSendLatencyMicroseconds { get; init; }

    /// <summary>Gets the average receive latency in microseconds.</summary>
    public double AverageReceiveLatencyMicroseconds { get; init; }

    /// <summary>Gets the peak throughput in messages per second.</summary>
    public double PeakThroughputMessagesPerSecond { get; init; }

    /// <summary>Gets the number of failed sends.</summary>
    public long FailedSends { get; init; }

    /// <summary>Gets the number of queue overflows (drops).</summary>
    public long QueueOverflows { get; init; }
}

/// <summary>
/// Options for configuring a P2P message queue.
/// </summary>
public sealed class P2PMessageQueueOptions
{
    /// <summary>
    /// Gets or sets the queue capacity (number of messages).
    /// </summary>
    /// <remarks>
    /// Must be a power of 2 for optimal performance. Will be rounded up if not.
    /// Default is 1024.
    /// </remarks>
    public int Capacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the preferred transfer mode.
    /// </summary>
    public P2PTransferMode PreferredTransferMode { get; set; } = P2PTransferMode.Adaptive;

    /// <summary>
    /// Gets or sets whether to enable transfer validation.
    /// </summary>
    public bool EnableTransferValidation { get; set; }

    /// <summary>
    /// Gets or sets the backpressure strategy when queue is full.
    /// </summary>
    public P2PBackpressureStrategy BackpressureStrategy { get; set; } = P2PBackpressureStrategy.Block;

    /// <summary>
    /// Gets or sets the timeout for blocking operations in milliseconds.
    /// </summary>
    public int BlockingTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the batch size for optimized transfers.
    /// </summary>
    public int BatchSize { get; set; } = 64;

    /// <summary>
    /// Gets or sets whether to use pinned memory for the ring buffer.
    /// </summary>
    public bool UsePinnedMemory { get; set; } = true;

    /// <summary>
    /// Gets or sets the size of the host staging buffer in bytes.
    /// </summary>
    public int StagingBufferSize { get; set; } = 1024 * 1024; // 1 MB

    /// <summary>
    /// Validates and normalizes the options.
    /// </summary>
    public void Validate()
    {
        // Round capacity to nearest power of 2
        if ((Capacity & (Capacity - 1)) != 0)
        {
            var power = (int)Math.Ceiling(Math.Log2(Capacity));
            Capacity = 1 << power;
        }

        if (Capacity < 16)
        {
            Capacity = 16;
        }

        if (BatchSize < 1)
        {
            BatchSize = 1;
        }

        if (BatchSize > Capacity)
        {
            BatchSize = Capacity;
        }

        if (StagingBufferSize < 4096)
        {
            StagingBufferSize = 4096;
        }
    }
}

/// <summary>
/// Backpressure strategies for P2P message queues.
/// </summary>
public enum P2PBackpressureStrategy
{
    /// <summary>
    /// Block until space is available.
    /// </summary>
    Block = 0,

    /// <summary>
    /// Drop the oldest message to make room.
    /// </summary>
    DropOldest = 1,

    /// <summary>
    /// Drop the new message (reject).
    /// </summary>
    DropNew = 2,

    /// <summary>
    /// Grow the queue dynamically.
    /// </summary>
    Grow = 3
}
