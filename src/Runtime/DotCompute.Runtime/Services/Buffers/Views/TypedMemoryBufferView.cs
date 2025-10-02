// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using System.Runtime.CompilerServices;

namespace DotCompute.Runtime.Services.Buffers.Views;

/// <summary>
/// View over a typed memory buffer.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
public sealed class TypedMemoryBufferView<T>(IUnifiedMemoryBuffer<T> parent, int offset, int length) : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer<T> _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    private readonly int _offset = offset;
    private readonly int _length = length;
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>

    // Properties from IUnifiedMemoryBuffer<T>

    public int Length => _length;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => _parent.Accelerator;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => _parent.IsOnHost;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => _parent.IsOnDevice;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty => _parent.IsDirty;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>

    // Properties from IUnifiedMemoryBuffer

    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _parent.Options;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _parent.IsDisposed;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _parent.State;
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Delegate all operations to parent with offset adjustments

    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        if (source.Length > _length)
        {
            throw new ArgumentException("Source too large for view");
        }

        // Handle offset properly for view operations
        var parentTyped = _parent.AsType<T>();
        var targetSlice = parentTyped.Slice(_offset, Math.Min(source.Length, _length));
        await targetSlice.CopyFromAsync(source.Slice(0, targetSlice.Length), cancellationToken);
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(destination, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(destination, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(_offset + sourceOffset, destination, destinationOffset, count, cancellationToken);
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    // Complete implementations with proper view semantics

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
    public DeviceMemory GetDeviceMemory() => _parent.GetDeviceMemory();
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset, _length, mode);
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset + offset, length, mode);
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => _parent.MapAsync(mode, cancellationToken);
    /// <summary>
    /// Performs ensure on host.
    /// </summary>


    public void EnsureOnHost() => _parent.EnsureOnHost();
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() => _parent.EnsureOnDevice();
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnHostAsync(context, cancellationToken);
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnDeviceAsync(context, cancellationToken);
    /// <summary>
    /// Performs synchronize.
    /// </summary>
    public void Synchronize() => _parent.Synchronize();
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.SynchronizeAsync(context, cancellationToken);
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>
    public void MarkHostDirty() => _parent.MarkHostDirty();
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => _parent.FillAsync(value, _offset, _length, cancellationToken);
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => _parent.FillAsync(value, _offset + offset, count, cancellationToken);

    // Non-generic interface implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyFromAsync(source, offset, cancellationToken);


    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyToAsync(destination, offset, cancellationToken);
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (offset + length > _length)
        {
            throw new ArgumentException("Slice extends beyond view");
        }


        return new TypedMemoryBufferView<T>(_parent, _offset + offset, length);
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        _ = (int)(SizeInBytes / Unsafe.SizeOf<TNew>());
        // Calculate proper offset and length for type conversion
        var elementSize = Unsafe.SizeOf<T>();
        var newElementSize = Unsafe.SizeOf<TNew>();
        var byteOffset = _offset * elementSize;
        var newLength = (int)(SizeInBytes / newElementSize);

        // Ensure alignment
        if (byteOffset % newElementSize != 0)
        {
            throw new InvalidOperationException($"Cannot convert view to type {typeof(TNew).Name} due to alignment constraints");
        }

        var parentAsNew = _parent.AsType<TNew>();
        var newOffset = byteOffset / newElementSize;
        return new TypedMemoryBufferView<TNew>(parentAsNew, newOffset, newLength);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public void Dispose() { /* Views don't own the parent buffer */ }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}