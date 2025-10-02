// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Represents a slice view of a UnifiedBuffer that provides access to a contiguous subset of elements.
/// This is a lightweight wrapper that doesn't own the underlying memory.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class UnifiedBufferSlice<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly UnifiedBuffer<T> _parentBuffer;
    private readonly int _offset;
    private readonly int _length;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the UnifiedBufferSlice class.
    /// </summary>
    /// <param name="parentBuffer">The parent buffer to slice.</param>
    /// <param name="offset">The offset in elements from the start of the parent buffer.</param>
    /// <param name="length">The length of the slice in elements.</param>
    public UnifiedBufferSlice(UnifiedBuffer<T> parentBuffer, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(parentBuffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, parentBuffer.Length);

        _parentBuffer = parentBuffer;
        _offset = offset;
        _length = length;
    }

    /// <inheritdoc />
    public int Length => _length;

    /// <inheritdoc />
    public long SizeInBytes => _length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

    /// <inheritdoc />
    public IAccelerator Accelerator => _parentBuffer.Accelerator;

    /// <inheritdoc />
    public MemoryOptions Options => _parentBuffer.Options;

    /// <inheritdoc />
    public bool IsDisposed => _disposed || _parentBuffer.IsDisposed;

    /// <inheritdoc />
    public bool IsOnHost => _parentBuffer.IsOnHost;

    /// <inheritdoc />
    public bool IsOnDevice => _parentBuffer.IsOnDevice;

    /// <inheritdoc />
    public bool IsDirty => _parentBuffer.IsDirty;

    /// <inheritdoc />
    public BufferState State => _parentBuffer.State;

    /// <summary>
    /// Gets the device memory handle for this slice.
    /// </summary>
    /// <returns>The device memory handle adjusted for the slice offset.</returns>
    public DeviceMemory GetDeviceMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentDeviceMemory = _parentBuffer.GetDeviceMemory();
        if (!parentDeviceMemory.IsValid)
        {

            return DeviceMemory.Invalid;
        }

        // Calculate offset in bytes

        var offsetInBytes = _offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var adjustedHandle = IntPtr.Add(parentDeviceMemory.Handle, offsetInBytes);

        return new DeviceMemory(adjustedHandle, SizeInBytes);
    }

    /// <summary>
    /// Gets a span to the slice portion of the parent buffer's host memory.
    /// </summary>
    /// <returns>A span to the slice data.</returns>
    public Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentSpan = _parentBuffer.AsSpan();
        return parentSpan.Slice(_offset, _length);
    }

    /// <summary>
    /// Gets a read-only span to the slice portion of the parent buffer's host memory.
    /// </summary>
    /// <returns>A read-only span to the slice data.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentSpan = _parentBuffer.AsReadOnlySpan();
        return parentSpan.Slice(_offset, _length);
    }

    /// <summary>
    /// Gets a memory handle to the slice portion of the parent buffer's host memory.
    /// </summary>
    /// <returns>A memory handle to the slice data.</returns>
    public Memory<T> AsMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentMemory = _parentBuffer.AsMemory();
        return parentMemory.Slice(_offset, _length);
    }

    /// <summary>
    /// Gets a read-only memory handle to the slice portion of the parent buffer's host memory.
    /// </summary>
    /// <returns>A read-only memory handle to the slice data.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentMemory = _parentBuffer.AsReadOnlyMemory();
        return parentMemory.Slice(_offset, _length);
    }

    /// <inheritdoc />
    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(source.Length, _length);

        var slice = _parentBuffer.Slice(_offset, source.Length);
        await slice.CopyFromAsync(source, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(destination.Length, _length);

        var slice = _parentBuffer.Slice(_offset, destination.Length);
        await slice.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var slice = _parentBuffer.Slice(_offset, _length);
        await slice.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, _length);

        // Adjust the source offset to the parent buffer's coordinate system
        var adjustedSourceOffset = _offset + sourceOffset;
        await _parentBuffer.CopyToAsync(adjustedSourceOffset, destination, destinationOffset, count, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        // Create a slice of the slice by adjusting the offset
        return new UnifiedBufferSlice<T>(_parentBuffer, _offset + offset, length);
    }

    /// <inheritdoc />
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        var elementSizeRatio = System.Runtime.CompilerServices.Unsafe.SizeOf<T>() / (double)System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        var newLength = (int)(_length * elementSizeRatio);

        // Create a view of the parent buffer and then slice it
        var parentView = _parentBuffer.AsType<TNew>();
        var newOffset = (int)(_offset * elementSizeRatio);

        return parentView.Slice(newOffset, newLength);
    }

    /// <inheritdoc />
    public async ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => await FillAsync(value, 0, _length, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, _length);

        // Fill the data by creating a temporary array and writing it
        var fillData = new T[count];
        fillData.AsSpan().Fill(value);

        var slice = _parentBuffer.Slice(_offset + offset, count);
        await slice.CopyFromAsync(fillData, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public MappedMemory<T> Map(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        return new MappedMemory<T>(AsMemory());
    }

    /// <inheritdoc />
    public MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        var sliceMemory = AsMemory().Slice(offset, length);
        return new MappedMemory<T>(sliceMemory);
    }

    /// <inheritdoc />
    public async ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        // Ensure parent buffer is on host before mapping
        await _parentBuffer.EnsureOnHostAsync(default, cancellationToken).ConfigureAwait(false);

        return new MappedMemory<T>(AsMemory());
    }

    /// <inheritdoc />
    public void EnsureOnHost()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        _parentBuffer.EnsureOnHost();
    }

    /// <inheritdoc />
    public void EnsureOnDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        _parentBuffer.EnsureOnDevice();
    }

    /// <inheritdoc />
    public ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        return _parentBuffer.EnsureOnHostAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        return _parentBuffer.EnsureOnDeviceAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        _parentBuffer.Synchronize();
    }

    /// <inheritdoc />
    public ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        return _parentBuffer.SynchronizeAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public void MarkHostDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        _parentBuffer.MarkHostDirty();
    }

    /// <inheritdoc />
    public void MarkDeviceDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        _parentBuffer.MarkDeviceDirty();
    }

    #region IUnifiedMemoryBuffer (non-generic) Implementation

    /// <inheritdoc />
    public ValueTask CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset = 0, CancellationToken cancellationToken = default) where U : unmanaged
    {
        if (typeof(U) != typeof(T))
        {
            throw new ArgumentException($"Type mismatch: expected {typeof(T)}, got {typeof(U)}");
        }

        var typedSource = MemoryMarshal.Cast<U, T>(source.Span);
        _ = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());

        return CopyFromAsync(typedSource.ToArray().AsMemory(), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync<U>(Memory<U> destination, long offset = 0, CancellationToken cancellationToken = default) where U : unmanaged
    {
        if (typeof(U) != typeof(T))
        {
            throw new ArgumentException($"Type mismatch: expected {typeof(T)}, got {typeof(U)}");
        }

        var elementOffset = (int)(offset / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        var slice = _parentBuffer.Slice(_offset + elementOffset, destination.Length);
        var tempArray = new T[destination.Length];
        await slice.CopyToAsync(tempArray.AsMemory(), cancellationToken).ConfigureAwait(false);
        var sliceData = tempArray;

        var typedDestination = MemoryMarshal.Cast<U, T>(destination.Span);
        sliceData.AsSpan().CopyTo(typedDestination);
    }

    #endregion

    /// <summary>
    /// Disposes the slice. This doesn't dispose the parent buffer, just invalidates this slice.
    /// </summary>
    public void Dispose() => _disposed = true;

    /// <summary>
    /// Asynchronously disposes the slice. This doesn't dispose the parent buffer, just invalidates this slice.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}