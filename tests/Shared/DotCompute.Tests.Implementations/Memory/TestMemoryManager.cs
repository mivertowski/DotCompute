using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Shared.Memory;

/// <summary>
/// Test implementation of IMemoryManager for testing without GPU hardware.
/// Uses pinned host memory to simulate device memory operations.
/// </summary>
public class TestMemoryManager : IMemoryManager, IDisposable
{
    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;
    private readonly ConcurrentDictionary<IntPtr, TestMemoryBuffer> _allocations = new();
    private long _totalAllocated;
    private long _peakAllocated;

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
            Interlocked.CompareExchange(ref _peakAllocated, newTotal, currentPeak);
            currentPeak = _peakAllocated;
        }

        return memBuffer;
    }

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

    public ValueTask<IMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        var elementSize = Marshal.SizeOf<T>();
        var sizeInBytes = count * elementSize;
        return AllocateAsync(sizeInBytes);
    }

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
            Interlocked.Add(ref _totalAllocated, -buffer.SizeInBytes);
        }
    }

    public long TotalAllocated => _totalAllocated;
    public long PeakAllocated => _peakAllocated;
    public int AllocationCount => _allocations.Count;

    public void Dispose()
    {
        foreach (var buffer in _allocations.Values)
        {
            buffer.Dispose();
        }
        _allocations.Clear();
    }
}

/// <summary>
/// Test implementation of IMemoryBuffer using pinned host memory.
/// </summary>
public class TestMemoryBuffer : IMemoryBuffer, IDisposable
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

    public IntPtr Handle { get; }
    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;

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

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        Dispose();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _handle.Free();
            ArrayPool<byte>.Shared.Return(_buffer);
            _manager.ReleaseBuffer(this);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TestMemoryBuffer));
        }
    }
}

/// <summary>
/// Test implementation of a memory view.
/// </summary>
public class TestMemoryView : IMemoryBuffer
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

    public long SizeInBytes { get; }
    public MemoryOptions Options { get; }
    public bool IsDisposed => false; // Views don't manage disposal directly

    public void Dispose()
    {
        // Views don't manage disposal directly - parent buffer handles this
    }

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged => _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);

    public ValueTask DisposeAsync()
        // Views don't own the memory, so nothing to dispose
        => ValueTask.CompletedTask;
}
