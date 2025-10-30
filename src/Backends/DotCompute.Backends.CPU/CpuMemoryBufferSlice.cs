// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Represents a slice view of a CpuMemoryBuffer without creating a new buffer.
/// Provides a window into the parent buffer with proper bounds checking.
/// </summary>
internal sealed class CpuMemoryBufferSlice(
    CpuMemoryBuffer parentBuffer,
    int offset,
    int length,
    CpuMemoryManager memoryManager,
    ILogger? logger) : IUnifiedMemoryBuffer<byte>, IDisposable
{
    // CA2213: These fields are not disposed because this class doesn't own them (they're references to shared resources)
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Parent buffer and memory manager are shared references that we don't own")]
    private readonly CpuMemoryBuffer _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
    private readonly int _offset = offset;
    private readonly int _length = length;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Parent buffer and memory manager are shared references that we don't own")]
    private readonly CpuMemoryManager _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
    private readonly ILogger? _logger = logger;
    private bool _isDisposed;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>

    public long SizeInBytes => _length;
    /// <summary>
    /// Gets or sets the count.
    /// </summary>
    /// <value>The count.</value>
    public int Count => _length;
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length => _length;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _parentBuffer.State;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _isDisposed || _parentBuffer.IsDisposed;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => _parentBuffer.Accelerator;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => _parentBuffer.State is BufferState.HostOnly or BufferState.Synchronized;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => _parentBuffer.State is BufferState.DeviceOnly or BufferState.Synchronized;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty => _parentBuffer.State is BufferState.HostDirty or BufferState.DeviceDirty;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _parentBuffer.Options;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Span<byte> AsSpan()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsSpan().Slice(_offset, _length);
    }
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ReadOnlySpan<byte> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsReadOnlySpan().Slice(_offset, _length);
    }
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Memory<byte> AsMemory()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsMemory().Slice(_offset, _length);
    }
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ReadOnlyMemory<byte> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsReadOnlyMemory().Slice(_offset, _length);
    }
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>

    // Device memory and mapping methods

    public DeviceMemory GetDeviceMemory()
    {
        EnsureNotDisposed();
        // For CPU backend with slicing, we need to adjust the pointer
        var parentDeviceMemory = _parentBuffer.GetDeviceMemory();
        return new DeviceMemory(parentDeviceMemory.Handle + _offset, _length);
    }
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<byte> Map(MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        var memory = AsMemory();
        return new MappedMemory<byte>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<byte> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        if (offset < 0 || length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid range for mapping");
        }

        var memory = AsMemory().Slice(offset, length);
        return new MappedMemory<byte>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "MappedMemory is returned to caller who is responsible for disposal")]
    public ValueTask<MappedMemory<byte>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));
    /// <summary>
    /// Performs ensure on host.
    /// </summary>

    public void EnsureOnHost() => _parentBuffer.EnsureOnHost();
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() => _parentBuffer.EnsureOnDevice();
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnHostAsync(context, cancellationToken);
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnDeviceAsync(context, cancellationToken);
    /// <summary>
    /// Performs synchronize.
    /// </summary>

    public void Synchronize() => _parentBuffer.Synchronize();
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.SynchronizeAsync(context, cancellationToken);
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>

    public void MarkHostDirty() => _parentBuffer.MarkHostDirty();
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() => _parentBuffer.MarkDeviceDirty();
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (source.Length > _length)
        {

            throw new ArgumentException("Source is larger than slice bounds", nameof(source));
        }

        // The parent buffer's CopyFromAsync doesn't take offset parameter directly

        var destSpan = AsSpan();
        source.Span.CopyTo(destSpan);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _length)
        {

            throw new ArgumentException("Destination is smaller than slice size", nameof(destination));
        }

        // Copy our slice data to the destination

        var sourceSpan = AsReadOnlySpan();
        sourceSpan.CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<byte> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _length)
        {

            throw new ArgumentException("Destination buffer has insufficient capacity", nameof(destination));
        }


        var sourceSpan = AsReadOnlySpan();
        var destSpan = destination.AsSpan();
        sourceSpan.CopyTo(destSpan);
        destination.MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<byte> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (sourceOffset < 0 || destinationOffset < 0 || count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Negative offsets or count not allowed");
        }

        if (sourceOffset + count > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Copy extends beyond source slice boundaries");
        }


        if (destinationOffset + count > destination.Length)
        {

            throw new ArgumentOutOfRangeException(nameof(count), "Copy extends beyond destination buffer boundaries");
        }


        var sourceSpan = AsReadOnlySpan().Slice(sourceOffset, count);
        var destSpan = destination.AsSpan().Slice(destinationOffset, count);
        sourceSpan.CopyTo(destSpan);
        destination.MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(byte value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(byte value, int offset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (offset + count > _length)
        {

            throw new ArgumentOutOfRangeException(nameof(count), "Fill operation extends beyond slice boundaries");
        }


        AsSpan().Slice(offset, count).Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<byte> Slice(int offset, int length)
    {
        EnsureNotDisposed();


        if (offset < 0)
        {

            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }


        if (offset + length > _length)
        {

            throw new ArgumentOutOfRangeException(nameof(length), "Slice extends beyond current slice boundaries");
        }

        // Create a slice of the slice by adjusting offset relative to parent

        return new CpuMemoryBufferSlice(_parentBuffer, _offset + offset, length, _memoryManager, _logger);
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        EnsureNotDisposed();


        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        if (_length % elementSize != 0)
        {

            throw new InvalidOperationException($"Slice size {_length} is not compatible with element size {elementSize}");
        }


        var elementCount = _length / elementSize;
        return new CpuMemoryBufferTypedSlice<TNew>(_parentBuffer, _offset, elementCount, _memoryManager, _logger);
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);


        if (_parentBuffer.IsDisposed)
        {

            throw new ObjectDisposedException(nameof(CpuMemoryBuffer), "Parent buffer has been disposed");
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            // Note: We don't dispose the parent buffer as we don't own it
        }
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

    #region IUnifiedMemoryBuffer Base Implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken)
    {
        var byteSource = System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.Span);
        var sourceMem = new ReadOnlyMemory<byte>(byteSource.ToArray());
        return ((IUnifiedMemoryBuffer)_parentBuffer).CopyFromAsync(sourceMem, _offset + offset, cancellationToken);
    }

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken)
    {
        var byteDestination = System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.Span);
        var destMem = new Memory<byte>(byteDestination.ToArray());
        return ((IUnifiedMemoryBuffer)_parentBuffer).CopyToAsync(destMem, _offset + offset, cancellationToken);
    }

    #endregion
}
