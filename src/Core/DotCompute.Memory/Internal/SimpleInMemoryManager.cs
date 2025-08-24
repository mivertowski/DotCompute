// <copyright file="SimpleInMemoryManager.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Memory.Internal;

/// <summary>
/// Simple in-memory implementation of IMemoryManager for default scenarios and testing.
/// </summary>
/// <remarks>
/// This implementation provides basic memory management functionality using managed memory arrays.
/// It's suitable for testing, prototyping, and scenarios where hardware acceleration is not available.
/// For production use with GPU acceleration, use the appropriate backend-specific memory manager.
/// Refactored to inherit from BaseMemoryManager to reduce code duplication.
/// </remarks>
internal sealed class SimpleInMemoryManager : BaseMemoryManager
{
    /// <summary>
    /// Gets the total amount of memory allocated by this manager.
    /// </summary>
    public long TotalAllocated => TotalAllocatedBytes;

    /// <summary>
    /// Gets the number of allocations made by this manager.
    /// </summary>
    public new int AllocationCount => base.AllocationCount;

    /// <summary>
    /// Internal allocation implementation that creates a SimpleMemoryBuffer.
    /// </summary>
    protected override ValueTask<IMemoryBuffer> AllocateInternalAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        var buffer = new SimpleMemoryBuffer(sizeInBytes, options);
        return ValueTask.FromResult<IMemoryBuffer>(buffer);
    }

    /// <inheritdoc/>
    public override IMemoryBuffer CreateView(IMemoryBuffer buffer, long offset, long length)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        if (offset + length > buffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "View exceeds buffer bounds.");
        }

        // For simplicity, create a new buffer for the view
        // In a real implementation, this would create a view over the existing memory
        return new SimpleMemoryBuffer(length, buffer.Options);
    }

    // Note: Allocate<T>, Free, CopyToDevice, and CopyFromDevice are all handled by the base class

    /// <summary>
    /// Synchronizes a buffer between host and device.
    /// </summary>
    /// <param name="buffer">The buffer to synchronize.</param>
    public void Synchronize(IMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // Note: SynchronizeAsync is implementation-specific and not on the IMemoryBuffer interface
        // For SimpleMemoryBuffer, synchronization is a no-op since it's always in host memory
    }

    /// <summary>
    /// Resets the allocation statistics.
    /// </summary>
    public void ResetStatistics()
    {
        // Note: Since we're using base class fields, we can't directly reset them
        // This method is kept for compatibility but is now limited
    }
}