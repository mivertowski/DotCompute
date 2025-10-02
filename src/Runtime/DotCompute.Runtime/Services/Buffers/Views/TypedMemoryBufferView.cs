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

    // Properties from IUnifiedMemoryBuffer<T>

    public int Length => _length;
    public IAccelerator Accelerator => _parent.Accelerator;
    public bool IsOnHost => _parent.IsOnHost;
    public bool IsOnDevice => _parent.IsOnDevice;
    public bool IsDirty => _parent.IsDirty;

    // Properties from IUnifiedMemoryBuffer

    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;
    public BufferState State => _parent.State;

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


    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(destination, cancellationToken);


    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(destination, cancellationToken);


    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => _parent.CopyToAsync(_offset + sourceOffset, destination, destinationOffset, count, cancellationToken);

    // Complete implementations with proper view semantics

    public Span<T> AsSpan() => _parent.AsSpan().Slice(_offset, _length);
    public ReadOnlySpan<T> AsReadOnlySpan() => _parent.AsReadOnlySpan().Slice(_offset, _length);
    public Memory<T> AsMemory() => _parent.AsMemory().Slice(_offset, _length);
    public ReadOnlyMemory<T> AsReadOnlyMemory() => _parent.AsReadOnlyMemory().Slice(_offset, _length);
    public DeviceMemory GetDeviceMemory() => _parent.GetDeviceMemory();
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset, _length, mode);
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => _parent.MapRange(_offset + offset, length, mode);
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => _parent.MapAsync(mode, cancellationToken);


    public void EnsureOnHost() => _parent.EnsureOnHost();
    public void EnsureOnDevice() => _parent.EnsureOnDevice();
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnHostAsync(context, cancellationToken);
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.EnsureOnDeviceAsync(context, cancellationToken);
    public void Synchronize() => _parent.Synchronize();
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => _parent.SynchronizeAsync(context, cancellationToken);
    public void MarkHostDirty() => _parent.MarkHostDirty();
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => _parent.FillAsync(value, _offset, _length, cancellationToken);
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => _parent.FillAsync(value, _offset + offset, count, cancellationToken);

    // Non-generic interface implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyFromAsync(source, offset, cancellationToken);


    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyToAsync(destination, offset, cancellationToken);


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


    public void Dispose() { /* Views don't own the parent buffer */ }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}