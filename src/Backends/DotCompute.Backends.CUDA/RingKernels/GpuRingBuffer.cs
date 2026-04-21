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
    /// Gets the device pointer to the message data buffer.
    /// </summary>
    public IntPtr DeviceBufferPtr { get; }

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
/// <item><description>Unified Memory (cudaMallocManaged) - CPU and GPU share a single address space</description></item>
/// <item><description>Device Memory (cudaMalloc) - explicit DMA transfers via cudaMemcpy</description></item>
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
    /// <param name="useUnifiedMemory">True to use unified memory (cudaMallocManaged); false to use device memory (cudaMalloc) with explicit DMA.</param>
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
            throw new ArgumentException(
                $"GpuRingBuffer<{typeof(T).Name}> capacity must be a positive power of 2 (received {capacity}). The ring-buffer uses bitwise index masking (idx & (capacity-1)) which only wraps correctly for powers of 2. Try 64, 128, 256, 512, 1024 …",
                nameof(capacity));
        }

        if (messageSize <= 0)
        {
            throw new ArgumentException(
                $"GpuRingBuffer<{typeof(T).Name}> messageSize must be greater than zero (received {messageSize} bytes). Pass the maximum serialized size of a single message — typically sizeof(T) for POD types or the MemoryPack-serialized size for IRingKernelMessage types.",
                nameof(messageSize));
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
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"GpuRingBuffer<{typeof(T).Name}> index {index} is outside the valid range [0, {_capacity}) for this buffer (capacity={_capacity}). Use head/tail counters (modulo capacity) to compute the slot index.");
        }

        // Serialize message with MemoryPack
        var bytes = MemoryPackSerializer.Serialize(message);
        if (bytes.Length > _messageSize)
        {
            throw new InvalidOperationException(
                $"Serialized {typeof(T).Name} is {bytes.Length} bytes, which exceeds the configured GpuRingBuffer message size {_messageSize}. Either shrink the message (remove variable-length fields) or allocate the GpuRingBuffer with a larger messageSize — all slots in the ring are fixed-size.");
        }

        // Calculate destination offset
        var offset = index * _messageSize;
        var destPtr = IntPtr.Add(_deviceBuffer, offset);

        // Copy to GPU
        if (_useUnifiedMemory)
        {
            // Unified memory - direct memory copy
            Marshal.Copy(bytes, 0, destPtr, bytes.Length);

            // CRITICAL: Memory barrier to ensure message data is visible to GPU
            // before tail pointer is updated. Without this, GPU might see advanced
            // tail but read stale/incomplete message data.
            Thread.MemoryBarrier();
        }
        else
        {
            // Ensure CUDA device is set on this thread (required for thread pool threads)
            var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaSetDevice({_deviceId}) failed for GpuRingBuffer<{typeof(T).Name}>: {setDeviceResult} ({(int)setDeviceResult}). Verify device index is valid, the GPU is not in exclusive-process mode, and this thread has a valid CUDA context attached.");
            }

            // Device memory - use cudaMemcpy
            var pinnedHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var srcPtr = pinnedHandle.AddrOfPinnedObject();
                var result = CudaRuntime.cudaMemcpy(destPtr, srcPtr, (nuint)bytes.Length, CudaMemcpyKind.HostToDevice);

                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException(
                        $"Host→Device cudaMemcpy failed while writing {typeof(T).Name} to GpuRingBuffer slot: {result} ({(int)result}). Bytes: {bytes.Length}, destination: 0x{destPtr.ToInt64():X}. Check the buffer is still allocated (not disposed) and the device is not in a fatal error state.");
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
            throw new ArgumentOutOfRangeException(nameof(index), index,
                $"GpuRingBuffer<{typeof(T).Name}> index {index} is outside the valid range [0, {_capacity}) for this buffer (capacity={_capacity}). Use head/tail counters (modulo capacity) to compute the slot index.");
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
            // Ensure CUDA device is set on this thread (required for thread pool threads)
            var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaSetDevice({_deviceId}) failed for GpuRingBuffer<{typeof(T).Name}>: {setDeviceResult} ({(int)setDeviceResult}). Verify device index is valid, the GPU is not in exclusive-process mode, and this thread has a valid CUDA context attached.");
            }

            // Device memory - use cudaMemcpy
            var pinnedHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var destPtr = pinnedHandle.AddrOfPinnedObject();
                var result = CudaRuntime.cudaMemcpy(destPtr, srcPtr, (nuint)_messageSize, CudaMemcpyKind.DeviceToHost);

                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException(
                        $"Device→Host cudaMemcpy failed while reading {typeof(T).Name} from GpuRingBuffer slot: {result} ({(int)result}). Bytes: {_messageSize}, source: 0x{srcPtr.ToInt64():X}. Check the buffer is still allocated (not disposed) and the device is not in a fatal error state.");
                }
            }
            finally
            {
                pinnedHandle.Free();
            }
        }

        // Deserialize with MemoryPack
        return MemoryPackSerializer.Deserialize<T>(bytes)
            ?? throw new InvalidOperationException(
                $"MemoryPackSerializer.Deserialize<{typeof(T).Name}>() returned null for a GpuRingBuffer slot. This indicates corrupted serialized data — possibly a producer wrote with a different schema version or the slot was partially written. Verify [MemoryPackable] type definitions match on both ends.");
    }

    /// <summary>
    /// Reads the current head counter value from the GPU.
    /// </summary>
    /// <remarks>
    /// For unified memory with system-scope atomics, uses Volatile.Read
    /// for CPU-GPU coherent atomic reads.
    /// </remarks>
    public unsafe uint ReadHead()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_useUnifiedMemory)
        {
            // For unified memory with system-scope atomics:
            // Volatile.Read ensures we see the latest value from GPU
            var ptr = (uint*)_deviceHead.ToPointer();
            return Volatile.Read(ref *ptr);
        }
        else
        {
            // Device memory - use cudaMemcpy
            var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaSetDevice({_deviceId}) failed for GpuRingBuffer<{typeof(T).Name}>: {setDeviceResult} ({(int)setDeviceResult}). Verify device index is valid, the GPU is not in exclusive-process mode, and this thread has a valid CUDA context attached.");
            }

            uint value = 0;
            var result = CudaRuntime.cudaMemcpy(
                new IntPtr(&value),
                _deviceHead,
                sizeof(uint),
                CudaMemcpyKind.DeviceToHost);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMemcpy D2H for head counter failed on GpuRingBuffer<{typeof(T).Name}> (device={_deviceId}): {result} ({(int)result}). Likely causes: buffer disposed, device fault, or pinned-host memory unmapped. Inspect device state via nvidia-smi.");
            }

            return value;
        }
    }

    /// <summary>
    /// Reads the current tail counter value from the GPU.
    /// </summary>
    /// <remarks>
    /// For unified memory with system-scope atomics, uses Volatile.Read
    /// for CPU-GPU coherent atomic reads.
    /// </remarks>
    public unsafe uint ReadTail()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_useUnifiedMemory)
        {
            // For unified memory with system-scope atomics:
            // Volatile.Read ensures we see the latest value from GPU
            var ptr = (uint*)_deviceTail.ToPointer();
            return Volatile.Read(ref *ptr);
        }
        else
        {
            // Device memory - use cudaMemcpy
            var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaSetDevice({_deviceId}) failed for GpuRingBuffer<{typeof(T).Name}>: {setDeviceResult} ({(int)setDeviceResult}). Verify device index is valid, the GPU is not in exclusive-process mode, and this thread has a valid CUDA context attached.");
            }

            uint value = 0;
            var result = CudaRuntime.cudaMemcpy(
                new IntPtr(&value),
                _deviceTail,
                sizeof(uint),
                CudaMemcpyKind.DeviceToHost);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMemcpy D2H for tail counter failed on GpuRingBuffer<{typeof(T).Name}> (device={_deviceId}): {result} ({(int)result}). Likely causes: buffer disposed, device fault, or pinned-host memory unmapped. Inspect device state via nvidia-smi.");
            }

            return value;
        }
    }

    /// <summary>
    /// Writes the head counter value to the GPU.
    /// </summary>
    /// <remarks>
    /// For unified memory with system-scope atomics, uses Interlocked.Exchange
    /// for CPU-GPU coherent atomic writes.
    /// </remarks>
    public unsafe void WriteHead(uint value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_useUnifiedMemory)
        {
            // For unified memory with system-scope atomics:
            // Direct atomic write with memory barrier for CPU-GPU coherence
            var ptr = (uint*)_deviceHead.ToPointer();
            _ = Interlocked.Exchange(ref *ptr, value);
        }
        else
        {
            // Device memory - use cudaMemcpy
            var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaSetDevice({_deviceId}) failed for GpuRingBuffer<{typeof(T).Name}>: {setDeviceResult} ({(int)setDeviceResult}). Verify device index is valid, the GPU is not in exclusive-process mode, and this thread has a valid CUDA context attached.");
            }

            var result = CudaRuntime.cudaMemcpy(
                _deviceHead,
                new IntPtr(&value),
                sizeof(uint),
                CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMemcpy H2D for head counter failed on GpuRingBuffer<{typeof(T).Name}> (device={_deviceId}, value={value}): {result} ({(int)result}). Likely causes: buffer disposed, device fault, or pinned-host memory unmapped. Inspect device state via nvidia-smi.");
            }
        }
    }

    /// <summary>
    /// Writes the tail counter value to the GPU.
    /// </summary>
    /// <remarks>
    /// For unified memory with system-scope atomics (cuda::atomic&lt;T, thread_scope_system&gt;),
    /// we use Interlocked.Exchange which provides:
    /// 1. Atomic write semantics compatible with CUDA system-scope atomics
    /// 2. Full memory barrier ensuring visibility across CPU-GPU boundary
    /// </remarks>
    public unsafe void WriteTail(uint value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_useUnifiedMemory)
        {
            // For unified memory with system-scope atomics:
            // Direct atomic write with memory barrier for CPU-GPU coherence
            var ptr = (uint*)_deviceTail.ToPointer();
            _ = Interlocked.Exchange(ref *ptr, value);
            // Interlocked.Exchange includes implicit full memory barrier
        }
        else
        {
            // Device memory - use cudaMemcpy
            var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaSetDevice({_deviceId}) failed for GpuRingBuffer<{typeof(T).Name}>: {setDeviceResult} ({(int)setDeviceResult}). Verify device index is valid, the GPU is not in exclusive-process mode, and this thread has a valid CUDA context attached.");
            }

            var result = CudaRuntime.cudaMemcpy(
                _deviceTail,
                new IntPtr(&value),
                sizeof(uint),
                CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMemcpy H2D for tail counter failed on GpuRingBuffer<{typeof(T).Name}> (device={_deviceId}, value={value}): {result} ({(int)result}). Likely causes: buffer disposed, device fault, or pinned-host memory unmapped. Inspect device state via nvidia-smi.");
            }
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
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer — defense-in-depth release of GPU buffer / head / tail allocations
    /// in case Dispose was never called. Best-effort, swallows driver errors.
    /// </summary>
    ~GpuRingBuffer()
    {
        if (_disposed)
        {
            return;
        }
        try
        {
            FreeGpuMemory();
        }
        catch
        {
            // Defense-in-depth: never throw from finalizer. The driver may already be torn down.
        }
    }

    private void AllocateGpuMemory()
    {
        // Set device context
        var setDeviceResult = CudaRuntime.cudaSetDevice(_deviceId);
        if (setDeviceResult != CudaError.Success)
        {
            throw new InvalidOperationException(
                $"cudaSetDevice({_deviceId}) failed during GpuRingBuffer<{typeof(T).Name}> allocation: {setDeviceResult} ({(int)setDeviceResult}). Verify device index is valid and the GPU is available.");
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
                    $"cudaMallocManaged (unified memory) for GpuRingBuffer<{typeof(T).Name}> failed: {result} ({(int)result}). Requested {bufferSize:N0} bytes on device {_deviceId} (capacity={_capacity}, messageSize={_messageSize}). " +
                    $"Common causes: GPU out of memory, unified memory unsupported on this hardware/driver (check cudaDeviceGetAttribute for ManagedMemory), or WSL2 limitations. Try useUnifiedMemory=false or reduce capacity.");
            }

            _logger?.LogDebug("Allocated unified memory buffer: ptr=0x{Ptr:X}, size={Size}",
                _deviceBuffer, bufferSize);

            // Allocate unified memory for head/tail
            result = CudaRuntime.cudaMallocManaged(ref _deviceHead, sizeof(uint), 0x01);
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                throw new InvalidOperationException(
                    $"cudaMallocManaged for GpuRingBuffer head counter (4 bytes) failed on device {_deviceId}: {result} ({(int)result}). Data buffer was freed. This is unusual — the main buffer allocated but a 4-byte counter could not, suggesting internal-table exhaustion in the CUDA driver.");
            }

            result = CudaRuntime.cudaMallocManaged(ref _deviceTail, sizeof(uint), 0x01);
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                CudaRuntime.cudaFree(_deviceHead);
                throw new InvalidOperationException(
                    $"cudaMallocManaged for GpuRingBuffer tail counter (4 bytes) failed on device {_deviceId}: {result} ({(int)result}). Data buffer and head counter were freed. This is unusual — the main buffer allocated but a 4-byte counter could not, suggesting internal-table exhaustion in the CUDA driver.");
            }

            _logger?.LogDebug("Allocated unified memory counters: head=0x{Head:X}, tail=0x{Tail:X}",
                _deviceHead, _deviceTail);
        }
        else
        {
            // Device memory allocation (explicit DMA via cudaMemcpy)
            result = CudaRuntime.cudaMalloc(ref _deviceBuffer, bufferSize);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMalloc (device memory) for GpuRingBuffer<{typeof(T).Name}> failed: {result} ({(int)result}). Requested {bufferSize:N0} bytes on device {_deviceId} (capacity={_capacity}, messageSize={_messageSize}). " +
                    $"Common causes: GPU out of memory (check nvidia-smi free VRAM), fragmentation, or device in error state. Reduce capacity or messageSize, or free other GPU allocations.");
            }

            _logger?.LogDebug("Allocated device memory buffer: ptr=0x{Ptr:X}, size={Size}",
                _deviceBuffer, bufferSize);

            // Allocate device memory for head/tail
            result = CudaRuntime.cudaMalloc(ref _deviceHead, sizeof(uint));
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                throw new InvalidOperationException(
                    $"cudaMalloc for GpuRingBuffer head counter (4 bytes) failed on device {_deviceId}: {result} ({(int)result}). Data buffer was freed. This is unusual — the main buffer allocated but a 4-byte counter could not, suggesting internal-table exhaustion in the CUDA driver.");
            }

            result = CudaRuntime.cudaMalloc(ref _deviceTail, sizeof(uint));
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaFree(_deviceBuffer);
                CudaRuntime.cudaFree(_deviceHead);
                throw new InvalidOperationException(
                    $"cudaMalloc for GpuRingBuffer tail counter (4 bytes) failed on device {_deviceId}: {result} ({(int)result}). Data buffer and head counter were freed. This is unusual — the main buffer allocated but a 4-byte counter could not, suggesting internal-table exhaustion in the CUDA driver.");
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
