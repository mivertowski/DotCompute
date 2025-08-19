// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Memory
{

/// <summary>
/// Memory manager for Metal GPU memory allocation and management.
/// </summary>
public sealed class MetalMemoryManager(IntPtr device, MetalAcceleratorOptions options) : IMemoryManager
{
    private readonly IntPtr _device = device;
    private readonly MetalAcceleratorOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ConcurrentDictionary<IMemoryBuffer, MetalMemoryBuffer> _allocations = new();
    private long _totalAllocated;
    private int _disposed;

    /// <inheritdoc/>
    public async ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive.");
        }

        if (sizeInBytes > _options.MaxMemoryAllocation)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes),
                $"Requested size {sizeInBytes} exceeds maximum allocation size {_options.MaxMemoryAllocation}.");
        }

        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        return await Task.Run(() =>
        {
            // Determine storage mode based on options
            var storageMode = GetStorageMode(options);

            // Create Metal buffer
            var buffer = MetalNative.CreateBuffer(_device, (nuint)sizeInBytes, storageMode);
            if (buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to allocate Metal buffer of size {sizeInBytes}.");
            }

            // Create memory buffer wrapper
            var memoryBuffer = new MetalMemoryBuffer(buffer, sizeInBytes, options, storageMode, this);

            // Track allocation
            if (!_allocations.TryAdd(memoryBuffer, memoryBuffer))
            {
                MetalNative.ReleaseBuffer(buffer);
                throw new InvalidOperationException("Failed to track memory allocation.");
            }

            Interlocked.Add(ref _totalAllocated, sizeInBytes);

            return memoryBuffer;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * Unsafe.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);

        try
        {
            await buffer.CopyFromHostAsync(source, 0, cancellationToken).ConfigureAwait(false);
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (!_allocations.TryGetValue(buffer, out var metalBuffer))
        {
            throw new ArgumentException("Buffer was not allocated by this manager.", nameof(buffer));
        }

        if (offset < 0 || offset >= metalBuffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length < 0 || offset + length > metalBuffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return new MetalMemoryBufferView(metalBuffer, offset, length);
    }

    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var elementSize = Unsafe.SizeOf<T>();
        var sizeInBytes = count * elementSize;
        return AllocateAsync(sizeInBytes);
    }

    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ObjectDisposedException.ThrowIf(_disposed > 0, this);
        
        if (!_allocations.TryGetValue(buffer, out var metalBuffer))
        {
            throw new ArgumentException("Buffer was not allocated by this manager.", nameof(buffer));
        }
        
        var elementSize = Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;
        
        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        unsafe
        {
            var contents = MetalNative.GetBufferContents(metalBuffer.Buffer);
            if (contents == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get buffer contents.");
            }

            fixed (T* dataPtr = data)
            {
                System.Buffer.MemoryCopy(dataPtr, contents.ToPointer(), buffer.SizeInBytes, sizeInBytes);
            }

            // For managed storage mode, mark the range as modified
            if (metalBuffer.StorageMode == MetalStorageMode.Managed)
            {
                MetalNative.DidModifyRange(metalBuffer.Buffer, 0, sizeInBytes);
            }
        }
    }

    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ObjectDisposedException.ThrowIf(_disposed > 0, this);
        
        if (!_allocations.TryGetValue(buffer, out var metalBuffer))
        {
            throw new ArgumentException("Buffer was not allocated by this manager.", nameof(buffer));
        }
        
        var elementSize = Unsafe.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;
        
        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        unsafe
        {
            var contents = MetalNative.GetBufferContents(metalBuffer.Buffer);
            if (contents == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to get buffer contents.");
            }

            fixed (T* dataPtr = data)
            {
                System.Buffer.MemoryCopy(contents.ToPointer(), dataPtr, sizeInBytes, sizeInBytes);
            }
        }
    }

    public void Free(IMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (_allocations.TryRemove(buffer, out var metalBuffer))
        {
            metalBuffer.Dispose();
        }
    }

    public long GetAllocatedMemory() => Interlocked.Read(ref _totalAllocated);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Free all remaining allocations
        foreach (var allocation in _allocations.Values)
        {
            await allocation.DisposeAsync().ConfigureAwait(false);
        }
        _allocations.Clear();
    }

    internal void OnMemoryFreed(MetalMemoryBuffer memory)
    {
        _allocations.TryRemove(memory, out _);
        Interlocked.Add(ref _totalAllocated, -memory.SizeInBytes);
    }

    private static MetalStorageMode GetStorageMode(MemoryOptions options)
    {
        // Determine optimal storage mode based on options
        if (options.HasFlag(MemoryOptions.HostVisible))
        {
            return MetalStorageMode.Shared; // Unified memory (most common on Apple Silicon)
        }

        if (options.HasFlag(MemoryOptions.Cached))
        {
            return MetalStorageMode.Managed; // Managed memory with explicit synchronization
        }

        // Default to private GPU memory for best performance
        return MetalStorageMode.Private;
    }
}

