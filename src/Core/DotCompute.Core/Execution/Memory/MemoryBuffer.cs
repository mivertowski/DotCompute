// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Execution.Memory;

/// <summary>
/// Simple memory buffer implementation for execution contexts.
/// This is a placeholder implementation marked as TODO in the original code.
/// </summary>
internal sealed class MemoryBuffer : IUnifiedMemoryBuffer<byte>
{
    private readonly byte[] _data;
    private bool _disposed;

    public MemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        if (sizeInBytes > int.MaxValue)
        {

            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size exceeds maximum array size");
        }


        _data = new byte[sizeInBytes];
        SizeInBytes = sizeInBytes;
        Options = options;
    }

    public int Length => _data.Length;
    public long SizeInBytes { get; }
    public IAccelerator Accelerator => null!; // CPU-based buffer
    public BufferState State => BufferState.HostReady;
    public MemoryOptions Options { get; }
    public bool IsDisposed => _disposed;
    public bool IsOnHost => true;
    public bool IsOnDevice => false;
    public bool IsDirty => false;

    public Span<byte> AsSpan() => _data.AsSpan();
    public ReadOnlySpan<byte> AsReadOnlySpan() => _data.AsSpan();
    public Memory<byte> AsMemory() => _data.AsMemory();
    public ReadOnlyMemory<byte> AsReadOnlyMemory() => _data.AsMemory();
    
    public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;
    
    public MappedMemory<byte> Map(MapMode mode = MapMode.ReadWrite) 
        => throw new NotSupportedException("Memory mapping not supported");
    
    public MappedMemory<byte> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
        => throw new NotSupportedException("Memory mapping not supported");
    
    public ValueTask<MappedMemory<byte>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Memory mapping not supported");

    public void EnsureOnHost() { }
    public void EnsureOnDevice() { }
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) 
        => ValueTask.CompletedTask;
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public void Synchronize() { }
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public void MarkHostDirty() { }
    public void MarkDeviceDirty() { }

    public ValueTask CopyFromAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_data);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        _data.CopyTo(destination);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<byte> destination, CancellationToken cancellationToken = default) => destination.CopyFromAsync(_data, cancellationToken);

    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<byte> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        var slice = _data.AsMemory(sourceOffset, count);
        return destination.CopyFromAsync(slice, cancellationToken);
    }

    public ValueTask FillAsync(byte value, CancellationToken cancellationToken = default)
    {
        _data.AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(byte value, int offset, int count, CancellationToken cancellationToken = default)
    {
        _data.AsSpan(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }

    public IUnifiedMemoryBuffer<byte> Slice(int offset, int length)
        => throw new NotSupportedException("Slicing not supported");

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
        => throw new NotSupportedException("Type conversion not supported");

    public void Dispose() => _disposed = true;

    /// <summary>
    /// Copies data from source memory to this buffer with offset support.
    /// </summary>
    /// <typeparam name="T">Type of elements to copy.</typeparam>
    /// <param name="source">Source data to copy from.</param>
    /// <param name="destinationOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long destinationOffset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            var sourceBytes = MemoryMarshal.AsBytes(source.Span);
            sourceBytes.CopyTo(_data.AsSpan((int)destinationOffset));
        }
        else
        {
            var sourceBytes = MemoryMarshal.AsBytes(source.Span);
            sourceBytes.CopyTo(_data.AsSpan((int)destinationOffset));
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Copies data from this buffer to destination memory with offset support.
    /// </summary>
    /// <typeparam name="T">Type of elements to copy.</typeparam>
    /// <param name="destination">Destination memory to copy to.</param>
    /// <param name="sourceOffset">Offset into this buffer in bytes.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Completed task when copy finishes.</returns>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long sourceOffset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            var destBytes = MemoryMarshal.AsBytes(destination.Span);
            _data.AsSpan((int)sourceOffset, destBytes.Length).CopyTo(destBytes);
        }
        else
        {
            var destBytes = MemoryMarshal.AsBytes(destination.Span);
            _data.AsSpan((int)sourceOffset, destBytes.Length).CopyTo(destBytes);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}