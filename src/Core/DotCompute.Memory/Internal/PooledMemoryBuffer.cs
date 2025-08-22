// <copyright file="PooledMemoryBuffer.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Memory.Enums;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Represents a pooled memory buffer that can be reused.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class PooledMemoryBuffer<T> : IMemoryBuffer<T>, IDisposable where T : unmanaged
{
    private readonly MemoryPool<T> _pool;
    private readonly IMemoryOwner<T> _owner;
    private readonly int _length;
    private readonly object _syncLock = new();
    private BufferState _state = BufferState.Allocated;
    private int _refCount = 1;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PooledMemoryBuffer{T}"/> class.
    /// </summary>
    /// <param name="pool">The memory pool.</param>
    /// <param name="owner">The memory owner.</param>
    /// <param name="length">The buffer length.</param>
    internal PooledMemoryBuffer(MemoryPool<T> pool, IMemoryOwner<T> owner, int length)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _length = length;
    }

    /// <inheritdoc/>
    public int Length => _length;

    /// <inheritdoc/>
    public long SizeInBytes => _length * Unsafe.SizeOf<T>();

    /// <inheritdoc/>
    public BufferState State
    {
        get
        {
            lock (_syncLock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public Memory<T> GetHostMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_syncLock)
        {
            _state = BufferState.HostAccess;
            return _owner.Memory.Slice(0, _length);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Memory<T>> GetHostMemoryAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<Memory<T>>(GetHostMemory());
    }

    /// <inheritdoc/>
    public Span<T> GetHostSpan()
    {
        return GetHostMemory().Span;
    }

    /// <inheritdoc/>
    public IntPtr GetDevicePointer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_syncLock)
        {
            _state = BufferState.DeviceAccess;
            using var handle = _owner.Memory.Pin();
            return new IntPtr(handle.Pointer);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IntPtr> GetDevicePointerAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<IntPtr>(GetDevicePointer());
    }

    /// <inheritdoc/>
    public void CopyFrom(ReadOnlySpan<T> source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (source.Length != _length)
        {
            throw new ArgumentException($"Source length {source.Length} does not match buffer length {_length}", nameof(source));
        }

        source.CopyTo(GetHostSpan());
    }

    /// <inheritdoc/>
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        CopyFrom(source.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void CopyTo(Span<T> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (destination.Length < _length)
        {
            throw new ArgumentException($"Destination length {destination.Length} is less than buffer length {_length}", nameof(destination));
        }

        GetHostSpan().CopyTo(destination);
    }

    /// <inheritdoc/>
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        GetHostSpan().Clear();
    }

    /// <inheritdoc/>
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public IMemoryBuffer<T> Slice(int offset, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (offset < 0 || offset >= _length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        
        if (length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        lock (_syncLock)
        {
            _refCount++;
        }

        return new PooledMemoryBufferSlice<T>(this, offset, length);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_syncLock)
        {
            _refCount--;
            if (_refCount > 0)
            {
                return;
            }

            _disposed = true;
            _state = BufferState.Disposed;
        }

        _owner.Dispose();
        _pool.ReturnBuffer(this);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal method to increment reference count.
    /// </summary>
    internal void AddRef()
    {
        lock (_syncLock)
        {
            _refCount++;
        }
    }

    /// <summary>
    /// Internal method to decrement reference count.
    /// </summary>
    internal void Release()
    {
        lock (_syncLock)
        {
            _refCount--;
            if (_refCount <= 0)
            {
                Dispose();
            }
        }
    }
}

/// <summary>
/// Represents a slice of a pooled memory buffer.
/// </summary>
internal sealed class PooledMemoryBufferSlice<T> : IMemoryBuffer<T> where T : unmanaged
{
    private readonly PooledMemoryBuffer<T> _parent;
    private readonly int _offset;
    private readonly int _length;

    internal PooledMemoryBufferSlice(PooledMemoryBuffer<T> parent, int offset, int length)
    {
        _parent = parent;
        _offset = offset;
        _length = length;
    }

    public int Length => _length;
    public long SizeInBytes => _length * Unsafe.SizeOf<T>();
    public BufferState State => _parent.State;
    public bool IsDisposed => _parent.IsDisposed;

    public Memory<T> GetHostMemory() => _parent.GetHostMemory().Slice(_offset, _length);
    public ValueTask<Memory<T>> GetHostMemoryAsync(CancellationToken cancellationToken = default) 
        => new ValueTask<Memory<T>>(GetHostMemory());
    public Span<T> GetHostSpan() => GetHostMemory().Span;
    
    public IntPtr GetDevicePointer()
    {
        var basePtr = _parent.GetDevicePointer();
        return IntPtr.Add(basePtr, _offset * Unsafe.SizeOf<T>());
    }
    
    public ValueTask<IntPtr> GetDevicePointerAsync(CancellationToken cancellationToken = default)
        => new ValueTask<IntPtr>(GetDevicePointer());

    public void CopyFrom(ReadOnlySpan<T> source) => source.CopyTo(GetHostSpan());
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        CopyFrom(source.Span);
        return ValueTask.CompletedTask;
    }

    public void CopyTo(Span<T> destination) => GetHostSpan().CopyTo(destination);
    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }

    public void Clear() => GetHostSpan().Clear();
    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        Clear();
        return ValueTask.CompletedTask;
    }

    public IMemoryBuffer<T> Slice(int offset, int length)
    {
        if (offset < 0 || offset >= _length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        
        if (length < 0 || offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return new PooledMemoryBufferSlice<T>(_parent, _offset + offset, length);
    }

    public void Dispose()
    {
        _parent.Release();
    }
}