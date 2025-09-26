// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Configuration options for memory pooling in pipeline execution.
/// Controls pool behavior, allocation strategies, and memory management policies.
/// </summary>
public sealed class MemoryPoolOptions
{
    /// <summary>
    /// Gets or sets whether memory pooling is enabled.
    /// </summary>
    public bool EnablePooling { get; set; } = true;

    /// <summary>
    /// Gets or sets the initial size of the memory pool in bytes.
    /// </summary>
    public long InitialPoolSize { get; set; } = 64 * 1024 * 1024; // 64 MB

    /// <summary>
    /// Gets or sets the maximum size of the memory pool in bytes.
    /// </summary>
    public long MaxPoolSize { get; set; } = 1024 * 1024 * 1024; // 1 GB

    /// <summary>
    /// Gets or sets the growth factor when expanding the pool.
    /// </summary>
    public double GrowthFactor { get; set; } = 1.5;

    /// <summary>
    /// Gets or sets the minimum block size for pool allocations.
    /// </summary>
    public int MinBlockSize { get; set; } = 4096; // 4 KB

    /// <summary>
    /// Gets or sets the maximum block size for pool allocations.
    /// </summary>
    public int MaxBlockSize { get; set; } = 16 * 1024 * 1024; // 16 MB

    /// <summary>
    /// Gets or sets the memory alignment for allocations.
    /// </summary>
    public int MemoryAlignment { get; set; } = 64; // 64-byte alignment for SIMD

    /// <summary>
    /// Gets or sets the allocation strategy for the memory pool.
    /// </summary>
    public PoolAllocationStrategy AllocationStrategy { get; set; } = PoolAllocationStrategy.BestFit;

    /// <summary>
    /// Gets or sets the deallocation strategy for the memory pool.
    /// </summary>
    public PoolDeallocationStrategy DeallocationStrategy { get; set; } = PoolDeallocationStrategy.Immediate;

    /// <summary>
    /// Gets or sets the memory locking mode for pool operations.
    /// </summary>
    public MemoryLockMode LockMode { get; set; } = MemoryLockMode.ReadWrite;

