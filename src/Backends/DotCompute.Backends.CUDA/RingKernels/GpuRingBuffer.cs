// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using MemoryPack;
using Microsoft.Extensions.Logging;

// Suppress trimming warning for MemoryPack - serialization requires runtime type info
#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type
namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Non-generic interface for GPU ring buffers, providing access to head/tail pointers.
/// </summary>
/// <remarks>
/// This interface allows the runtime to access GPU ring buffer pointers without
/// knowing the generic message type at compile time.
/// </remarks>
public interface IGpuRingBuffer : IDisposable
{
    /// <summary>
    /// Gets the device pointer to the head atomic counter.
    /// </summary>
    public IntPtr DeviceHeadPtr { get; }

    /// <summary>
    /// Gets the device pointer to the tail atomic counter.
    /// </summary>
    public IntPtr DeviceTailPtr { get; }

    /// <summary>
    /// Gets the capacity of the ring buffer (power of 2).
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the size of each message in bytes.
    /// </summary>
    public int MessageSize { get; }

    /// <summary>
    /// Gets whether unified memory is being used.
    /// </summary>
    public bool IsUnifiedMemory { get; }
}

/// <summary>
/// Manages GPU-resident ring buffer memory for message passing.
/// </summary>
/// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// <para>
/// Allocates and manages GPU device memory for lock-free message queues:
/// - Message buffer (serialized MemoryPack data)
/// - Head/tail atomic counters for lock-free coordination
/// </para>
/// <para>
/// Supports two allocation modes:
/// <list type="bullet">
/// <item><description>Unified Memory (cudaMallocManaged) for non-WSL2 systems</description></item>
/// <item><description>Device Memory (cudaMalloc) for WSL2 systems</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class GpuRingBuffer<T> : IGpuRingBuffer
    where T : IRingKernelMessage
{
    private readonly ILogger? _logger;
    private readonly int _deviceId;
    private readonly int _capacity;
    private readonly int _messageSize;
    private readonly bool _useUnifiedMemory;

    private IntPtr _deviceBuffer;  // Message data buffer
    private IntPtr _deviceHead;    // atomic<unsigned int> head counter
    private IntPtr _deviceTail;    // atomic<unsigned int> tail counter

    private bool _disposed;

    /// <summary>
    /// Gets the device pointer to the message buffer.
    /// </summary>
    public IntPtr DeviceBufferPtr => _deviceBuffer;

    /// <summary>
    /// Gets the device pointer to the head atomic counter.
    /// </summary>
    public IntPtr DeviceHeadPtr => _deviceHead;

    /// <summary>
    /// Gets the device pointer to the tail atomic counter.
    /// </summary>
    public IntPtr DeviceTailPtr => _deviceTail;

    /// <summary>
    /// Gets the capacity of the ring buffer (power of 2).
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the size of each message in bytes.
    /// </summary>
    public int MessageSize => _messageSize;

    /// <summary>
    /// Gets whether unified memory is being used.
    /// </summary>
    public bool IsUnifiedMemory => _useUnifiedMemory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuRingBuffer{T}"/> class.
    /// </summary>
    /// <param name="deviceId">CUDA device ID.</param>
    /// <param name="capacity">Ring buffer capacity (must be power of 2).</param>
    /// <param name="messageSize">Size of each message in bytes.</param>
    /// <param name="useUnifiedMemory">True to use unified memory (non-WSL2), false for device memory (WSL2).</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentException">Thrown when capacity is not a power of 2.</exception>
    /// <exception cref="InvalidOperationException">Thrown when GPU allocation fails.</exception>
    public GpuRingBuffer(
        int deviceId,
        int capacity,
        int messageSize,
        bool useUnifiedMemory,
        ILogger? logger = null)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException($"Capacity must be a power of 2, got {capacity}", nameof(capacity));
        }

        if (messageSize <= 0)
        {
            throw new ArgumentException($"Message size must be positive, got {messageSize}", nameof(messageSize));
        }

        _logger = logger;
        _deviceId = deviceId;
        _capacity = capacity;
        _messageSize = messageSize;
        _useUnifiedMemory = useUnifiedMemory;

        AllocateGpuMemory();
    }

    /// <summary>
    /// Writes a message to the GPU buffer at the specified index.
    /// </summary>
    /// <param name="message">Message to write.</param>
    /// <param name="index">Index in the ring buffer (0 to Capacity-1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public void WriteMessage(T message, int index, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (index < 0 || index >= _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range [0, {_capacity})");
        }

        // Serialize message with MemoryPack
        var bytes = MemoryPackSerializer.Serialize(message);
        if (bytes.Length > _messageSize)
        {
            throw new InvalidOperationException(
                $"Serialized message size {bytes.Length} exceeds configured size {_messageSize}");
        }

        // Calculate destination offset
        var offset = index * _messageSize;
        var destPtr = IntPtr.Add(_deviceBuffer, offset);

        // Copy to GPU
        if (_useUnifiedMemory)
        {
            // Unified memory - direct memory copy
            Marshal.Copy(bytes, 0, destPtr, bytes.Length);
        }
        else
        {
            // Device memory - use cudaMemcpy
            var pinnedHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var srcPtr = pinnedHandle.AddrOfPinnedObject();
                var result = CudaRuntime.cudaMemcpy(destPtr, srcPtr, (nuint)bytes.Length, CudaMemcpyKind.HostToDevice);

                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException($"cudaMemcpy (H2D) failed: {result}");
                }
            }
            finally
            {
                pinnedHandle.Free();
            }
        }
    }

    /// <summary>
    /// Reads a message from the GPU buffer at the specified index.
    /// </summary>
    /// <param name="index">Index in the ring buffer (0 to Capacity-1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized message.</returns>
    public T ReadMessage(int index, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (index < 0 || index >= _capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of range [0, {_capacity})");
        }

        // Calculate source offset
        var offset = index * _messageSize;
        var srcPtr = IntPtr.Add(_deviceBuffer, offset);

        // Read from GPU
        var bytes = new byte[_messageSize];

        if (_useUnifiedMemory)
        {
            // Unified memory - direct memory copy
            Marshal.Copy(srcPtr, bytes, 0, _messageSize);
        }
        else
        {
            // Device memory - use cudaMemcpy
            var pinnedHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var destPtr = pinnedHandle.AddrOfPinnedObject();
                var result = CudaRuntime.cudaMemcpy(destPtr, srcPtr, (nuint)_messageSize, CudaMemcpyKind.DeviceToHost);

                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException($"cudaMemcpy (D2H) failed: {result}");
                }
            }
            finally
            {
                pinnedHandle.Free();
            }
        }

        // Deserialize with MemoryPack
        return MemoryPackSerializer.Deserialize<T>(bytes)
            ?? throw new InvalidOperationException("Failed to deserialize message");
    }

    /// <summary>
    /// Reads the current head counter value from the GPU.
    /// </summary>
    public unsafe uint ReadHead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint value = 0;
        var result = CudaRuntime.cudaMemcpy(
            new IntPtr(&value),
            _deviceHead,
            sizeof(uint),
            CudaMemcpyKind.DeviceToHost);

        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to read head counter: {result}");
        }

        return value;
    }

    /// <summary>
    /// Reads the current tail counter value from the GPU.
    /// </summary>
    public unsafe uint ReadTail()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint value = 0;
        var result = CudaRuntime.cudaMemcpy(
            new IntPtr(&value),
            _deviceTail,
            sizeof(uint),
            CudaMemcpyKind.DeviceToHost);

        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to read tail counter: {result}");
        }

        return value;
    }

    /// <summary>
    /// Writes the head counter value to the GPU.
    /// </summary>
    public unsafe void WriteHead(uint value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = CudaRuntime.cudaMemcpy(
            _deviceHead,
            new IntPtr(&value),
            sizeof(uint),
            CudaMemcpyKind.HostToDevice);

        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to write head counter: {result}");
        }
    }

    /// <summary>
    /// Writes the tail counter value to the GPU.
    /// </summary>
    public unsafe void WriteTail(uint value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = CudaRuntime.cudaMemcpy(
            _deviceTail,
            new IntPtr(&value),
            sizeof(uint),
            CudaMemcpyKind.HostToDevice);

        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to write tail counter: {result}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        FreeGpuMemory();
        _disposed = true;
    }

    private void AllocateGpuMemory()
    {
        // Set device context
        var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
        if (setDeviceResult != CudaError.Success)
        {
            throw new InvalidOperationException($"cudaSetDevice({_deviceId}) failed: {setDeviceResult}");
        }

        // Calculate total buffer size
        var bufferSize = (nuint)(_capacity * _messageSize);

        CudaError result;

        if (_useUnifiedMemory)
        {
            // Try unified memory allocation
            result = CudaRuntime.cudaMallocManaged(ref _deviceBuffer, bufferSize, 0x01);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMallocManaged failed for buffer (size={bufferSize}): {result}");
            }

            _logger?.LogDebug("Allocated unified memory buffer: ptr=0x{Ptr:X}, size={Size}",
                _deviceBuffer, bufferSize);

            // Allocate unified memory for head/tail
            result = CudaRuntime.cudaMallocManaged(ref _deviceHead, sizeof(uint), 0x01);
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                throw new InvalidOperationException($"cudaMallocManaged failed for head: {result}");
            }

            result = CudaRuntime.cudaMallocManaged(ref _deviceTail, sizeof(uint), 0x01);
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                CudaRuntime.cudaFree(_deviceHead);
                throw new InvalidOperationException($"cudaMallocManaged failed for tail: {result}");
            }

            _logger?.LogDebug("Allocated unified memory counters: head=0x{Head:X}, tail=0x{Tail:X}",
                _deviceHead, _deviceTail);
        }
        else
        {
            // Device memory allocation for WSL2
            result = CudaRuntime.cudaMalloc(ref _deviceBuffer, bufferSize);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMalloc failed for buffer (size={bufferSize}): {result}");
            }

            _logger?.LogDebug("Allocated device memory buffer: ptr=0x{Ptr:X}, size={Size}",
                _deviceBuffer, bufferSize);

            // Allocate device memory for head/tail
            result = CudaRuntime.cudaMalloc(ref _deviceHead, sizeof(uint));
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                throw new InvalidOperationException($"cudaMalloc failed for head: {result}");
            }

            result = CudaRuntime.cudaMalloc(ref _deviceTail, sizeof(uint));
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                CudaRuntime.cudaFree(_deviceHead);
                throw new InvalidOperationException($"cudaMalloc failed for tail: {result}");
            }

            _logger?.LogDebug("Allocated device memory counters: head=0x{Head:X}, tail=0x{Tail:X}",
                _deviceHead, _deviceTail);
        }

        // Initialize head/tail to 0
        WriteHead(0);
        WriteTail(0);

        _logger?.LogInformation(
            "GPU ring buffer allocated: capacity={Capacity}, messageSize={MessageSize}, " +
            "unified={Unified}, buffer=0x{Buffer:X}, head=0x{Head:X}, tail=0x{Tail:X}",
            _capacity, _messageSize, _useUnifiedMemory, _deviceBuffer, _deviceHead, _deviceTail);
    }

    private void FreeGpuMemory()
    {
        if (_deviceBuffer != IntPtr.Zero)
        {
            _ = CudaRuntime.cudaFree(_deviceBuffer);
            _deviceBuffer = IntPtr.Zero;
        }

        if (_deviceHead != IntPtr.Zero)
        {
            _ = CudaRuntime.cudaFree(_deviceHead);
            _deviceHead = IntPtr.Zero;
        }

        if (_deviceTail != IntPtr.Zero)
        {
            _ = CudaRuntime.cudaFree(_deviceTail);
            _deviceTail = IntPtr.Zero;
        }

        _logger?.LogDebug("GPU ring buffer memory freed");
    }
}
