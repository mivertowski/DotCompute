// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Enums
{
    /// <summary>
    /// Specifies the preferred cache configuration for CUDA kernels.
    /// </summary>
    /// <remarks>
    /// On devices with configurable L1 cache and shared memory, this setting
    /// determines how the 64KB of on-chip memory is partitioned between the two.
    /// The actual configuration used depends on device capabilities and kernel requirements.
    /// </remarks>
    public enum CudaCacheConfig : int
    {
        /// <summary>
        /// No preference for cache configuration. Uses device default.
        /// </summary>
        PreferNone = 0,

        /// <summary>
        /// Prefer larger shared memory and smaller L1 cache.
        /// Typical split: 48KB shared memory, 16KB L1 cache.
        /// Use when kernel has high shared memory requirements.
        /// </summary>
        PreferShared = 1,

        /// <summary>
        /// Prefer larger L1 cache and smaller shared memory.
        /// Typical split: 48KB L1 cache, 16KB shared memory.
        /// Use when kernel benefits from L1 caching of global memory accesses.
        /// </summary>
        PreferCache = 2,

        /// <summary>
        /// Prefer equal partitioning between L1 cache and shared memory.
        /// Typical split: 32KB L1 cache, 32KB shared memory.
        /// Balanced configuration for mixed workloads.
        /// </summary>
        PreferEqual = 3
    }

    /// <summary>
    /// Specifies the bank size configuration for CUDA shared memory.
    /// </summary>
    /// <remarks>
    /// Shared memory is organized into banks to enable parallel access.
    /// Bank conflicts occur when multiple threads access the same bank,
    /// serializing the accesses. Proper bank size selection can reduce conflicts
    /// based on the data access patterns in your kernel.
    /// </remarks>
    public enum CudaSharedMemConfig : int
    {
        /// <summary>
        /// Use default shared memory bank size (device-dependent).
        /// Typically 4-byte banks on most devices.
        /// </summary>
        BankSizeDefault = 0,

        /// <summary>
        /// Configure shared memory with 4-byte (32-bit) banks.
        /// Optimal for kernels accessing 32-bit data types (float, int).
        /// Reduces bank conflicts when adjacent threads access 4-byte aligned data.
        /// </summary>
        BankSizeFourByte = 1,

        /// <summary>
        /// Configure shared memory with 8-byte (64-bit) banks.
        /// Optimal for kernels accessing 64-bit data types (double, long).
        /// Reduces bank conflicts when adjacent threads access 8-byte aligned data.
        /// Available on compute capability 2.0 and higher.
        /// </summary>
        BankSizeEightByte = 2
    }

    /// <summary>
    /// Specifies runtime limits that can be set for CUDA device execution.
    /// </summary>
    /// <remarks>
    /// These limits control various runtime resource allocations and behaviors.
    /// Limits are set per-device and affect kernel execution characteristics.
    /// Use cuCtxSetLimit or cudaDeviceSetLimit to configure these values.
    /// </remarks>
    public enum CudaLimit : int
    {
        /// <summary>
        /// Stack size in bytes for each GPU thread.
        /// Default is typically 1KB. Increase if kernels use deep recursion
        /// or large stack-allocated variables. Affects register usage.
        /// </summary>
        StackSize = 0,

        /// <summary>
        /// Size of the FIFO buffer used for printf output from device code.
        /// Default is typically 1MB. Increase if kernels produce extensive printf output.
        /// Buffer overflow causes output truncation.
        /// </summary>
        PrintfFifoSize = 1,

        /// <summary>
        /// Size of the heap used for device-side malloc/free operations.
        /// Default is typically 8MB. Increase if kernels dynamically allocate
        /// significant device memory. Requires compute capability 2.0+.
        /// </summary>
        MallocHeapSize = 2,

        /// <summary>
        /// Maximum nesting depth for device runtime kernel launches (dynamic parallelism).
        /// Default is 2. Controls how many levels of nested kernel launches are permitted.
        /// Requires compute capability 3.5+ for dynamic parallelism support.
        /// </summary>
        DevRuntimeSyncDepth = 3,

        /// <summary>
        /// Maximum number of outstanding device runtime kernel launches.
        /// Default is 2048. Controls concurrent pending kernel launches
        /// in dynamic parallelism scenarios. Requires compute capability 3.5+.
        /// </summary>
        DevRuntimePendingLaunchCount = 4,

        /// <summary>
        /// Maximum L2 cache fetch granularity in bytes.
        /// Controls the granularity of L2 cache line fetches.
        /// Compute capability 8.0+ only.
        /// </summary>
        MaxL2FetchGranularity = 5,

        /// <summary>
        /// Size of L2 cache reserved for persisting accesses in bytes.
        /// Allows reserving part of L2 cache for data with high reuse.
        /// Use cudaDeviceSetLimit to configure. Compute capability 8.0+ only.
        /// </summary>
        PersistingL2CacheSize = 6
    }
}
