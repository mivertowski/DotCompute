// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Memory;

/// <summary>
/// Wraps an untyped memory buffer to provide typed access.
/// </summary>
internal class TypedMemoryBufferWrapper<T>(IUnifiedMemoryBuffer underlyingBuffer, int length) : IUnifiedMemoryBuffer<T>, IBufferWrapper where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer _underlyingBuffer = underlyingBuffer ?? throw new ArgumentNullException(nameof(underlyingBuffer));
    private readonly int _length = length;

    /// <summary>
    /// Gets the untyped buffer this wrapper delegates to. Used by memory managers to
    /// reconcile a typed wrapper handed back to the caller with the underlying buffer
    /// they actually track for allocation/deallocation accounting.
    /// </summary>
    public IUnifiedMemoryBuffer UnderlyingBuffer => _underlyingBuffer;
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>

    public int Length => _length;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes => _underlyingBuffer.SizeInBytes;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => null!; // Will be set by specific implementations
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _underlyingBuffer.Options;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _underlyingBuffer.State;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _underlyingBuffer.IsDisposed;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    // Recognize every host-resident state. The BufferState enum carries two parallel vocabularies:
    // HostOnly/Synchronized (used by UnifiedBuffer) and HostReady (used by the CPU memory manager's
    // buffer). The wrapper must accept both, otherwise a host-resident UnifiedBuffer wrapped here is
    // mis-reported as not-on-host.
    public bool IsOnHost => State is BufferState.HostReady or BufferState.HostDirty
        or BufferState.HostOnly or BufferState.Synchronized;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => State is BufferState.DeviceReady or BufferState.DeviceDirty
        or BufferState.DeviceOnly or BufferState.Synchronized;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public Span<T> AsSpan() => throw new NotSupportedException("Direct span access not supported for generic buffer wrapper");
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException("Direct span access not supported for generic buffer wrapper");
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public Memory<T> AsMemory() => throw new NotSupportedException("Direct memory access not supported for generic buffer wrapper");
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotSupportedException("Direct memory access not supported for generic buffer wrapper");
    /// <summary>
    /// Performs ensure on host.
    /// </summary>



    public void EnsureOnHost()
    {
        // State management is handled internally
    }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>


    public void EnsureOnDevice()
    {
        // State management is handled internally
    }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
    {
        EnsureOnHost();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
    {
        EnsureOnDevice();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs synchronize.
    /// </summary>


    public void Synchronize() { }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>


    public void MarkHostDirty() { /* State management is handled internally */ }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() { /* State management is handled internally */ }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        // The underlying buffer is byte-typed, so reinterpret the typed source as bytes
        // and delegate to the underlying buffer's byte-offset copy. Doing nothing here
        // silently discarded all writes, leaving subsequent reads returning zeros.
        var byteSource = MemoryMarshal.AsBytes(source.Span).ToArray();
        await _underlyingBuffer.CopyFromAsync<byte>(byteSource, 0, cancellationToken).ConfigureAwait(false);
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        // Read back from the byte-typed underlying buffer into a byte staging buffer,
        // then reinterpret those bytes as T. Doing nothing here returned all-zero data.
        var byteDestination = new byte[destination.Length * Unsafe.SizeOf<T>()];
        await _underlyingBuffer.CopyToAsync<byte>(byteDestination, 0, cancellationToken).ConfigureAwait(false);
        MemoryMarshal.Cast<byte, T>(byteDestination).CopyTo(destination.Span);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public void Dispose() => _underlyingBuffer.Dispose();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ValueTask DisposeAsync() => _underlyingBuffer.DisposeAsync();
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>

    // Additional interface methods

    public bool IsDirty => State is BufferState.HostDirty or BufferState.DeviceDirty;
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>


    public DeviceMemory GetDeviceMemory()
        // Return a default device memory handle



        => new(IntPtr.Zero, SizeInBytes);
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<T> Map(MapMode mode) => throw new NotSupportedException("Mapping not supported for generic buffer wrapper");
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public MappedMemory<T> MapRange(int offset, int length, MapMode mode) => throw new NotSupportedException("Mapping not supported for generic buffer wrapper");
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException("Mapping not supported for generic buffer wrapper");
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        var temp = new T[Length];
        await CopyToAsync(temp, cancellationToken);
        await destination.CopyFromAsync(temp, cancellationToken);
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public async ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var elementSize = Unsafe.SizeOf<T>();

        // Read the requested source range out of the byte-typed underlying buffer.
        var byteStaging = new byte[length * elementSize];
        await _underlyingBuffer.CopyToAsync<byte>(byteStaging, (long)sourceOffset * elementSize, cancellationToken).ConfigureAwait(false);
        var temp = MemoryMarshal.Cast<byte, T>(byteStaging).ToArray();

        // Write the range into the destination at the requested destination offset.
        if (destination is TypedMemoryBufferWrapper<T> typedDest)
        {
            await typedDest._underlyingBuffer.CopyFromAsync<byte>(
                MemoryMarshal.AsBytes(temp.AsSpan()).ToArray(),
                (long)destinationOffset * elementSize,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Generic fallback for non-wrapper destination buffers: read the destination,
            // splice the range in, and write it back.
            var full = new T[destination.Length];
            await destination.CopyToAsync(full, cancellationToken).ConfigureAwait(false);
            temp.AsSpan().CopyTo(full.AsSpan(destinationOffset, length));
            await destination.CopyFromAsync(full, cancellationToken).ConfigureAwait(false);
        }
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        // Default implementation - derived classes can override for better performance



        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
        // Default implementation - derived classes can override for better performance



        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>


    public IUnifiedMemoryBuffer<T> Slice(int offset, int length) => throw new NotSupportedException("Slicing not supported for generic buffer wrapper");


    /// <summary>
    /// Copies data from source memory to this buffer wrapper with offset support.
    /// </summary>
    /// <typeparam name="TSource">Type of elements to copy.</typeparam>
    /// <param name="source">Source data to copy from.</param>
    /// <param name="destinationOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyFromAsync<TSource>(ReadOnlyMemory<TSource> source, long destinationOffset, CancellationToken cancellationToken = default) where TSource : unmanaged => _underlyingBuffer.CopyFromAsync(source, destinationOffset, cancellationToken);

    /// <summary>
    /// Copies data from this buffer wrapper to destination memory with offset support.
    /// </summary>
    /// <typeparam name="TDest">Type of elements to copy.</typeparam>
    /// <param name="destination">Destination memory to copy to.</param>
    /// <param name="sourceOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public virtual ValueTask CopyToAsync<TDest>(Memory<TDest> destination, long sourceOffset, CancellationToken cancellationToken = default) where TDest : unmanaged => _underlyingBuffer.CopyToAsync(destination, sourceOffset, cancellationToken);
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => throw new NotSupportedException("Type conversion not supported for generic buffer wrapper");
}
