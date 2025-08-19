using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Implementations.Memory;


/// <summary>
/// Test implementation of IMemoryManager for testing without GPU hardware.
/// Uses pinned host memory to simulate device memory operations.
/// </summary>
public sealed class TestMemoryManager : IMemoryManager, IDisposable
{
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;
    private readonly ConcurrentDictionary<IntPtr, TestMemoryBuffer> _allocations = new();
    private long _totalAllocated;
    private long _peakAllocated;

    /// <summary>
    /// Allocates memory on the accelerator.
    /// </summary>
    /// <param name="sizeInBytes"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentOutOfRangeException">sizeInBytes - Size must be positive</exception>
    public async ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive");
        }

        await Task.Yield(); // Simulate async operation

        var buffer = _pool.Rent((int)sizeInBytes);
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        var ptr = handle.AddrOfPinnedObject();

        var memBuffer = new TestMemoryBuffer(this, ptr, sizeInBytes, options, handle, buffer);
        _allocations[ptr] = memBuffer;

        var newTotal = Interlocked.Add(ref _totalAllocated, sizeInBytes);
        var currentPeak = _peakAllocated;
        while (newTotal > currentPeak)
        {
            _ = Interlocked.CompareExchange(ref _peakAllocated, newTotal, currentPeak);
            currentPeak = _peakAllocated;
        }

        return memBuffer;
    }

    /// <summary>
    /// Allocates memory and copies data from host.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * Marshal.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken);
        await buffer.CopyFromHostAsync(source, 0, cancellationToken);
        return buffer;
    }

    /// <summary>
    /// Creates a view over existing memory.
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentException">Buffer must be a TestMemoryBuffer - buffer</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// offset
    /// or
    /// length
    /// </exception>
    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        if (buffer is not TestMemoryBuffer testBuffer)
        {
            throw new ArgumentException("Buffer must be a TestMemoryBuffer", nameof(buffer));
        }

        if (offset < 0 || offset >= buffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length <= 0 || offset + length > buffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return new TestMemoryView(testBuffer, offset, length);
    }

    /// <summary>
    /// Allocates memory for a specific number of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="count">The number of elements to allocate.</param>
    /// <returns>
    /// A memory buffer for the allocated elements.
    /// </returns>
    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var elementSize = Marshal.SizeOf<T>();
        var sizeInBytes = count * elementSize;
        return AllocateAsync(sizeInBytes);
    }

    /// <summary>
    /// Copies data from host memory to a device buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="data">The source data span.</param>
    /// <exception cref="System.ArgumentException">
    /// Buffer must be a TestMemoryBuffer - buffer
    /// or
    /// Data size exceeds buffer capacity - data
    /// </exception>
    public void CopyToDevice<T>(IMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        if (buffer is not TestMemoryBuffer testBuffer)
        {
            throw new ArgumentException("Buffer must be a TestMemoryBuffer", nameof(buffer));
        }

        var elementSize = Marshal.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;

        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        unsafe
        {
            fixed (T* dataPtr = data)
            {
                var destPtr = (byte*)testBuffer.Handle.ToPointer();
                Buffer.MemoryCopy(dataPtr, destPtr, buffer.SizeInBytes, sizeInBytes);
            }
        }
    }

    /// <summary>
    /// Copies data from a device buffer to host memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="data">The destination data span.</param>
    /// <param name="buffer">The source buffer.</param>
    /// <exception cref="System.ArgumentException">
    /// Buffer must be a TestMemoryBuffer - buffer
    /// or
    /// Data size exceeds buffer capacity - data
    /// </exception>
    public void CopyFromDevice<T>(Span<T> data, IMemoryBuffer buffer) where T : unmanaged
    {
        if (buffer is not TestMemoryBuffer testBuffer)
        {
            throw new ArgumentException("Buffer must be a TestMemoryBuffer", nameof(buffer));
        }

        var elementSize = Marshal.SizeOf<T>();
        var sizeInBytes = data.Length * elementSize;

        if (sizeInBytes > buffer.SizeInBytes)
        {
            throw new ArgumentException("Data size exceeds buffer capacity", nameof(data));
        }

        unsafe
        {
            fixed (T* dataPtr = data)
            {
                var sourcePtr = (byte*)testBuffer.Handle.ToPointer();
                Buffer.MemoryCopy(sourcePtr, dataPtr, sizeInBytes, sizeInBytes);
            }
        }
    }

    /// <summary>
    /// Frees a memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to free.</param>
    /// <exception cref="System.ArgumentException">Buffer must be a TestMemoryBuffer - buffer</exception>
    public void Free(IMemoryBuffer buffer)
    {
        if (buffer is TestMemoryBuffer testBuffer)
        {
            testBuffer.Dispose();
        }
        else
        {
            throw new ArgumentException("Buffer must be a TestMemoryBuffer", nameof(buffer));
        }
    }

    internal void ReleaseBuffer(TestMemoryBuffer buffer)
    {
        if (_allocations.TryRemove(buffer.Handle, out _))
        {
            _ = Interlocked.Add(ref _totalAllocated, -buffer.SizeInBytes);
        }
    }

    /// <summary>
    /// Gets the total allocated.
    /// </summary>
    /// <value>
    /// The total allocated.
    /// </value>
    public long TotalAllocated => _totalAllocated;

    /// <summary>
    /// Gets the peak allocated.
    /// </summary>
    /// <value>
    /// The peak allocated.
    /// </value>
    public long PeakAllocated => _peakAllocated;

    /// <summary>
    /// Gets the allocation count.
    /// </summary>
    /// <value>
    /// The allocation count.
    /// </value>
    public int AllocationCount => _allocations.Count;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        foreach (var buffer in _allocations.Values)
        {
            buffer.Dispose();
        }
        _allocations.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test implementation of IMemoryBuffer using pinned host memory.
