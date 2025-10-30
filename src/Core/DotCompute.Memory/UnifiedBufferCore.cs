// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DeviceMemory = DotCompute.Abstractions.DeviceMemory;

namespace DotCompute.Memory;

/// <summary>
/// Core unified buffer implementation providing basic buffer operations and properties.
/// Handles fundamental buffer state management and access patterns.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed partial class UnifiedBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryManager _memoryManager;
    private readonly SemaphoreSlim _asyncLock = new(1, 1);
    private readonly Lock _lock = new();

    private GCHandle _pinnedHandle;
    private T[]? _hostArray;
    private DeviceMemory _deviceMemory;
#pragma warning disable CS0414 // Field is assigned but never used
    private IUnifiedMemoryBuffer<T>? _deviceBuffer;
#pragma warning restore CS0414
    private BufferState _state;
    private volatile bool _disposed;

    /// <summary>
    /// Gets the length of the buffer in elements.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets whether the buffer is currently available on the host.
    /// </summary>
    public bool IsOnHost => _state is BufferState.HostOnly or BufferState.Synchronized or BufferState.HostDirty;

    /// <summary>
    /// Gets whether the buffer is currently available on the device.
    /// </summary>
    public bool IsOnDevice => _state is BufferState.DeviceOnly or BufferState.Synchronized or BufferState.DeviceDirty;

    /// <summary>
    /// Gets whether the buffer has been modified and needs synchronization.
    /// </summary>
    public bool IsDirty => _state is BufferState.HostDirty or BufferState.DeviceDirty;

    /// <summary>
    /// Gets the accelerator this buffer is associated with (always null for unified buffers).
    /// </summary>
    public IAccelerator Accelerator => null!;

    /// <summary>
    /// Gets the memory options for this buffer.
    /// </summary>
    public MemoryOptions Options => MemoryOptions.None;

    /// <summary>
    /// Gets the buffer state for tracking transfers.
    /// </summary>
    public BufferState State => _state;

    /// <summary>
    /// Gets whether this buffer has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets the device pointer for this buffer. Returns IntPtr.Zero if buffer is not on device.
    /// </summary>
    public IntPtr DevicePointer
    {
        get
        {
            if (!IsOnDevice || _deviceMemory.Handle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return _deviceMemory.Handle;
        }
    }

    /// <summary>
    /// Initializes a new instance of the UnifiedBuffer class.
    /// </summary>
    /// <param name="memoryManager">The memory manager to use for device operations.</param>
    /// <param name="length">The length of the buffer in elements.</param>
    public UnifiedBuffer(IUnifiedMemoryManager memoryManager, int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive");
        }

        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        Length = length;
        SizeInBytes = length * Unsafe.SizeOf<T>();

        // Check allocation limit
        if (SizeInBytes > _memoryManager.MaxAllocationSize)
        {
            throw new InvalidOperationException($"Buffer allocation exceeds maximum allowed size: requested {SizeInBytes} bytes, limit is {_memoryManager.MaxAllocationSize} bytes");
        }

        _state = BufferState.Uninitialized;

        // Initialize host array immediately
        _hostArray = new T[length];
        _pinnedHandle = GCHandle.Alloc(_hostArray, GCHandleType.Pinned);
        _state = BufferState.HostOnly;
    }

    /// <summary>
    /// Initializes a new instance of the UnifiedBuffer class with initial data.
    /// </summary>
    /// <param name="memoryManager">The memory manager to use for device operations.</param>
    /// <param name="data">Initial data to populate the buffer.</param>
    public UnifiedBuffer(IUnifiedMemoryManager memoryManager, ReadOnlySpan<T> data)
        : this(memoryManager, data.Length)
    {
        data.CopyTo(_hostArray.AsSpan());
    }

    /// <summary>
    /// Gets a span representing the buffer contents on the host.
    /// </summary>
    /// <returns>A span of the buffer data.</returns>
    public Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return _hostArray.AsSpan();
    }

    /// <summary>
    /// Gets a read-only span representing the buffer contents on the host.
    /// </summary>
    /// <returns>A read-only span of the buffer data.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return _hostArray.AsSpan();
    }

    /// <summary>
    /// Gets a memory representing the buffer contents on the host.
    /// </summary>
    /// <returns>A memory of the buffer data.</returns>
    public Memory<T> AsMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return _hostArray.AsMemory();
    }

    /// <summary>
    /// Gets a read-only memory representing the buffer contents on the host.
    /// </summary>
    /// <returns>A read-only memory of the buffer data.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return _hostArray.AsMemory();
    }

    /// <summary>
    /// Gets the device memory representation of this buffer.
    /// </summary>
    /// <returns>Device memory handle.</returns>
    public DeviceMemory GetDeviceMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnDevice();
        return _deviceMemory;
    }

    /// <summary>
    /// Gets the host pointer for pinned memory access.
    /// </summary>
    /// <returns>Pointer to pinned host memory.</returns>
    public IntPtr GetHostPointer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return _pinnedHandle.AddrOfPinnedObject();
    }

    /// <summary>
    /// Creates a slice of this buffer.
    /// </summary>
    /// <param name="start">Start index of the slice.</param>
    /// <param name="length">Length of the slice.</param>
    /// <returns>A new buffer representing the slice.</returns>
    public IUnifiedMemoryBuffer<T> Slice(int start, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (start < 0 || start >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }


        if (length < 0 || start + length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        EnsureOnHost();
        var sliceData = _hostArray.AsSpan(start, length);
        return new UnifiedBuffer<T>(_memoryManager, sliceData);
    }

    /// <summary>
    /// Copies data from another buffer to this buffer.
    /// </summary>
    /// <param name="source">Source buffer to copy from.</param>
    public void CopyFrom(IUnifiedMemoryBuffer<T> source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);

        if (source.Length != Length)
        {
            throw new ArgumentException("Buffer lengths must match", nameof(source));
        }

        if (source is UnifiedBuffer<T> unifiedSource)
        {
            // Optimized copy between unified buffers
            CopyFromUnified(unifiedSource);
        }
        else
        {
            // Generic copy
            EnsureOnHost();
            source.AsReadOnlySpan().CopyTo(_hostArray.AsSpan());
            MarkHostDirty();
        }
    }

    /// <summary>
    /// Copies data to another buffer from this buffer.
    /// </summary>
    /// <param name="destination">Destination buffer to copy to.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(destination);

        if (destination.Length != Length)
        {
            throw new ArgumentException("Buffer lengths must match", nameof(destination));
        }

        await destination.CopyFromAsync(this.AsMemory(), cancellationToken: default);
    }

    private void CopyFromUnified(UnifiedBuffer<T> source)
    {
        // Try device-to-device copy first if both are on device
        if (IsOnDevice && source.IsOnDevice)
        {
            try
            {
                _memoryManager.CopyDeviceToDevice(source._deviceMemory, _deviceMemory, SizeInBytes);
                _state = BufferState.DeviceOnly;
                return;
            }
            catch
            {
                // Fall back to host copy
            }
        }

        // Ensure both buffers are on host and copy
        EnsureOnHost();
        source.EnsureOnHost();
        source._hostArray.AsSpan().CopyTo(_hostArray.AsSpan());
        MarkHostDirty();
    }
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>

    public void MarkHostDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _state = _state switch
        {
            BufferState.HostOnly => BufferState.HostOnly,
            BufferState.DeviceOnly => BufferState.HostDirty,
            BufferState.Synchronized => BufferState.HostDirty,
            BufferState.DeviceDirty => BufferState.HostDirty,
            _ => BufferState.HostDirty
        };
    }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>

    public void MarkDeviceDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _state = _state switch
        {
            BufferState.DeviceOnly => BufferState.DeviceOnly,
            BufferState.HostOnly => BufferState.DeviceDirty,
            BufferState.Synchronized => BufferState.DeviceDirty,
            BufferState.HostDirty => BufferState.DeviceDirty,
            _ => BufferState.DeviceDirty
        };
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Synchronization methods are implemented in UnifiedBufferSync.cs

    // Interface implementations are provided by other partial class files:
    // - Map, MapRange, MapAsync: UnifiedBufferSync.cs
    // - EnsureOnHostAsync, EnsureOnDeviceAsync, SynchronizeAsync: UnifiedBufferSync.cs

    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(source.Length, Length);
        await Task.CompletedTask.ConfigureAwait(false);
        EnsureOnHost();
        source.Span.CopyTo(_hostArray.AsSpan());
        MarkHostDirty();
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(destination.Length, Length);
        await Task.CompletedTask.ConfigureAwait(false);
        EnsureOnHost();
        _hostArray.AsSpan().CopyTo(destination.Span);
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Duplicate method removed - already defined above

    public async ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, Length);

        await Task.CompletedTask.ConfigureAwait(false);
        EnsureOnHost();
        var sourceSpan = _hostArray.AsSpan(sourceOffset, count);
        if (destination is UnifiedBuffer<T> unified)
        {
            unified.EnsureOnHost();
            sourceSpan.CopyTo(unified._hostArray.AsSpan(destinationOffset, count));
            unified.MarkHostDirty();
        }
        else
        {
            // Use the slice for non-UnifiedBuffer destinations
            var destSlice = destination.Slice(destinationOffset, count);
            await destSlice.CopyFromAsync(sourceSpan.ToArray(), cancellationToken);
        }
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.CompletedTask.ConfigureAwait(false);
        EnsureOnHost();
        _hostArray.AsSpan().Fill(value);
        MarkHostDirty();
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, Length);
        await Task.CompletedTask.ConfigureAwait(false);
        EnsureOnHost();
        _hostArray.AsSpan(offset, count).Fill(value);
        MarkHostDirty();
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Calculate the new length ensuring the byte size remains valid
        var originalSizeInBytes = SizeInBytes;
        var newElementSize = Unsafe.SizeOf<TNew>();
        var newLength = (int)(originalSizeInBytes / newElementSize);

        if (originalSizeInBytes % newElementSize != 0)
        {
            throw new ArgumentException(
                $"Cannot cast buffer of size {originalSizeInBytes} bytes to type {typeof(TNew).Name} " +
                $"(element size: {newElementSize} bytes) - sizes are not compatible.");
        }

        // Create a new buffer and copy the data
        var newBuffer = new UnifiedBuffer<TNew>(_memoryManager, newLength);
        var sourceBytes = MemoryMarshal.AsBytes(_hostArray.AsSpan());
        var destBytes = MemoryMarshal.AsBytes(newBuffer._hostArray.AsSpan());
        sourceBytes.CopyTo(destBytes);

        return newBuffer;
    }

    // Non-generic interface implementation
    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken)
    {
        if (typeof(U) != typeof(T))
        {
            throw new ArgumentException($"Type mismatch: expected {typeof(T)}, got {typeof(U)}");
        }

        var typedSource = MemoryMarshal.Cast<U, T>(source.Span);
        var elementOffset = (int)(offset / Unsafe.SizeOf<T>());

        // Create a slice and copy data
        if (elementOffset + typedSource.Length > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(source), "Source data exceeds buffer capacity");
        }

        EnsureOnHost();
        typedSource.CopyTo(_hostArray.AsSpan(elementOffset, typedSource.Length));
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken)
    {
        if (typeof(U) != typeof(T))
        {
            throw new ArgumentException($"Type mismatch: expected {typeof(T)}, got {typeof(U)}");
        }

        var elementOffset = (int)(offset / Unsafe.SizeOf<T>());
        var typedDestination = MemoryMarshal.Cast<U, T>(destination.Span);

        EnsureOnHost();
        var sourceSpan = _hostArray.AsSpan(elementOffset, Math.Min(typedDestination.Length, Length - elementOffset));
        sourceSpan.CopyTo(typedDestination);
        return ValueTask.CompletedTask;
    }

    // Disposal methods are implemented in UnifiedBufferDiagnostics.cs
}
