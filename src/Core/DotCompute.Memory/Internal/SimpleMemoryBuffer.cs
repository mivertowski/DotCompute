// <copyright file="SimpleMemoryBuffer.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using MemoryOptions = DotCompute.Abstractions.MemoryOptions;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Simple in-memory buffer implementation for testing and default scenarios.
/// </summary>
/// <remarks>
/// This is a basic implementation that uses managed memory arrays for storage.
/// It's suitable for testing, prototyping, and scenarios where hardware acceleration is not available.
/// </remarks>
internal sealed class SimpleMemoryBuffer : IMemoryBuffer, IDisposable
{
    private readonly byte[] _data;
    private readonly GCHandle _handle;
    private readonly long _sizeInBytes;
    private readonly DotCompute.Abstractions.MemoryOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleMemoryBuffer"/> class.
    /// </summary>
    /// <param name="sizeInBytes">The size of the buffer in bytes.</param>
    /// <param name="options">Memory allocation options.</param>
    public SimpleMemoryBuffer(long sizeInBytes, DotCompute.Abstractions.MemoryOptions options = DotCompute.Abstractions.MemoryOptions.None)
    {
        if (sizeInBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size must be positive.");
        }

        if (sizeInBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size exceeds maximum array size.");
        }

        _sizeInBytes = sizeInBytes;
        _options = options;
        _data = new byte[sizeInBytes];

        // Pin the memory if requested
        if ((options & DotCompute.Abstractions.MemoryOptions.Pinned) != 0)
        {
            _handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        }
    }

    /// <inheritdoc/>
    public long SizeInBytes => _sizeInBytes;

    /// <inheritdoc/>
    public DotCompute.Abstractions.MemoryOptions Options => _options;

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public ValueTask CopyFromHostAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (source.Length > _sizeInBytes)
        {
            throw new ArgumentException($"Source data ({source.Length} bytes) exceeds buffer size ({_sizeInBytes} bytes).", nameof(source));
        }

        source.Span.CopyTo(_data.AsSpan());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sourceBytes = MemoryMarshal.AsBytes(source.Span);
        return CopyFromHostAsync(new ReadOnlyMemory<byte>(sourceBytes.ToArray()), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var sourceBytes = MemoryMarshal.AsBytes(source.Span);
        var offsetBytes = checked((int)offset);
        var targetSpan = _data.AsSpan(offsetBytes);
        sourceBytes.CopyTo(targetSpan);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CopyToHostAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (destination.Length < _sizeInBytes)
        {
            throw new ArgumentException($"Destination buffer ({destination.Length} bytes) is smaller than source ({_sizeInBytes} bytes).", nameof(destination));
        }

        _data.AsSpan().CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var destinationBytes = MemoryMarshal.AsBytes(destination.Span);
        return CopyToHostAsync(new Memory<byte>(destinationBytes.ToArray()), cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken = default) where T : unmanaged
    {
        var destinationBytes = MemoryMarshal.AsBytes(destination.Span);
        var offsetBytes = checked((int)offset);
        var sourceSpan = _data.AsSpan(offsetBytes);
        sourceSpan.CopyTo(destinationBytes);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Array.Clear(_data);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask FillAsync(byte value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Array.Fill(_data, value);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IntPtr> GetDevicePointerAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (_handle.IsAllocated)
        {
            return ValueTask.FromResult(_handle.AddrOfPinnedObject());
        }

        // For unpinned memory, this would typically fail in a real device scenario
        // Here we return IntPtr.Zero to indicate no device pointer is available
        return ValueTask.FromResult(IntPtr.Zero);
    }

    /// <inheritdoc/>
    public ValueTask<Memory<byte>> GetHostMemoryAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ValueTask.FromResult(_data.AsMemory());
    }

    /// <inheritdoc/>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // No-op for simple memory buffer as there's no device/host separation
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handle.IsAllocated)
        {
            _handle.Free();
        }

        // Clear sensitive data if requested
        if ((_options & DotCompute.Abstractions.MemoryOptions.SecureClear) != 0)
        {
            Array.Clear(_data);
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Finalizer to ensure resources are released.
    /// </summary>
    ~SimpleMemoryBuffer()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }
}