/// </summary>
public sealed class TestMemoryBuffer : IMemoryBuffer, IDisposable
{
    private readonly TestMemoryManager _manager;
    private readonly GCHandle _handle;
    private readonly byte[] _buffer;
    private bool _disposed;

    internal TestMemoryBuffer(
        TestMemoryManager manager,
        IntPtr handle,
        long sizeInBytes,
        MemoryOptions options,
        GCHandle gcHandle,
        byte[] buffer)
    {
        _manager = manager;
        Handle = handle;
        SizeInBytes = sizeInBytes;
        Options = options;
        _handle = gcHandle;
        _buffer = buffer;
    }

    /// <summary>
    /// Gets the handle.
    /// </summary>
    /// <value>
    /// The handle.
    /// </value>
    public IntPtr Handle { get; }

    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the memory flags.
    /// </summary>
    public MemoryOptions Options { get; }

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Copies data from host memory to this buffer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="offset"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentOutOfRangeException">offset</exception>
    public async ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var elementSize = Marshal.SizeOf<T>();
        var bytesToCopy = source.Length * elementSize;

        if (offset < 0 || offset + bytesToCopy > SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        await Task.Run(() =>
        {
            using var sourceHandle = source.Pin();
            unsafe
            {
                var sourcePtr = sourceHandle.Pointer;
                var destPtr = (byte*)Handle.ToPointer() + offset;
                Buffer.MemoryCopy(sourcePtr, destPtr, SizeInBytes - offset, bytesToCopy);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Copies data from this buffer to host memory.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="destination"></param>
    /// <param name="offset"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentOutOfRangeException">offset</exception>
    public async ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var elementSize = Marshal.SizeOf<T>();
        var bytesToCopy = destination.Length * elementSize;

        if (offset < 0 || offset + bytesToCopy > SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        await Task.Run(() =>
        {
            using var destHandle = destination.Pin();
            unsafe
            {
                var sourcePtr = (byte*)Handle.ToPointer() + offset;
                var destPtr = destHandle.Pointer;
                Buffer.MemoryCopy(sourcePtr, destPtr, bytesToCopy, bytesToCopy);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        Dispose();
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _handle.Free();
            ArrayPool<byte>.Shared.Return(_buffer);
            _manager.ReleaseBuffer(this);
            GC.SuppressFinalize(this);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Test implementation of a memory view.
/// </summary>
public sealed class TestMemoryView : IMemoryBuffer
{
    private readonly TestMemoryBuffer _parent;
    private readonly long _offset;

    internal TestMemoryView(TestMemoryBuffer parent, long offset, long length)
    {
        _parent = parent;
        _offset = offset;
        SizeInBytes = length;
        Options = parent.Options;
    }

    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public long SizeInBytes { get; }

    /// <summary>
    /// Gets the memory flags.
    /// </summary>
    public MemoryOptions Options { get; }

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    public bool IsDisposed => false; // Views don't manage disposal directly

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Views don't manage disposal directly - parent buffer handles this
    }

    /// <summary>
    /// Copies data from host memory to this buffer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="offset"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    /// <summary>
    /// Copies data from this buffer to host memory.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="destination"></param>
    /// <param name="offset"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous dispose operation.
    /// </returns>
    public ValueTask DisposeAsync()
        // Views don't own the memory, so nothing to dispose
        => ValueTask.CompletedTask;
}