/// <summary>
/// Metal-specific memory buffer implementation.
/// </summary>
internal sealed class MetalMemoryBuffer(IntPtr buffer, long sizeInBytes, MemoryOptions options, MetalStorageMode storageMode, MetalMemoryManager manager) : IMemoryBuffer
{
    private readonly MetalMemoryManager _manager = manager;
    private readonly MetalStorageMode _storageMode = storageMode;
    private int _disposed;

    public IntPtr Buffer { get; } = buffer;

    public long SizeInBytes { get; } = sizeInBytes;

    public MemoryOptions Options { get; } = options;

    public bool IsDisposed => _disposed > 0;

    public MetalStorageMode StorageMode => _storageMode;

    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        if (offset < 0 || offset >= SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var sizeInBytes = source.Length * Unsafe.SizeOf<T>();
        if (offset + sizeInBytes > SizeInBytes)
        {
            throw new ArgumentException("Source data exceeds buffer size.");
        }

        await Task.Run(() =>
        {
            unsafe
            {
                var contents = MetalNative.GetBufferContents(Buffer);
                if (contents == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get buffer contents.");
                }

                using var handle = source.Pin();
                var sourcePtr = new IntPtr(handle.Pointer);
                var destPtr = IntPtr.Add(contents, (int)offset);

                System.Buffer.MemoryCopy(sourcePtr.ToPointer(), destPtr.ToPointer(), SizeInBytes - offset, sizeInBytes);

                // For managed storage mode, mark the range as modified
                if (_storageMode == MetalStorageMode.Managed)
                {
                    MetalNative.DidModifyRange(Buffer, offset, sizeInBytes);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        if (offset < 0 || offset >= SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var sizeInBytes = destination.Length * Unsafe.SizeOf<T>();
        if (offset + sizeInBytes > SizeInBytes)
        {
            throw new ArgumentException("Requested range exceeds buffer size.");
        }

        await Task.Run(() =>
        {
            unsafe
            {
                var contents = MetalNative.GetBufferContents(Buffer);
                if (contents == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to get buffer contents.");
                }

                using var handle = destination.Pin();
                var destPtr = new IntPtr(handle.Pointer);
                var sourcePtr = IntPtr.Add(contents, (int)offset);

                System.Buffer.MemoryCopy(sourcePtr.ToPointer(), destPtr.ToPointer(), sizeInBytes, sizeInBytes);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await Task.Run(() =>
        {
            if (Buffer != IntPtr.Zero)
            {
                MetalNative.ReleaseBuffer(Buffer);
            }
            _manager.OnMemoryFreed(this);
        }).ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            // Dispose synchronously - simplified approach for sync dispose
            if (Buffer != IntPtr.Zero)
            {
                MetalNative.ReleaseBuffer(Buffer);
            }
            _manager.OnMemoryFreed(this);
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    ~MetalMemoryBuffer()
    {
        if (_disposed == 0 && Buffer != IntPtr.Zero)
        {
            MetalNative.ReleaseBuffer(Buffer);
        }
    }
}

/// <summary>
/// View over a Metal memory buffer.
/// </summary>
internal sealed class MetalMemoryBufferView(MetalMemoryBuffer parent, long offset, long length) : IMemoryBuffer
{
#pragma warning disable CA2213 // Disposable fields should be disposed - View doesn't own parent buffer
    private readonly MetalMemoryBuffer _parent = parent ?? throw new ArgumentNullException(nameof(parent));
#pragma warning restore CA2213
    private readonly long _offset = offset;

    public long SizeInBytes { get; } = length;

    public MemoryOptions Options => _parent.Options;

    public bool IsDisposed => _parent.IsDisposed;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask; // Views don't own the underlying buffer

    public void Dispose()
    {
        // Views don't own the underlying buffer, so nothing to dispose
    }
}
}
