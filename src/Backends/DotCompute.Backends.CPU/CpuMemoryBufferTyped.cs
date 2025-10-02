// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Provides a typed view of a CpuMemoryBuffer for strongly-typed element access.
/// </summary>
public sealed class CpuMemoryBufferTyped<T>(
    CpuMemoryBuffer parentBuffer,
    int elementCount,
    CpuMemoryManager memoryManager,
    ILogger? logger) : IUnifiedMemoryBuffer<T>, IDisposable
    where T : unmanaged
{
    private readonly CpuMemoryBuffer _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
    private readonly int _elementCount = elementCount;
    private readonly CpuMemoryManager _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
    private readonly ILogger? _logger = logger;
    private bool _isDisposed;

    public long SizeInBytes => _elementCount * Unsafe.SizeOf<T>();
    public int Count => _elementCount;
    public int Length => _elementCount;
    public BufferState State => _parentBuffer.State;
    public bool IsDisposed => _isDisposed || _parentBuffer.IsDisposed;
    public IAccelerator Accelerator => _parentBuffer.Accelerator;
    public bool IsOnHost => _parentBuffer.State == BufferState.HostOnly || _parentBuffer.State == BufferState.Synchronized;
    public bool IsOnDevice => _parentBuffer.State == BufferState.DeviceOnly || _parentBuffer.State == BufferState.Synchronized;
    public bool IsDirty => _parentBuffer.State == BufferState.HostDirty || _parentBuffer.State == BufferState.DeviceDirty;
    public MemoryOptions Options => _parentBuffer.Options;

    public Span<T> AsSpan()
    {
        EnsureNotDisposed();
        // Cast the byte span to the target type
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(_parentBuffer.AsSpan());
    }

    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(_parentBuffer.AsReadOnlySpan());
    }

    public Memory<T> AsMemory()
    {
        EnsureNotDisposed();
        // For simplicity, we'll use the span-based approach
        // In production, this would need a proper MemoryManager<T> implementation
        var span = AsSpan();
        return new Memory<T>(span.ToArray());
    }

    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        var span = AsReadOnlySpan();
        return new ReadOnlyMemory<T>(span.ToArray());
    }

    // Device memory and mapping methods

    public DeviceMemory GetDeviceMemory()
    {
        EnsureNotDisposed();
        // For CPU backend, device memory is the same as host memory
        var parentDeviceMemory = _parentBuffer.GetDeviceMemory();
        return parentDeviceMemory;
    }

    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        var memory = AsMemory();
        return new MappedMemory<T>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }

    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        if (offset < 0 || length < 0 || offset + length > _elementCount)
        {
            throw new ArgumentOutOfRangeException();
        }

        var memory = AsMemory().Slice(offset, length);
        return new MappedMemory<T>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }

    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));

    public void EnsureOnHost() => _parentBuffer.EnsureOnHost();
    public void EnsureOnDevice() => _parentBuffer.EnsureOnDevice();

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnHostAsync(context, cancellationToken);

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnDeviceAsync(context, cancellationToken);

    public void Synchronize() => _parentBuffer.Synchronize();

    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.SynchronizeAsync(context, cancellationToken);

    public void MarkHostDirty() => _parentBuffer.MarkHostDirty();
    public void MarkDeviceDirty() => _parentBuffer.MarkDeviceDirty();

    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (source.Length > _elementCount)
        {

            throw new ArgumentException("Source contains more elements than buffer capacity", nameof(source));
        }

        // Convert to bytes and copy

        var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(source.Span);
        var sourceMem = new ReadOnlyMemory<byte>(sourceBytes.ToArray());
        return _parentBuffer.CopyFromAsync(sourceMem, cancellationToken);
    }

    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _elementCount)
        {

            throw new ArgumentException("Destination has insufficient capacity", nameof(destination));
        }

        // Convert to bytes and copy

        var destinationBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(destination.Span);
        var destMem = new Memory<byte>(destinationBytes.ToArray());
        return _parentBuffer.CopyToAsync(destMem, cancellationToken);
    }

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _elementCount)
        {

            throw new ArgumentException("Destination buffer has insufficient capacity", nameof(destination));
        }


        var sourceSpan = AsReadOnlySpan();
        var destSpan = destination.AsSpan();
        sourceSpan.CopyTo(destSpan);
        destination.MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (sourceOffset < 0 || destinationOffset < 0 || count < 0)
        {

            throw new ArgumentOutOfRangeException();
        }


        if (sourceOffset + count > _elementCount)
        {

            throw new ArgumentOutOfRangeException(nameof(count), "Copy extends beyond source buffer boundaries");
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

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (offset < 0)
        {

            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");
        }


        if (offset + count > _elementCount)
        {

            throw new ArgumentOutOfRangeException(nameof(count), "Fill operation extends beyond buffer boundaries");
        }


        AsSpan().Slice(offset, count).Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
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


        if (offset + length > _elementCount)
        {

            throw new ArgumentOutOfRangeException(nameof(length), "Slice extends beyond buffer boundaries");
        }

        // Calculate byte offset and length

        var elementSize = Unsafe.SizeOf<T>();
        var byteOffset = offset * elementSize;
        _ = length * elementSize;

        return new CpuMemoryBufferTypedSlice<T>(_parentBuffer, byteOffset, length, _memoryManager, _logger);
    }

    /// <summary>
    /// Creates a view of this buffer with the specified offset and length.
    /// This is an alias for Slice for backward compatibility.
    /// </summary>
    /// <param name="offset">The element offset.</param>
    /// <param name="length">The number of elements in the view.</param>
    /// <returns>A view of this buffer.</returns>
    public IUnifiedMemoryBuffer<T> CreateView(int offset, int length) => Slice(offset, length);

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        EnsureNotDisposed();


        var currentSize = (int)SizeInBytes;
        var newElementSize = Unsafe.SizeOf<TNew>();


        if (currentSize % newElementSize != 0)
        {

            throw new InvalidOperationException($"Buffer size {currentSize} is not compatible with element size {newElementSize}");
        }


        var newElementCount = currentSize / newElementSize;
        return new CpuMemoryBufferTyped<TNew>(_parentBuffer, newElementCount, _memoryManager, _logger);
    }

    private void EnsureNotDisposed()
    {
        if (_isDisposed)
        {

            throw new ObjectDisposedException(nameof(CpuMemoryBufferTyped<T>));
        }


        if (_parentBuffer.IsDisposed)
        {

            throw new ObjectDisposedException(nameof(CpuMemoryBuffer), "Parent buffer has been disposed");
        }

    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            // Note: We don't dispose the parent buffer as we don't own it
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #region IUnifiedMemoryBuffer Base Implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long offset, CancellationToken cancellationToken)
    {
        var byteSource = System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.Span);
        var sourceMem = new ReadOnlyMemory<byte>(byteSource.ToArray());
        return ((IUnifiedMemoryBuffer)_parentBuffer).CopyFromAsync(sourceMem, offset, cancellationToken);
    }

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<TDestination>(Memory<TDestination> destination, long offset, CancellationToken cancellationToken)
    {
        var byteDestination = System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.Span);
        var destMem = new Memory<byte>(byteDestination.ToArray());
        return ((IUnifiedMemoryBuffer)_parentBuffer).CopyToAsync(destMem, offset, cancellationToken);
    }

    #endregion
}

