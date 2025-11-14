// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Threading;

namespace DotCompute.Core.Messaging;

/// <summary>
/// Lock-free pinned memory buffer for staging messages before GPU transfer.
/// </summary>
/// <remarks>
/// <para>
/// This buffer uses pinned (non-movable) memory to enable zero-copy DMA transfers
/// to GPU via CUDA/OpenCL/Metal. It implements a lock-free multi-producer/single-consumer
/// ring buffer for maximum throughput.
/// </para>
/// <para>
/// <b>Performance Characteristics:</b>
/// - Enqueue: O(1) amortized, lock-free CAS operations
/// - Dequeue: O(1), single-consumer (pump thread)
/// - Memory: Pinned, non-GC heap (careful with large capacities)
/// - Latency: Sub-microsecond for cache-resident operations
/// </para>
/// <para>
/// <b>Usage Pattern:</b>
/// <code>
/// using var buffer = new PinnedStagingBuffer(capacity: 4096, messageSize: 256);
///
/// // Producer threads (lock-free)
/// if (buffer.TryEnqueue(messageBytes))
/// {
///     // Message staged successfully
/// }
///
/// // Consumer thread (pump service)
/// Span&lt;byte&gt; batch = stackalloc byte[batchSize * messageSize];
/// int count = buffer.DequeueBatch(batch, batchSize);
/// // Transfer 'batch' to GPU via cuMemcpy/clEnqueueWrite/MTLBuffer.copy
/// </code>
/// </para>
/// </remarks>
public sealed class PinnedStagingBuffer : IDisposable
{
    private readonly int _capacity;
    private readonly int _messageSize;
    private readonly GCHandle _bufferHandle;
    private readonly byte[] _buffer;
    private readonly int[] _head; // Atomic head index
    private readonly int[] _tail; // Atomic tail index
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PinnedStagingBuffer"/> class.
    /// </summary>
    /// <param name="capacity">Maximum number of messages the buffer can hold (must be power of 2).</param>
    /// <param name="messageSize">Fixed size of each message in bytes.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="capacity"/> is not a power of 2 or less than 2.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="messageSize"/> is less than 1.
    /// </exception>
    public PinnedStagingBuffer(int capacity, int messageSize)
    {
        if (capacity < 2 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a power of 2 and at least 2", nameof(capacity));
        }

        if (messageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(messageSize), "Message size must be at least 1 byte");
        }

        _capacity = capacity;
        _messageSize = messageSize;

        // Allocate pinned buffer
        _buffer = GC.AllocateUninitializedArray<byte>(capacity * messageSize, pinned: true);
        _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

