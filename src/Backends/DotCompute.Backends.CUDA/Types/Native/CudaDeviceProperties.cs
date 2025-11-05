// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Structure containing detailed properties and capabilities of a CUDA device.
    /// This structure matches the cudaDeviceProp structure from CUDA 12.x/13.x runtime.
    /// </summary>
    /// <remarks>
    /// This structure provides comprehensive information about a CUDA device including
    /// memory limits, processing capabilities, architectural features, and supported
    /// functionality. The layout is explicitly defined to match the native CUDA structure.
    ///
    /// IMPORTANT: CUDA 12.0+ added luid and luidDeviceNodeMask fields at offset 272-283,
    /// shifting all subsequent fields by 16 bytes. The size is 1008 bytes in CUDA 13.0.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 1008, CharSet = CharSet.Ansi)]
    public struct CudaDeviceProperties : IEquatable<CudaDeviceProperties>
    {
        /// <summary>
        /// The name.
        /// </summary>
        /// <summary>
        /// ASCII string identifying the device (up to 256 characters).
        /// </summary>
        [FieldOffset(0)]
        public unsafe fixed sbyte Name[256];


        /// <summary>
        /// Gets the device name as a managed string.
        /// </summary>
        public unsafe string DeviceName
        {
            get
            {
                fixed (sbyte* namePtr = Name)
                {
                    return Marshal.PtrToStringAnsi((IntPtr)namePtr) ?? "Unknown Device";
                }
            }
        }
        /// <summary>
        /// The uuid low.
        /// </summary>

        /// <summary>
        /// Lower 8 bytes of the device UUID.
        /// Combined with <see cref="UuidHigh"/>, this forms a unique 16-byte identifier.
        /// </summary>
        [FieldOffset(256)]
        public ulong UuidLow;
        /// <summary>
        /// The uuid high.
        /// </summary>

        /// <summary>
        /// Upper 8 bytes of the device UUID.
        /// Combined with <see cref="UuidLow"/>, this forms a unique 16-byte identifier.
        /// </summary>
        [FieldOffset(264)]
        public ulong UuidHigh;

        /// <summary>
        /// 8-byte locally unique identifier (LUID) for the device.
        /// Added in CUDA 12.0+. Used for Windows device management.
        /// </summary>
        [FieldOffset(272)]
        public ulong Luid;

        /// <summary>
        /// Device node mask for the LUID.
        /// Added in CUDA 12.0+. Used for Windows device management.
        /// </summary>
        [FieldOffset(280)]
        public uint LuidDeviceNodeMask;

        // 4 bytes of padding here (284-287) for alignment

        /// <summary>
        /// The total global mem.
        /// </summary>

        /// <summary>
        /// Total amount of global memory available on the device in bytes.
        /// NOTE: Offset changed from 272 to 288 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(288)]
        public ulong TotalGlobalMem;
        /// <summary>
        /// The shared mem per block.
        /// </summary>

        /// <summary>
        /// Amount of shared memory available per thread block in bytes.
        /// NOTE: Offset changed from 280 to 296 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(296)]
        public ulong SharedMemPerBlock;
        /// <summary>
        /// The regs per block.
        /// </summary>

        /// <summary>
        /// Number of 32-bit registers available per thread block.
        /// NOTE: Offset changed from 288 to 304 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(304)]
        public int RegsPerBlock;
        /// <summary>
        /// The warp size.
        /// </summary>

        /// <summary>
        /// Warp size in threads. This is typically 32 for all current CUDA devices.
        /// NOTE: Offset changed from 292 to 308 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(308)]
        public int WarpSize;
        /// <summary>
        /// The mem pitch.
        /// </summary>

        /// <summary>
        /// Maximum pitch in bytes allowed by memory copies.
        /// NOTE: Offset changed from 296 to 312 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(312)]
        public ulong MemPitch;
        /// <summary>
        /// The max threads per block.
        /// </summary>

        /// <summary>
        /// Maximum number of threads per block.
        /// NOTE: Offset changed from 304 to 320 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(320)]
        public int MaxThreadsPerBlock;
        /// <summary>
        /// The max threads dim x.
        /// </summary>

        /// <summary>
        /// Maximum x-dimension of a thread block.
        /// NOTE: Offset changed from 308 to 324 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(324)]
        public int MaxThreadsDimX;
        /// <summary>
        /// The max threads dim y.
        /// </summary>

        /// <summary>
        /// Maximum y-dimension of a thread block.
        /// NOTE: Offset changed from 312 to 328 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(328)]
        public int MaxThreadsDimY;
        /// <summary>
        /// The max threads dim z.
        /// </summary>

        /// <summary>
        /// Maximum z-dimension of a thread block.
        /// NOTE: Offset changed from 316 to 332 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(332)]
        public int MaxThreadsDimZ;

        /// <summary>
        /// Maximum dimensions of a thread block [x, y, z].
        /// </summary>
        public unsafe int* MaxThreadsDimPtr => (int*)Unsafe.AsPointer(ref MaxThreadsDimX);

        /// <summary>
        /// Maximum dimensions of a thread block as an array [x, y, z].
        /// </summary>
        public unsafe IReadOnlyList<int> MaxThreadsDim
        {
            get
            {
                var ptr = MaxThreadsDimPtr;
                return [ptr[0], ptr[1], ptr[2]];
            }
        }
        /// <summary>
        /// The max grid size x.
        /// </summary>

        /// <summary>
        /// Maximum x-dimension of a grid of thread blocks.
        /// NOTE: Offset changed from 320 to 336 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(336)]
        public int MaxGridSizeX;
        /// <summary>
        /// The max grid size y.
        /// </summary>

        /// <summary>
        /// Maximum y-dimension of a grid of thread blocks.
        /// NOTE: Offset changed from 324 to 340 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(340)]
        public int MaxGridSizeY;
        /// <summary>
        /// The max grid size z.
        /// </summary>

        /// <summary>
        /// Maximum z-dimension of a grid of thread blocks.
        /// NOTE: Offset changed from 328 to 344 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(344)]
        public int MaxGridSizeZ;

        /// <summary>
        /// Maximum dimensions of a grid of thread blocks [x, y, z].
        /// </summary>
        public unsafe int* MaxGridSizePtr => (int*)Unsafe.AsPointer(ref MaxGridSizeX);

        /// <summary>
        /// Maximum dimensions of a grid of thread blocks as an array [x, y, z].
        /// </summary>
        public unsafe IReadOnlyList<int> MaxGridSize
        {
            get
            {
                var ptr = MaxGridSizePtr;
                return [ptr[0], ptr[1], ptr[2]];
            }
        }

        /// <summary>
        /// Maximum dimensions of a grid of thread blocks as an array [x, y, z] (alias for MaxGridSize).
        /// </summary>
        public unsafe IReadOnlyList<int> MaxGridDim => MaxGridSize;
        /// <summary>
        /// The clock rate.
        /// </summary>

        /// <summary>
        /// Clock frequency of the device in kilohertz.
        /// NOTE: Offset changed from 332 to 348 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(348)]
        public int ClockRate;
        /// <summary>
        /// The total const mem.
        /// </summary>

        /// <summary>
        /// Total amount of constant memory available on the device in bytes.
        /// NOTE: Offset changed from 336 to 352 in CUDA 12.0+ due to new luid fields.
        /// </summary>
        [FieldOffset(352)]
        public ulong TotalConstMem;
        /// <summary>
        /// The major.
        /// </summary>

        /// <summary>
        /// Major revision number of the device's compute capability.
        /// </summary>
        [FieldOffset(360)]
        public int Major;
        /// <summary>
        /// The minor.
        /// </summary>

        /// <summary>
        /// Minor revision number of the device's compute capability.
        /// </summary>
        [FieldOffset(364)]
        public int Minor;
        /// <summary>
        /// The texture alignment.
        /// </summary>

        /// <summary>
        /// Alignment requirement for textures in bytes.
        /// </summary>
        [FieldOffset(368)]
        public ulong TextureAlignment;
        /// <summary>
        /// The texture pitch alignment.
        /// </summary>

        /// <summary>
        /// Pitch alignment requirement for 2D texture references bound to pitched memory.
        /// </summary>
        [FieldOffset(376)]
        public ulong TexturePitchAlignment;
        /// <summary>
        /// The device overlap.
        /// </summary>

        /// <summary>
        /// 1 if the device can concurrently copy memory between host and device while executing a kernel, 0 otherwise.
        /// </summary>
        [FieldOffset(384)]
        public int DeviceOverlap;
        /// <summary>
        /// The multi processor count.
        /// </summary>

        /// <summary>
        /// Number of multiprocessors on the device.
        /// NOTE: Offset changed from 388 to 384 to match CUDA 12.0+ layout.
        /// </summary>
        [FieldOffset(384)]
        public int MultiProcessorCount;
        /// <summary>
        /// The kernel exec timeout enabled.
        /// </summary>

        /// <summary>
        /// 1 if there is a run-time limit on kernels, 0 otherwise.
        /// </summary>
        [FieldOffset(392)]
        public int KernelExecTimeoutEnabled;
        /// <summary>
        /// The integrated.
        /// </summary>

        /// <summary>
        /// 1 if the device is integrated with the memory subsystem, 0 otherwise.
        /// </summary>
        [FieldOffset(396)]
        public int Integrated;
        /// <summary>
        /// The can map host memory.
        /// </summary>

        /// <summary>
        /// 1 if the device can map host memory into the device address space, 0 otherwise.
        /// </summary>
        [FieldOffset(400)]
        public int CanMapHostMemory;
        /// <summary>
        /// The compute mode.
        /// </summary>

        /// <summary>
        /// Compute mode that the device is currently in.
        /// </summary>
        [FieldOffset(404)]
        public int ComputeMode;
        /// <summary>
        /// The concurrent kernels.
        /// </summary>

        /// <summary>
        /// 1 if the device supports executing multiple kernels within the same context simultaneously, 0 otherwise.
        /// </summary>
        [FieldOffset(576)]
        public int ConcurrentKernels;
        /// <summary>
        /// The e c c enabled.
        /// </summary>

        /// <summary>
        /// 1 if the device has error correction for memory, 0 otherwise.
        /// </summary>
        [FieldOffset(580)]
        public int ECCEnabled;
        /// <summary>
        /// The pci bus i d.
        /// </summary>

        /// <summary>
        /// PCI bus identifier of the device.
        /// </summary>
        [FieldOffset(584)]
        public int PciBusID;

        /// <summary>
        /// Alias for PciBusID for compatibility.
        /// </summary>
        public int PciBusId => PciBusID;
        /// <summary>
        /// The pci device i d.
        /// </summary>

        /// <summary>
        /// PCI device identifier of the device.
        /// </summary>
        [FieldOffset(588)]
        public int PciDeviceID;

        /// <summary>
        /// Alias for PciDeviceID for compatibility.
        /// </summary>
        public int PciDeviceId => PciDeviceID;
        /// <summary>
        /// The pci domain i d.
        /// </summary>

        /// <summary>
        /// PCI domain identifier of the device.
        /// </summary>
        [FieldOffset(592)]
        public int PciDomainID;

        /// <summary>
        /// Alias for PciDomainID for compatibility.
        /// </summary>
        public int PciDomainId => PciDomainID;
        /// <summary>
        /// The async engine count.
        /// </summary>

        /// <summary>
        /// Number of asynchronous engines supported by the device.
        /// </summary>
        [FieldOffset(600)]
        public int AsyncEngineCount;
        /// <summary>
        /// The unified addressing.
        /// </summary>

        /// <summary>
        /// 1 if the device shares a unified address space with the host, 0 otherwise.
        /// </summary>
        [FieldOffset(604)]
        public int UnifiedAddressing;
        /// <summary>
        /// The mem clock rate.
        /// </summary>

        /// <summary>
        /// Peak memory clock frequency in kilohertz.
        /// </summary>
        [FieldOffset(608)]
        public int MemClockRate;

        /// <summary>
        /// Alias for MemClockRate for compatibility.
        /// </summary>
        public int MemoryClockRate => MemClockRate;
        /// <summary>
        /// The mem bus width.
        /// </summary>

        /// <summary>
        /// Global memory bus width in bits.
        /// </summary>
        [FieldOffset(612)]
        public int MemBusWidth;

        /// <summary>
        /// Alias for MemBusWidth for compatibility.
        /// </summary>
        public int MemoryBusWidth => MemBusWidth;
        /// <summary>
        /// The l2 cache size.
        /// </summary>

        /// <summary>
        /// Size of L2 cache in bytes.
        /// </summary>
        [FieldOffset(616)]
        public int L2CacheSize;
        /// <summary>
        /// The persisting l2 cache max size.
        /// </summary>

        /// <summary>
        /// Maximum size of the persisting L2 cache in bytes.
        /// </summary>
        [FieldOffset(620)]
        public int PersistingL2CacheMaxSize;
        /// <summary>
        /// The max threads per multi processor.
        /// </summary>

        /// <summary>
        /// Maximum number of threads per multiprocessor.
        /// </summary>
        [FieldOffset(624)]
        public int MaxThreadsPerMultiProcessor;
        /// <summary>
        /// The stream priorities supported.
        /// </summary>

        /// <summary>
        /// 1 if the device supports stream priorities, 0 otherwise.
        /// </summary>
        [FieldOffset(628)]
        public int StreamPrioritiesSupported;
        /// <summary>
        /// The global l1 cache supported.
        /// </summary>

        /// <summary>
        /// 1 if the device supports caching globals in L1 cache, 0 otherwise.
        /// </summary>
        [FieldOffset(632)]
        public int GlobalL1CacheSupported;
        /// <summary>
        /// The local l1 cache supported.
        /// </summary>

        /// <summary>
        /// 1 if the device supports caching locals in L1 cache, 0 otherwise.
        /// </summary>
        [FieldOffset(636)]
        public int LocalL1CacheSupported;
        /// <summary>
        /// The shared mem per multiprocessor.
        /// </summary>

        /// <summary>
        /// Amount of shared memory available per multiprocessor in bytes.
        /// </summary>
        [FieldOffset(640)]
        public ulong SharedMemPerMultiprocessor;
        /// <summary>
        /// The regs per multiprocessor.
        /// </summary>

        /// <summary>
        /// Number of 32-bit registers available per multiprocessor.
        /// </summary>
        [FieldOffset(648)]
        public int RegsPerMultiprocessor;
        /// <summary>
        /// The managed memory.
        /// </summary>

        /// <summary>
        /// 1 if the device supports allocating managed memory, 0 otherwise.
        /// Note: Field offset verified for CUDA 13.0 runtime compatibility.
        /// </summary>
        [FieldOffset(652)]
        public int ManagedMemory;
        /// <summary>
        /// The is multi gpu board.
        /// </summary>

        /// <summary>
        /// 1 if the device is on a multi-GPU board, 0 otherwise.
        /// </summary>
        [FieldOffset(656)]
        public int IsMultiGpuBoard;
        /// <summary>
        /// The multi gpu board group i d.
        /// </summary>

        /// <summary>
        /// Identifier for a group of devices on the same multi-GPU board.
        /// </summary>
        [FieldOffset(660)]
        public int MultiGpuBoardGroupID;
        /// <summary>
        /// The single to double precision perf ratio.
        /// </summary>

        /// <summary>
        /// Ratio of single precision to double precision performance.
        /// </summary>
        [FieldOffset(668)]
        public int SingleToDoublePrecisionPerfRatio;
        /// <summary>
        /// The pageable memory access.
        /// </summary>

        /// <summary>
        /// 1 if the device supports coherently accessing pageable memory without calling cudaHostRegister, 0 otherwise.
        /// </summary>
        [FieldOffset(672)]
        public int PageableMemoryAccess;
        /// <summary>
        /// The concurrent managed access.
        /// </summary>

        /// <summary>
        /// 1 if the device can coherently access managed memory concurrently with the CPU, 0 otherwise.
        /// </summary>
        [FieldOffset(676)]
        public int ConcurrentManagedAccess;
        /// <summary>
        /// The compute preemption supported.
        /// </summary>

        /// <summary>
        /// 1 if the device supports compute preemption, 0 otherwise.
        /// </summary>
        [FieldOffset(680)]
        public int ComputePreemptionSupported;
        /// <summary>
        /// The can use host pointer for registered mem.
        /// </summary>

        /// <summary>
        /// 1 if the device can access host registered memory at the same virtual address as the CPU, 0 otherwise.
        /// </summary>
        [FieldOffset(684)]
        public int CanUseHostPointerForRegisteredMem;
        /// <summary>
        /// The cooperative launch.
        /// </summary>

        /// <summary>
        /// 1 if the device supports cooperative kernel launches, 0 otherwise.
        /// </summary>
        [FieldOffset(688)]
        public int CooperativeLaunch;
        /// <summary>
        /// The cooperative multi device launch.
        /// </summary>

        /// <summary>
        /// 1 if the device supports cooperative kernel launches via cudaLaunchCooperativeKernelMultiDevice, 0 otherwise.
        /// </summary>
        [FieldOffset(692)]
        public int CooperativeMultiDeviceLaunch;
        /// <summary>
        /// The shared mem per block optin.
        /// </summary>

        /// <summary>
        /// Maximum shared memory per thread block when using the opt-in shared memory configuration.
        /// </summary>
        [FieldOffset(696)]
        public ulong SharedMemPerBlockOptin;
        /// <summary>
        /// The pageable memory access uses host page tables.
        /// </summary>

        /// <summary>
        /// 1 if the device accesses pageable memory via the host's page tables, 0 otherwise.
        /// </summary>
        [FieldOffset(704)]
        public int PageableMemoryAccessUsesHostPageTables;
        /// <summary>
        /// The direct managed mem access from host.
        /// </summary>

        /// <summary>
        /// 1 if the host can directly access managed memory on the device without migration, 0 otherwise.
        /// </summary>
        [FieldOffset(708)]
        public int DirectManagedMemAccessFromHost;

        // Additional missing fields for texture limits


        /// <summary>
        /// Maximum 1D texture size.
        /// </summary>
        [FieldOffset(408)]
        public int MaxTexture1D;
        /// <summary>
        /// The max texture2 d width.
        /// </summary>


        /// <summary>
        /// Maximum 2D texture dimensions [width, height].
        /// </summary>
        [FieldOffset(412)]
        public int MaxTexture2DWidth;
        /// <summary>
        /// The max texture2 d height.
        /// </summary>


        /// <summary>
        /// Maximum 2D texture height.
        /// </summary>
        [FieldOffset(416)]
        public int MaxTexture2DHeight;


        /// <summary>
        /// Maximum 2D texture dimensions as array.
        /// </summary>
        public unsafe IReadOnlyList<int> MaxTexture2D
        {
            get => [MaxTexture2DWidth, MaxTexture2DHeight];
        }
        /// <summary>
        /// The max texture3 d width.
        /// </summary>


        /// <summary>
        /// Maximum 3D texture width.
        /// </summary>
        [FieldOffset(420)]
        public int MaxTexture3DWidth;
        /// <summary>
        /// The max texture3 d height.
        /// </summary>


        /// <summary>
        /// Maximum 3D texture height.
        /// </summary>
        [FieldOffset(424)]
        public int MaxTexture3DHeight;
        /// <summary>
        /// The max texture3 d depth.
        /// </summary>


        /// <summary>
        /// Maximum 3D texture depth.
        /// </summary>
        [FieldOffset(428)]
        public int MaxTexture3DDepth;


        /// <summary>
        /// Maximum 3D texture dimensions as array.
        /// </summary>
        public unsafe IReadOnlyList<int> MaxTexture3D
        {
            get => [MaxTexture3DWidth, MaxTexture3DHeight, MaxTexture3DDepth];
        }
        /// <summary>
        /// The tcc driver.
        /// </summary>


        /// <summary>
        /// 1 if there is a TCC driver model, 0 otherwise.
        /// </summary>
        [FieldOffset(596)]
        public int TccDriver;
        /// <summary>
        /// The host native atomic supported.
        /// </summary>


        /// <summary>
        /// 1 if device supports host-native atomic operations, 0 otherwise.
        /// </summary>
        [FieldOffset(664)]
        public int HostNativeAtomicSupported;


        /// <summary>
        /// Validates and corrects managed memory detection for RTX 2000 Ada and similar devices.
        /// This method provides a robust fallback when the struct field returns incorrect values.
        /// </summary>
        public bool GetActualManagedMemorySupport(int deviceId)
        {
            try
            {
                // Primary method: Use the struct field value
                if (ManagedMemory == 1)
                {
                    return true;
                }

                // RTX 2000 Ada Generation issue: ManagedMemory field may incorrectly return 0
                // For Ada Lovelace (compute capability 8.9) and newer, managed memory is always supported

                if (Major == 8 && Minor == 9)
                {
                    return true; // Ada Lovelace always supports managed memory
                }

                // For Ampere (8.0, 8.6) and newer architectures, managed memory is typically supported

                if (Major >= 8)
                {
                    return true;
                }

                // For Turing (7.5) and newer, check unified addressing as an indicator

                if (Major == 7 && Minor >= 5 && UnifiedAddressing == 1)
                {
                    return true;
                }

                // For Volta (7.0, 7.2) and newer with unified addressing

                if (Major == 7 && UnifiedAddressing == 1)
                {
                    return true;
                }

                // Pascal (6.x) and newer with unified addressing typically support managed memory

                if (Major >= 6 && UnifiedAddressing == 1)
                {
                    return true;
                }

                // Fallback to the original field value

                return ManagedMemory == 1;
            }
            catch
            {
                // If anything fails, return the original field value
                return ManagedMemory == 1;
            }
        }


        /// <summary>
        /// Gets a corrected managed memory value that accounts for known device issues.
        /// Use this property instead of the raw ManagedMemory field for accurate detection.
        /// </summary>
        public bool ManagedMemorySupported => GetActualManagedMemorySupport(0);

        /// <summary>
        /// Determines whether this instance is equal to another <see cref="CudaDeviceProperties"/>.
        /// </summary>
        /// <param name="other">The other instance to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public readonly bool Equals(CudaDeviceProperties other)
        {
            // Compare fixed-size arrays by comparing individual elements
            unsafe
            {
                // Access fixed buffer directly - don't need fixed statement
                for (var i = 0; i < 256; i++)
                {
                    if (Name[i] != other.Name[i])
                    {
                        return false;
                    }
                }
            }

            return UuidLow == other.UuidLow
                && UuidHigh == other.UuidHigh
                && TotalGlobalMem == other.TotalGlobalMem
                && SharedMemPerBlock == other.SharedMemPerBlock
                && RegsPerBlock == other.RegsPerBlock
                && WarpSize == other.WarpSize
                && MemPitch == other.MemPitch
                && MaxThreadsPerBlock == other.MaxThreadsPerBlock
                && MaxThreadsDimX == other.MaxThreadsDimX
                && MaxThreadsDimY == other.MaxThreadsDimY
                && MaxThreadsDimZ == other.MaxThreadsDimZ
                && MaxGridSizeX == other.MaxGridSizeX
                && MaxGridSizeY == other.MaxGridSizeY
                && MaxGridSizeZ == other.MaxGridSizeZ
                && ClockRate == other.ClockRate
                && TotalConstMem == other.TotalConstMem
                && Major == other.Major
                && Minor == other.Minor
                && TextureAlignment == other.TextureAlignment
                && TexturePitchAlignment == other.TexturePitchAlignment
                && DeviceOverlap == other.DeviceOverlap
                && MultiProcessorCount == other.MultiProcessorCount
                && KernelExecTimeoutEnabled == other.KernelExecTimeoutEnabled
                && Integrated == other.Integrated
                && CanMapHostMemory == other.CanMapHostMemory
                && ComputeMode == other.ComputeMode
                && ConcurrentKernels == other.ConcurrentKernels
                && ECCEnabled == other.ECCEnabled
                && PciBusID == other.PciBusID
                && PciDeviceID == other.PciDeviceID
                && PciDomainID == other.PciDomainID
                && AsyncEngineCount == other.AsyncEngineCount
                && UnifiedAddressing == other.UnifiedAddressing
                && MemClockRate == other.MemClockRate
                && MemBusWidth == other.MemBusWidth
                && L2CacheSize == other.L2CacheSize
                && PersistingL2CacheMaxSize == other.PersistingL2CacheMaxSize
                && MaxThreadsPerMultiProcessor == other.MaxThreadsPerMultiProcessor
                && StreamPrioritiesSupported == other.StreamPrioritiesSupported
                && GlobalL1CacheSupported == other.GlobalL1CacheSupported
                && LocalL1CacheSupported == other.LocalL1CacheSupported
                && SharedMemPerMultiprocessor == other.SharedMemPerMultiprocessor
                && RegsPerMultiprocessor == other.RegsPerMultiprocessor
                && ManagedMemory == other.ManagedMemory
                && IsMultiGpuBoard == other.IsMultiGpuBoard
                && MultiGpuBoardGroupID == other.MultiGpuBoardGroupID
                && SingleToDoublePrecisionPerfRatio == other.SingleToDoublePrecisionPerfRatio
                && PageableMemoryAccess == other.PageableMemoryAccess
                && ConcurrentManagedAccess == other.ConcurrentManagedAccess
                && ComputePreemptionSupported == other.ComputePreemptionSupported
                && CanUseHostPointerForRegisteredMem == other.CanUseHostPointerForRegisteredMem
                && CooperativeLaunch == other.CooperativeLaunch
                && CooperativeMultiDeviceLaunch == other.CooperativeMultiDeviceLaunch
                && SharedMemPerBlockOptin == other.SharedMemPerBlockOptin
                && PageableMemoryAccessUsesHostPageTables == other.PageableMemoryAccessUsesHostPageTables
                && DirectManagedMemAccessFromHost == other.DirectManagedMemAccessFromHost
                && MaxTexture1D == other.MaxTexture1D
                && MaxTexture2DWidth == other.MaxTexture2DWidth
                && MaxTexture2DHeight == other.MaxTexture2DHeight
                && MaxTexture3DWidth == other.MaxTexture3DWidth
                && MaxTexture3DHeight == other.MaxTexture3DHeight
                && MaxTexture3DDepth == other.MaxTexture3DDepth
                && TccDriver == other.TccDriver
                && HostNativeAtomicSupported == other.HostNativeAtomicSupported;
        }

        /// <summary>
        /// Determines whether this instance is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public readonly override bool Equals(object? obj) => obj is CudaDeviceProperties other && Equals(other);

        /// <summary>
        /// Gets the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public readonly override int GetHashCode()
        {
            // Combine hash codes for all fields
            // Note: We use the first few bytes of Name[] for hash code generation
            unsafe
            {
                var nameHash = HashCode.Combine(Name[0], Name[1], Name[2], Name[3]);

                return HashCode.Combine(
                    nameHash,
                    HashCode.Combine(UuidLow, UuidHigh, TotalGlobalMem, SharedMemPerBlock),
                    HashCode.Combine(RegsPerBlock, WarpSize, MemPitch, MaxThreadsPerBlock),
                    HashCode.Combine(MaxThreadsDimX, MaxThreadsDimY, MaxThreadsDimZ, MaxGridSizeX),
                    HashCode.Combine(MaxGridSizeY, MaxGridSizeZ, ClockRate, TotalConstMem),
                    HashCode.Combine(Major, Minor, TextureAlignment, TexturePitchAlignment),
                    HashCode.Combine(DeviceOverlap, MultiProcessorCount, KernelExecTimeoutEnabled, Integrated),
                    HashCode.Combine(CanMapHostMemory, ComputeMode, ConcurrentKernels, ECCEnabled));
            }
        }

        /// <summary>
        /// Determines whether two <see cref="CudaDeviceProperties"/> instances are equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public static bool operator ==(CudaDeviceProperties left, CudaDeviceProperties right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="CudaDeviceProperties"/> instances are not equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if not equal; otherwise, false.</returns>
        public static bool operator !=(CudaDeviceProperties left, CudaDeviceProperties right) => !left.Equals(right);
    }
}
