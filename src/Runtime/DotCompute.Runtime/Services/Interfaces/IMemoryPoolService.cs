// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Service for managing memory pools across accelerators
/// </summary>
public interface IMemoryPoolService
{
    /// <summary>
    /// Gets a memory pool for the specified accelerator
    /// </summary>
    /// <param name="acceleratorId">The accelerator identifier</param>
    /// <returns>The memory pool for the accelerator</returns>
    public IMemoryPool GetPool(string acceleratorId);

    /// <summary>
    /// Creates a new memory pool for an accelerator
    /// </summary>
    /// <param name="acceleratorId">The accelerator identifier</param>
    /// <param name="initialSize">The initial pool size in bytes</param>
    /// <param name="maxSize">The maximum pool size in bytes</param>
    /// <returns>The created memory pool</returns>
    public Task<IMemoryPool> CreatePoolAsync(string acceleratorId, long initialSize, long maxSize);

    /// <summary>
    /// Gets memory usage statistics across all pools
    /// </summary>
    /// <returns>Memory usage statistics</returns>
    public MemoryUsageStatistics GetUsageStatistics();

    /// <summary>
    /// Optimizes memory usage across all pools
    /// </summary>
    /// <returns>A task representing the optimization operation</returns>
    public Task OptimizeMemoryUsageAsync();

    /// <summary>
    /// Releases unused memory from all pools
    /// </summary>
    /// <returns>The amount of memory released in bytes</returns>
    public Task<long> ReleaseUnusedMemoryAsync();
}