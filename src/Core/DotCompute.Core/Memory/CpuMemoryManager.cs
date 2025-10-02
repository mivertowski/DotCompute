// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory;

/// <summary>
/// CPU memory manager implementation that extends BaseMemoryManager.
/// </summary>
public class CpuMemoryManager(IAccelerator accelerator, ILogger<CpuMemoryManager> logger) : BaseMemoryManager(logger)
{
    private readonly IAccelerator _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _currentAllocatedBytes;
    private long _peakAllocatedBytes;


    /// <inheritdoc/>
    public override IAccelerator Accelerator => _accelerator;


    /// <inheritdoc/>
    public override MemoryStatistics Statistics => new()

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
        CancellationToken cancellationToken = default) => AllocateAsync(sizeInBytes, options, cancellationToken);


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
        CancellationToken cancellationToken = default) => await destination.CopyFromAsync(source, cancellationToken);


    /// <inheritdoc/>
    public override async ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default) => await source.CopyToAsync(destination, cancellationToken);


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
        // CPU memory doesn't support views in this implementation
        // Would need to implement a view wrapper for CPU buffers


        => throw new NotSupportedException("CPU memory views are not yet implemented");
}

/// <summary>
/// CPU memory buffer implementation.
/// </summary>
public class CpuMemoryBuffer : IUnifiedMemoryBuffer
{
    private readonly byte[] _data;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CpuMemoryBuffer class.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">The options.</param>


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
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>


    public long SizeInBytes => _data.Length;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options { get; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _disposed;
    /// <summary>
    /// Gets copy from host asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        var sourceBytes = MemoryMarshal.AsBytes(source.Span);
        sourceBytes.CopyTo(_data.AsSpan((int)offset));
        State = BufferState.HostReady;
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to host asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0,

        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        var destBytes = MemoryMarshal.AsBytes(destination.Span);
        _data.AsSpan((int)offset, destBytes.Length).CopyTo(destBytes);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }


    /// <summary>
    /// Copies data from source memory to this CPU buffer with offset support.
    /// </summary>
    /// <typeparam name="T">Type of elements to copy.</typeparam>
    /// <param name="source">Source data to copy from.</param>
    /// <param name="destinationOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long destinationOffset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        var sourceBytes = MemoryMarshal.AsBytes(source.Span);
        sourceBytes.CopyTo(_data.AsSpan((int)destinationOffset));
        State = BufferState.HostReady;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Copies data from this CPU buffer to destination memory with offset support.
    /// </summary>
    /// <typeparam name="T">Type of elements to copy.</typeparam>
    /// <param name="destination">Destination memory to copy to.</param>
    /// <param name="sourceOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyToAsync<T>(Memory<T> destination, long sourceOffset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        var destBytes = MemoryMarshal.AsBytes(destination.Span);
        _data.AsSpan((int)sourceOffset, destBytes.Length).CopyTo(destBytes);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }


    internal byte[] GetData() => _data;


    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// CPU memory buffer implementation with type safety.
