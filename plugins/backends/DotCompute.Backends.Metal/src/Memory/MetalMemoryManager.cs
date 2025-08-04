// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Memory manager for Metal GPU memory allocation and management.
/// </summary>
public sealed class MetalMemoryManager : IMemoryManager
{
    private readonly IntPtr _device;
    private readonly MetalAcceleratorOptions _options;
    private readonly ConcurrentDictionary<IMemoryBuffer, MetalMemoryBuffer> _allocations;
    private long _totalAllocated;
    private int _disposed;

    public MetalMemoryManager(IntPtr device, MetalAcceleratorOptions options)
    {
        _device = device;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _allocations = new ConcurrentDictionary<IMemoryBuffer, MetalMemoryBuffer>();
    }

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

        if (_disposed > 0)
        {
            throw new ObjectDisposedException(nameof(MetalMemoryManager));
        }

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
internal sealed class MetalMemoryBuffer : IMemoryBuffer
{
    private readonly MetalMemoryManager _manager;
    private readonly MetalStorageMode _storageMode;
    private int _disposed;

    public MetalMemoryBuffer(IntPtr buffer, long sizeInBytes, MemoryOptions options, MetalStorageMode storageMode, MetalMemoryManager manager)
    {
        Buffer = buffer;
        SizeInBytes = sizeInBytes;
        Options = options;
        _storageMode = storageMode;
        _manager = manager;
    }

    public IntPtr Buffer { get; }

    public long SizeInBytes { get; }

    public MemoryOptions Options { get; }

    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (_disposed > 0)
        {
            throw new ObjectDisposedException(nameof(MetalMemoryBuffer));
        }

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
        if (_disposed > 0)
        {
            throw new ObjectDisposedException(nameof(MetalMemoryBuffer));
        }

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
internal sealed class MetalMemoryBufferView : IMemoryBuffer
{
    private readonly MetalMemoryBuffer _parent;
    private readonly long _offset;

    public MetalMemoryBufferView(MetalMemoryBuffer parent, long offset, long length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        SizeInBytes = length;
    }

    public long SizeInBytes { get; }

    public MemoryOptions Options => _parent.Options;

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        // Views don't own the underlying buffer
        return ValueTask.CompletedTask;
    }
}
