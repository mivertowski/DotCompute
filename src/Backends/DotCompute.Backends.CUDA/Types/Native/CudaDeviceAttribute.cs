// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// CUDA device attributes that can be queried via cudaDeviceGetAttribute
    /// </summary>
    public enum CudaDeviceAttribute
    {
        /// <summary>
        /// Maximum number of threads per block
        /// </summary>
        MaxThreadsPerBlock = 1,

        /// <summary>
        /// Maximum x-dimension of a block
        /// </summary>
        MaxBlockDimX = 2,

        /// <summary>
        /// Maximum y-dimension of a block
        /// </summary>
        MaxBlockDimY = 3,

        /// <summary>
        /// Maximum z-dimension of a block
        /// </summary>
        MaxBlockDimZ = 4,

        /// <summary>
        /// Maximum x-dimension of a grid
        /// </summary>
        MaxGridDimX = 5,

        /// <summary>
        /// Maximum y-dimension of a grid
        /// </summary>
        MaxGridDimY = 6,

        /// <summary>
        /// Maximum z-dimension of a grid
        /// </summary>
        MaxGridDimZ = 7,

        /// <summary>
        /// Maximum shared memory available per block in bytes
        /// </summary>
        MaxSharedMemoryPerBlock = 8,

        /// <summary>
        /// Memory available on device for __constant__ variables in a CUDA kernel in bytes
        /// </summary>
        TotalConstantMemory = 9,

        /// <summary>
        /// Warp size in threads
        /// </summary>
        WarpSize = 10,

        /// <summary>
        /// Maximum pitch in bytes allowed by memory copies
        /// </summary>
        MaxPitch = 11,

        /// <summary>
        /// Maximum number of 32-bit registers available per block
        /// </summary>
        MaxRegistersPerBlock = 12,

        /// <summary>
        /// Typical clock frequency in kilohertz
        /// </summary>
        ClockRate = 13,

        /// <summary>
        /// Alignment requirement for textures
        /// </summary>
        TextureAlignment = 14,

        /// <summary>
        /// Device can possibly copy memory and execute a kernel concurrently
        /// </summary>
        AsyncEngineCount = 15,

        /// <summary>
        /// Device is integrated with host memory
        /// </summary>
        Integrated = 17,

        /// <summary>
        /// Device can map host memory into CUDA address space
        /// </summary>
        CanMapHostMemory = 19,

        /// <summary>
        /// Compute mode (see CudaComputeMode)
        /// </summary>
        ComputeMode = 20,

        /// <summary>
        /// Device can execute multiple kernels concurrently
        /// </summary>
        ConcurrentKernels = 31,

        /// <summary>
        /// Device has ECC support enabled
        /// </summary>
        ECCEnabled = 32,

        /// <summary>
        /// PCI bus ID of the device
        /// </summary>
        PCIBusID = 33,

        /// <summary>
        /// PCI device ID of the device
        /// </summary>
        PCIDeviceID = 34,

        /// <summary>
        /// Device is using TCC driver model
        /// </summary>
        TccDriver = 35,

        /// <summary>
        /// Peak memory clock frequency in kilohertz
        /// </summary>
        MemoryClockRate = 36,

        /// <summary>
        /// Global memory bus width in bits
        /// </summary>
        GlobalMemoryBusWidth = 37,

        /// <summary>
        /// Size of L2 cache in bytes
        /// </summary>
        L2CacheSize = 38,

        /// <summary>
        /// Maximum resident threads per multiprocessor
        /// </summary>
        MaxThreadsPerMultiProcessor = 39,

        /// <summary>
        /// Device supports stream priorities
        /// </summary>
        StreamPrioritiesSupported = 40,

        /// <summary>
        /// Device supports caching globals in L1
        /// </summary>
        GlobalL1CacheSupported = 41,

        /// <summary>
        /// Device supports caching locals in L1
        /// </summary>
        LocalL1CacheSupported = 42,

        /// <summary>
        /// Maximum shared memory available per multiprocessor in bytes
        /// </summary>
        MaxSharedMemoryPerMultiprocessor = 43,

        /// <summary>
        /// Maximum number of 32-bit registers available per multiprocessor
        /// </summary>
        MaxRegistersPerMultiprocessor = 44,

        /// <summary>
        /// Device can allocate managed memory
        /// </summary>
        ManagedMemory = 45,

        /// <summary>
        /// Device is on a multi-GPU board
        /// </summary>
        MultiGpuBoard = 46,

        /// <summary>
        /// Unique ID for a group of devices on the same multi-GPU board
        /// </summary>
        MultiGpuBoardGroupID = 47,

        /// <summary>
        /// Device supports host memory registration
        /// </summary>
        HostRegisterSupported = 48,

        /// <summary>
        /// Maximum number of blocks per multiprocessor
        /// </summary>
        MaxBlocksPerMultiprocessor = 49,

        /// <summary>
        /// Device can coherently access managed memory concurrently with the CPU
        /// </summary>
        ConcurrentManagedAccess = 50,

        /// <summary>
        /// Device supports Compute Preemption
        /// </summary>
        ComputePreemptionSupported = 51,

        /// <summary>
        /// Device can use host memory as registered memory
        /// </summary>
        CanUseHostPointerForRegisteredMem = 52,

        /// <summary>
        /// Device supports cooperative kernels
        /// </summary>
        CooperativeLaunch = 53,

        /// <summary>
        /// Device supports cooperative kernels across multiple devices
        /// </summary>
        CooperativeMultiDeviceLaunch = 54,

        /// <summary>
        /// Maximum shared memory available per block for dynamic allocation in bytes
        /// </summary>
        MaxSharedMemoryPerBlockOptin = 55,

        /// <summary>
        /// Device supports flushing denormalized values to zero
        /// </summary>
        CanFlushRemoteWrites = 56,

        /// <summary>
        /// Device supports host memory registration with read-only flag
        /// </summary>
        HostRegisterReadOnlySupported = 57,

        /// <summary>
        /// Shared memory regions can be allocated atomically
        /// </summary>
        PageableMemoryAccessUsesHostPageTables = 58,

        /// <summary>
        /// Host can directly access managed memory on the device without migration
        /// </summary>
        DirectManagedMemAccessFromHost = 59,

        /// <summary>
        /// Major compute capability version number
        /// </summary>
        ComputeCapabilityMajor = 75,

        /// <summary>
        /// Minor compute capability version number
        /// </summary>
        ComputeCapabilityMinor = 76,

        /// <summary>
        /// Device supports caching of memory accesses to remote GPUs
        /// </summary>
        CanUseStreamMemOps = 77,

        /// <summary>
        /// Device supports GPUDirect RDMA
        /// </summary>
        CanUse64BitStreamMemOps = 78,

        /// <summary>
        /// Device supports IPC events
        /// </summary>
        CanUseStreamWaitValueNor = 79,

        /// <summary>
        /// Device supports launching cooperative kernels via cudaLaunchCooperativeKernelMultiDevice
        /// </summary>
        CooperativeMultiDeviceUnmatchedFunc = 80,

        /// <summary>
        /// Device supports shared memory carveout configurations
        /// </summary>
        CooperativeMultiDeviceUnmatchedGridDim = 81,

        /// <summary>
        /// Device supports shared memory configurations
        /// </summary>
        CooperativeMultiDeviceUnmatchedBlockDim = 82,

        /// <summary>
        /// Device supports async memory operations
        /// </summary>
        CooperativeMultiDeviceUnmatchedSharedMem = 83,

        /// <summary>
        /// Device supports stream ordered memory allocator
        /// </summary>
        MemoryPoolsSupported = 115,

        /// <summary>
        /// Device supports unified virtual addressing with the host
        /// </summary>
        UnifiedAddressing = 18
    }

    /// <summary>
    /// Host register flags for cudaHostRegister
    /// </summary>
    [Flags]
    public enum CudaHostRegisterFlags : uint
    {
        /// <summary>
        /// Default flag
        /// </summary>
        None = 0,

        /// <summary>
        /// Memory is portable between CUDA contexts
        /// </summary>
        Portable = 1,

        /// <summary>
        /// Map the allocation to device space
        /// </summary>
        DeviceMap = 2,

        /// <summary>
        /// Memory is accessible from any stream on any device
        /// </summary>
        IoMemory = 4,

        /// <summary>
        /// Memory is read-only
        /// </summary>
        ReadOnly = 8
    }
}
