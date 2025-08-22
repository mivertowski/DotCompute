// <copyright file="SimpleInMemoryManager.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Simple in-memory implementation of IMemoryManager for default scenarios and testing.
/// </summary>
/// <remarks>
/// This implementation provides basic memory management functionality using managed memory arrays.
/// It's suitable for testing, prototyping, and scenarios where hardware acceleration is not available.
/// For production use with GPU acceleration, use the appropriate backend-specific memory manager.
/// </remarks>
internal sealed class SimpleInMemoryManager : IMemoryManager, IDisposable
{
    private bool _disposed;
    private long _totalAllocated;
    private long _totalFreed;
    private int _allocationCount;

    /// <summary>
    /// Gets the total amount of memory allocated by this manager.
    /// </summary>
    public long TotalAllocated => _totalAllocated;

    /// <summary>
    /// Gets the number of allocations made by this manager.
    /// </summary>
    public int AllocationCount => _allocationCount;

    /// <inheritdoc/>
    public ValueTask<IMemoryBuffer> AllocateAsync(long sizeInBytes, DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var buffer = new SimpleMemoryBuffer(sizeInBytes, options);
        
        // Track statistics
        Interlocked.Add(ref _totalAllocated, sizeInBytes);
        Interlocked.Increment(ref _allocationCount);
        
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <inheritdoc/>
    public ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var sizeInBytes = source.Length * Unsafe.SizeOf<T>();
        var buffer = new SimpleMemoryBuffer(sizeInBytes, options);
        
        // Copy the source data to the buffer
        var task = buffer.CopyFromHostAsync(source, cancellationToken);
        
        // Track statistics
        Interlocked.Add(ref _totalAllocated, sizeInBytes);
        Interlocked.Increment(ref _allocationCount);
        
        // Since CopyFromHostAsync is already async, we need to wait for it
        if (task.IsCompletedSuccessfully)
        {
            return ValueTask.FromResult<IMemoryBuffer>(buffer);
        }
        
        return AllocateAndCopyAsyncCore(task, buffer);
        
        static async ValueTask<IMemoryBuffer> AllocateAndCopyAsyncCore(ValueTask copyTask, SimpleMemoryBuffer buffer)
        {
            await copyTask.ConfigureAwait(false);
            return buffer;
        }
    }

    /// <inheritdoc/>
    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative.");
        }

        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be positive.");
        }

        if (offset + length > buffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "View exceeds buffer bounds.");
        }

        // For simplicity, create a new buffer for the view
        // In a real implementation, this would create a view over the existing memory
        return new SimpleMemoryBuffer(length, buffer.Options);
    }

    /// <summary>
    /// Allocates a buffer for a specific type.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="count">The number of elements.</param>
    /// <returns>The allocated buffer.</returns>
    public async ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var sizeInBytes = count * Unsafe.SizeOf<T>();
        return await AllocateAsync(sizeInBytes).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Free(IMemoryBuffer buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Dispose();
        
        // Track statistics
        Interlocked.Add(ref _totalFreed, buffer.SizeInBytes);
        Interlocked.Decrement(ref _allocationCount);
    }

    /// <summary>
    /// Copies data from host to device buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="data">The source data.</param>
    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        var memory = new ReadOnlyMemory<T>(data.ToArray());
        buffer.CopyFromHostAsync(memory).AsTask().Wait();
    }

    /// <summary>
    /// Copies data from device buffer to host.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="data">The destination span.</param>
    /// <param name="buffer">The source buffer.</param>
    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        var memory = new Memory<T>(data.ToArray());
        buffer.CopyToHostAsync(memory).AsTask().Wait();
        memory.Span.CopyTo(data);
    }

    /// <summary>
    /// Synchronizes a buffer between host and device.
    /// </summary>
    /// <param name="buffer">The buffer to synchronize.</param>
    public void Synchronize(IMemoryBuffer buffer)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        // Note: SynchronizeAsync is implementation-specific and not on the IMemoryBuffer interface
        // For SimpleMemoryBuffer, synchronization is a no-op since it's always in host memory
    }

    /// <summary>
    /// Resets the allocation statistics.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalAllocated, 0);
        Interlocked.Exchange(ref _allocationCount, 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}