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
internal sealed class CpuMemoryBufferSlice : IUnifiedMemoryBuffer<byte>, IDisposable
{
    private readonly CpuMemoryBuffer _parentBuffer;
    private readonly int _offset;
    private readonly int _length;
    private readonly CpuMemoryManager _memoryManager;
    private readonly ILogger? _logger;
    private bool _isDisposed;

    public CpuMemoryBufferSlice(
        CpuMemoryBuffer parentBuffer,
        int offset,
        int length,
        CpuMemoryManager memoryManager,
        ILogger? logger)
    {
        _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        _offset = offset;
        _length = length;
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _logger = logger;
    }

    public long SizeInBytes => _length;
    public int Count => _length;
    public int Length => _length;
    public BufferState State => _parentBuffer.State;
    public bool IsDisposed => _isDisposed || _parentBuffer.IsDisposed;
    public IAccelerator Accelerator => _parentBuffer.Accelerator;
    public bool IsOnHost => _parentBuffer.State == BufferState.HostOnly || _parentBuffer.State == BufferState.Synchronized;
    public bool IsOnDevice => _parentBuffer.State == BufferState.DeviceOnly || _parentBuffer.State == BufferState.Synchronized;
    public bool IsDirty => _parentBuffer.State == BufferState.HostDirty || _parentBuffer.State == BufferState.DeviceDirty;
    public MemoryOptions Options => _parentBuffer.Options;

    public Span<byte> AsSpan()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsSpan().Slice(_offset, _length);
    }

    public ReadOnlySpan<byte> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsReadOnlySpan().Slice(_offset, _length);
    }

    public Memory<byte> AsMemory()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsMemory().Slice(_offset, _length);
    }

    public ReadOnlyMemory<byte> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        return _parentBuffer.AsReadOnlyMemory().Slice(_offset, _length);
    }

    // Device memory and mapping methods

    public DeviceMemory GetDeviceMemory()
    {
        EnsureNotDisposed();
        // For CPU backend with slicing, we need to adjust the pointer
        var parentDeviceMemory = _parentBuffer.GetDeviceMemory();
        return new DeviceMemory(parentDeviceMemory.Handle + _offset, _length);
    }

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

    public MappedMemory<byte> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        if (offset < 0 || length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException();
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

    public ValueTask<MappedMemory<byte>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(Map(mode));
    }

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

            throw new ArgumentOutOfRangeException();
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

    public ValueTask FillAsync(byte value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

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
        if (_isDisposed)
        {

            throw new ObjectDisposedException(nameof(CpuMemoryBufferSlice));
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