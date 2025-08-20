// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services;


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
    IMemoryPool GetPool(string acceleratorId);

    /// <summary>
    /// Creates a new memory pool for an accelerator
    /// </summary>
    /// <param name="acceleratorId">The accelerator identifier</param>
    /// <param name="initialSize">The initial pool size in bytes</param>
    /// <param name="maxSize">The maximum pool size in bytes</param>
    /// <returns>The created memory pool</returns>
    Task<IMemoryPool> CreatePoolAsync(string acceleratorId, long initialSize, long maxSize);

    /// <summary>
    /// Gets memory usage statistics across all pools
    /// </summary>
    /// <returns>Memory usage statistics</returns>
    MemoryUsageStatistics GetUsageStatistics();

    /// <summary>
    /// Optimizes memory usage across all pools
    /// </summary>
    /// <returns>A task representing the optimization operation</returns>
    Task OptimizeMemoryUsageAsync();

    /// <summary>
    /// Releases unused memory from all pools
    /// </summary>
    /// <returns>The amount of memory released in bytes</returns>
    Task<long> ReleaseUnusedMemoryAsync();
}

/// <summary>
/// Service for managing unified memory across different accelerators
/// </summary>
public interface IUnifiedMemoryService
{
    /// <summary>
    /// Allocates unified memory that can be accessed by multiple accelerators
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes</param>
    /// <param name="acceleratorIds">The accelerator IDs that will access this memory</param>
    /// <returns>The allocated unified memory buffer</returns>
    Task<IMemoryBuffer> AllocateUnifiedAsync(long sizeInBytes, params string[] acceleratorIds);

    /// <summary>
    /// Migrates data between accelerators
    /// </summary>
    /// <param name="buffer">The memory buffer to migrate</param>
    /// <param name="sourceAcceleratorId">The source accelerator ID</param>
    /// <param name="targetAcceleratorId">The target accelerator ID</param>
    /// <returns>A task representing the migration operation</returns>
    Task MigrateAsync(IMemoryBuffer buffer, string sourceAcceleratorId, string targetAcceleratorId);

    /// <summary>
    /// Synchronizes memory coherence across accelerators
    /// </summary>
    /// <param name="buffer">The memory buffer to synchronize</param>
    /// <param name="acceleratorIds">The accelerator IDs to synchronize</param>
    /// <returns>A task representing the synchronization operation</returns>
    Task SynchronizeCoherenceAsync(IMemoryBuffer buffer, params string[] acceleratorIds);

    /// <summary>
    /// Gets memory coherence status for a buffer
    /// </summary>
    /// <param name="buffer">The memory buffer</param>
    /// <returns>The coherence status</returns>
    MemoryCoherenceStatus GetCoherenceStatus(IMemoryBuffer buffer);
}

/// <summary>
/// Memory pool interface
/// </summary>
public interface IMemoryPool : IDisposable
{
    /// <summary>
    /// Gets the accelerator ID this pool belongs to
    /// </summary>
    string AcceleratorId { get; }

    /// <summary>
    /// Gets the total pool size in bytes
    /// </summary>
    long TotalSize { get; }

    /// <summary>
    /// Gets the available size in bytes
    /// </summary>
    long AvailableSize { get; }

    /// <summary>
    /// Gets the used size in bytes
    /// </summary>
    long UsedSize { get; }

    /// <summary>
    /// Allocates memory from the pool
    /// </summary>
    /// <param name="sizeInBytes">The size to allocate</param>
    /// <returns>The allocated memory buffer</returns>
    Task<IMemoryBuffer> AllocateAsync(long sizeInBytes);

    /// <summary>
    /// Returns memory to the pool
    /// </summary>
    /// <param name="buffer">The memory buffer to return</param>
    /// <returns>A task representing the return operation</returns>
    Task ReturnAsync(IMemoryBuffer buffer);

    /// <summary>
    /// Defragments the memory pool
    /// </summary>
    /// <returns>A task representing the defragmentation operation</returns>
    Task DefragmentAsync();

    /// <summary>
    /// Gets pool statistics
    /// </summary>
    /// <returns>Pool statistics</returns>
    MemoryPoolStatistics GetStatistics();
}

/// <summary>
/// Memory usage statistics
/// </summary>
public class MemoryUsageStatistics
{
    /// <summary>
    /// Gets the total allocated memory across all pools
    /// </summary>
    public long TotalAllocated { get; init; }

    /// <summary>
    /// Gets the total available memory across all pools
    /// </summary>
    public long TotalAvailable { get; init; }

    /// <summary>
    /// Gets the fragmentation percentage
    /// </summary>
    public double FragmentationPercentage { get; init; }

    /// <summary>
    /// Gets per-accelerator statistics
    /// </summary>
    public Dictionary<string, AcceleratorMemoryStatistics> PerAcceleratorStats { get; init; } = new();

    /// <summary>
    /// Gets the timestamp when these statistics were collected
    /// </summary>
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Memory statistics for a specific accelerator
/// </summary>
public class AcceleratorMemoryStatistics
{
    /// <summary>
    /// Gets the accelerator ID
    /// </summary>
    public required string AcceleratorId { get; init; }

    /// <summary>
    /// Gets the total memory size
    /// </summary>
    public long TotalMemory { get; init; }

    /// <summary>
    /// Gets the allocated memory size
    /// </summary>
    public long AllocatedMemory { get; init; }

    /// <summary>
    /// Gets the available memory size
    /// </summary>
    public long AvailableMemory { get; init; }

    /// <summary>
    /// Gets the number of active allocations
    /// </summary>
    public int ActiveAllocations { get; init; }

    /// <summary>
    /// Gets the largest available block size
    /// </summary>
    public long LargestAvailableBlock { get; init; }
}

/// <summary>
/// Memory pool statistics
/// </summary>
public class MemoryPoolStatistics
{
    /// <summary>
    /// Gets the number of allocations performed
    /// </summary>
    public long AllocationCount { get; init; }

    /// <summary>
    /// Gets the number of deallocations performed
    /// </summary>
    public long DeallocationCount { get; init; }

    /// <summary>
    /// Gets the total bytes allocated
    /// </summary>
    public long TotalBytesAllocated { get; init; }

    /// <summary>
    /// Gets the total bytes deallocated
    /// </summary>
    public long TotalBytesDeallocated { get; init; }

    /// <summary>
    /// Gets the peak memory usage
    /// </summary>
    public long PeakMemoryUsage { get; init; }

    /// <summary>
    /// Gets the average allocation size
    /// </summary>
    public double AverageAllocationSize { get; init; }

    /// <summary>
    /// Gets the number of defragmentation operations performed
    /// </summary>
    public int DefragmentationCount { get; init; }
}

/// <summary>
/// Memory coherence status
/// </summary>
public enum MemoryCoherenceStatus
{
    /// <summary>
    /// Memory is coherent across all accelerators
    /// </summary>
    Coherent,

    /// <summary>
    /// Memory is incoherent and needs synchronization
    /// </summary>
    Incoherent,

    /// <summary>
    /// Memory coherence state is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// Memory is being synchronized
    /// </summary>
    Synchronizing
}
