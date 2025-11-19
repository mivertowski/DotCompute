// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Messaging;

/// <summary>
/// Metal-accelerated message queue with lock-free atomic operations on GPU.
/// Implements persistent circular buffer with serialized message support.
/// </summary>
[SuppressMessage("Design", "CA2216:Disposable types should declare finalizer", Justification = "Finalizer not needed, native resources released via DisposeAsync")]
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Semaphore is disposed in DisposeAsync")]
public sealed class MetalMessageQueue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IMessageQueue<T>
    where T : IRingKernelMessage, new()
{
    private readonly ILogger<MetalMessageQueue<T>> _logger;
    private readonly MessageQueueOptions _options;
    private readonly int _capacityMask; // capacity - 1, for fast modulo
    private readonly ConcurrentDictionary<Guid, byte>? _seenMessages;
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in DisposeAsync")]
    private readonly SemaphoreSlim? _semaphore;
    private readonly DateTime _createdAt;

    // Metal resources
    private IntPtr _device; // MTLDevice
    private IntPtr _commandQueue; // MTLCommandQueue
    private IntPtr _deviceBuffer; // MTLBuffer for serialized messages
    private IntPtr _deviceHead;   // MTLBuffer for atomic head index
    private IntPtr _deviceTail;   // MTLBuffer for atomic tail index
    private readonly int _maxMessageSize = 1312;  // Maximum serialized message size
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the MetalMessageQueue class.
    /// </summary>
    public MetalMessageQueue(MessageQueueOptions options, ILogger<MetalMessageQueue<T>> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        options.Validate(); // Rounds capacity to power of 2
        _options = options;
        _logger = logger;
        _capacityMask = options.Capacity - 1;
        _createdAt = DateTime.UtcNow;

        // Initialize deduplication tracking if enabled
        if (options.EnableDeduplication)
        {
            _seenMessages = new ConcurrentDictionary<Guid, byte>();
        }

        // Initialize semaphore for blocking backpressure
        if (options.BackpressureStrategy == BackpressureStrategy.Block)
        {
            _semaphore = new SemaphoreSlim(options.Capacity, options.Capacity);
        }
    }

    /// <summary>
    /// Initializes the Metal message queue with device resources.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await Task.Run(() =>
        {
            // Get default Metal device
            _device = Native.MetalNative.CreateSystemDefaultDevice();
            if (_device == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Metal device");
            }

            // Create command queue
            _commandQueue = Native.MetalNative.CreateCommandQueue(_device);
            if (_commandQueue == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Metal command queue");
            }

            var bufferSize = (ulong)(_options.Capacity * _maxMessageSize);
            var atomicSize = (ulong)sizeof(long);

            // Allocate Metal device buffers (StorageModeShared for CPU/GPU access)
            _deviceBuffer = Native.MetalNative.CreateBuffer(_device, (nuint)bufferSize, Native.MetalStorageMode.Shared);
            _deviceHead = Native.MetalNative.CreateBuffer(_device, (nuint)atomicSize, Native.MetalStorageMode.Shared);
            _deviceTail = Native.MetalNative.CreateBuffer(_device, (nuint)atomicSize, Native.MetalStorageMode.Shared);

            if (_deviceBuffer == IntPtr.Zero || _deviceHead == IntPtr.Zero || _deviceTail == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to allocate Metal buffers");
            }

            // Initialize head/tail to zero
            var headPtr = Native.MetalNative.GetBufferContents(_deviceHead);
            var tailPtr = Native.MetalNative.GetBufferContents(_deviceTail);

            Marshal.WriteInt64(headPtr, 0);
            Marshal.WriteInt64(tailPtr, 0);

            // Zero the message buffer
            var bufferPtr = Native.MetalNative.GetBufferContents(_deviceBuffer);
            unsafe
            {
                new Span<byte>((void*)bufferPtr, (int)bufferSize).Clear();
            }

            _initialized = true;
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the current number of messages in the queue.
    /// </summary>
    public int Count
    {
        get
        {
            if (!_initialized)
            {
                return 0;
            }

            var headPtr = Native.MetalNative.GetBufferContents(_deviceHead);
            var tailPtr = Native.MetalNative.GetBufferContents(_deviceTail);

            var head = Marshal.ReadInt64(headPtr);
            var tail = Marshal.ReadInt64(tailPtr);

            return (int)(head - tail);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the queue is empty.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Gets a value indicating whether the queue is full.
    /// </summary>
    public bool IsFull => Count >= _options.Capacity;

    /// <summary>
    /// Gets the maximum number of messages the queue can hold.
    /// </summary>
    public int Capacity => _options.Capacity;

    /// <summary>
    /// Attempts to enqueue a message to the GPU queue.
    /// </summary>
    public unsafe bool TryEnqueue(T message, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized");
        }

        // Deduplication check
        if (_seenMessages is not null)
        {
            if (!_seenMessages.TryAdd(message.MessageId, 0))
            {
                return true; // Duplicate, silently succeed
            }

            // Cleanup old entries when window size exceeded
            if (_seenMessages.Count > _options.DeduplicationWindowSize)
            {
                var toRemove = _seenMessages.Keys.Take(_options.DeduplicationWindowSize / 2).ToList();
                foreach (var key in toRemove)
                {
                    _ = _seenMessages.TryRemove(key, out _);
                }
            }
        }

        // Backpressure handling
        if (IsFull)
        {
            switch (_options.BackpressureStrategy)
            {
                case BackpressureStrategy.Block:
                    _semaphore?.Wait(cancellationToken);
                    break;
                case BackpressureStrategy.Reject:
                    return false;
                case BackpressureStrategy.DropOldest:
                    _ = TryDequeue(out _);
                    break;
                case BackpressureStrategy.DropNew:
                    return false;
            }
        }

        // Serialize message
        var serialized = message.Serialize();
        if (serialized.Length > _maxMessageSize)
        {
            return false;
        }

        // Get buffer pointers
        var headPtr = Native.MetalNative.GetBufferContents(_deviceHead);
        var tailPtr = Native.MetalNative.GetBufferContents(_deviceTail);
        var bufferPtr = Native.MetalNative.GetBufferContents(_deviceBuffer);

        // Atomically increment head (Metal doesn't have atomic C# API, use Interlocked)
        var currentHead = Interlocked.Increment(ref *(long*)headPtr) - 1;

        // Check if full
        var currentTail = Marshal.ReadInt64(tailPtr);
        if (currentHead >= currentTail + _options.Capacity)
        {
            // Rollback
            _ = Interlocked.Decrement(ref *(long*)headPtr);
            return false;
        }

        // Calculate slot with wrap-around
        var slotIndex = (int)(currentHead & _capacityMask);
        var bufferOffset = slotIndex * _maxMessageSize;

        // Copy message to device buffer
        unsafe
        {
            var destPtr = (byte*)bufferPtr + bufferOffset;
            fixed (byte* srcPtr = serialized)
            {
                Buffer.MemoryCopy(srcPtr, destPtr, _maxMessageSize, serialized.Length);
            }
        }

        _semaphore?.Release();
        return true;
    }

    /// <summary>
    /// Attempts to dequeue a message from the GPU queue.
    /// </summary>
    public unsafe bool TryDequeue(out T? message)
    {
        message = default;

        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized");
        }

        if (IsEmpty)
        {
            return false;
        }

        // Get buffer pointers
        var headPtr = Native.MetalNative.GetBufferContents(_deviceHead);
        var tailPtr = Native.MetalNative.GetBufferContents(_deviceTail);
        var bufferPtr = Native.MetalNative.GetBufferContents(_deviceBuffer);

        // Atomically increment tail
        var myTail = Interlocked.Increment(ref *(long*)tailPtr) - 1;

        // Re-check if we got a valid slot
        var currentHead = Marshal.ReadInt64(headPtr);
        if (myTail >= currentHead)
        {
            return false; // Lost race
        }

        // Calculate slot
        var slotIndex = (int)(myTail & _capacityMask);
        var bufferOffset = slotIndex * _maxMessageSize;

        // Read message size (first 4 bytes)
        int messageSize;
        unsafe
        {
            var srcPtr = (byte*)bufferPtr + bufferOffset;
            messageSize = *(int*)srcPtr;
        }

        if (messageSize <= 0 || messageSize > _maxMessageSize)
        {
            return false;
        }

        // Read message data
        var data = new byte[messageSize];
        unsafe
        {
            var srcPtr = (byte*)bufferPtr + bufferOffset;
            fixed (byte* destPtr = data)
            {
                Buffer.MemoryCopy(srcPtr, destPtr, messageSize, messageSize);
            }
        }

        // Deserialize
        message = new T();
        message.Deserialize(data);

        return true;
    }

    /// <summary>
    /// Attempts to peek at the next message without removing it.
    /// </summary>
    public unsafe bool TryPeek(out T? message)
    {
        message = default;

        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized");
        }

        if (IsEmpty)
        {
            return false;
        }

        // Get buffer pointers
        var tailPtr = Native.MetalNative.GetBufferContents(_deviceTail);
        var bufferPtr = Native.MetalNative.GetBufferContents(_deviceBuffer);

        // Read tail without modifying
        var currentTail = Marshal.ReadInt64(tailPtr);

        // Calculate slot
        var slotIndex = (int)(currentTail & _capacityMask);
        var bufferOffset = slotIndex * _maxMessageSize;

        // Read message size
        int messageSize;
        unsafe
        {
            var srcPtr = (byte*)bufferPtr + bufferOffset;
            messageSize = *(int*)srcPtr;
        }

        if (messageSize <= 0 || messageSize > _maxMessageSize)
        {
            return false;
        }

        // Read message data
        var data = new byte[messageSize];
        unsafe
        {
            var srcPtr = (byte*)bufferPtr + bufferOffset;
            fixed (byte* destPtr = data)
            {
                Buffer.MemoryCopy(srcPtr, destPtr, messageSize, messageSize);
            }
        }

        // Deserialize
        message = new T();
        message.Deserialize(data);

        return true;
    }

    /// <summary>
    /// Clears all messages from the queue.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            return;
        }

        // Read current head position
        var headPtr = Native.MetalNative.GetBufferContents(_deviceHead);
        var tailPtr = Native.MetalNative.GetBufferContents(_deviceTail);

        var currentHead = Marshal.ReadInt64(headPtr);

        // Set tail to head (queue becomes empty)
        Marshal.WriteInt64(tailPtr, currentHead);

        // Clear deduplication tracking
        _seenMessages?.Clear();

        // Reset semaphore to full capacity
        if (_semaphore != null)
        {
            while (_semaphore.CurrentCount > 0)
            {
                _ = _semaphore.Wait(0);
            }

            _semaphore.Release(_options.Capacity);
        }
    }

    /// <summary>
    /// Synchronously disposes the message queue.
    /// </summary>
    [SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Synchronous Dispose required by IDisposable interface")]
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the Metal message queue and releases GPU resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await Task.Run(() =>
        {
            if (_deviceBuffer != IntPtr.Zero)
            {
                Native.MetalNative.ReleaseBuffer(_deviceBuffer);
                _deviceBuffer = IntPtr.Zero;
            }

            if (_deviceHead != IntPtr.Zero)
            {
                Native.MetalNative.ReleaseBuffer(_deviceHead);
                _deviceHead = IntPtr.Zero;
            }

            if (_deviceTail != IntPtr.Zero)
            {
                Native.MetalNative.ReleaseBuffer(_deviceTail);
                _deviceTail = IntPtr.Zero;
            }

            if (_commandQueue != IntPtr.Zero)
            {
                Native.MetalNative.ReleaseCommandQueue(_commandQueue);
                _commandQueue = IntPtr.Zero;
            }

            if (_device != IntPtr.Zero)
            {
                Native.MetalNative.ReleaseDevice(_device);
                _device = IntPtr.Zero;
            }

            _semaphore?.Dispose();
        });

        _disposed = true;
    }
}
