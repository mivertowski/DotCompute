// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Abstractions;

/// <summary>
/// Synchronous memory manager interface for backends that don't require async operations.
/// </summary>
public interface ISyncMemoryManager : IDisposable
{
    /// <summary>
    /// Allocates memory with the specified size.
    /// </summary>
    ISyncMemoryBuffer Allocate(long sizeInBytes, MemoryOptions options = MemoryOptions.None);

    /// <summary>
    /// Allocates memory with the specified size and alignment.
    /// </summary>
    ISyncMemoryBuffer AllocateAligned(long sizeInBytes, int alignment, MemoryOptions options = MemoryOptions.None);

    /// <summary>
    /// Copies data between memory buffers.
    /// </summary>
    void Copy(ISyncMemoryBuffer source, ISyncMemoryBuffer destination, long sizeInBytes, long sourceOffset = 0, long destinationOffset = 0);

    /// <summary>
    /// Copies data from host memory to a device buffer.
    /// </summary>
    unsafe void CopyFromHost(void* source, ISyncMemoryBuffer destination, long sizeInBytes, long destinationOffset = 0);

    /// <summary>
    /// Copies data from a device buffer to host memory.
    /// </summary>
    unsafe void CopyToHost(ISyncMemoryBuffer source, void* destination, long sizeInBytes, long sourceOffset = 0);

    /// <summary>
    /// Fills a buffer with a specific byte value.
    /// </summary>
    void Fill(ISyncMemoryBuffer buffer, byte value, long sizeInBytes, long offset = 0);

    /// <summary>
    /// Zeros out a buffer.
    /// </summary>
    void Zero(ISyncMemoryBuffer buffer);

    /// <summary>
    /// Frees a memory buffer.
    /// </summary>
    void Free(ISyncMemoryBuffer buffer);

    /// <summary>
    /// Gets memory usage statistics.
    /// </summary>
    MemoryStatistics GetStatistics();

    /// <summary>
    /// Resets the memory manager, freeing all allocations.
    /// </summary>
    void Reset();
}