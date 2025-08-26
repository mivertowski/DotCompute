// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory;

/// <summary>
/// CPU memory manager implementation that extends BaseMemoryManager.
/// </summary>
public class CpuMemoryManager : BaseMemoryManager
{
    private readonly IAccelerator _accelerator;
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _currentAllocatedBytes;
    private long _peakAllocatedBytes;
    
    public CpuMemoryManager(IAccelerator accelerator, ILogger<CpuMemoryManager> logger) 
        : base(logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    }
    
    /// <inheritdoc/>
    public override IAccelerator Accelerator => _accelerator;
    
    /// <inheritdoc/>
    public override DotCompute.Abstractions.Memory.MemoryStatistics Statistics => new DotCompute.Abstractions.Memory.MemoryStatistics
    {
        TotalAllocated = _currentAllocatedBytes,
        CurrentUsed = _currentAllocatedBytes,
        PeakUsage = _peakAllocatedBytes,
        ActiveAllocations = (int)_totalAllocations - (int)_totalDeallocations
    };
    
    /// <inheritdoc/>
    public override long MaxAllocationSize => long.MaxValue / 2; // Reasonable limit for CPU
    
    /// <inheritdoc/>
    public override long TotalAvailableMemory => GC.GetTotalMemory(false);
    
    /// <inheritdoc/>
    public override long CurrentAllocatedMemory => TotalAllocatedBytes;
    
    /// <inheritdoc/>
    protected override ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(
        long sizeInBytes, 
        MemoryOptions options, 
        CancellationToken cancellationToken)
    {
        var buffer = new CpuMemoryBuffer(sizeInBytes, options);
        _totalAllocations++;
        _currentAllocatedBytes += sizeInBytes;
        _peakAllocatedBytes = Math.Max(_peakAllocatedBytes, _currentAllocatedBytes);
        
        return ValueTask.FromResult<IUnifiedMemoryBuffer>(buffer);
    }
    
    /// <inheritdoc/>
    public override async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var sizeInBytes = count * Unsafe.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken);
        return new CpuMemoryBuffer<T>(buffer, count);
    }
    
    /// <inheritdoc/>
    public override async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken);
        await buffer.CopyFromAsync(source, cancellationToken);
        return buffer;
    }
    
    /// <inheritdoc/>
    public override ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        return AllocateAsync(sizeInBytes, options, cancellationToken);
    }
    
    /// <inheritdoc/>
    public override IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        
        if (buffer is CpuMemoryBuffer<T> cpuBuffer)
        {
            return new CpuMemoryBufferView<T>(cpuBuffer, offset, length);
        }
        
        throw new ArgumentException("Buffer must be a CPU buffer", nameof(buffer));
    }
    
    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        
        var temp = new T[source.Length];
        await source.CopyToAsync(temp, cancellationToken);
        await destination.CopyFromAsync(temp, cancellationToken);
    }
    
    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int length,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        
        var temp = new T[length];
        var sourceView = CreateView(source, sourceOffset, length);
        var destView = CreateView(destination, destinationOffset, length);
        
        await sourceView.CopyToAsync(temp, cancellationToken);
        await destView.CopyFromAsync(temp, cancellationToken);
    }
    
    /// <inheritdoc/>
    public override async ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        await destination.CopyFromAsync(source, cancellationToken);
    }
    
    /// <inheritdoc/>
    public override async ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default)
    {
        await source.CopyToAsync(destination, cancellationToken);
    }
    
    /// <inheritdoc/>
    public override ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (buffer != null)
        {
            _currentAllocatedBytes -= buffer.SizeInBytes;
            _totalDeallocations++;
            buffer.Dispose();
        }
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc/>
    public override ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        // Force garbage collection for CPU memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return ValueTask.CompletedTask;
    }
    
    /// <inheritdoc/>
    public override void Clear()
    {
        // Clear is handled by Dispose in base class
        _totalAllocations = 0;
        _totalDeallocations = 0;
        _currentAllocatedBytes = 0;
        _peakAllocatedBytes = 0;
    }
    
    /// <inheritdoc/>
    protected override IUnifiedMemoryBuffer CreateViewCore(
        IUnifiedMemoryBuffer sourceBuffer,
        long offsetInBytes,
        long sizeInBytes)
    {
        // CPU memory doesn't support views in this implementation
        // Would need to implement a view wrapper for CPU buffers
        throw new NotSupportedException("CPU memory views are not yet implemented");
    }
}

