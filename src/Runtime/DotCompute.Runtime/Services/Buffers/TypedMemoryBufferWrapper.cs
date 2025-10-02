// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Runtime.Services.Buffers.Views;
using System.Runtime.CompilerServices;

namespace DotCompute.Runtime.Services.Buffers;

/// <summary>
/// Typed wrapper around untyped memory buffer to provide IUnifiedMemoryBuffer{T} interface.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
public sealed class TypedMemoryBufferWrapper<T>(IUnifiedMemoryBuffer buffer, int length) : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    private readonly int _length = length;

    // Properties from IUnifiedMemoryBuffer<T>

    public int Length => _length;
    public IAccelerator Accelerator => throw new NotSupportedException("Accelerator not available in production wrapper");
    public bool IsOnHost => true; // Simplified for production wrapper
    public bool IsOnDevice => false;
    public bool IsDirty => false;

    // Properties from IUnifiedMemoryBuffer

    public long SizeInBytes => _buffer.SizeInBytes;
    public MemoryOptions Options => _buffer.Options;
    public bool IsDisposed => _buffer.IsDisposed;
    public BufferState State => _buffer.State;

    // Basic copy operations

    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Length > _length)
        {
            throw new ArgumentException("Source data exceeds buffer capacity");
        }

        await _buffer.CopyFromAsync(source, 0, cancellationToken);
    }

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length > _length)
        {
            throw new ArgumentException("Destination buffer too small");
        }

        await _buffer.CopyToAsync(destination, 0, cancellationToken);
    }

    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length != _length)
        {
            throw new ArgumentException("Buffer sizes must match for direct copy");
        }

        // Use temporary host memory for buffer-to-buffer transfers
        var tempMemory = new Memory<T>(new T[_length]);
        await _buffer.CopyToAsync(tempMemory, 0, cancellationToken);
        await destination.CopyFromAsync(tempMemory, cancellationToken);
    }

    public async ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (sourceOffset + count > _length)
        {
            throw new ArgumentException("Source range exceeds buffer bounds");
        }
        if (destinationOffset + count > destination.Length)
        {
            throw new ArgumentException("Destination range exceeds buffer bounds");
        }

        // Use temporary memory for offset copying
        var tempMemory = new Memory<T>(new T[count]);

        // Calculate byte offset for the source data
        long byteOffset = sourceOffset * Unsafe.SizeOf<T>();
        await _buffer.CopyToAsync(tempMemory, byteOffset, cancellationToken);

        var destSlice = destination.Slice(destinationOffset, count);
        await destSlice.CopyFromAsync(tempMemory, cancellationToken);
    }

    // Simplified implementations for remaining methods

    public Span<T> AsSpan() => throw new NotSupportedException("Direct span access not supported in production wrapper");
    public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException("Direct span access not supported in production wrapper");
    public Memory<T> AsMemory() => throw new NotSupportedException("Direct memory access not supported in production wrapper");
    public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotSupportedException("Direct memory access not supported in production wrapper");
    public DeviceMemory GetDeviceMemory() => throw new NotSupportedException("Device memory not supported in production wrapper");
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping not supported in production wrapper");
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping not supported in production wrapper");
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => throw new NotSupportedException("Memory mapping not supported in production wrapper");


    public void EnsureOnHost() { /* No-op for production */ }
    public void EnsureOnDevice() { /* No-op for production */ }
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public void Synchronize() { /* No-op for production */ }
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public void MarkHostDirty() { /* No-op for production */ }
    public void MarkDeviceDirty() { /* No-op for production */ }


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;


    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (offset + length > _length)
        {
            throw new ArgumentException("Slice extends beyond buffer");
        }


        return new TypedMemoryBufferView<T>(this, offset, length);
    }

    // Non-generic interface implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken) => _buffer.CopyFromAsync(source, offset, cancellationToken);


    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken) => _buffer.CopyToAsync(destination, offset, cancellationToken);


    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        var newLength = (int)(_buffer.SizeInBytes / Unsafe.SizeOf<TNew>());
        return new TypedMemoryBufferWrapper<TNew>(_buffer, newLength);
    }


    public void Dispose() => _buffer?.Dispose();
    public ValueTask DisposeAsync() => _buffer?.DisposeAsync() ?? ValueTask.CompletedTask;
}