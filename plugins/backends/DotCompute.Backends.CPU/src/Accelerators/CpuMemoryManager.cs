using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Memory manager for CPU-based compute.
/// </summary>
internal sealed class CpuMemoryManager : IMemoryManager, IDisposable
{
    private readonly object _lock = new();
    private readonly List<WeakReference<CpuMemoryBuffer>> _buffers = new();
    private long _totalAllocated;
    private int _disposed;

    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocated);

    public ValueTask<IMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        var buffer = new CpuMemoryBuffer(sizeInBytes, options, this);
        
        lock (_lock)
        {
            _buffers.Add(new WeakReference<CpuMemoryBuffer>(buffer));
        }

        Interlocked.Add(ref _totalAllocated, sizeInBytes);
        
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    public async ValueTask<IMemoryBuffer> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sizeInBytes = source.Length * Marshal.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);
        
        await buffer.CopyFromHostAsync(source, 0, cancellationToken).ConfigureAwait(false);
        
        return buffer;
    }

    public IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (buffer is not CpuMemoryBuffer cpuBuffer)
            throw new ArgumentException("Buffer must be a CPU memory buffer", nameof(buffer));

        return cpuBuffer.CreateView(offset, length);
    }

    internal void OnBufferDisposed(long sizeInBytes)
    {
        Interlocked.Add(ref _totalAllocated, -sizeInBytes);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        lock (_lock)
        {
            // Force dispose all remaining buffers
            foreach (var weakRef in _buffers)
            {
                if (weakRef.TryGetTarget(out var buffer))
                {
                    buffer.Dispose();
                }
            }
            _buffers.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CpuMemoryManager));
    }
}

/// <summary>
/// CPU memory buffer implementation.
/// </summary>
internal sealed class CpuMemoryBuffer : IMemoryBuffer
{
    private readonly CpuMemoryManager _manager;
    private readonly IMemoryOwner<byte> _memoryOwner;
    private readonly long _sizeInBytes;
    private readonly MemoryOptions _options;
    private readonly long _viewOffset;
    private readonly long _viewLength;
    private int _disposed;

    public CpuMemoryBuffer(long sizeInBytes, MemoryOptions options, CpuMemoryManager manager)
    {
        _sizeInBytes = sizeInBytes;
        _options = options;
        _manager = manager;
        _viewOffset = 0;
        _viewLength = sizeInBytes;

        // Use ArrayPool for smaller allocations, native memory for larger ones
        const int maxArrayLength = 1024 * 1024 * 1024; // 1GB limit
        if (sizeInBytes <= maxArrayLength)
        {
            _memoryOwner = MemoryPool<byte>.Shared.Rent((int)sizeInBytes);
        }
        else
        {
            // For very large allocations, we'd use native memory
            // For now, throw an exception
            throw new NotSupportedException($"Allocations larger than {maxArrayLength} bytes are not yet supported");
        }
    }

    private CpuMemoryBuffer(CpuMemoryBuffer parent, long offset, long length)
    {
        _memoryOwner = parent._memoryOwner;
        _sizeInBytes = parent._sizeInBytes;
        _options = parent._options;
        _manager = parent._manager;
        _viewOffset = parent._viewOffset + offset;
        _viewLength = length;
    }

    public long SizeInBytes => _viewLength;

    public MemoryOptions Options => _options;

    public Memory<byte> GetMemory()
    {
        ThrowIfDisposed();
        
        var memory = _memoryOwner.Memory;
        if (_viewOffset > 0 || _viewLength < _sizeInBytes)
        {
            memory = memory.Slice((int)_viewOffset, (int)_viewLength);
        }
        
        return memory;
    }

    public ValueTask CopyFromHostAsync<T>(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        if ((_options & MemoryOptions.WriteOnly) != 0 && (_options & MemoryOptions.ReadOnly) != 0)
            throw new InvalidOperationException("Cannot write to read-only buffer");

        var elementSize = Marshal.SizeOf<T>();
        var bytesToCopy = source.Length * elementSize;
        
        if (offset < 0 || offset + bytesToCopy > _viewLength)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var destMemory = GetMemory().Slice((int)offset, bytesToCopy);
        var sourceSpan = MemoryMarshal.AsBytes(source.Span);
        sourceSpan.CopyTo(destMemory.Span);

        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToHostAsync<T>(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        
        if ((_options & MemoryOptions.ReadOnly) != 0 && (_options & MemoryOptions.WriteOnly) != 0)
            throw new InvalidOperationException("Cannot read from write-only buffer");

        var elementSize = Marshal.SizeOf<T>();
        var bytesToCopy = destination.Length * elementSize;
        
        if (offset < 0 || offset + bytesToCopy > _viewLength)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var sourceMemory = GetMemory().Slice((int)offset, bytesToCopy);
        var destSpan = MemoryMarshal.AsBytes(destination.Span);
        sourceMemory.Span.CopyTo(destSpan);

        return ValueTask.CompletedTask;
    }

    public CpuMemoryBuffer CreateView(long offset, long length)
    {
        ThrowIfDisposed();
        
        if (offset < 0 || length < 0 || offset + length > _viewLength)
            throw new ArgumentOutOfRangeException();

        return new CpuMemoryBuffer(this, offset, length);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Only dispose if we're the original buffer, not a view
        if (_viewOffset == 0 && _viewLength == _sizeInBytes)
        {
            _memoryOwner?.Dispose();
            _manager.OnBufferDisposed(_sizeInBytes);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed != 0)
            throw new ObjectDisposedException(nameof(CpuMemoryBuffer));
    }
}