/// <summary>
/// CPU memory buffer implementation.
/// </summary>
public class CpuMemoryBuffer : IUnifiedMemoryBuffer
{
    private readonly byte[] _data;
    private bool _disposed;
    
    public CpuMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        if (sizeInBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), 
                "CPU buffer size cannot exceed int.MaxValue");
        }
        
        _data = GC.AllocateUninitializedArray<byte>((int)sizeInBytes, 
            options.HasFlag(MemoryOptions.Pinned));
        Options = options;
        State = BufferState.HostReady;
    }
    
    public long SizeInBytes => _data.Length;
    public MemoryOptions Options { get; }
    public BufferState State { get; set; }
    public bool IsDisposed => _disposed;
    
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, 
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        var sourceBytes = MemoryMarshal.AsBytes(source.Span);
        sourceBytes.CopyTo(_data.AsSpan((int)offset));
        State = BufferState.HostReady;
        return ValueTask.CompletedTask;
    }
    
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, 
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        var destBytes = MemoryMarshal.AsBytes(destination.Span);
        _data.AsSpan((int)offset, destBytes.Length).CopyTo(destBytes);
        return ValueTask.CompletedTask;
    }
    
    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }
    
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
    
    internal byte[] GetData() => _data;
    
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// CPU memory buffer implementation with type safety.
/// </summary>
public class CpuMemoryBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer _underlyingBuffer;
    private readonly int _length;
    
    public CpuMemoryBuffer(IUnifiedMemoryBuffer underlyingBuffer, int length)
    {
        _underlyingBuffer = underlyingBuffer ?? throw new ArgumentNullException(nameof(underlyingBuffer));
        _length = length;
    }
    
    public int Length => _length;
    public long SizeInBytes => _underlyingBuffer.SizeInBytes;
    public IAccelerator Accelerator => null!; // CPU doesn't have a specific accelerator
    public MemoryOptions Options => _underlyingBuffer.Options;
    public BufferState State => _underlyingBuffer.State;
    public bool IsDisposed => _underlyingBuffer.IsDisposed;
    public bool IsOnHost => true;
    public bool IsOnDevice => false;
    
    public Span<T> AsSpan()
    {
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            return MemoryMarshal.Cast<byte, T>(cpuBuffer.GetData().AsSpan());
        }
        throw new NotSupportedException();
    }
    
    public ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();
    
    public Memory<T> AsMemory()
    {
        // For CPU buffers, we need to create a properly typed memory
        // This is a temporary implementation - proper buffer management needed
        throw new NotSupportedException("Direct Memory<T> access not yet implemented for CpuMemoryBuffer<T>");
    }
    
    public ReadOnlyMemory<T> AsReadOnlyMemory() => AsMemory();
    
    public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;
    
    public bool IsDirty => State == BufferState.HostDirty || State == BufferState.DeviceDirty;
    
    public MappedMemory<T> Map(MapMode mode)
    {
        throw new NotSupportedException("CPU buffers do not require mapping");
    }
    
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode)
    {
        throw new NotSupportedException("CPU buffers do not require mapping");
    }
    
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("CPU buffers do not require mapping");
    }
    
    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        return destination.CopyFromAsync(AsMemory(), cancellationToken);
    }
    
    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
    {
        var slice = AsMemory().Slice(sourceOffset, length);
        return destination.CopyFromAsync(slice, cancellationToken);
    }
    
    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }
    
    public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
    {
        AsSpan().Slice(offset, length).Fill(value);
        return ValueTask.CompletedTask;
    }
    
    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        return new CpuMemoryBufferView<T>(this, offset, length);
    }
    
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        throw new NotSupportedException("Type conversion not supported for CPU buffers");
    }
    
    public void EnsureOnHost() { /* Already on host */ }
    public void EnsureOnDevice() { /* CPU is both host and device */ }
    
    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public void Synchronize() { /* No synchronization needed for CPU */ }
    
    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public void MarkHostDirty() 
    { 
        // State tracking would be handled internally by the buffer implementation
    }
    
    public void MarkDeviceDirty() 
    { 
        // State tracking would be handled internally by the buffer implementation
    }
    
    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        // Copy data directly to the underlying buffer's memory
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            var span = MemoryMarshal.Cast<byte, T>(cpuBuffer.GetData().AsSpan());
            source.Span.CopyTo(span);
        }
        await ValueTask.CompletedTask;
    }
    
    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        // Copy data from the underlying buffer's memory
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            var span = MemoryMarshal.Cast<byte, T>(cpuBuffer.GetData().AsSpan());
            span.Slice(0, Math.Min(span.Length, destination.Length)).CopyTo(destination.Span);
        }
        await ValueTask.CompletedTask;
    }
    
    public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, 
        CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        // For CPU buffers, this is a direct memory copy
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            var span = cpuBuffer.GetData().AsSpan()[(int)offset..];
            MemoryMarshal.AsBytes(source.Span).CopyTo(span);
        }
        return ValueTask.CompletedTask;
    }
    
    public ValueTask CopyToHostAsync<TDest>(Memory<TDest> destination, long offset = 0, 
        CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        // For CPU buffers, this is a direct memory copy
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            var span = cpuBuffer.GetData().AsSpan()[(int)offset..];
            var destBytes = MemoryMarshal.AsBytes(destination.Span);
            span.Slice(0, Math.Min(span.Length, destBytes.Length)).CopyTo(destBytes);
        }
        return ValueTask.CompletedTask;
    }
    
    public void Dispose() => _underlyingBuffer.Dispose();
    public ValueTask DisposeAsync() => _underlyingBuffer.DisposeAsync();
}

