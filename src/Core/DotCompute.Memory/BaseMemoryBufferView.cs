// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Base class for memory buffer views that represent a slice of a parent buffer.
/// Provides common implementation for views across all backends (CPU, CUDA, Metal, OpenCL).
/// </summary>
/// <remarks>
/// Views do not own the underlying memory - the parent buffer manages allocation and disposal.
/// Backends should extend this class to implement backend-specific behavior.
/// </remarks>
/// <typeparam name="T">The element type of the buffer.</typeparam>
public abstract class BaseMemoryBufferView<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer<T> _parent;
    private readonly int _offset;
    private readonly int _length;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseMemoryBufferView{T}"/> class.
    /// </summary>
    /// <param name="parent">The parent buffer to create a view into.</param>
    /// <param name="offset">The offset in elements from the start of the parent buffer.</param>
    /// <param name="length">The number of elements in the view.</param>
    /// <exception cref="ArgumentNullException">Thrown if parent is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if offset or length are invalid.</exception>
    protected BaseMemoryBufferView(IUnifiedMemoryBuffer<T> parent, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, parent.Length);

        _parent = parent;
        _offset = offset;
        _length = length;
    }

    /// <summary>
    /// Gets the parent buffer this view references.
    /// </summary>
    protected IUnifiedMemoryBuffer<T> Parent => _parent;

    /// <summary>
    /// Gets the offset in elements from the parent buffer start.
    /// </summary>
    protected int Offset => _offset;

    /// <inheritdoc />
    public int Length => _length;

    /// <inheritdoc />
    public long SizeInBytes => (long)_length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

    // DevicePointer and MemoryType removed - no longer in interface

    /// <inheritdoc />
    public bool IsDisposed => _disposed || _parent.IsDisposed;

    /// <inheritdoc />
    public IAccelerator Accelerator => _parent.Accelerator;

    /// <inheritdoc />
    public BufferState State => _parent.State;

    /// <inheritdoc />
    public MemoryOptions Options => _parent.Options;

    /// <inheritdoc />
    public bool IsOnHost => _parent.IsOnHost;

    /// <inheritdoc />
    public bool IsOnDevice => _parent.IsOnDevice;

    /// <inheritdoc />
    public bool IsDirty => _parent.IsDirty;

    /// <inheritdoc />
    public virtual Span<T> AsSpan()
    {
        ThrowIfDisposed();
        return _parent.AsSpan().Slice(_offset, _length);
    }

    /// <inheritdoc />
    public virtual ReadOnlySpan<T> AsReadOnlySpan()
    {
        ThrowIfDisposed();
        return _parent.AsReadOnlySpan().Slice(_offset, _length);
    }

    /// <inheritdoc />
    public virtual Memory<T> AsMemory()
    {
        ThrowIfDisposed();
        return _parent.AsMemory().Slice(_offset, _length);
    }

    /// <inheritdoc />
    public virtual ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ThrowIfDisposed();
        return _parent.AsReadOnlyMemory().Slice(_offset, _length);
    }

    /// <inheritdoc />
    public virtual DeviceMemory GetDeviceMemory()
    {
        ThrowIfDisposed();
        var parentDeviceMemory = _parent.GetDeviceMemory();
        var offsetBytes = _offset * System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        return new DeviceMemory(
            IntPtr.Add(parentDeviceMemory.Handle, offsetBytes),
            SizeInBytes);
    }

    /// <inheritdoc />
    public virtual MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite)
    {
        ThrowIfDisposed();
        return new MappedMemory<T>(AsMemory());
    }

    /// <inheritdoc />
    public virtual MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        return new MappedMemory<T>(AsMemory().Slice(offset, length));
    }

    /// <inheritdoc />
    public virtual async ValueTask<MappedMemory<T>> MapAsync(
        MapMode mode = MapMode.ReadWrite,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureOnHostAsync(default, cancellationToken).ConfigureAwait(false);
        return new MappedMemory<T>(AsMemory());
    }

    /// <inheritdoc />
    public virtual void EnsureOnHost() => _parent.EnsureOnHost();

    /// <inheritdoc />
    public virtual void EnsureOnDevice() => _parent.EnsureOnDevice();

    /// <inheritdoc />
    public virtual ValueTask EnsureOnHostAsync(
        AcceleratorContext context = default,
        CancellationToken cancellationToken = default)
        => _parent.EnsureOnHostAsync(context, cancellationToken);

    /// <inheritdoc />
    public virtual ValueTask EnsureOnDeviceAsync(
        AcceleratorContext context = default,
        CancellationToken cancellationToken = default)
        => _parent.EnsureOnDeviceAsync(context, cancellationToken);

    /// <inheritdoc />
    public virtual void Synchronize() => _parent.Synchronize();

    /// <inheritdoc />
    public virtual ValueTask SynchronizeAsync(
        AcceleratorContext context = default,
        CancellationToken cancellationToken = default)
        => _parent.SynchronizeAsync(context, cancellationToken);

    /// <inheritdoc />
    public virtual void MarkHostDirty() => _parent.MarkHostDirty();

    /// <inheritdoc />
    public virtual void MarkDeviceDirty() => _parent.MarkDeviceDirty();

    /// <inheritdoc />
    public virtual async ValueTask CopyFromAsync(
        ReadOnlyMemory<T> source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (source.Length > _length)
            throw new ArgumentException("Source is larger than view", nameof(source));

        var span = AsSpan();
        source.Span.CopyTo(span);
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async ValueTask CopyFromAsync(
        ReadOnlyMemory<T> source,
        long offset = 0,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (offset + source.Length > _length)
            throw new ArgumentException("Source would overflow view", nameof(source));

        var span = AsSpan().Slice((int)offset);
        source.Span.CopyTo(span);
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async ValueTask CopyToAsync(
        Memory<T> destination,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (destination.Length < _length)
            throw new ArgumentException("Destination is smaller than view", nameof(destination));

        AsReadOnlySpan().CopyTo(destination.Span);
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async ValueTask CopyToAsync(
        Memory<T> destination,
        long offset = 0,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (offset >= _length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var source = AsReadOnlySpan().Slice((int)offset);
        source.CopyTo(destination.Span);
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async ValueTask CopyToAsync(
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);

        await destination.CopyFromAsync(AsReadOnlyMemory(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sourceOffset + count, _length);

        var source = AsReadOnlyMemory().Slice(sourceOffset, count);
        await destination.CopyFromAsync(source, destinationOffset, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async ValueTask CopyFromAsync(
        IUnifiedMemoryBuffer<T> source,
        long sourceOffset = 0,
        long destinationOffset = 0,
        long count = -1,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);

        var actualCount = count == -1 ? Math.Min(source.Length - sourceOffset, _length - destinationOffset) : count;
        var sourceData = source.AsReadOnlyMemory().Slice((int)sourceOffset, (int)actualCount);
        await CopyFromAsync(sourceData, destinationOffset, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, _length);

        AsSpan().Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public virtual IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, _length);

        // Create a new view at the combined offset
        return CreateSlice(_offset + offset, length);
    }

    /// <summary>
    /// Creates a new slice view. Override in derived classes to return the appropriate type.
    /// </summary>
    /// <param name="absoluteOffset">The absolute offset from the parent buffer start.</param>
    /// <param name="length">The length of the slice.</param>
    /// <returns>A new view instance.</returns>
    protected abstract IUnifiedMemoryBuffer<T> CreateSlice(int absoluteOffset, int length);

    /// <inheritdoc />
    public virtual IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        ThrowIfDisposed();
        var parentRetyped = _parent.AsType<TNew>();
        var elementRatio = System.Runtime.CompilerServices.Unsafe.SizeOf<T>() /
                           System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        return parentRetyped.Slice(_offset * elementRatio, _length * elementRatio);
    }

    /// <summary>
    /// Throws if the view or parent buffer has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
        ObjectDisposedException.ThrowIf(_parent.IsDisposed, _parent.GetType());
    }

    /// <summary>
    /// Views do not own memory - disposal only marks the view as unusable.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // Non-generic interface implementation
    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken)
    {
        if (typeof(U) != typeof(T))
            throw new ArgumentException($"Type mismatch: expected {typeof(T)}, got {typeof(U)}");

        var typedSource = System.Runtime.InteropServices.MemoryMarshal.Cast<U, T>(source.Span);
        return CopyFromAsync(typedSource.ToArray(), offset, cancellationToken);
    }

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken)
    {
        if (typeof(U) != typeof(T))
            throw new ArgumentException($"Type mismatch: expected {typeof(T)}, got {typeof(U)}");

        var typedDestination = System.Runtime.InteropServices.MemoryMarshal.Cast<U, T>(destination.Span);
        return CopyToAsync(typedDestination.ToArray(), offset, cancellationToken);
    }
}
