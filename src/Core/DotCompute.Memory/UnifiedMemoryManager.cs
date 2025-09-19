// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// Basic implementation of unified memory manager for cross-platform compatibility.
/// </summary>
public class UnifiedMemoryManager : IUnifiedMemoryManager
{
    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedMemoryManager"/> class.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public UnifiedMemoryManager(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public IAccelerator Accelerator { get; } = null!; // Will be set by DI or factory

    /// <inheritdoc />
    public MemoryStatistics Statistics { get; } = new();

    /// <inheritdoc />
    public long MaxAllocationSize { get; } = long.MaxValue;

    /// <inheritdoc />
    public long TotalAvailableMemory { get; } = Environment.WorkingSet;

    /// <inheritdoc />
    public long CurrentAllocatedMemory { get; private set; }

    /// <inheritdoc />
    public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,

        MemoryOptions options = MemoryOptions.None,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(UnifiedMemoryManager));
        }


        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);


        _logger.LogDebug("Allocating unified memory buffer for {Count} elements of type {Type}", count, typeof(T));

        var buffer = new UnifiedBuffer<T>(this, count);
        CurrentAllocatedMemory += buffer.SizeInBytes;

        return await Task.FromResult<IUnifiedMemoryBuffer<T>>(buffer);
    }

    /// <inheritdoc />
    public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,

        MemoryOptions options = MemoryOptions.None,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken);
        await buffer.CopyFromAsync(source, cancellationToken);
        return buffer;
    }

    /// <inheritdoc />
    public async ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,

        MemoryOptions options = MemoryOptions.None,

        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(UnifiedMemoryManager));
        }


        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);


        _logger.LogDebug("Allocating raw unified memory buffer of {Size} bytes", sizeInBytes);

        // For simplicity, use byte type for raw allocation
        var elementCount = (int)(sizeInBytes / sizeof(byte));
        var buffer = new UnifiedBuffer<byte>(this, elementCount);
        CurrentAllocatedMemory += buffer.SizeInBytes;

        return await Task.FromResult<IUnifiedMemoryBuffer>(buffer);
    }

    /// <inheritdoc />
    public IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length) where T : unmanaged
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnifiedMemoryManager));
        }

        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                $"View range ({offset}..{offset + length}) exceeds buffer length ({buffer.Length})");
        }

        _logger.LogDebug("Creating view of buffer with offset {Offset} and length {Length}", offset, length);

        // Check if the buffer is our own UnifiedBuffer type
        if (buffer is UnifiedBuffer<T> unifiedBuffer)
        {
            return new UnifiedBufferSlice<T>(unifiedBuffer, offset, length);
        }

        // For other buffer types, we would need to create a generic wrapper
        // For now, throw an exception indicating unsupported buffer type
        throw new NotSupportedException(
            $"Creating views of buffer type {buffer.GetType().Name} is not currently supported. " +
            "Only UnifiedBuffer<T> instances support view creation.");
    }

    /// <inheritdoc />
    public async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,

        IUnifiedMemoryBuffer<T> destination,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await source.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,

        int sourceOffset,

        IUnifiedMemoryBuffer<T> destination,

        int destinationOffset,

        int count,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        await source.CopyToAsync(sourceOffset, destination, destinationOffset, count, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,

        IUnifiedMemoryBuffer<T> destination,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(destination);
        await destination.CopyFromAsync(source, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,

        Memory<T> destination,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(source);
        await source.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc />
    public void Free(IUnifiedMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        await buffer.DisposeAsync();
    }

    /// <inheritdoc />
    public async ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Optimizing unified memory manager");

        // Run garbage collection to free up managed memory

        await Task.Run(() =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _logger.LogDebug("Clearing all allocated memory");
        CurrentAllocatedMemory = 0;

        // Force garbage collection

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// Deallocates memory for a buffer (called internally by buffers).
    /// </summary>
    /// <param name="sizeInBytes">Size of the buffer being deallocated.</param>
    internal void Deallocate(long sizeInBytes)
    {
        CurrentAllocatedMemory -= sizeInBytes;
        if (CurrentAllocatedMemory < 0)
        {
            CurrentAllocatedMemory = 0;
        }

    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _logger.LogDebug("UnifiedMemoryManager disposed");
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await Task.CompletedTask;
    }
}