/// <summary>
/// CPU memory buffer view implementation.
/// </summary>
public class CpuMemoryBufferView<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly CpuMemoryBuffer<T> _parent;
    private readonly int _offset;
    private readonly int _length;
    
    public CpuMemoryBufferView(CpuMemoryBuffer<T> parent, int offset, int length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _length = length;
        
        if (offset < 0 || offset >= parent.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0 || offset + length > parent.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
    }
    
    public int Length => _length;
    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    public IAccelerator Accelerator => _parent.Accelerator;
    public MemoryOptions Options => _parent.Options;
    public BufferState State => _parent.State;
    public bool IsDisposed => _parent.IsDisposed;
    public bool IsOnHost => true;
    public bool IsOnDevice => false;
    
    public Span<T> AsSpan() => _parent.AsSpan().Slice(_offset, _length);
    public ReadOnlySpan<T> AsReadOnlySpan() => _parent.AsReadOnlySpan().Slice(_offset, _length);
    public Memory<T> AsMemory() => _parent.AsMemory().Slice(_offset, _length);
    public ReadOnlyMemory<T> AsReadOnlyMemory() => _parent.AsReadOnlyMemory().Slice(_offset, _length);
    
    public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;
    
    public bool IsDirty => _parent.IsDirty;
    
    public MappedMemory<T> Map(MapMode mode)
    {
        throw new NotSupportedException("CPU buffers do not require mapping");
    }
    
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode)
    {
        throw new NotSupportedException("CPU buffers do not require mapping");
    }
    
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("CPU buffers do not require mapping");
    }
    
    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        return destination.CopyFromAsync(AsMemory(), cancellationToken);
    }
    
    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
    {
        var slice = AsMemory().Slice(sourceOffset, length);
        return destination.CopyFromAsync(slice, cancellationToken);
    }
    
    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }
    
    public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
    {
        AsSpan().Slice(offset, length).Fill(value);
        return ValueTask.CompletedTask;
    }
    
    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        return new CpuMemoryBufferView<T>(_parent, _offset + offset, length);
    }
    
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        throw new NotSupportedException("Type conversion not supported for CPU buffer views");
    }
    
    public void EnsureOnHost() { }
    public void EnsureOnDevice() { }
    
    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public void Synchronize() { }
    
    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public void MarkHostDirty() => _parent.MarkHostDirty();
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();
    
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(AsMemory());
        return ValueTask.CompletedTask;
    }
    
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        AsMemory().CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    
    public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, 
        CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        if (typeof(TSource) != typeof(T))
            throw new ArgumentException($"Type mismatch: {typeof(TSource)} != {typeof(T)}");
            
        var typedSource = MemoryMarshal.Cast<TSource, T>(source.Span);
        typedSource.CopyTo(AsSpan().Slice((int)offset));
        return ValueTask.CompletedTask;
    }
    
    public ValueTask CopyToHostAsync<TDest>(Memory<TDest> destination, long offset = 0, 
        CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        if (typeof(TDest) != typeof(T))
            throw new ArgumentException($"Type mismatch: {typeof(TDest)} != {typeof(T)}");
            
        var typedDest = MemoryMarshal.Cast<TDest, T>(destination.Span);
        AsSpan().Slice((int)offset, typedDest.Length).CopyTo(typedDest);
        return ValueTask.CompletedTask;
    }
    
    public void Dispose() { /* Views don't own memory */ }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

