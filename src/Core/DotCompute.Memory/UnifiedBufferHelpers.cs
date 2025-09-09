// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Represents a slice of a unified buffer - production-grade implementation.
/// </summary>
internal sealed class UnifiedBufferSlice<T> : IUnifiedMemoryBuffer<T>, IDisposable where T : unmanaged
{
    private readonly UnifiedBuffer<T> _parent;
    private readonly int _offset;
    private readonly int _length;

    public UnifiedBufferSlice(UnifiedBuffer<T> parent, int offset, int length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _length = length;
    }

    #region Core Properties

    public int Length => _length;
    public long SizeInBytes => _length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
    public IAccelerator Accelerator => _parent.Accelerator;
    public BufferState State => _parent.State;
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;
    public bool IsOnHost => _parent.IsOnHost;
    public bool IsOnDevice => _parent.IsOnDevice;
    public bool IsDirty => _parent.IsDirty;

    #endregion

    #region Host Memory Access

    public Span<T> AsSpan() => _parent.AsSpan().Slice(_offset, _length);

    public ReadOnlySpan<T> AsReadOnlySpan() => _parent.AsReadOnlySpan().Slice(_offset, _length);

    public Memory<T> AsMemory() => _parent.AsMemory().Slice(_offset, _length);

    public ReadOnlyMemory<T> AsReadOnlyMemory() => _parent.AsReadOnlyMemory().Slice(_offset, _length);

    #endregion

    #region Device Memory Access

    public DeviceMemory GetDeviceMemory()
    {
        var parentDevice = _parent.GetDeviceMemory();
        var offsetBytes = _offset * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return new DeviceMemory(IntPtr.Add(parentDevice.Handle, offsetBytes), SizeInBytes);
    }

    #endregion

    #region Memory Mapping

    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite)
    {
        var parentMapped = _parent.Map(mode);
        var sliceMemory = parentMapped.Memory.Slice(_offset, _length);
        return new MappedMemory<T>(sliceMemory);
    }

    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        var parentMapped = _parent.Map(mode);
        var rangeMemory = parentMapped.Memory.Slice(_offset + offset, length);
        return new MappedMemory<T>(rangeMemory);
    }

    public async ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        var parentMapped = await _parent.MapAsync(mode, cancellationToken).ConfigureAwait(false);
        var sliceMemory = parentMapped.Memory.Slice(_offset, _length);
        return new MappedMemory<T>(sliceMemory);
    }

    #endregion

    #region Synchronization

    public void EnsureOnHost() => _parent.EnsureOnHost();
    public void EnsureOnDevice() => _parent.EnsureOnDevice();
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parent.EnsureOnHostAsync(context, cancellationToken);
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parent.EnsureOnDeviceAsync(context, cancellationToken);
    public void Synchronize() => _parent.Synchronize();
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parent.SynchronizeAsync(context, cancellationToken);
    public void MarkHostDirty() => _parent.MarkHostDirty();
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();

    #endregion

    #region Copy Operations

    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        if (source.Length > _length)
        {

            throw new ArgumentException($"Source length {source.Length} exceeds slice length {_length}");
        }


        await _parent.WriteAsync(source, _offset, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        var data = await _parent.ReadAsync(_offset, Math.Min(_length, destination.Length), cancellationToken).ConfigureAwait(false);
        data.AsMemory().CopyTo(destination);
    }

    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var data = await _parent.ReadAsync(_offset, _length, cancellationToken).ConfigureAwait(false);
        await destination.CopyFromAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, _length);

        var data = await _parent.ReadAsync(_offset + sourceOffset, count, cancellationToken).ConfigureAwait(false);

        // Create a temporary buffer to use CopyFromAsync with proper offset

        var tempDest = destination.Slice(destinationOffset, count);
        await tempDest.CopyFromAsync(data.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Fill Operations

    public async ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        var fillData = new T[_length];
        Array.Fill(fillData, value);
        await _parent.WriteAsync(fillData, _offset, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, _length);

        var fillData = new T[count];
        Array.Fill(fillData, value);
        await _parent.WriteAsync(fillData, _offset + offset, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region View and Slice Operations

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        return new UnifiedBufferSlice<T>(_parent, _offset + offset, length);
    }

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        var newLength = _length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>() / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        return new UnifiedBufferView<T, TNew>(_parent, newLength, _offset);
    }

    #endregion

    #region Non-generic interface implementation

    // Non-generic interface implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyFromAsync(source, offset, cancellationToken);


    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyToAsync(destination, offset, cancellationToken);

    #endregion

    #region Disposal


    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        // Slice doesn't own the parent buffer, so no disposal needed
    }

    #endregion
}

