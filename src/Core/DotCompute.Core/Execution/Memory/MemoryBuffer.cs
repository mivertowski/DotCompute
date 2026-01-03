// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Execution.Memory;

/// <summary>
/// Simple memory buffer implementation for execution contexts.
/// </summary>
/// <remarks>
/// This is a legacy placeholder implementation. For new code, use:
/// <list type="bullet">
/// <item><see cref="DotCompute.Memory.UnifiedBuffer{T}"/> for general unified memory</item>
/// <item><see cref="DotCompute.Memory.OptimizedUnifiedBuffer{T}"/> for high-performance scenarios</item>
/// </list>
/// </remarks>
[Obsolete("Use UnifiedBuffer<T> or OptimizedUnifiedBuffer<T> from DotCompute.Memory namespace. " +
          "This type will be removed in v1.0. See docs/migration/buffer-migration.md for details.")]
internal sealed class MemoryBuffer : IUnifiedMemoryBuffer<byte>
{
    private readonly byte[] _data;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the MemoryBuffer class.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">The options.</param>

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
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>

    public int Length => _data.Length;
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; }
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => null!; // CPU-based buffer
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => BufferState.HostReady;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options { get; }
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _disposed;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => true;
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
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Span<byte> AsSpan() => _data.AsSpan();
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlySpan<byte> AsReadOnlySpan() => _data.AsSpan();
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public Memory<byte> AsMemory() => _data.AsMemory();
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public ReadOnlyMemory<byte> AsReadOnlyMemory() => _data.AsMemory();
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>


    public DeviceMemory GetDeviceMemory() => DeviceMemory.Invalid;
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public MappedMemory<byte> Map(MapMode mode = MapMode.ReadWrite)

        => throw new NotSupportedException("Memory mapping not supported");
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public MappedMemory<byte> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
        => throw new NotSupportedException("Memory mapping not supported");
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public ValueTask<MappedMemory<byte>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Memory mapping not supported");
    /// <summary>
    /// Performs ensure on host.
    /// </summary>

    public void EnsureOnHost() { }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public void EnsureOnDevice() { }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)

        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
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
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>


    public void MarkHostDirty() { }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public void MarkDeviceDirty() { }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_data);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        _data.CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<byte> destination, CancellationToken cancellationToken = default) => destination.CopyFromAsync(_data, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<byte> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        var slice = _data.AsMemory(sourceOffset, count);
        return destination.CopyFromAsync(slice, cancellationToken);
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(byte value, CancellationToken cancellationToken = default)
    {
        _data.AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(byte value, int offset, int count, CancellationToken cancellationToken = default)
    {
        _data.AsSpan(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<byte> Slice(int offset, int length)
        => throw new NotSupportedException("Slicing not supported");
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
        => throw new NotSupportedException("Type conversion not supported");
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
