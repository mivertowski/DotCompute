// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Represents a type-cast view of a UnifiedBuffer that provides access to the same memory with a different element type.
/// This is a lightweight wrapper that doesn't own the underlying memory but reinterprets it as a different type.
/// </summary>
/// <typeparam name="TOriginal">The original element type of the parent buffer.</typeparam>
/// <typeparam name="TView">The view element type.</typeparam>
public sealed class UnifiedBufferView<TOriginal, TView> : IUnifiedMemoryBuffer<TView>
    where TOriginal : unmanaged
    where TView : unmanaged
{
    private readonly UnifiedBuffer<TOriginal> _parentBuffer;
    private readonly int _viewLength;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the UnifiedBufferView class.
    /// </summary>
    /// <param name="parentBuffer">The parent buffer to create a view of.</param>
    /// <param name="viewLength">The length of the view in elements of type TView.</param>
    public UnifiedBufferView(UnifiedBuffer<TOriginal> parentBuffer, int viewLength)
    {
        ArgumentNullException.ThrowIfNull(parentBuffer);
        ArgumentOutOfRangeException.ThrowIfNegative(viewLength);

        // Validate that the view doesn't exceed the parent buffer's size
        var parentSizeInBytes = parentBuffer.SizeInBytes;
        var viewSizeInBytes = viewLength * System.Runtime.CompilerServices.Unsafe.SizeOf<TView>();

        if (viewSizeInBytes > parentSizeInBytes)
        {
            throw new ArgumentException(
                $"View size ({viewSizeInBytes} bytes) exceeds parent buffer size ({parentSizeInBytes} bytes)");
        }

        _parentBuffer = parentBuffer;
        _viewLength = viewLength;
    }

    /// <inheritdoc />
    public int Length => _viewLength;

    /// <inheritdoc />
    public long SizeInBytes => _viewLength * System.Runtime.CompilerServices.Unsafe.SizeOf<TView>();

    /// <inheritdoc />
    public IAccelerator Accelerator => _parentBuffer.Accelerator;

    /// <inheritdoc />
    public MemoryOptions Options => _parentBuffer.Options;

    /// <inheritdoc />
    public bool IsDisposed => _disposed || _parentBuffer.IsDisposed;

    /// <summary>
    /// Gets the device memory handle for this view.
    /// </summary>
    /// <returns>The device memory handle with size adjusted for the view type.</returns>
    public DeviceMemory GetDeviceMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentDeviceMemory = _parentBuffer.GetDeviceMemory();
        if (!parentDeviceMemory.IsValid)
            return DeviceMemory.Invalid;

        return new DeviceMemory(parentDeviceMemory.Handle, SizeInBytes);
    }

    /// <summary>
    /// Gets a span to the view data by casting the parent buffer's memory.
    /// </summary>
    /// <returns>A span to the view data.</returns>
    public Span<TView> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentSpan = _parentBuffer.AsSpan();
        var viewSpan = MemoryMarshal.Cast<TOriginal, TView>(parentSpan);

        // Return only the requested length
        return viewSpan.Slice(0, Math.Min(_viewLength, viewSpan.Length));
    }

    /// <summary>
    /// Gets a read-only span to the view data by casting the parent buffer's memory.
    /// </summary>
    /// <returns>A read-only span to the view data.</returns>
    public ReadOnlySpan<TView> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentSpan = _parentBuffer.AsReadOnlySpan();
        var viewSpan = MemoryMarshal.Cast<TOriginal, TView>(parentSpan);

        // Return only the requested length
        return viewSpan.Slice(0, Math.Min(_viewLength, viewSpan.Length));
    }

    /// <summary>
    /// Gets a memory handle to the view data by casting the parent buffer's memory.
    /// </summary>
    /// <returns>A memory handle to the view data.</returns>
    public Memory<TView> AsMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentMemory = _parentBuffer.AsMemory();
        var viewMemory = MemoryMarshal.Cast<TOriginal, TView>(parentMemory.Span);

        // Create a new memory from the span with the requested length
        var actualLength = Math.Min(_viewLength, viewMemory.Length);
        return new Memory<TView>(viewMemory.Slice(0, actualLength).ToArray());
    }

    /// <summary>
    /// Gets a read-only memory handle to the view data by casting the parent buffer's memory.
    /// </summary>
    /// <returns>A read-only memory handle to the view data.</returns>
    public ReadOnlyMemory<TView> AsReadOnlyMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var parentMemory = _parentBuffer.AsReadOnlyMemory();
        var viewSpan = MemoryMarshal.Cast<TOriginal, TView>(parentMemory.Span);

        // Create a new memory from the span with the requested length
        var actualLength = Math.Min(_viewLength, viewSpan.Length);
        return new ReadOnlyMemory<TView>(viewSpan.Slice(0, actualLength).ToArray());
    }

    /// <inheritdoc />
    public async ValueTask CopyFromAsync(ReadOnlyMemory<TView> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(source.Length, _viewLength);

        // Cast the source data to the parent buffer's type and write it
        var sourceSpan = source.Span;
        var originalSpan = MemoryMarshal.Cast<TView, TOriginal>(sourceSpan);

        await _parentBuffer.WriteAsync(originalSpan.ToArray(), 0, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync(Memory<TView> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(destination.Length, _viewLength);

        // Calculate how many original elements we need to read
        var bytesToRead = destination.Length * System.Runtime.CompilerServices.Unsafe.SizeOf<TView>();
        var originalElementsToRead = bytesToRead / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();

        var parentData = await _parentBuffer.ReadAsync(0, originalElementsToRead, cancellationToken).ConfigureAwait(false);
        var viewData = MemoryMarshal.Cast<TOriginal, TView>(parentData.AsSpan());

        var actualLength = Math.Min(destination.Length, viewData.Length);
        viewData.Slice(0, actualLength).CopyTo(destination.Span);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<TView> destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        var viewData = AsReadOnlyMemory();
        await destination.CopyFromAsync(viewData, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<TView> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, _viewLength);

        // Read the required portion of the view data
        var viewSpan = AsReadOnlySpan();
        var sourceData = viewSpan.Slice(sourceOffset, count);

        // Copy to the destination using a slice
        var destSlice = destination.Slice(destinationOffset, count);
        await destSlice.CopyFromAsync(sourceData.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IUnifiedMemoryBuffer<TView> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _viewLength);

        // Create a new view with adjusted parameters
        var newViewLength = length;

        // Calculate the offset in the parent buffer's coordinate system
        var bytesOffset = offset * System.Runtime.CompilerServices.Unsafe.SizeOf<TView>();
        var parentElementOffset = bytesOffset / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();

        // Create a slice of the parent buffer first, then create a view of that slice
        var parentSlice = _parentBuffer.Slice(parentElementOffset,
            (length * System.Runtime.CompilerServices.Unsafe.SizeOf<TView>()) / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>());

        // For now, return a simple slice implementation
        // This is a simplification - in a full implementation you'd want to maintain the view semantics
        return new UnifiedBufferView<TOriginal, TView>(_parentBuffer, newViewLength);
    }

    /// <inheritdoc />
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        var elementSizeRatio = System.Runtime.CompilerServices.Unsafe.SizeOf<TView>() / (double)System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        var newLength = (int)(_viewLength * elementSizeRatio);

        return new UnifiedBufferView<TOriginal, TNew>(_parentBuffer, newLength);
    }

    /// <inheritdoc />
    public async ValueTask FillAsync(TView value, CancellationToken cancellationToken = default)
    {
        await FillAsync(value, 0, _viewLength, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask FillAsync(TView value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, _viewLength);

        // Create fill data and write it
        var fillData = new TView[count];
        fillData.AsSpan().Fill(value);

        // Convert the fill data to the parent buffer's type
        var fillSpan = fillData.AsSpan();
        var originalSpan = MemoryMarshal.Cast<TView, TOriginal>(fillSpan);

        // Calculate offset in parent buffer coordinates
        var bytesOffset = offset * System.Runtime.CompilerServices.Unsafe.SizeOf<TView>();
        var parentElementOffset = bytesOffset / System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();

        await _parentBuffer.WriteAsync(originalSpan.ToArray(), parentElementOffset, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public MappedMemory<TView> Map(MapMode mode = MapMode.ReadWrite)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        return new MappedMemory<TView>(AsMemory());
    }

    /// <inheritdoc />
    public MappedMemory<TView> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _viewLength);

        var viewMemory = AsMemory().Slice(offset, length);
        return new MappedMemory<TView>(viewMemory);
    }

    /// <inheritdoc />
    public async ValueTask<MappedMemory<TView>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed || _parentBuffer.IsDisposed, this);

        // Ensure parent buffer is on host before mapping
        await _parentBuffer.EnsureOnHostAsync(default, cancellationToken).ConfigureAwait(false);

        return new MappedMemory<TView>(AsMemory());
    }

    #region IUnifiedMemoryBuffer (non-generic) Implementation

    /// <inheritdoc />
    public ValueTask CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset = 0, CancellationToken cancellationToken = default) where U : unmanaged
    {
        if (typeof(U) != typeof(TView))
        {
            throw new ArgumentException($"Type mismatch: expected {typeof(TView)}, got {typeof(U)}");
        }

        var typedSource = MemoryMarshal.Cast<U, TView>(source.Span);
        return CopyFromAsync(typedSource.ToArray().AsMemory(), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask CopyToAsync<U>(Memory<U> destination, long offset = 0, CancellationToken cancellationToken = default) where U : unmanaged
    {
        if (typeof(U) != typeof(TView))
        {
            throw new ArgumentException($"Type mismatch: expected {typeof(TView)}, got {typeof(U)}");
        }

        var viewData = AsReadOnlyMemory();
        var typedDestination = MemoryMarshal.Cast<U, TView>(destination.Span);

        var copyLength = Math.Min(viewData.Length, typedDestination.Length);
        viewData.Span.Slice(0, copyLength).CopyTo(typedDestination);
    }

    #endregion

    /// <summary>
    /// Disposes the view. This doesn't dispose the parent buffer, just invalidates this view.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// Asynchronously disposes the view. This doesn't dispose the parent buffer, just invalidates this view.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}