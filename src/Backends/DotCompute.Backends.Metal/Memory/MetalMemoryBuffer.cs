// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.Metal.Native;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Metal-specific memory buffer implementation with actual Metal API integration.
/// </summary>
public sealed class MetalMemoryBuffer : IUnifiedMemoryBuffer
{
    /// <inheritdoc/>
    public long SizeInBytes { get; }

    /// <inheritdoc/>
    public MemoryOptions Options { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public BufferState State { get; private set; }

    /// <summary>
    /// Gets the native Metal buffer handle.
    /// </summary>
    public IntPtr Buffer { get; private set; }

    /// <summary>
    /// Gets the native handle (alias for Buffer for consistency).
    /// </summary>
    public IntPtr NativeHandle => Buffer;

    /// <summary>
    /// Gets the Metal device used for this buffer.
    /// </summary>
    public IntPtr Device { get; private set; }

    /// <summary>
    /// Gets the storage mode used for this buffer.
    /// </summary>
    public MetalStorageMode StorageMode { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMemoryBuffer"/> class.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">Memory options.</param>
    /// <param name="device">The Metal device to use for buffer allocation.</param>
    /// <param name="storageMode">The Metal storage mode (defaults to Shared).</param>
    public MetalMemoryBuffer(long sizeInBytes, MemoryOptions options, IntPtr device = default, MetalStorageMode storageMode = MetalStorageMode.Shared)
    {
        SizeInBytes = sizeInBytes;
        Options = options;
        State = BufferState.Uninitialized;
        Buffer = IntPtr.Zero;
        Device = device != IntPtr.Zero ? device : GetDefaultDevice();
        StorageMode = storageMode;
    }

    /// <summary>
    /// Initializes a new instance with an existing Metal buffer.
    /// </summary>
    /// <param name="buffer">Existing Metal buffer handle.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">Memory options.</param>
    /// <param name="device">The Metal device.</param>
    /// <param name="storageMode">The Metal storage mode.</param>
    internal MetalMemoryBuffer(IntPtr buffer, long sizeInBytes, MemoryOptions options, IntPtr device, MetalStorageMode storageMode = MetalStorageMode.Shared)
    {
        Buffer = buffer;
        SizeInBytes = sizeInBytes;
        Options = options;
        Device = device;
        StorageMode = storageMode;
        State = BufferState.Allocated;
    }

    /// <summary>
    /// Initializes the Metal buffer (async version for enhanced memory manager).
    /// </summary>
    public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (State != BufferState.Uninitialized)
        {
            return;
        }

        await Task.Run(() =>
        {
            // Allocate actual Metal buffer with specified storage mode
            Buffer = MetalNative.CreateBuffer(Device, (nuint)SizeInBytes, StorageMode);

            if (Buffer == IntPtr.Zero)
            {
                throw new OutOfMemoryException($"Failed to allocate Metal buffer of size {SizeInBytes} bytes with {StorageMode} mode");
            }

            State = BufferState.Allocated;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (State != BufferState.Allocated)
        {

            throw new InvalidOperationException("Buffer must be allocated before copying data");
        }


        return new ValueTask(Task.Run(() =>
        {
            unsafe
            {
                var elementSize = sizeof(T);
                var totalBytes = source.Length * elementSize;

                if (offset + totalBytes > SizeInBytes)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "Copy would exceed buffer bounds");
                }

                // Get buffer contents pointer
                var bufferContents = MetalNative.GetBufferContents(Buffer);
                if (bufferContents == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get Metal buffer contents pointer");
                }

                // Copy data from source to Metal buffer
                var sourceHandle = source.Pin();
                try
                {
                    var destPtr = (byte*)bufferContents + offset;
                    var srcPtr = (byte*)sourceHandle.Pointer;
                    System.Buffer.MemoryCopy(srcPtr, destPtr, totalBytes, totalBytes);
                }
                finally
                {
                    sourceHandle.Dispose();
                }

                // Mark the modified range if using managed storage
                if (StorageMode == MetalStorageMode.Managed)
                {
                    MetalNative.DidModifyRange(Buffer, offset, totalBytes);
                }
            }
        }, cancellationToken));
    }

    /// <inheritdoc/>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (State != BufferState.Allocated)
        {

            throw new InvalidOperationException("Buffer must be allocated before copying data");
        }


        return new ValueTask(Task.Run(() =>
        {
            unsafe
            {
                var elementSize = sizeof(T);
                var totalBytes = destination.Length * elementSize;

                if (offset + totalBytes > SizeInBytes)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), "Copy would exceed buffer bounds");
                }

                // Get buffer contents pointer
                var bufferContents = MetalNative.GetBufferContents(Buffer);
                if (bufferContents == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get Metal buffer contents pointer");
                }

                // Copy data from Metal buffer to destination
                var destHandle = destination.Pin();
                try
                {
                    var srcPtr = (byte*)bufferContents + offset;
                    var destPtr = (byte*)destHandle.Pointer;
                    System.Buffer.MemoryCopy(srcPtr, destPtr, totalBytes, totalBytes);
                }
                finally
                {
                    destHandle.Dispose();
                }
            }
        }, cancellationToken));
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

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!IsDisposed)
        {
            State = BufferState.Disposed;
            IsDisposed = true;

            // Release native Metal buffer

            if (Buffer != IntPtr.Zero)
            {
                try
                {
                    MetalNative.ReleaseBuffer(Buffer);
                }
                catch
                {
                    // Suppress exceptions during disposal
                }
                finally
                {
                    Buffer = IntPtr.Zero;
                }
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static IntPtr GetDefaultDevice()
    {
        var device = MetalNative.CreateSystemDefaultDevice();
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create default Metal device");
        }
        return device;
    }

    /// <summary>
    /// Gets the actual buffer length as reported by Metal.
    /// </summary>
    public long GetActualLength()
    {
        if (Buffer == IntPtr.Zero)
        {
            return 0;
        }


        return (long)MetalNative.GetBufferLength(Buffer);
    }

    /// <summary>
    /// Creates a copy of this buffer with the same data.
    /// </summary>
    public async ValueTask<MetalMemoryBuffer> CloneAsync(CancellationToken cancellationToken = default)
    {
        if (State != BufferState.Allocated)
        {
            throw new InvalidOperationException("Cannot clone unallocated buffer");
        }

        var clone = new MetalMemoryBuffer(SizeInBytes, Options, Device, StorageMode);
        await clone.InitializeAsync(cancellationToken);

        // Copy buffer contents using Metal API
        MetalNative.CopyBuffer(Buffer, 0, clone.Buffer, 0, SizeInBytes);

        return clone;
    }

    /// <summary>
    /// Gets whether this buffer uses zero-copy unified memory.
    /// </summary>
    public bool IsZeroCopyUnifiedMemory()
    {
        // Zero-copy is available when using Shared storage mode
        return StorageMode == MetalStorageMode.Shared;
    }
}