/// <summary>
/// Provides a typed slice view of a CpuMemoryBuffer for strongly-typed element access.
/// </summary>
public sealed class CpuMemoryBufferTypedSlice<T>(
    CpuMemoryBuffer parentBuffer,
    int byteOffset,
    int elementCount,
    CpuMemoryManager memoryManager,
    ILogger? logger) : IUnifiedMemoryBuffer<T>, IDisposable
    where T : unmanaged
{
    private readonly CpuMemoryBuffer _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
    private readonly int _byteOffset = byteOffset;
    private readonly int _elementCount = elementCount;
    private readonly CpuMemoryManager _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
    private readonly ILogger? _logger = logger;
    private bool _isDisposed;

    public long SizeInBytes => _elementCount * Unsafe.SizeOf<T>();
    public int Count => _elementCount;
    public int Length => _elementCount;
    public BufferState State => _parentBuffer.State;
    public bool IsDisposed => _isDisposed || _parentBuffer.IsDisposed;
    public IAccelerator Accelerator => _parentBuffer.Accelerator;
    public bool IsOnHost => _parentBuffer.State == BufferState.HostOnly || _parentBuffer.State == BufferState.Synchronized;
    public bool IsOnDevice => _parentBuffer.State == BufferState.DeviceOnly || _parentBuffer.State == BufferState.Synchronized;
    public bool IsDirty => _parentBuffer.State == BufferState.HostDirty || _parentBuffer.State == BufferState.DeviceDirty;
    public MemoryOptions Options => _parentBuffer.Options;

    public Span<T> AsSpan()
    {
        EnsureNotDisposed();
        var byteSpan = _parentBuffer.AsSpan().Slice(_byteOffset, (int)SizeInBytes);
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(byteSpan);
    }

    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        var byteSpan = _parentBuffer.AsReadOnlySpan().Slice(_byteOffset, (int)SizeInBytes);
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(byteSpan);
    }

    public Memory<T> AsMemory()
    {
        EnsureNotDisposed();
        var span = AsSpan();
        return new Memory<T>(span.ToArray());
    }

    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        var span = AsReadOnlySpan();
        return new ReadOnlyMemory<T>(span.ToArray());
    }

    // Device memory and mapping methods

    public DeviceMemory GetDeviceMemory()
    {
        EnsureNotDisposed();
        // For CPU backend with slicing, we need to adjust the pointer
        var parentDeviceMemory = _parentBuffer.GetDeviceMemory();
        return new DeviceMemory(parentDeviceMemory.Handle + _byteOffset, SizeInBytes);
    }

    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        var memory = AsMemory();
        return new MappedMemory<T>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }

    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        if (offset < 0 || length < 0 || offset + length > _elementCount)
        {
            throw new ArgumentOutOfRangeException();
        }

        var memory = AsMemory().Slice(offset, length);
        return new MappedMemory<T>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }

    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));

    public void EnsureOnHost() => _parentBuffer.EnsureOnHost();
    public void EnsureOnDevice() => _parentBuffer.EnsureOnDevice();

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnHostAsync(context, cancellationToken);

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnDeviceAsync(context, cancellationToken);

    public void Synchronize() => _parentBuffer.Synchronize();
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.SynchronizeAsync(context, cancellationToken);

    public void MarkHostDirty() => _parentBuffer.MarkHostDirty();
    public void MarkDeviceDirty() => _parentBuffer.MarkDeviceDirty();

    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (source.Length > _elementCount)
        {

            throw new ArgumentException("Source contains more elements than slice capacity", nameof(source));
        }


        var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(source.Span);
        var sourceMem = new ReadOnlyMemory<byte>(sourceBytes.ToArray());
        // Copy directly to our slice 
        var destSpan = AsSpan();
        sourceMem.Span.CopyTo(System.Runtime.InteropServices.MemoryMarshal.AsBytes(destSpan));
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _elementCount)
        {

            throw new ArgumentException("Destination has insufficient capacity", nameof(destination));
        }


        var span = AsSpan();
        span.CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _elementCount)
        {

            throw new ArgumentException("Destination buffer has insufficient capacity", nameof(destination));
        }


        var sourceSpan = AsReadOnlySpan();
        var destSpan = destination.AsSpan();
        sourceSpan.CopyTo(destSpan);
        destination.MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (sourceOffset < 0 || destinationOffset < 0 || count < 0)
        {

            throw new ArgumentOutOfRangeException();
        }


        if (sourceOffset + count > _elementCount)
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

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (offset < 0)
        {

            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");
        }


        if (offset + count > _elementCount)
        {

            throw new ArgumentOutOfRangeException(nameof(count), "Fill operation extends beyond slice boundaries");
        }


        AsSpan().Slice(offset, count).Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
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


        if (offset + length > _elementCount)
        {

            throw new ArgumentOutOfRangeException(nameof(length), "Slice extends beyond current slice boundaries");
        }


        var elementSize = Unsafe.SizeOf<T>();
        var newByteOffset = _byteOffset + (offset * elementSize);


        return new CpuMemoryBufferTypedSlice<T>(_parentBuffer, newByteOffset, length, _memoryManager, _logger);
    }

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        EnsureNotDisposed();


        var currentSize = SizeInBytes;
        var newElementSize = Unsafe.SizeOf<TNew>();


        if (currentSize % newElementSize != 0)
        {

            throw new InvalidOperationException($"Slice size {currentSize} is not compatible with element size {newElementSize}");
        }


        var newElementCount = (int)(currentSize / newElementSize);
        return new CpuMemoryBufferTypedSlice<TNew>(_parentBuffer, _byteOffset, newElementCount, _memoryManager, _logger);
    }

    private void EnsureNotDisposed()
    {
        if (_isDisposed)
        {

            throw new ObjectDisposedException(nameof(CpuMemoryBufferTypedSlice<T>));
        }


        if (_parentBuffer.IsDisposed)
        {

            throw new ObjectDisposedException(nameof(CpuMemoryBuffer), "Parent buffer has been disposed");
        }

    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            // Note: We don't dispose the parent buffer as we don't own it
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #region IUnifiedMemoryBuffer Base Implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long offset, CancellationToken cancellationToken)
    {
        var byteSource = System.Runtime.InteropServices.MemoryMarshal.AsBytes(source.Span);
        var sourceMem = new ReadOnlyMemory<byte>(byteSource.ToArray());
        return ((IUnifiedMemoryBuffer)_parentBuffer).CopyFromAsync(sourceMem, _byteOffset + offset, cancellationToken);
    }

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<TDestination>(Memory<TDestination> destination, long offset, CancellationToken cancellationToken)
    {
        var byteDestination = System.Runtime.InteropServices.MemoryMarshal.AsBytes(destination.Span);
        var destMem = new Memory<byte>(byteDestination.ToArray());
        return ((IUnifiedMemoryBuffer)_parentBuffer).CopyToAsync(destMem, _byteOffset + offset, cancellationToken);
    }

    #endregion
}