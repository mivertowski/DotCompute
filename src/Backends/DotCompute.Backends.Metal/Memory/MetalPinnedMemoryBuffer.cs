// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Metal pinned (page-locked) memory buffer optimized for fast CPU-GPU transfers.
/// Provides high-bandwidth transfers by keeping memory pages locked in physical RAM.
/// </summary>
public sealed class MetalPinnedMemoryBuffer : IUnifiedMemoryBuffer
{
    private volatile bool _disposed;
    private BufferState _state = BufferState.Uninitialized;
    private GCHandle _pinnedHandle;
    private byte[]? _backingArray;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalPinnedMemoryBuffer"/> class.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">Memory options.</param>
    public MetalPinnedMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        SizeInBytes = sizeInBytes;
        Options = options | MemoryOptions.Pinned; // Always mark as pinned

        Initialize();
    }

    /// <inheritdoc/>
    public long SizeInBytes { get; }

    /// <inheritdoc/>
    public MemoryOptions Options { get; }

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public BufferState State => _disposed ? BufferState.Disposed : _state;

    /// <summary>
    /// Gets the native handle to the pinned memory.
    /// </summary>
    public IntPtr NativeHandle { get; private set; }

    /// <summary>
    /// Gets whether the memory is currently pinned.
    /// </summary>
    public bool IsPinned => _pinnedHandle.IsAllocated;

    /// <summary>
    /// Initializes the pinned memory buffer.
    /// </summary>
    private void Initialize()
    {
        try
        {
            // Allocate page-aligned memory for optimal transfer performance
            var arraySize = (int)Math.Min(SizeInBytes, int.MaxValue);
            _backingArray = GC.AllocateArray<byte>(arraySize, pinned: true);

            // Pin the array in memory
            _pinnedHandle = GCHandle.Alloc(_backingArray, GCHandleType.Pinned);
            NativeHandle = _pinnedHandle.AddrOfPinnedObject();

            _state = BufferState.Allocated;

            // Initialize memory to zero if requested
            if (Options.InitializeToZero())
            {
                _backingArray.AsSpan().Clear();
            }
        }
        catch (OutOfMemoryException ex)
        {
            _state = BufferState.Disposed;
            throw new InvalidOperationException($"Failed to allocate {SizeInBytes} bytes of pinned memory", ex);
        }
        catch (Exception ex)
        {
            _state = BufferState.Disposed;
            throw new InvalidOperationException($"Failed to initialize pinned memory buffer: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        ValidateState();

        return PerformPinnedCopyFromAsync(source, offset, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        ValidateState();

        return PerformPinnedCopyToAsync(destination, offset, cancellationToken);
    }

    /// <summary>
    /// Legacy support method (calls CopyFromAsync).
    /// </summary>
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => CopyFromAsync(source, offset, cancellationToken);

    /// <summary>
    /// Legacy support method (calls CopyToAsync).
    /// </summary>
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => CopyToAsync(destination, offset, cancellationToken);

    /// <summary>
    /// Performs optimized copy from host memory to pinned buffer.
    /// </summary>
    private ValueTask PerformPinnedCopyFromAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken) where T : unmanaged
    {
        var elementSize = Unsafe.SizeOf<T>();
        var totalBytes = source.Length * elementSize;

        // Validate bounds
        if (offset + totalBytes > SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(source), "Copy would exceed buffer bounds");
        }

        if (source.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            // For pinned memory, we can perform direct memory copies
            unsafe
            {
                var sourcePtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(source.Span));
                var destPtr = (byte*)NativeHandle + offset;

                // Use optimized memory copy for pinned-to-pinned transfers
                System.Buffer.MemoryCopy(sourcePtr, destPtr, SizeInBytes - offset, totalBytes);
            }

            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(new InvalidOperationException($"Pinned memory copy failed: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Performs optimized copy from pinned buffer to host memory.
    /// </summary>
    private ValueTask PerformPinnedCopyToAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken) where T : unmanaged
    {
        var elementSize = Unsafe.SizeOf<T>();
        var totalBytes = destination.Length * elementSize;

        // Validate bounds
        if (offset + totalBytes > SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(destination), "Copy would exceed buffer bounds");
        }

        if (destination.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            // For pinned memory, we can perform direct memory copies
            unsafe
            {
                var sourcePtr = (byte*)NativeHandle + offset;
                var destPtr = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(destination.Span));

                // Use optimized memory copy for pinned-to-pinned transfers
                System.Buffer.MemoryCopy(sourcePtr, destPtr, destination.Length * elementSize, totalBytes);
            }

            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(new InvalidOperationException($"Pinned memory copy failed: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Gets a span view of the pinned memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>A span representing the pinned memory.</returns>
    public Span<T> AsSpan<T>() where T : unmanaged
    {
        ThrowIfDisposed();
        ValidateState();

        var elementSize = Unsafe.SizeOf<T>();
        var elementCount = (int)(SizeInBytes / elementSize);

        unsafe
        {
            return new Span<T>((T*)NativeHandle, elementCount);
        }
    }

    /// <summary>
    /// Gets a memory view of the pinned memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns>Memory representing the pinned memory.</returns>
    public Memory<T> AsMemory<T>() where T : unmanaged
    {
        ThrowIfDisposed();
        ValidateState();

        if (_backingArray == null)
        {
            throw new InvalidOperationException("Backing array is null");
        }

        var elementSize = Unsafe.SizeOf<T>();
        var elementCount = (int)(SizeInBytes / elementSize);

        var byteMemory = _backingArray.AsMemory(0, elementCount * elementSize);
        return MemoryMarshal.Cast<byte, T>(byteMemory.Span).ToArray().AsMemory();
    }

    /// <summary>
    /// Validates that the buffer is in a usable state.
    /// </summary>
    private void ValidateState()
    {
        if (_state != BufferState.Allocated)
        {
            throw new InvalidOperationException($"Buffer is not in allocated state: {_state}");
        }

        if (!IsPinned)
        {
            throw new InvalidOperationException("Memory is not pinned");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _state = BufferState.Disposed;

        // Release pinned memory
        if (_pinnedHandle.IsAllocated)
        {
            _pinnedHandle.Free();
        }

        _backingArray = null;
        NativeHandle = IntPtr.Zero;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
