// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Utilities.TestInfrastructure
{

/// <summary>
/// Test implementation of IMemoryManager for testing purposes
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestMemoryManager : IMemoryManager
{
    private readonly Dictionary<int, TestMemoryBuffer> _allocatedBuffers = [];
    private int _nextBufferId = 1;
    private bool _disposed;

    /// <summary>
    /// Gets the total number of currently allocated buffers
    /// </summary>
    public int AllocatedBufferCount => _allocatedBuffers.Count;

    /// <summary>
    /// Gets the total amount of allocated memory in bytes
    /// </summary>
    public long TotalAllocatedMemory => _allocatedBuffers.Values.Sum(b => b.SizeInBytes);

    /// <summary>
    /// Allocates memory on the test accelerator
    /// </summary>
    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive");
        }

        var bufferId = _nextBufferId++;
        var buffer = new TestMemoryBuffer(bufferId, sizeInBytes, options, this);
        _allocatedBuffers[bufferId] = buffer;

        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <summary>
    /// Allocates memory and copies data from host
    /// </summary>
    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var sizeInBytes = source.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken);
        await buffer.CopyFromHostAsync(source, 0, cancellationToken);
        return buffer;
    }

    /// <summary>
    /// Creates a view over existing memory
    /// </summary>
    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer is not TestMemoryBuffer testBuffer)
        {
            throw new ArgumentException("Buffer must be a TestMemoryBuffer", nameof(buffer));
        }

        if (offset < 0 || length <= 0 || offset + length > testBuffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException("Invalid offset or length for buffer view");
        }

        var viewId = _nextBufferId++;
        var view = new TestMemoryBufferView(viewId, testBuffer, offset, length, this);
        _allocatedBuffers[viewId] = view;

        return view;
    }

    /// <summary>
    /// Allocates memory for a specific number of elements
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="count">The number of elements to allocate</param>
    /// <returns>A memory buffer for the allocated elements</returns>
    public async ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var sizeInBytes = count * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return await AllocateAsync(sizeInBytes);
    }

    /// <summary>
    /// Copies data from host memory to a device buffer
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="buffer">The destination buffer</param>
    /// <param name="data">The source data span</param>
    /// <returns>A task representing the async operation</returns>
    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        var memory = new ReadOnlyMemory<T>(data.ToArray());
        buffer.CopyFromHostAsync(memory).AsTask().Wait();
    }

    /// <summary>
    /// Copies data from a device buffer to host memory
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    /// <param name="data">The destination data span</param>
    /// <param name="buffer">The source buffer</param>
    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        var memory = new Memory<T>(new T[data.Length]);
        buffer.CopyToHostAsync(memory).AsTask().Wait();
        memory.Span.CopyTo(data);
    }

    /// <summary>
    /// Frees a memory buffer
    /// </summary>
    /// <param name="buffer">The buffer to free</param>
    public void Free(IMemoryBuffer buffer)
    {
        if (buffer is TestMemoryBuffer testBuffer)
        {
            // The TestMemoryBuffer.Dispose() method will call ReleaseBuffer internally
            testBuffer.Dispose();
        }
        else
        {
            buffer?.Dispose();
        }
    }

    /// <summary>
    /// Internal method to release a buffer
    /// </summary>
    internal void ReleaseBuffer(int bufferId) => _allocatedBuffers.Remove(bufferId);

    /// <summary>
    /// Simulates a memory allocation failure
    /// </summary>
    public static void SimulateAllocationFailure() => throw new OutOfMemoryException("Simulated memory allocation failure");

    /// <summary>
    /// Clears all allocated buffers
    /// </summary>
    public void Clear()
    {
        foreach (var buffer in _allocatedBuffers.Values.ToList())
        {
            buffer.Dispose();
        }
        _allocatedBuffers.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>
    /// Disposes the memory manager and all allocated buffers
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test implementation of IMemoryBuffer
/// </summary>
[ExcludeFromCodeCoverage]
public class TestMemoryBuffer : IMemoryBuffer
{
    private readonly int _bufferId;
    private readonly TestMemoryManager _manager;
    private readonly byte[] _data;
    private bool _disposed;

    internal TestMemoryBuffer(int bufferId, long sizeInBytes, MemoryOptions options, TestMemoryManager manager)
    {
        _bufferId = bufferId;
        _manager = manager;
        SizeInBytes = sizeInBytes;
        Options = options;
        _data = new byte[sizeInBytes];
    }

    /// <inheritdoc/>
    public long SizeInBytes { get; }

    /// <inheritdoc/>
    public MemoryOptions Options { get; }

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets access to the underlying data for testing
    /// </summary>
    public ReadOnlySpan<byte> Data => _data;

    /// <inheritdoc/>
    public virtual ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.Span);
        if (offset + sourceBytes.Length > SizeInBytes)
        {
            throw new ArgumentException("Source data is too large for buffer");
        }

        sourceBytes.CopyTo(_data.AsSpan((int)offset));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var destBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.Span);
        if (offset + destBytes.Length > SizeInBytes)
        {
            throw new ArgumentException("Destination buffer is too small");
        }

        _data.AsSpan((int)offset, destBytes.Length).CopyTo(destBytes);
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the buffer.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _manager.ReleaseBuffer(_bufferId);
        }

        _disposed = true;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Test implementation of a memory buffer view
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestMemoryBufferView : TestMemoryBuffer
{
    private readonly TestMemoryBuffer _parentBuffer;
    private readonly long _offset;
    private readonly long _length;

    internal TestMemoryBufferView(int bufferId, TestMemoryBuffer parentBuffer, long offset, long length, TestMemoryManager manager)
        : base(bufferId, length, parentBuffer.Options, manager)
    {
        _parentBuffer = parentBuffer;
        _offset = offset;
        _length = length;
    }

    /// <summary>
    /// Gets the parent buffer this view is based on
    /// </summary>
    public TestMemoryBuffer ParentBuffer => _parentBuffer;

    /// <summary>
    /// Gets the offset within the parent buffer
    /// </summary>
    public long Offset => _offset;

    /// <summary>
    /// Gets access to the view data
    /// </summary>
    public new ReadOnlySpan<byte> Data => _parentBuffer.Data.Slice((int)_offset, (int)_length);

    /// <inheritdoc/>
    public override ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Adjust offset to parent buffer's coordinate system
        return _parentBuffer.CopyFromHostAsync(source, _offset + offset, cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Adjust offset to parent buffer's coordinate system
        return _parentBuffer.CopyToHostAsync(destination, _offset + offset, cancellationToken);
    }
}
}
