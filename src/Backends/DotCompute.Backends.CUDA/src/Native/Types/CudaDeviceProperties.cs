// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Native.Types
{
    /// <summary>
    /// Structure containing detailed properties and capabilities of a CUDA device.
    /// This structure matches the cudaDeviceProp structure from CUDA 12.x runtime.
    /// </summary>
    /// <remarks>
    /// This structure provides comprehensive information about a CUDA device including
    /// memory limits, processing capabilities, architectural features, and supported
    /// functionality. The layout is explicitly defined to match the native CUDA structure.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 1032, CharSet = CharSet.Ansi)]
#pragma warning disable CA1815 // Override equals and operator equals on value types - P/Invoke struct doesn't need equality
    public struct CudaDeviceProperties
#pragma warning restore CA1815
    {
        /// <summary>
        /// ASCII string identifying the device (up to 256 characters).
        /// </summary>
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;

        /// <summary>
        /// Lower 8 bytes of the device UUID.
        /// Combined with <see cref="UuidHigh"/>, this forms a unique 16-byte identifier.
        /// </summary>
        [FieldOffset(256)]
        public ulong UuidLow;

        /// <summary>
        /// Upper 8 bytes of the device UUID.
        /// Combined with <see cref="UuidLow"/>, this forms a unique 16-byte identifier.
        /// </summary>
        [FieldOffset(264)]
        public ulong UuidHigh;

        /// <summary>
        /// Total amount of global memory available on the device in bytes.
        /// </summary>
        [FieldOffset(272)]
        public ulong TotalGlobalMem;

        /// <summary>
        /// Amount of shared memory available per thread block in bytes.
        /// </summary>
        [FieldOffset(280)]
        public ulong SharedMemPerBlock;

        /// <summary>
        /// Number of 32-bit registers available per thread block.
        /// </summary>
        [FieldOffset(288)]
        public int RegsPerBlock;

        /// <summary>
        /// Warp size in threads. This is typically 32 for all current CUDA devices.
        /// </summary>
        [FieldOffset(292)]
        public int WarpSize;

        /// <summary>
        /// Maximum pitch in bytes allowed by memory copies.
        /// </summary>
        [FieldOffset(296)]
        public ulong MemPitch;

        /// <summary>
        /// Maximum number of threads per block.
        /// </summary>
        [FieldOffset(304)]
        public int MaxThreadsPerBlock;

        /// <summary>
        /// Maximum x-dimension of a thread block.
        /// </summary>
        [FieldOffset(308)]
        public int MaxThreadsDimX;

        /// <summary>
        /// Maximum y-dimension of a thread block.
        /// </summary>
        [FieldOffset(312)]
        public int MaxThreadsDimY;

        /// <summary>
        /// Maximum z-dimension of a thread block.
        /// </summary>
        [FieldOffset(316)]
        public int MaxThreadsDimZ;

        /// <summary>
        /// Maximum x-dimension of a grid of thread blocks.
        /// </summary>
        [FieldOffset(320)]
        public int MaxGridSizeX;

        /// <summary>
        /// Maximum y-dimension of a grid of thread blocks.
        /// </summary>
        [FieldOffset(324)]
        public int MaxGridSizeY;

        /// <summary>
        /// Maximum z-dimension of a grid of thread blocks.
        /// </summary>
        [FieldOffset(328)]
        public int MaxGridSizeZ;

        /// <summary>
        /// Clock frequency of the device in kilohertz.
        /// </summary>
        [FieldOffset(332)]
        public int ClockRate;

        /// <summary>
        /// Total amount of constant memory available on the device in bytes.
        /// </summary>
        [FieldOffset(336)]
        public ulong TotalConstMem;

        /// <summary>
        /// Major revision number of the device's compute capability.
        /// </summary>
        [FieldOffset(360)]
        public int Major;

        /// <summary>
        /// Minor revision number of the device's compute capability.
        /// </summary>
        [FieldOffset(364)]
        public int Minor;

        /// <summary>
        /// Alignment requirement for textures in bytes.
        /// </summary>
        [FieldOffset(368)]
        public ulong TextureAlignment;

        /// <summary>
        /// Pitch alignment requirement for 2D texture references bound to pitched memory.
        /// </summary>
        [FieldOffset(376)]
        public ulong TexturePitchAlignment;

        /// <summary>
        /// 1 if the device can concurrently copy memory between host and device while executing a kernel, 0 otherwise.
        /// </summary>
        [FieldOffset(384)]
        public int DeviceOverlap;

        /// <summary>
        /// Number of multiprocessors on the device.
        /// </summary>
        [FieldOffset(388)]
        public int MultiProcessorCount;

        /// <summary>
        /// 1 if there is a run-time limit on kernels, 0 otherwise.
        /// </summary>
        [FieldOffset(392)]
        public int KernelExecTimeoutEnabled;

        /// <summary>
        /// 1 if the device is integrated with the memory subsystem, 0 otherwise.
        /// </summary>
        [FieldOffset(396)]
        public int Integrated;

        /// <summary>
        /// 1 if the device can map host memory into the device address space, 0 otherwise.
        /// </summary>
        [FieldOffset(400)]
        public int CanMapHostMemory;

        /// <summary>
        /// Compute mode that the device is currently in.
        /// </summary>
        [FieldOffset(404)]
        public int ComputeMode;

        /// <summary>
        /// 1 if the device supports executing multiple kernels within the same context simultaneously, 0 otherwise.
        /// </summary>
        [FieldOffset(576)]
        public int ConcurrentKernels;

        /// <summary>
        /// 1 if the device has error correction for memory, 0 otherwise.
        /// </summary>
        [FieldOffset(580)]
        public int ECCEnabled;

        /// <summary>
        /// PCI bus identifier of the device.
        /// </summary>
        [FieldOffset(584)]
        public int PciBusId;

        /// <summary>
        /// PCI device identifier of the device.
        /// </summary>
        [FieldOffset(588)]
        public int PciDeviceId;

        /// <summary>
        /// PCI domain identifier of the device.
        /// </summary>
        [FieldOffset(592)]
        public int PciDomainId;

        /// <summary>
        /// Number of asynchronous engines supported by the device.
        /// </summary>
        [FieldOffset(600)]
        public int AsyncEngineCount;

        /// <summary>
        /// 1 if the device shares a unified address space with the host, 0 otherwise.
        /// </summary>
        [FieldOffset(604)]
        public int UnifiedAddressing;

        /// <summary>
        /// Peak memory clock frequency in kilohertz.
        /// </summary>
        [FieldOffset(608)]
        public int MemoryClockRate;

        /// <summary>
        /// Global memory bus width in bits.
        /// </summary>
        [FieldOffset(612)]
        public int MemoryBusWidth;

        /// <summary>
        /// Size of L2 cache in bytes.
        /// </summary>
        [FieldOffset(616)]
        public int L2CacheSize;

        /// <summary>
        /// Maximum size of the persisting L2 cache in bytes.
        /// </summary>
        [FieldOffset(620)]
        public int PersistingL2CacheMaxSize;

        /// <summary>
        /// Maximum number of threads per multiprocessor.
        /// </summary>
        [FieldOffset(624)]
        public int MaxThreadsPerMultiProcessor;

        /// <summary>
        /// 1 if the device supports stream priorities, 0 otherwise.
        /// </summary>
        [FieldOffset(628)]
        public int StreamPrioritiesSupported;

        /// <summary>
        /// 1 if the device supports caching globals in L1 cache, 0 otherwise.
        /// </summary>
        [FieldOffset(632)]
        public int GlobalL1CacheSupported;

        /// <summary>
        /// 1 if the device supports caching locals in L1 cache, 0 otherwise.
        /// </summary>
        [FieldOffset(636)]
        public int LocalL1CacheSupported;

        /// <summary>
        /// Amount of shared memory available per multiprocessor in bytes.
        /// </summary>
        [FieldOffset(640)]
        public ulong SharedMemPerMultiprocessor;

        /// <summary>
        /// Number of 32-bit registers available per multiprocessor.
        /// </summary>
        [FieldOffset(648)]
        public int RegsPerMultiprocessor;

        /// <summary>
        /// 1 if the device supports allocating managed memory, 0 otherwise.
        /// </summary>
        [FieldOffset(652)]
        public int ManagedMemory;

        /// <summary>
        /// 1 if the device is on a multi-GPU board, 0 otherwise.
        /// </summary>
        [FieldOffset(656)]
        public int IsMultiGpuBoard;

        /// <summary>
        /// Identifier for a group of devices on the same multi-GPU board.
        /// </summary>
        [FieldOffset(660)]
        public int MultiGpuBoardGroupID;

        /// <summary>
        /// Ratio of single precision to double precision performance.
        /// </summary>
        [FieldOffset(668)]
        public int SingleToDoublePrecisionPerfRatio;

        /// <summary>
        /// 1 if the device supports coherently accessing pageable memory without calling cudaHostRegister, 0 otherwise.
        /// </summary>
        [FieldOffset(672)]
        public int PageableMemoryAccess;

        /// <summary>
        /// 1 if the device can coherently access managed memory concurrently with the CPU, 0 otherwise.
        /// </summary>
        [FieldOffset(676)]
        public int ConcurrentManagedAccess;

        /// <summary>
        /// 1 if the device supports compute preemption, 0 otherwise.
        /// </summary>
        [FieldOffset(680)]
        public int ComputePreemptionSupported;

        /// <summary>
        /// 1 if the device can access host registered memory at the same virtual address as the CPU, 0 otherwise.
        /// </summary>
        [FieldOffset(684)]
        public int CanUseHostPointerForRegisteredMem;

        /// <summary>
        /// 1 if the device supports cooperative kernel launches, 0 otherwise.
        /// </summary>
        [FieldOffset(688)]
        public int CooperativeLaunch;

        /// <summary>
        /// 1 if the device supports cooperative kernel launches via cudaLaunchCooperativeKernelMultiDevice, 0 otherwise.
        /// </summary>
        [FieldOffset(692)]
        public int CooperativeMultiDeviceLaunch;

        /// <summary>
        /// Maximum shared memory per thread block when using the opt-in shared memory configuration.
        /// </summary>
        [FieldOffset(696)]
        public ulong SharedMemPerBlockOptin;

        /// <summary>
        /// 1 if the device accesses pageable memory via the host's page tables, 0 otherwise.
        /// </summary>
        [FieldOffset(704)]
        public int PageableMemoryAccessUsesHostPageTables;

        /// <summary>
        /// 1 if the host can directly access managed memory on the device without migration, 0 otherwise.
        /// </summary>
        [FieldOffset(708)]
        public int DirectManagedMemAccessFromHost;
    }
}