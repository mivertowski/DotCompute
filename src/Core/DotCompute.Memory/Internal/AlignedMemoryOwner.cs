// <copyright file="AlignedMemoryOwner.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Provides aligned memory ownership for optimal SIMD and cache performance.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class AlignedMemoryOwner<T> : IMemoryOwner<T> where T : unmanaged
{
    private readonly MemoryAllocator _allocator;
    private readonly IntPtr _alignedPointer;
    private readonly int _length;
    private readonly long _sizeInBytes;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlignedMemoryOwner{T}"/> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="alignedPointer">The aligned memory pointer.</param>
    /// <param name="length">The number of elements.</param>
    /// <param name="sizeInBytes">The total size in bytes.</param>
    internal AlignedMemoryOwner(MemoryAllocator allocator, IntPtr alignedPointer, int length, long sizeInBytes)
    {
        _allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        _alignedPointer = alignedPointer;
        _length = length;
        _sizeInBytes = sizeInBytes;
    }

    /// <inheritdoc/>
    public unsafe Memory<T> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new UnmanagedMemoryManager<T>((T*)_alignedPointer, _length).Memory;
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
        _allocator.NotifyDeallocation(_sizeInBytes);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer for aligned memory cleanup.
    /// </summary>
    ~AlignedMemoryOwner()
    {
        if (!_disposed)
        {
            _allocator.NotifyDeallocation(_sizeInBytes);
        }
    }

    /// <summary>
    /// Internal unmanaged memory manager for aligned memory.
    /// </summary>
    private sealed unsafe class UnmanagedMemoryManager<TElement>(TElement* pointer, int length) : MemoryManager<TElement> where TElement : unmanaged
    {
        /// <inheritdoc/>
        public override Span<TElement> GetSpan() => new(pointer, length);

        /// <inheritdoc/>
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= length)
            {
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            }

            return new MemoryHandle(pointer + elementIndex);
        }

        /// <inheritdoc/>
        public override void Unpin()
        {
            // No-op for unmanaged memory
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // Memory is managed by AlignedMemoryOwner
        }
    }
}