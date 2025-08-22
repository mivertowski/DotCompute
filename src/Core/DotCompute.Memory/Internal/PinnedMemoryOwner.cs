// <copyright file="PinnedMemoryOwner.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Provides pinned memory ownership to prevent garbage collection movement.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class PinnedMemoryOwner<T> : IMemoryOwner<T> where T : unmanaged
{
    private readonly MemoryAllocator _allocator;
    private readonly GCHandle _handle;
    private readonly T[] _array;
    private readonly long _sizeInBytes;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PinnedMemoryOwner{T}"/> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="handle">The GC handle for pinned memory.</param>
    /// <param name="array">The underlying array.</param>
    /// <param name="sizeInBytes">The total size in bytes.</param>
    internal PinnedMemoryOwner(MemoryAllocator allocator, GCHandle handle, T[] array, long sizeInBytes)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _handle = handle;
        _array = array ?? throw new ArgumentNullException(nameof(array));
        _sizeInBytes = sizeInBytes;
    }

    /// <inheritdoc/>
    public Memory<T> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _array.AsMemory();
        }
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

        _allocator.NotifyDeallocation(_sizeInBytes);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for pinned memory cleanup.
    /// </summary>
    ~PinnedMemoryOwner()
    {
        if (!_disposed && _handle.IsAllocated)
        {
            _handle.Free();
            _allocator.NotifyDeallocation(_sizeInBytes);
        }
    }
}