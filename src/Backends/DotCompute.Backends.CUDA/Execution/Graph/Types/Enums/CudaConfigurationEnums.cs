// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Enums
{
    /// <summary>
    /// An cuda cache config enumeration.
    /// </summary>
    /// <summary>
    /// CUDA cache configuration
    /// </summary>
    public enum CudaCacheConfig : uint
    {
        PreferNone = 0,
        PreferShared = 1,
        PreferCache = 2,
        PreferEqual = 3
    }
    /// <summary>
    /// An cuda shared mem config enumeration.
    /// </summary>

    /// <summary>
    /// CUDA shared memory configuration
    /// </summary>
    public enum CudaSharedMemConfig : uint
    {
        BankSizeDefault = 0,
        BankSizeFourByte = 1,
        BankSizeEightByte = 2
    }
    /// <summary>
    /// An cuda limit enumeration.
    /// </summary>

    /// <summary>
    /// CUDA limit types
    /// </summary>
    public enum CudaLimit : uint
    {
        StackSize = 0,
        PrintfFifoSize = 1,
        MallocHeapSize = 2,
        DevRuntimeSyncDepth = 3,
        DevRuntimePendingLaunchCount = 4,
        MaxL2FetchGranularity = 5,
        PersistingL2CacheSize = 6
    }
}