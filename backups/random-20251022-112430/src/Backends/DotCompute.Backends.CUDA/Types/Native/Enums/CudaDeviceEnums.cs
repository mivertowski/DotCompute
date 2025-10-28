// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda cache config enumeration.
    /// </summary>
    /// <summary>
    /// CUDA cache configuration enumeration
    /// </summary>
    public enum CudaCacheConfig : uint
    {
        /// <summary>
        /// No preference between shared memory and L1 cache.
        /// </summary>
        PreferNone = 0,

        /// <summary>
        /// Prefer larger shared memory and smaller L1 cache.
        /// </summary>
        PreferShared = 1,

        /// <summary>
        /// Prefer larger L1 cache and smaller shared memory.
        /// </summary>
        PreferCache = 2,

        /// <summary>
        /// Prefer equal split between shared memory and L1 cache.
        /// </summary>
        PreferEqual = 3
    }
    /// <summary>
    /// An cuda shared mem config enumeration.
    /// </summary>

    /// <summary>
    /// CUDA shared memory configuration enumeration
    /// </summary>
    public enum CudaSharedMemConfig : uint
    {
        /// <summary>
        /// Use the default shared memory bank size for the device.
        /// </summary>
        BankSizeDefault = 0,

        /// <summary>
        /// Configure shared memory banks to 4-byte wide banks.
        /// </summary>
        BankSizeFourByte = 1,

        /// <summary>
        /// Configure shared memory banks to 8-byte wide banks.
        /// </summary>
        BankSizeEightByte = 2
    }
    /// <summary>
    /// An cuda limit enumeration.
    /// </summary>

    /// <summary>
    /// CUDA device limit types enumeration
    /// </summary>
    public enum CudaLimit : uint
    {
        /// <summary>
        /// Stack size limit for each GPU thread in bytes.
        /// </summary>
        StackSize = 0,

        /// <summary>
        /// FIFO buffer size for printf output from device kernels in bytes.
        /// </summary>
        PrintfFifoSize = 1,

        /// <summary>
        /// Heap size for malloc operations within device code in bytes.
        /// </summary>
        MallocHeapSize = 2,

        /// <summary>
        /// Maximum depth of synchronization nesting in device runtime.
        /// </summary>
        DevRuntimeSyncDepth = 3,

        /// <summary>
        /// Maximum number of pending kernel launches from device runtime.
        /// </summary>
        DevRuntimePendingLaunchCount = 4,

        /// <summary>
        /// Maximum fetch granularity for L2 cache access in bytes.
        /// </summary>
        MaxL2FetchGranularity = 5,

        /// <summary>
        /// Size of persisting L2 cache in bytes.
        /// </summary>
        PersistingL2CacheSize = 6
    }
}