// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Enums
{
    /// <summary>
    /// Defines attributes that describe CUDA device capabilities and characteristics.
    /// </summary>
    /// <remarks>
    /// These attributes map directly to CUDA driver API device attributes and are used
    /// to query GPU hardware capabilities, memory configuration, and supported features.
    /// Values are typically queried using cuDeviceGetAttribute or similar API calls.
    /// </remarks>
    public enum CudaDeviceAttribute : int
    {
        /// <summary>
        /// No attribute specified (invalid value).
        /// </summary>
        None = 0,

        /// <summary>
        /// Maximum number of threads per block. Typical values: 512, 1024.
        /// </summary>
        MaxThreadsPerBlock = 1,

        /// <summary>
        /// Maximum X dimension of a thread block.
        /// </summary>
        MaxBlockDimX = 2,

        /// <summary>
        /// Maximum Y dimension of a thread block.
        /// </summary>
        MaxBlockDimY = 3,

        /// <summary>
        /// Maximum Z dimension of a thread block.
        /// </summary>
        MaxBlockDimZ = 4,

        /// <summary>
        /// Maximum X dimension of a grid. Typically 2^31-1 for modern GPUs.
        /// </summary>
        MaxGridDimX = 5,

        /// <summary>
        /// Maximum Y dimension of a grid. Typically 65535 for most GPUs.
        /// </summary>
        MaxGridDimY = 6,

        /// <summary>
        /// Maximum Z dimension of a grid. Typically 65535 for most GPUs.
        /// </summary>
        MaxGridDimZ = 7,

        /// <summary>
        /// Maximum amount of shared memory per block in bytes. Typical range: 48KB-96KB.
        /// </summary>
        MaxSharedMemoryPerBlock = 8,

        /// <summary>
        /// Total constant memory available in bytes. Typically 64KB.
        /// </summary>
        TotalConstantMemory = 9,

        /// <summary>
        /// Warp size in threads. Always 32 for NVIDIA GPUs.
        /// </summary>
        WarpSize = 10,

        /// <summary>
        /// Maximum pitch in bytes allowed by memory copies.
        /// </summary>
        MaxPitch = 11,

        /// <summary>
        /// Maximum number of 32-bit registers per block.
        /// </summary>
        MaxRegistersPerBlock = 12,

        /// <summary>
        /// Clock frequency in kilohertz for the GPU cores.
        /// </summary>
        ClockRate = 13,

        /// <summary>
        /// Texture alignment requirement in bytes.
        /// </summary>
        TextureAlignment = 14,

        /// <summary>
        /// Device can concurrently copy memory and execute kernel (deprecated).
        /// </summary>
        GpuOverlap = 15,

        /// <summary>
        /// Number of multiprocessors (SMs) on the device.
        /// </summary>
        MultiProcessorCount = 16,

        /// <summary>
        /// Whether kernel execution timeout is enabled (1=yes, 0=no).
        /// </summary>
        KernelExecTimeout = 17,

        /// <summary>
        /// Device is integrated with host memory (1=yes, 0=no).
        /// </summary>
        Integrated = 18,

        /// <summary>
        /// Device can map host memory into CUDA address space (1=yes, 0=no).
        /// </summary>
        CanMapHostMemory = 19,

        /// <summary>
        /// Compute mode: Default (0), Exclusive (1), Prohibited (2), ExclusiveProcess (3).
        /// </summary>
        ComputeMode = 20,

        /// <summary>
        /// Maximum 1D texture width.
        /// </summary>
        MaxTexture1DWidth = 21,

        /// <summary>
        /// Maximum 2D texture width.
        /// </summary>
        MaxTexture2DWidth = 22,

        /// <summary>
        /// Maximum 2D texture height.
        /// </summary>
        MaxTexture2DHeight = 23,

        /// <summary>
        /// Maximum 3D texture width.
        /// </summary>
        MaxTexture3DWidth = 24,

        /// <summary>
        /// Maximum 3D texture height.
        /// </summary>
        MaxTexture3DHeight = 25,

        /// <summary>
        /// Maximum 3D texture depth.
        /// </summary>
        MaxTexture3DDepth = 26,

        /// <summary>
        /// Maximum width for 2D layered textures.
        /// </summary>
        MaxTexture2DLayeredWidth = 27,

        /// <summary>
        /// Maximum height for 2D layered textures.
        /// </summary>
        MaxTexture2DLayeredHeight = 28,

        /// <summary>
        /// Maximum number of layers for 2D layered textures.
        /// </summary>
        MaxTexture2DLayeredLayers = 29,

        /// <summary>
        /// Surface alignment requirement in bytes.
        /// </summary>
        SurfaceAlignment = 30,

        /// <summary>
        /// Device can execute multiple kernels concurrently (1=yes, 0=no).
        /// </summary>
        ConcurrentKernels = 31,

        /// <summary>
        /// Device has ECC (Error Correcting Code) memory enabled (1=yes, 0=no).
        /// </summary>
        EccEnabled = 32,

        /// <summary>
        /// PCI bus ID of the device.
        /// </summary>
        PciBusId = 33,

        /// <summary>
        /// PCI device ID.
        /// </summary>
        PciDeviceId = 34,

        /// <summary>
        /// Device is using TCC driver model (1=yes, 0=no). Windows only.
        /// </summary>
        TccDriver = 35,

        /// <summary>
        /// Peak memory clock frequency in kilohertz.
        /// </summary>
        MemoryClockRate = 36,

        /// <summary>
        /// Global memory bus width in bits.
        /// </summary>
        GlobalMemoryBusWidth = 37,

        /// <summary>
        /// Size of L2 cache in bytes.
        /// </summary>
        L2CacheSize = 38,

        /// <summary>
        /// Maximum resident threads per multiprocessor.
        /// </summary>
        MaxThreadsPerMultiProcessor = 39,

        /// <summary>
        /// Number of asynchronous engines for memory operations.
        /// </summary>
        AsyncEngineCount = 40,

        /// <summary>
        /// Device shares unified address space with host (1=yes, 0=no).
        /// </summary>
        UnifiedAddressing = 41,

        /// <summary>
        /// Maximum width for 1D layered textures.
        /// </summary>
        MaxTexture1DLayeredWidth = 42,

        /// <summary>
        /// Maximum number of layers for 1D layered textures.
        /// </summary>
        MaxTexture1DLayeredLayers = 43,

        /// <summary>
        /// Device supports texture gather operations (1=yes, 0=no).
        /// </summary>
        CanTex2DGather = 44,

        /// <summary>
        /// Maximum width for 2D texture gather operations.
        /// </summary>
        MaxTexture2DGatherWidth = 45,

        /// <summary>
        /// Maximum height for 2D texture gather operations.
        /// </summary>
        MaxTexture2DGatherHeight = 46,

        /// <summary>
        /// Alternate maximum 3D texture width.
        /// </summary>
        MaxTexture3DWidthAlt = 47,

        /// <summary>
        /// Alternate maximum 3D texture height.
        /// </summary>
        MaxTexture3DHeightAlt = 48,

        /// <summary>
        /// Alternate maximum 3D texture depth.
        /// </summary>
        MaxTexture3DDepthAlt = 49,

        /// <summary>
        /// PCI domain ID of the device.
        /// </summary>
        PciDomainId = 50,

        /// <summary>
        /// Texture pitch alignment requirement in bytes.
        /// </summary>
        TexturePitchAlignment = 51,

        /// <summary>
        /// Maximum cubemap texture width.
        /// </summary>
        MaxTextureCubemapWidth = 52,

        /// <summary>
        /// Maximum width for layered cubemap textures.
        /// </summary>
        MaxTextureCubemapLayeredWidth = 53,

        /// <summary>
        /// Maximum number of layers for layered cubemap textures.
        /// </summary>
        MaxTextureCubemapLayeredLayers = 54,

        /// <summary>
        /// Maximum 1D surface width.
        /// </summary>
        MaxSurface1DWidth = 55,

        /// <summary>
        /// Maximum 2D surface width.
        /// </summary>
        MaxSurface2DWidth = 56,

        /// <summary>
        /// Maximum 2D surface height.
        /// </summary>
        MaxSurface2DHeight = 57,

        /// <summary>
        /// Maximum 3D surface width.
        /// </summary>
        MaxSurface3DWidth = 58,

        /// <summary>
        /// Maximum 3D surface height.
        /// </summary>
        MaxSurface3DHeight = 59,

        /// <summary>
        /// Maximum 3D surface depth.
        /// </summary>
        MaxSurface3DDepth = 60,

        /// <summary>
        /// Maximum width for 1D layered surfaces.
        /// </summary>
        MaxSurface1DLayeredWidth = 61,

        /// <summary>
        /// Maximum number of layers for 1D layered surfaces.
        /// </summary>
        MaxSurface1DLayeredLayers = 62,

        /// <summary>
        /// Maximum width for 2D layered surfaces.
        /// </summary>
        MaxSurface2DLayeredWidth = 63,

        /// <summary>
        /// Maximum height for 2D layered surfaces.
        /// </summary>
        MaxSurface2DLayeredHeight = 64,

        /// <summary>
        /// Maximum number of layers for 2D layered surfaces.
        /// </summary>
        MaxSurface2DLayeredLayers = 65,

        /// <summary>
        /// Maximum cubemap surface width.
        /// </summary>
        MaxSurfaceCubemapWidth = 66,

        /// <summary>
        /// Maximum width for layered cubemap surfaces.
        /// </summary>
        MaxSurfaceCubemapLayeredWidth = 67,

        /// <summary>
        /// Maximum number of layers for layered cubemap surfaces.
        /// </summary>
        MaxSurfaceCubemapLayeredLayers = 68,

        /// <summary>
        /// Maximum 1D linear texture width.
        /// </summary>
        MaxTexture1DLinearWidth = 69,

        /// <summary>
        /// Maximum 2D linear texture width.
        /// </summary>
        MaxTexture2DLinearWidth = 70,

        /// <summary>
        /// Maximum 2D linear texture height.
        /// </summary>
        MaxTexture2DLinearHeight = 71,

        /// <summary>
        /// Maximum 2D linear texture pitch.
        /// </summary>
        MaxTexture2DLinearPitch = 72,

        /// <summary>
        /// Maximum 2D mipmapped texture width.
        /// </summary>
        MaxTexture2DMipmappedWidth = 73,

        /// <summary>
        /// Maximum 2D mipmapped texture height.
        /// </summary>
        MaxTexture2DMipmappedHeight = 74,

        /// <summary>
        /// Major compute capability version number. E.g., 8 for compute capability 8.9.
        /// </summary>
        ComputeCapabilityMajor = 75,

        /// <summary>
        /// Minor compute capability version number. E.g., 9 for compute capability 8.9.
        /// </summary>
        ComputeCapabilityMinor = 76,

        /// <summary>
        /// Maximum 1D mipmapped texture width.
        /// </summary>
        MaxTexture1DMipmappedWidth = 77,

        /// <summary>
        /// Device supports stream priorities (1=yes, 0=no).
        /// </summary>
        StreamPrioritiesSupported = 78,

        /// <summary>
        /// Device supports global L1 cache (1=yes, 0=no).
        /// </summary>
        GlobalL1CacheSupported = 79,

        /// <summary>
        /// Device supports local L1 cache (1=yes, 0=no).
        /// </summary>
        LocalL1CacheSupported = 80,

        /// <summary>
        /// Maximum shared memory per multiprocessor in bytes.
        /// </summary>
        MaxSharedMemoryPerMultiprocessor = 81,

        /// <summary>
        /// Maximum number of 32-bit registers per multiprocessor.
        /// </summary>
        MaxRegistersPerMultiprocessor = 82,

        /// <summary>
        /// Device can allocate managed memory (1=yes, 0=no).
        /// </summary>
        ManagedMemory = 83,

        /// <summary>
        /// Device is part of a multi-GPU board (1=yes, 0=no).
        /// </summary>
        MultiGpuBoard = 84,

        /// <summary>
        /// Unique identifier for multi-GPU board group.
        /// </summary>
        MultiGpuBoardGroupId = 85,

        /// <summary>
        /// Host native atomic operations supported on device memory (1=yes, 0=no).
        /// </summary>
        HostNativeAtomicSupported = 86,

        /// <summary>
        /// Ratio of single to double precision performance. Higher means double is slower.
        /// </summary>
        SingleToDoublePrecisionPerfRatio = 87,

        /// <summary>
        /// Device supports pageable memory access via host page tables (1=yes, 0=no).
        /// </summary>
        PageableMemoryAccess = 88,

        /// <summary>
        /// Device can concurrently access managed memory with CPU (1=yes, 0=no).
        /// </summary>
        ConcurrentManagedAccess = 89,

        /// <summary>
        /// Device supports compute preemption (1=yes, 0=no).
        /// </summary>
        ComputePreemptionSupported = 90,

        /// <summary>
        /// Device can use host pointer for registered memory (1=yes, 0=no).
        /// </summary>
        CanUseHostPointerForRegisteredMem = 91,

        /// <summary>
        /// Device supports cooperative kernel launches (1=yes, 0=no).
        /// </summary>
        CooperativeLaunch = 92,

        /// <summary>
        /// Device supports multi-device cooperative kernel launches (1=yes, 0=no).
        /// </summary>
        CooperativeMultiDeviceLaunch = 93,

        /// <summary>
        /// Maximum opt-in shared memory per block in bytes. Requires explicit configuration.
        /// </summary>
        MaxSharedMemoryPerBlockOptin = 94,

        /// <summary>
        /// Device can flush remote writes (1=yes, 0=no).
        /// </summary>
        CanFlushRemoteWrites = 95,

        /// <summary>
        /// Device supports host memory registration (1=yes, 0=no).
        /// </summary>
        HostRegisterSupported = 96,

        /// <summary>
        /// Pageable memory access uses host page tables (1=yes, 0=no).
        /// </summary>
        PageableMemoryAccessUsesHostPageTables = 97,

        /// <summary>
        /// Host can directly access managed memory on device (1=yes, 0=no).
        /// </summary>
        DirectManagedMemAccessFromHost = 98,

        /// <summary>
        /// Maximum number of resident blocks per multiprocessor.
        /// </summary>
        MaxBlocksPerMultiprocessor = 99,

        /// <summary>
        /// Maximum size of L2 cache that can be persisted in bytes.
        /// </summary>
        MaxPersistingL2CacheSize = 100,

        /// <summary>
        /// Maximum access policy window size in bytes.
        /// </summary>
        MaxAccessPolicyWindowSize = 101,

        /// <summary>
        /// Amount of shared memory reserved per block by the system in bytes.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1700:Do not name enum values 'Reserved'",
            Justification = "CUDA API constant - matches official NVIDIA SDK naming")]
        ReservedSharedMemoryPerBlock = 102,

        /// <summary>
        /// Device supports sparse CUDA arrays (1=yes, 0=no).
        /// </summary>
        SparseCudaArraySupported = 103,

        /// <summary>
        /// Device supports read-only host memory registration (1=yes, 0=no).
        /// </summary>
        HostRegisterReadOnlySupported = 104,

        /// <summary>
        /// Device supports timeline semaphore interop (1=yes, 0=no).
        /// </summary>
        TimelineSemaphoreInteropSupported = 105,

        /// <summary>
        /// Device supports memory pools (1=yes, 0=no).
        /// </summary>
        MemoryPoolsSupported = 106,

        /// <summary>
        /// Device supports GPUDirect RDMA (1=yes, 0=no).
        /// </summary>
        GpuDirectRdmaSupported = 107,

        /// <summary>
        /// GPUDirect RDMA flush writes options bitmask.
        /// </summary>
        GpuDirectRdmaFlushWritesOptions = 108,

        /// <summary>
        /// GPUDirect RDMA writes ordering guarantees.
        /// </summary>
        GpuDirectRdmaWritesOrdering = 109,

        /// <summary>
        /// Memory pool supported handle types bitmask.
        /// </summary>
        MempoolSupportedHandleTypes = 110,

        /// <summary>
        /// Device supports deferred mapping of CUDA arrays (1=yes, 0=no).
        /// </summary>
        DeferredMappingCudaArraySupported = 111,

        /// <summary>
        /// Device supports IPC events (1=yes, 0=no).
        /// </summary>
        IpcEventSupported = 112,

        /// <summary>
        /// Device supports cluster launch (1=yes, 0=no). Compute capability 9.0+.
        /// </summary>
        ClusterLaunch = 113,

        /// <summary>
        /// Device supports unified function pointers (1=yes, 0=no).
        /// </summary>
        UnifiedFunctionPointers = 114,

        /// <summary>
        /// NUMA configuration for the device.
        /// </summary>
        NumaConfig = 115,

        /// <summary>
        /// NUMA node ID associated with the device.
        /// </summary>
        NumaId = 116,

        /// <summary>
        /// Multi-Process Service (MPS) is enabled on the device (1=yes, 0=no).
        /// </summary>
        MpsEnabled = 117,

        /// <summary>
        /// NUMA node ID of the host memory preferred by this device.
        /// </summary>
        HostNumaId = 118
    }
}
