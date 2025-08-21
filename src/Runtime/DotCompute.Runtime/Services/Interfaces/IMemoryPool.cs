// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Memory pool interface
/// </summary>
public interface IMemoryPool : IDisposable
{
    /// <summary>
    /// Gets the accelerator ID this pool belongs to
    /// </summary>
    public string AcceleratorId { get; }

    /// <summary>
    /// Gets the total pool size in bytes
    /// </summary>
    public long TotalSize { get; }

    /// <summary>
    /// Gets the available size in bytes
    /// </summary>
    public long AvailableSize { get; }

    /// <summary>
    /// Gets the used size in bytes
    /// </summary>
    public long UsedSize { get; }

    /// <summary>
    /// Allocates memory from the pool
    /// </summary>
    /// <param name="sizeInBytes">The size to allocate</param>
    /// <returns>The allocated memory buffer</returns>
    public Task<IMemoryBuffer> AllocateAsync(long sizeInBytes);

    /// <summary>
    /// Returns memory to the pool
    /// </summary>
    /// <param name="buffer">The memory buffer to return</param>
    /// <returns>A task representing the return operation</returns>
    public Task ReturnAsync(IMemoryBuffer buffer);

    /// <summary>
    /// Defragments the memory pool
    /// </summary>
    /// <returns>A task representing the defragmentation operation</returns>
    public Task DefragmentAsync();

    /// <summary>
    /// Gets pool statistics
    /// </summary>
    /// <returns>Pool statistics</returns>
    public MemoryPoolStatistics GetStatistics();
}