/// <summary>
/// Represents a view of a unified buffer with a different element type - production-grade implementation.
/// </summary>
internal sealed class UnifiedBufferView<TOriginal, TNew> : IUnifiedMemoryBuffer<TNew>, IDisposable
    where TOriginal : unmanaged
    where TNew : unmanaged
{
    private readonly UnifiedBuffer<TOriginal> _parent;
    private readonly int _length;
    private readonly int _offset;

    public UnifiedBufferView(UnifiedBuffer<TOriginal> parent, int length, int offset = 0)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _length = length;
        _offset = offset;
    }

    #region Core Properties

    public int Length => _length;
    public long SizeInBytes => _length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
    public IAccelerator Accelerator => _parent.Accelerator;
    public BufferState State => _parent.State;
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _parent.IsDisposed;
    public bool IsOnHost => _parent.IsOnHost;
    public bool IsOnDevice => _parent.IsOnDevice;
    public bool IsDirty => _parent.IsDirty;

    #endregion

    #region Host Memory Access

    public Span<TNew> AsSpan()
    {
        var parentSpan = _parent.AsSpan();
        if (_offset > 0)
        {
            parentSpan = parentSpan.Slice(_offset);
        }


        return MemoryMarshal.Cast<TOriginal, TNew>(parentSpan).Slice(0, _length);
    }

    public ReadOnlySpan<TNew> AsReadOnlySpan()
    {
        var parentSpan = _parent.AsReadOnlySpan();
        if (_offset > 0)
        {
            parentSpan = parentSpan.Slice(_offset);
        }


        return MemoryMarshal.Cast<TOriginal, TNew>(parentSpan).Slice(0, _length);
    }

    public Memory<TNew> AsMemory()
    {
        // For Memory, we need to copy since we can't safely cast Memory<T>
        var data = new TNew[_length];
        AsSpan().CopyTo(data);
        return data.AsMemory();
    }

    public ReadOnlyMemory<TNew> AsReadOnlyMemory()
    {
        // For Memory, we need to copy since we can't safely cast Memory<T>
        var data = new TNew[_length];
        AsReadOnlySpan().CopyTo(data);
        return data.AsMemory();
    }

    #endregion

    #region Device Memory Access

    public DeviceMemory GetDeviceMemory()
    {
        var parentDevice = _parent.GetDeviceMemory();
        var offsetBytes = _offset * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();
        return new DeviceMemory(IntPtr.Add(parentDevice.Handle, offsetBytes), SizeInBytes);
    }

    #endregion

    #region Memory Mapping

    public MappedMemory<TNew> Map(MapMode mode = MapMode.ReadWrite)
    {
        var parentMapped = _parent.Map(mode);
        var parentMemory = parentMapped.Memory;
        if (_offset > 0)
        {
            parentMemory = parentMemory.Slice(_offset);
        }


        var viewSpan = MemoryMarshal.Cast<TOriginal, TNew>(parentMemory.Span).Slice(0, _length);
        return new MappedMemory<TNew>(viewSpan.ToArray().AsMemory());
    }

    public MappedMemory<TNew> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        var parentMapped = _parent.Map(mode);
        var parentMemory = parentMapped.Memory;
        if (_offset > 0)
        {
            parentMemory = parentMemory.Slice(_offset);
        }


        var viewSpan = MemoryMarshal.Cast<TOriginal, TNew>(parentMemory.Span);
        var rangeSpan = viewSpan.Slice(offset, length);
        return new MappedMemory<TNew>(rangeSpan.ToArray().AsMemory());
    }

    public async ValueTask<MappedMemory<TNew>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        var parentMapped = await _parent.MapAsync(mode, cancellationToken).ConfigureAwait(false);
        var parentMemory = parentMapped.Memory;
        if (_offset > 0)
        {

            parentMemory = parentMemory.Slice(_offset);
        }


        var viewSpan = MemoryMarshal.Cast<TOriginal, TNew>(parentMemory.Span).Slice(0, _length);
        return new MappedMemory<TNew>(viewSpan.ToArray().AsMemory());
    }

    #endregion

    #region Synchronization

    public void EnsureOnHost() => _parent.EnsureOnHost();
    public void EnsureOnDevice() => _parent.EnsureOnDevice();
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parent.EnsureOnHostAsync(context, cancellationToken);
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parent.EnsureOnDeviceAsync(context, cancellationToken);
    public void Synchronize() => _parent.Synchronize();
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parent.SynchronizeAsync(context, cancellationToken);
    public void MarkHostDirty() => _parent.MarkHostDirty();
    public void MarkDeviceDirty() => _parent.MarkDeviceDirty();

    #endregion

    #region Copy Operations

    public async ValueTask CopyFromAsync(ReadOnlyMemory<TNew> source, CancellationToken cancellationToken = default)
    {
        if (source.Length > _length)
        {

            throw new ArgumentException($"Source length {source.Length} exceeds view length {_length}");
        }

        // Convert from TNew to TOriginal

        var tempArray = new TOriginal[(_length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>() - 1) / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>()];
        var destSpan = MemoryMarshal.Cast<TOriginal, TNew>(tempArray.AsSpan()).Slice(0, source.Length);
        source.Span.CopyTo(destSpan);


        await _parent.WriteAsync(tempArray.AsMemory(0, tempArray.Length), _offset, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyToAsync(Memory<TNew> destination, CancellationToken cancellationToken = default)
    {
        var originalCount = (_length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>() - 1) / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();
        var data = await _parent.ReadAsync(_offset, originalCount, cancellationToken).ConfigureAwait(false);
        var viewSpan = MemoryMarshal.Cast<TOriginal, TNew>(data.AsSpan()).Slice(0, Math.Min(_length, destination.Length));
        viewSpan.CopyTo(destination.Span);
    }

    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<TNew> destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var tempData = new TNew[_length];
        await CopyToAsync(tempData.AsMemory(), cancellationToken).ConfigureAwait(false);
        await destination.CopyFromAsync(tempData.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<TNew> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, _length);

        var tempData = new TNew[count];

        // Calculate original offsets

        var originalOffset = _offset + (sourceOffset * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>() / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>());
        var originalCount = (count * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>() + global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>() - 1) / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();


        var data = await _parent.ReadAsync(originalOffset, originalCount, cancellationToken).ConfigureAwait(false);
        var viewSpan = MemoryMarshal.Cast<TOriginal, TNew>(data.AsSpan()).Slice(0, count);
        viewSpan.CopyTo(tempData);

        // Use slice to handle destination offset

        var destSlice = destination.Slice(destinationOffset, count);
        await destSlice.CopyFromAsync(tempData.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Fill Operations

    public async ValueTask FillAsync(TNew value, CancellationToken cancellationToken = default)
    {
        var fillData = new TNew[_length];
        Array.Fill(fillData, value);
        await CopyFromAsync(fillData.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask FillAsync(TNew value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, _length);

        // Read current data
        var currentData = new TNew[_length];
        await CopyToAsync(currentData.AsMemory(), cancellationToken).ConfigureAwait(false);

        // Fill the specified range

        Array.Fill(currentData, value, offset, count);

        // Write back

        await CopyFromAsync(currentData.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region View and Slice Operations

    public IUnifiedMemoryBuffer<TNew> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        // Calculate new offset in original type
        var newOffsetBytes = (_offset * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>()) +

                            (offset * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>());
        var newOffset = newOffsetBytes / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TOriginal>();


        return new UnifiedBufferView<TOriginal, TNew>(_parent, length, newOffset);
    }

    public IUnifiedMemoryBuffer<TNew2> AsType<TNew2>() where TNew2 : unmanaged
    {
        var newLength = _length * global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>() / global::System.Runtime.CompilerServices.Unsafe.SizeOf<TNew2>();
        return new UnifiedBufferView<TOriginal, TNew2>(_parent, newLength, _offset);
    }

    #endregion

    #region Non-generic interface implementation

    // Non-generic interface implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyFromAsync(source, offset, cancellationToken);


    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken) => ((IUnifiedMemoryBuffer)_parent).CopyToAsync(destination, offset, cancellationToken);

    #endregion

    #region Disposal


    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        // View doesn't own the parent buffer, so no disposal needed
    }

    #endregion
}