    /// <summary>
    /// Gets or sets whether to use thread-local pools for better performance.
    /// </summary>
    public bool UseThreadLocalPools { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of thread-local pools.
    /// </summary>
    public int MaxThreadLocalPools { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the pool cleanup interval for releasing unused memory.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the threshold for triggering pool cleanup (utilization percentage).
    /// </summary>
    public double CleanupThreshold { get; set; } = 0.1; // 10% utilization

    /// <summary>
    /// Gets or sets whether to pre-allocate memory blocks on pool initialization.
    /// </summary>
    public bool PreAllocateBlocks { get; set; }


    /// <summary>
    /// Gets or sets the number of blocks to pre-allocate.
    /// </summary>
    public int PreAllocatedBlockCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets whether to zero-initialize allocated memory.
    /// </summary>
    public bool ZeroInitialize { get; set; }


    /// <summary>
    /// Gets or sets whether to validate pool integrity during operations.
    /// </summary>
    public bool ValidateIntegrity { get; set; }


    /// <summary>
    /// Gets or sets whether to collect pool usage statistics.
    /// </summary>
    public bool CollectStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets the memory pressure threshold for triggering aggressive cleanup.
    /// </summary>
    public double MemoryPressureThreshold { get; set; } = 0.8; // 80% of max pool size

    /// <summary>
    /// Gets or sets whether to enable memory pressure monitoring.
    /// </summary>
    public bool EnableMemoryPressureMonitoring { get; set; } = true;

    /// <summary>
    /// Gets or sets the fragmentation threshold for triggering defragmentation.
    /// </summary>
    public double FragmentationThreshold { get; set; } = 0.3; // 30% fragmentation

    /// <summary>
    /// Gets or sets whether to enable automatic defragmentation.
    /// </summary>
    public bool EnableDefragmentation { get; set; } = true;

    /// <summary>
    /// Gets or sets the policy for handling allocation failures.
    /// </summary>
    public AllocationFailurePolicy FailurePolicy { get; set; } = AllocationFailurePolicy.GrowPool;

    /// <summary>
    /// Gets or sets whether to use large pages for better performance.
    /// </summary>
    public bool UseLargePages { get; set; }


    /// <summary>
    /// Gets or sets whether to pin memory to prevent paging.
    /// </summary>
    public bool PinMemory { get; set; }


    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    /// <returns>A new instance with the same configuration</returns>
    public MemoryPoolOptions Clone()
    {
        return new MemoryPoolOptions
        {
            EnablePooling = EnablePooling,
            InitialPoolSize = InitialPoolSize,
            MaxPoolSize = MaxPoolSize,
            GrowthFactor = GrowthFactor,
            MinBlockSize = MinBlockSize,
            MaxBlockSize = MaxBlockSize,
            MemoryAlignment = MemoryAlignment,
            AllocationStrategy = AllocationStrategy,
            DeallocationStrategy = DeallocationStrategy,
            LockMode = LockMode,
            UseThreadLocalPools = UseThreadLocalPools,
            MaxThreadLocalPools = MaxThreadLocalPools,
            CleanupInterval = CleanupInterval,
            CleanupThreshold = CleanupThreshold,
            PreAllocateBlocks = PreAllocateBlocks,
            PreAllocatedBlockCount = PreAllocatedBlockCount,
            ZeroInitialize = ZeroInitialize,
            ValidateIntegrity = ValidateIntegrity,
            CollectStatistics = CollectStatistics,
            MemoryPressureThreshold = MemoryPressureThreshold,
            EnableMemoryPressureMonitoring = EnableMemoryPressureMonitoring,
            FragmentationThreshold = FragmentationThreshold,
            EnableDefragmentation = EnableDefragmentation,
            FailurePolicy = FailurePolicy,
            UseLargePages = UseLargePages,
            PinMemory = PinMemory
        };
    }

    /// <summary>
    /// Creates default options optimized for performance.
    /// </summary>
    /// <returns>Performance-optimized memory pool options</returns>
    public static MemoryPoolOptions Performance()
    {
        return new MemoryPoolOptions
        {
            InitialPoolSize = 128 * 1024 * 1024, // 128 MB
            MaxPoolSize = 2L * 1024 * 1024 * 1024, // 2 GB
            GrowthFactor = 2.0,
            AllocationStrategy = PoolAllocationStrategy.FirstFit,
            UseThreadLocalPools = true,
            PreAllocateBlocks = true,
            PreAllocatedBlockCount = 32,
            UseLargePages = true,
            PinMemory = true
        };
    }

    /// <summary>
    /// Creates default options optimized for memory efficiency.
    /// </summary>
    /// <returns>Memory-efficient pool options</returns>
    public static MemoryPoolOptions MemoryEfficient()
    {
        return new MemoryPoolOptions
        {
            InitialPoolSize = 16 * 1024 * 1024, // 16 MB
            MaxPoolSize = 256 * 1024 * 1024, // 256 MB
            GrowthFactor = 1.25,
            AllocationStrategy = PoolAllocationStrategy.BestFit,
            CleanupInterval = TimeSpan.FromMinutes(1),
            CleanupThreshold = 0.05, // 5% utilization
            EnableDefragmentation = true,
            FragmentationThreshold = 0.2 // 20% fragmentation
        };
    }

    /// <summary>
    /// Creates default options for debugging and development.
    /// </summary>
    /// <returns>Debug-friendly pool options</returns>
    public static MemoryPoolOptions Debug()
    {
        return new MemoryPoolOptions
        {
            InitialPoolSize = 32 * 1024 * 1024, // 32 MB
            MaxPoolSize = 512 * 1024 * 1024, // 512 MB
            ZeroInitialize = true,
            ValidateIntegrity = true,
            CollectStatistics = true,
            UseThreadLocalPools = false // Easier debugging
        };
    }
}

/// <summary>
/// Strategies for allocating memory blocks in the pool.
/// </summary>
public enum PoolAllocationStrategy
{
    /// <summary>
    /// Allocate from the first block that fits the request.
    /// </summary>
    FirstFit,

    /// <summary>
    /// Allocate from the best-fitting block to minimize fragmentation.
    /// </summary>
    BestFit,

    /// <summary>
    /// Allocate from the worst-fitting block to leave larger contiguous spaces.
    /// </summary>
    WorstFit,

    /// <summary>
    /// Allocate from the next available block after the last allocation.
    /// </summary>
    NextFit,

    /// <summary>
    /// Allocate using a buddy system for efficient power-of-two sizing.
    /// </summary>
    BuddySystem,

    /// <summary>
    /// Allocate using segregated free lists for different size classes.
    /// </summary>
    SegregatedFreeList
}

/// <summary>
/// Strategies for deallocating memory blocks in the pool.
/// </summary>
public enum PoolDeallocationStrategy
{
    /// <summary>
    /// Immediately return the block to the pool.
    /// </summary>
    Immediate,

    /// <summary>
    /// Defer deallocation until cleanup or pressure threshold.
    /// </summary>
    Deferred,

    /// <summary>
    /// Batch multiple deallocations for efficiency.
    /// </summary>
    Batched,

    /// <summary>
    /// Use reference counting to delay actual deallocation.
    /// </summary>
    ReferenceCounted
}

/// <summary>
/// Policies for handling memory allocation failures.
/// </summary>
public enum AllocationFailurePolicy
{
    /// <summary>
    /// Throw an exception on allocation failure.
    /// </summary>
    ThrowException,

    /// <summary>
    /// Attempt to grow the pool and retry allocation.
    /// </summary>
    GrowPool,

    /// <summary>
    /// Trigger cleanup and retry allocation.
    /// </summary>
    CleanupAndRetry,

    /// <summary>
    /// Fall back to system allocation outside the pool.
    /// </summary>
    FallbackToSystem,

    /// <summary>
    /// Return null and let the caller handle the failure.
    /// </summary>
    ReturnNull
}