/// </summary>
public class CpuMemoryBuffer<T>(IUnifiedMemoryBuffer underlyingBuffer, int length) : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer _underlyingBuffer = underlyingBuffer ?? throw new ArgumentNullException(nameof(underlyingBuffer));
    private readonly int _length = length;
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>

    public int Length => _length;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes => _underlyingBuffer.SizeInBytes;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => null!; // CPU doesn't have a specific accelerator
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _underlyingBuffer.Options;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _underlyingBuffer.State;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _underlyingBuffer.IsDisposed;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => true;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => false;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public Span<T> AsSpan()
    {
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            return MemoryMarshal.Cast<byte, T>(cpuBuffer.GetData().AsSpan());
        }
        throw new NotSupportedException();
    }
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public Memory<T> AsMemory()
        // For CPU buffers, we need to create a properly typed memory
        // This is a temporary implementation - proper buffer management needed


        => throw new NotSupportedException("Direct Memory<T> access not yet implemented for CpuMemoryBuffer<T>");
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public ReadOnlyMemory<T> AsReadOnlyMemory() => AsMemory();
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>


    public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>


    public bool IsDirty => State == BufferState.HostDirty || State == BufferState.DeviceDirty;
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public MappedMemory<T> Map(MapMode mode) => throw new NotSupportedException("CPU buffers do not require mapping");
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public MappedMemory<T> MapRange(int offset, int length, MapMode mode) => throw new NotSupportedException("CPU buffers do not require mapping");
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException("CPU buffers do not require mapping");
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => destination.CopyFromAsync(AsMemory(), cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
    {
        var slice = AsMemory().Slice(sourceOffset, length);
        return destination.CopyFromAsync(slice, cancellationToken);
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
    {
        AsSpan().Slice(offset, length).Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<T> Slice(int offset, int length) => new CpuMemoryBufferView<T>(this, offset, length);
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => throw new NotSupportedException("Type conversion not supported for CPU buffers");
    /// <summary>
    /// Performs ensure on host.
    /// </summary>


    public void EnsureOnHost() { /* Already on host */ }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() { /* CPU is both host and device */ }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>


    public void Synchronize() { /* No synchronization needed for CPU */ }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>


    public void MarkHostDirty()
    {
        // State tracking would be handled internally by the buffer implementation
    }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>


    public void MarkDeviceDirty()
    {
        // State tracking would be handled internally by the buffer implementation
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


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
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


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
    /// <summary>
    /// Gets copy from host asynchronously.
    /// </summary>
    /// <typeparam name="TSource">The TSource type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


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
    /// <summary>
    /// Gets copy to host asynchronously.
    /// </summary>
    /// <typeparam name="TDest">The TDest type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


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


    /// <summary>
    /// Copies data from source memory to this typed CPU buffer with offset support.
    /// </summary>
    /// <typeparam name="TSource">Type of elements to copy.</typeparam>
    /// <param name="source">Source data to copy from.</param>
    /// <param name="destinationOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long destinationOffset, CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            var span = cpuBuffer.GetData().AsSpan()[(int)destinationOffset..];
            MemoryMarshal.AsBytes(source.Span).CopyTo(span);
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Copies data from this typed CPU buffer to destination memory with offset support.
    /// </summary>
    /// <typeparam name="TDest">Type of elements to copy.</typeparam>
    /// <param name="destination">Destination memory to copy to.</param>
    /// <param name="sourceOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyToAsync<TDest>(Memory<TDest> destination, long sourceOffset, CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        if (_underlyingBuffer is CpuMemoryBuffer cpuBuffer)
        {
            var span = cpuBuffer.GetData().AsSpan()[(int)sourceOffset..];
            var destBytes = MemoryMarshal.AsBytes(destination.Span);
            span.Slice(0, Math.Min(span.Length, destBytes.Length)).CopyTo(destBytes);
        }
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() => _underlyingBuffer.Dispose();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
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
    /// <summary>
    /// Initializes a new instance of the CpuMemoryBufferView class.
    /// </summary>
    /// <param name="parent">The parent.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>


    public CpuMemoryBufferView(CpuMemoryBuffer<T> parent, int offset, int length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _length = length;


        if (offset < 0 || offset >= parent.Length)
        {

            throw new ArgumentOutOfRangeException(nameof(offset));
        }


        if (length < 0 || offset + length > parent.Length)
        {

            throw new ArgumentOutOfRangeException(nameof(length));
        }
    }
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>


    public int Length => _length;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => _parent.Accelerator;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _parent.Options;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _parent.State;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _parent.IsDisposed;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => true;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => false;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public Span<T> AsSpan() => _parent.AsSpan().Slice(_offset, _length);
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan() => _parent.AsReadOnlySpan().Slice(_offset, _length);
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Memory<T> AsMemory() => _parent.AsMemory().Slice(_offset, _length);
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory() => _parent.AsReadOnlyMemory().Slice(_offset, _length);
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>


    public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>


    public bool IsDirty => _parent.IsDirty;
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public MappedMemory<T> Map(MapMode mode) => throw new NotSupportedException("CPU buffers do not require mapping");
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public MappedMemory<T> MapRange(int offset, int length, MapMode mode) => throw new NotSupportedException("CPU buffers do not require mapping");
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException("CPU buffers do not require mapping");
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default) => destination.CopyFromAsync(AsMemory(), cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
    {
        var slice = AsMemory().Slice(sourceOffset, length);
        return destination.CopyFromAsync(slice, cancellationToken);
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
    {
        AsSpan().Slice(offset, length).Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<T> Slice(int offset, int length) => new CpuMemoryBufferView<T>(_parent, _offset + offset, length);
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => throw new NotSupportedException("Type conversion not supported for CPU buffer views");
    /// <summary>
    /// Performs ensure on host.
    /// </summary>


    public void EnsureOnHost() { }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() { }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>


    public void Synchronize() { }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>


    public void MarkHostDirty() => _parent.MarkHostDirty();
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(AsMemory());
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        AsMemory().CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from host asynchronously.
    /// </summary>
    /// <typeparam name="TSource">The TSource type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0,

        CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        if (typeof(TSource) != typeof(T))
        {

            throw new ArgumentException($"Type mismatch: {typeof(TSource)} != {typeof(T)}");
        }


        var typedSource = MemoryMarshal.Cast<TSource, T>(source.Span);
        typedSource.CopyTo(AsSpan().Slice((int)offset));
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to host asynchronously.
    /// </summary>
    /// <typeparam name="TDest">The TDest type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToHostAsync<TDest>(Memory<TDest> destination, long offset = 0,

        CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        if (typeof(TDest) != typeof(T))
        {

            throw new ArgumentException($"Type mismatch: {typeof(TDest)} != {typeof(T)}");
        }


        var typedDest = MemoryMarshal.Cast<TDest, T>(destination.Span);
        AsSpan().Slice((int)offset, typedDest.Length).CopyTo(typedDest);
        return ValueTask.CompletedTask;
    }


    /// <summary>
    /// Copies data from source memory to this CPU buffer view with offset support.
    /// </summary>
    /// <typeparam name="TSource">Type of elements to copy.</typeparam>
    /// <param name="source">Source data to copy from.</param>
    /// <param name="destinationOffset">Offset into this view in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long destinationOffset, CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        if (typeof(TSource) != typeof(T))
        {

            throw new ArgumentException($"Type mismatch: {typeof(TSource)} != {typeof(T)}");
        }


        var typedSource = MemoryMarshal.Cast<TSource, T>(source.Span);
        var targetOffset = _offset + (int)(destinationOffset / Unsafe.SizeOf<T>());
        typedSource.CopyTo(_parent.AsSpan().Slice(targetOffset));
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Copies data from this CPU buffer view to destination memory with offset support.
    /// </summary>
    /// <typeparam name="TDest">Type of elements to copy.</typeparam>
    /// <param name="destination">Destination memory to copy to.</param>
    /// <param name="sourceOffset">Offset into this view in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyToAsync<TDest>(Memory<TDest> destination, long sourceOffset, CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        if (typeof(TDest) != typeof(T))
        {

            throw new ArgumentException($"Type mismatch: {typeof(TDest)} != {typeof(T)}");
        }


        var typedDest = MemoryMarshal.Cast<TDest, T>(destination.Span);
        var sourceOffsetElements = _offset + (int)(sourceOffset / Unsafe.SizeOf<T>());
        _parent.AsSpan().Slice(sourceOffsetElements, typedDest.Length).CopyTo(typedDest);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() { /* Views don't own memory */ }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

