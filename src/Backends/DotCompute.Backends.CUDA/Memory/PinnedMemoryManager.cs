// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Buffers;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// A custom MemoryManager implementation that wraps CUDA pinned memory.
/// Provides safe access to unmanaged memory through the Memory<T> abstraction.
/// </summary>
internal sealed unsafe class PinnedMemoryManager<T> : MemoryManager<T>, IDisposable
    where T : unmanaged
{
    private readonly IntPtr _pinnedPtr;
    private readonly int _elementCount;
    private readonly IDisposable _parent;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of PinnedMemoryManager.
    /// </summary>
    /// <param name="pinnedPtr">Pointer to the pinned memory.</param>
    /// <param name="elementCount">Number of elements in the memory.</param>
    /// <param name="parent">Parent object that owns the memory (for lifetime management).</param>
    public PinnedMemoryManager(IntPtr pinnedPtr, int elementCount, IDisposable parent)
    {
        if (pinnedPtr == IntPtr.Zero)
            throw new ArgumentException("Pinned pointer cannot be zero", nameof(pinnedPtr));
        if (elementCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(elementCount), "Element count must be positive");

        _pinnedPtr = pinnedPtr;
        _elementCount = elementCount;
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    /// <summary>
    /// Returns a span over the pinned memory.
    /// </summary>
    /// <returns>A span over the managed memory.</returns>
    public override Span<T> GetSpan()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PinnedMemoryManager<T>));

        return new Span<T>(_pinnedPtr.ToPointer(), _elementCount);
    }

    /// <summary>
    /// Returns a memory handle for the pinned memory.
    /// </summary>
    /// <param name="elementIndex">The element index to start from.</param>
    /// <returns>A memory handle for the pinned memory.</returns>
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PinnedMemoryManager<T>));
        if (elementIndex < 0 || elementIndex >= _elementCount)
            throw new ArgumentOutOfRangeException(nameof(elementIndex));

        // Memory is already pinned, so we just return a handle with the offset
        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        var offsetPtr = _pinnedPtr + (elementIndex * elementSize);
        
        return new MemoryHandle(offsetPtr.ToPointer(), default, this);
    }

    /// <summary>
    /// Unpins the memory handle. Since the memory is managed by CUDA, this is a no-op.
    /// </summary>
    public override void Unpin()
    {
        // Memory is managed by CUDA pinned allocator, no action needed
    }

    /// <summary>
    /// Disposes the memory manager.
    /// Note: This doesn't free the underlying memory - that's managed by the parent allocator.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Note: We don't dispose the parent here as the memory manager
                // is owned by the parent, not the other way around
            }
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}