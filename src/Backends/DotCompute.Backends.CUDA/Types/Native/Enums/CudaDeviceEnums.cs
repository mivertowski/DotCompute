// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// CUDA cache configuration enumeration
    /// </summary>
    public enum CudaCacheConfig : uint
    {
        PreferNone = 0,
        PreferShared = 1,
        PreferCache = 2,
        PreferEqual = 3
    }

    /// <summary>
    /// CUDA shared memory configuration enumeration
    /// </summary>
    public enum CudaSharedMemConfig : uint
    {
        BankSizeDefault = 0,
        BankSizeFourByte = 1,
        BankSizeEightByte = 2
    }

    /// <summary>
    /// CUDA device limit types enumeration
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