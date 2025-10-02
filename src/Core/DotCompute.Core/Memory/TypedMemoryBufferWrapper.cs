// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Memory;

/// <summary>
/// Wraps an untyped memory buffer to provide typed access.
/// </summary>
internal class TypedMemoryBufferWrapper<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer _underlyingBuffer;
    private readonly int _length;


    public TypedMemoryBufferWrapper(IUnifiedMemoryBuffer underlyingBuffer, int length)
    {
        _underlyingBuffer = underlyingBuffer ?? throw new ArgumentNullException(nameof(underlyingBuffer));
        _length = length;
    }


    public int Length => _length;
    public long SizeInBytes => _underlyingBuffer.SizeInBytes;
    public IAccelerator Accelerator => null!; // Will be set by specific implementations
    public MemoryOptions Options => _underlyingBuffer.Options;
    public BufferState State => _underlyingBuffer.State;
    public bool IsDisposed => _underlyingBuffer.IsDisposed;
    public bool IsOnHost => State == BufferState.HostReady || State == BufferState.HostDirty;
    public bool IsOnDevice => State == BufferState.DeviceReady || State == BufferState.DeviceDirty;


    public Span<T> AsSpan() => throw new NotSupportedException("Direct span access not supported for generic buffer wrapper");


    public ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException("Direct span access not supported for generic buffer wrapper");


    public Memory<T> AsMemory() => throw new NotSupportedException("Direct memory access not supported for generic buffer wrapper");


    public ReadOnlyMemory<T> AsReadOnlyMemory() => throw new NotSupportedException("Direct memory access not supported for generic buffer wrapper");



    public void EnsureOnHost()
    {
        // State management is handled internally
    }


    public void EnsureOnDevice()
    {
        // State management is handled internally
    }


    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
    {
        EnsureOnHost();
        return ValueTask.CompletedTask;
    }


    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
    {
        EnsureOnDevice();
        return ValueTask.CompletedTask;
    }


    public void Synchronize() { }


    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public void MarkHostDirty() { /* State management is handled internally */ }
    public void MarkDeviceDirty() { /* State management is handled internally */ }


    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        // Copy from host memory to buffer
        // Since the underlying buffer doesn't have typed methods, we'll need to handle this



        => await ValueTask.CompletedTask;

    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        // Copy from buffer to host memory
        // Since the underlying buffer doesn't have typed methods, we'll need to handle this



        => await ValueTask.CompletedTask;


    public void Dispose() => _underlyingBuffer.Dispose();
    public ValueTask DisposeAsync() => _underlyingBuffer.DisposeAsync();

    // Additional interface methods

    public bool IsDirty => State == BufferState.HostDirty || State == BufferState.DeviceDirty;


    public DeviceMemory GetDeviceMemory()
        // Return a default device memory handle



        => new(IntPtr.Zero, SizeInBytes);

    public MappedMemory<T> Map(MapMode mode) => throw new NotSupportedException("Mapping not supported for generic buffer wrapper");


    public MappedMemory<T> MapRange(int offset, int length, MapMode mode) => throw new NotSupportedException("Mapping not supported for generic buffer wrapper");


    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default) => throw new NotSupportedException("Mapping not supported for generic buffer wrapper");


    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        var temp = new T[Length];
        await CopyToAsync(temp, cancellationToken);
        await destination.CopyFromAsync(temp, cancellationToken);
    }


    public async ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
    {
        // Copy a range from this buffer to another buffer
        // Since we don't have direct access to the underlying buffer's data, we'll use a temporary array
        var temp = new T[length];
        await CopyToAsync(temp, cancellationToken);
        await destination.CopyFromAsync(temp, cancellationToken);
    }


    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        // Default implementation - derived classes can override for better performance



        => ValueTask.CompletedTask;


    public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
        // Default implementation - derived classes can override for better performance



        => ValueTask.CompletedTask;


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

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged => throw new NotSupportedException("Type conversion not supported for generic buffer wrapper");
}