        // Atomic indices (separate arrays to avoid false sharing with padding)
        _head = new int[16]; // Padded to 64 bytes (cache line)
        _tail = new int[16]; // Padded to 64 bytes (cache line)
        _head[0] = 0;
        _tail[0] = 0;
    }

    /// <summary>
    /// Gets the maximum number of messages the buffer can hold.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the fixed size of each message in bytes.
    /// </summary>
    public int MessageSize => _messageSize;

    /// <summary>
    /// Gets the current number of messages in the buffer.
    /// </summary>
    /// <remarks>
    /// This is an approximate count due to lock-free operations. Use for monitoring only.
    /// </remarks>
    public int Count
    {
        get
        {
            var head = Volatile.Read(ref _head[0]);
            var tail = Volatile.Read(ref _tail[0]);
            var count = head - tail;
            return count < 0 ? count + _capacity : count;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the buffer is empty.
    /// </summary>
    public bool IsEmpty => Volatile.Read(ref _head[0]) == Volatile.Read(ref _tail[0]);

    /// <summary>
    /// Gets a value indicating whether the buffer is full.
    /// </summary>
    public bool IsFull
    {
        get
        {
            var head = Volatile.Read(ref _head[0]);
            var tail = Volatile.Read(ref _tail[0]);
            return ((head + 1) & (_capacity - 1)) == tail;
        }
    }

    /// <summary>
    /// Gets a pointer to the pinned buffer for direct GPU access.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This pointer remains valid for the lifetime of the <see cref="PinnedStagingBuffer"/>.
    /// Use this for zero-copy DMA transfers to GPU via:
    /// - CUDA: cuMemcpyHtoD(devicePtr, BufferPointer, size)
    /// - OpenCL: clEnqueueWriteBuffer(queue, buffer, CL_TRUE, 0, size, BufferPointer, ...)
    /// - Metal: [mtlBuffer contents] = BufferPointer (or use didModifyRange)
    /// </para>
    /// <para>
    /// <b>Safety:</b> Do not dereference this pointer after Dispose() is called.
    /// </para>
    /// </remarks>
    public IntPtr BufferPointer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bufferHandle.AddrOfPinnedObject();
        }
    }

    /// <summary>
    /// Attempts to enqueue a message into the staging buffer.
    /// </summary>
    /// <param name="message">The message bytes to enqueue (must be exactly <see cref="MessageSize"/> bytes).</param>
    /// <returns>
    /// <see langword="true"/> if the message was successfully enqueued;
    /// <see langword="false"/> if the buffer is full.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="message"/> length does not match <see cref="MessageSize"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the buffer has been disposed.
    /// </exception>
    /// <remarks>
    /// This method is lock-free and thread-safe for multiple concurrent producers.
    /// Uses Compare-And-Swap (CAS) to ensure only one producer claims each slot.
    /// </remarks>
    public bool TryEnqueue(ReadOnlySpan<byte> message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (message.Length != _messageSize)
        {
            throw new ArgumentException(
                $"Message size mismatch. Expected {_messageSize} bytes, got {message.Length}",
                nameof(message));
        }

        while (true)
        {
            var head = Volatile.Read(ref _head[0]);
            var tail = Volatile.Read(ref _tail[0]);
            var nextHead = (head + 1) & (_capacity - 1);

            // Check if full
            if (nextHead == tail)
            {
                return false; // Buffer full
            }

            // Try to claim this slot (CAS)
            if (Interlocked.CompareExchange(ref _head[0], nextHead, head) == head)
            {
                // Successfully claimed slot 'head', now write message
                var offset = head * _messageSize;
                message.CopyTo(_buffer.AsSpan(offset, _messageSize));

                // Memory barrier to ensure write is visible before head update
                Thread.MemoryBarrier();

                return true;
            }

            // CAS failed, another producer claimed this slot, retry
        }
    }

    /// <summary>
    /// Dequeues a batch of messages from the staging buffer.
    /// </summary>
    /// <param name="destination">
    /// The destination buffer to write messages to (must be at least <paramref name="maxMessages"/> * <see cref="MessageSize"/> bytes).
    /// </param>
    /// <param name="maxMessages">Maximum number of messages to dequeue.</param>
    /// <returns>The actual number of messages dequeued (0 if buffer is empty).</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="destination"/> is too small.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the buffer has been disposed.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method is designed for single-consumer use (the pump thread).
    /// It is NOT thread-safe for multiple concurrent consumers.
    /// </para>
    /// <para>
    /// Batching reduces per-message overhead for GPU transfers. Typical batch sizes:
    /// - Low-latency: 1-8 messages (minimize latency)
    /// - Balanced: 16-64 messages (balance latency vs throughput)
    /// - High-throughput: 128-512 messages (maximize PCIe bandwidth)
    /// </para>
    /// </remarks>
    public int DequeueBatch(Span<byte> destination, int maxMessages)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (maxMessages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMessages), "Must dequeue at least 1 message");
        }

        if (destination.Length < maxMessages * _messageSize)
        {
            throw new ArgumentException(
                $"Destination too small. Need {maxMessages * _messageSize} bytes, got {destination.Length}",
                nameof(destination));
        }

        var tail = Volatile.Read(ref _tail[0]);
        var head = Volatile.Read(ref _head[0]);

        // Calculate available messages
        var available = head >= tail ? head - tail : (_capacity - tail) + head;

        if (available == 0)
        {
            return 0; // Buffer empty
        }

        // Dequeue up to maxMessages
        var toDequeue = Math.Min(available, maxMessages);

        for (var i = 0; i < toDequeue; i++)
        {
            var offset = tail * _messageSize;
            _buffer.AsSpan(offset, _messageSize).CopyTo(destination.Slice(i * _messageSize, _messageSize));

            tail = (tail + 1) & (_capacity - 1);
        }

        // Update tail (single consumer, no CAS needed)
        Volatile.Write(ref _tail[0], tail);

        return toDequeue;
    }

    /// <summary>
    /// Gets a read-only span of the pinned buffer for direct GPU read access.
    /// </summary>
    /// <remarks>
    /// Use this for zero-copy reads when the GPU can directly access host pinned memory.
    /// The span remains valid until Dispose() is called.
    /// </remarks>
    public ReadOnlySpan<byte> GetBuffer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _buffer;
    }

    /// <summary>
    /// Clears all messages from the buffer.
    /// </summary>
    /// <remarks>
    /// This operation is NOT thread-safe with concurrent enqueue/dequeue operations.
    /// Use only when the buffer is idle (e.g., during shutdown or reset).
    /// </remarks>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Volatile.Write(ref _head[0], 0);
        Volatile.Write(ref _tail[0], 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Unpin the buffer
        if (_bufferHandle.IsAllocated)
        {
            _bufferHandle.Free();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure pinned memory is released.
    /// </summary>
    ~PinnedStagingBuffer()
    {
        Dispose();
    }
}
