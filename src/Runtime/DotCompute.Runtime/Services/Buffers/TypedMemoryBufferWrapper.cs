// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Runtime.Services.Buffers.Views;

namespace DotCompute.Runtime.Services.Buffers;

/// <summary>
/// Typed wrapper around untyped memory buffer to provide IUnifiedMemoryBuffer{T} interface.
/// </summary>
/// <typeparam name="T">The unmanaged element type.</typeparam>
public sealed class TypedMemoryBufferWrapper<T>(IUnifiedMemoryBuffer buffer, int length) : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
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
    public IAccelerator Accelerator => throw new NotSupportedException("Accelerator not available in production wrapper");
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => true; // Simplified for production wrapper
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => false;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty => false;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>

    // Properties from IUnifiedMemoryBuffer

    public long SizeInBytes => _buffer.SizeInBytes;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _buffer.Options;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _buffer.IsDisposed;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _buffer.State;
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

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
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination.Length > _length)
        {
            throw new ArgumentException("Destination buffer too small");
        }

        await _buffer.CopyToAsync(destination, 0, cancellationToken);
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

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
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

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
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    // Simplified implementations for remaining methods

    public Span<T> AsSpan() => throw new NotSupportedException("Direct span access not supported in production wrapper");
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException("Direct span access not supported in production wrapper");
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Memory<T> AsMemory() => throw new NotSupportedException("Direct memory access not supported in production wrapper");
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotSupportedException("Direct memory access not supported in production wrapper");
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>
    public DeviceMemory GetDeviceMemory() => throw new NotSupportedException("Device memory not supported in production wrapper");
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping not supported in production wrapper");
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => throw new NotSupportedException("Memory mapping not supported in production wrapper");
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => throw new NotSupportedException("Memory mapping not supported in production wrapper");
    /// <summary>
    /// Performs ensure on host.
    /// </summary>


    public void EnsureOnHost() { /* No-op for production */ }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() { /* No-op for production */ }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>
    public void Synchronize() { /* No-op for production */ }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>
    public void MarkHostDirty() { /* No-op for production */ }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() { /* No-op for production */ }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
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
            throw new ArgumentException("Slice extends beyond buffer");
        }


        return new TypedMemoryBufferView<T>(this, offset, length);
    }

    // Non-generic interface implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<U>(ReadOnlyMemory<U> source, long offset, CancellationToken cancellationToken) => _buffer.CopyFromAsync(source, offset, cancellationToken);


    ValueTask IUnifiedMemoryBuffer.CopyToAsync<U>(Memory<U> destination, long offset, CancellationToken cancellationToken) => _buffer.CopyToAsync(destination, offset, cancellationToken);
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        var newLength = (int)(_buffer.SizeInBytes / Unsafe.SizeOf<TNew>());
        return new TypedMemoryBufferWrapper<TNew>(_buffer, newLength);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public void Dispose() => _buffer?.Dispose();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ValueTask DisposeAsync() => _buffer?.DisposeAsync() ?? ValueTask.CompletedTask;
}
