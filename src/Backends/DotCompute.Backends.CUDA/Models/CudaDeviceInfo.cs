// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Models
{
    /// <summary>
    /// Comprehensive information about a CUDA device.
    /// </summary>
    public sealed class CudaDeviceInfo
    {
        private readonly CudaDevice? _device;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaDeviceInfo"/> class from a CudaDevice.
        /// </summary>
        internal CudaDeviceInfo(CudaDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            DeviceId = device.DeviceId;
            Name = device.Name;
            ComputeCapability = $"{device.ComputeCapabilityMajor}.{device.ComputeCapabilityMinor}";
            ComputeCapabilityMajor = device.ComputeCapabilityMajor;
            ComputeCapabilityMinor = device.ComputeCapabilityMinor;
            TotalMemory = (long)device.GlobalMemorySize;
            TotalMemoryBytes = (long)device.GlobalMemorySize;
            ManagedMemory = device.SupportsManagedMemory;
            ConcurrentManagedAccess = device.SupportsConcurrentKernels; // Approximation based on available property
            PageableMemoryAccess = device.SupportsManagedMemory; // Approximation based on available property
            StreamingMultiprocessorCount = device.StreamingMultiprocessorCount;
            MultiprocessorCount = device.StreamingMultiprocessorCount;
            MaxThreadsPerBlock = device.MaxThreadsPerBlock;
            MaxThreadsPerMultiprocessor = device.MaxThreadsPerMultiprocessor;
            WarpSize = device.WarpSize;
            MemoryClockRate = device.MemoryClockRate;
            ClockRate = device.ClockRate;
            MemoryBusWidth = device.MemoryBusWidth;
            L2CacheSize = device.L2CacheSize;
            UnifiedAddressing = device.SupportsManagedMemory; // Use available property
            CanMapHostMemory = device.SupportsManagedMemory; // Use available property
            ConcurrentKernels = device.ConcurrentManagedAccess;
            MemoryBandwidth = device.MemoryBandwidthGBps;
            Architecture = device.ArchitectureGeneration;
            PciDomainId = device.PciDomainId;
            PciBusId = device.PciBusId;
            PciDeviceId = device.PciDeviceId;
            SupportsManagedMemory = device.SupportsManagedMemory;
            MultiProcessorCount = device.StreamingMultiprocessorCount;
            SharedMemoryPerBlock = (int)device.SharedMemoryPerBlock;
            RegistersPerBlock = 32768; // Default for modern GPUs
            MaxBlockDimX = 1024; // Standard max block dimension
            MaxBlockDimY = 1024; // Standard max block dimension
            MaxBlockDimZ = 64;   // Standard max block dimension

            // Initialize grid dimensions to sensible defaults

            MaxGridDimX = 2147483647; // Max int32 value for most GPUs
            MaxGridDimY = 65535;     // Standard max grid dimension
            MaxGridDimZ = 65535;     // Standard max grid dimension

            // Initialize texture dimensions to defaults

            MaxTexture1DSize = 131072; // 128K default
            MaxTexture2DWidth = 131072;
            MaxTexture2DHeight = 65536;
            MaxTexture3DWidth = 16384;
            MaxTexture3DHeight = 16384;
            MaxTexture3DDepth = 16384;

            // Initialize feature flags to reasonable defaults

            KernelExecutionTimeout = false;
            EccEnabled = false;
            TccDriver = false;
            AsyncEngineCount = 2;
            StreamPrioritiesSupported = true;
            GlobalL1CacheSupported = true;
            LocalL1CacheSupported = true;
            IsMultiGpuBoard = false;
            MultiGpuBoardGroupId = 0;
            HostNativeAtomicSupported = true;
            SingleToDoublePrecisionPerfRatio = 2;
            ComputePreemptionSupported = true;
            CanUseHostPointerForRegisteredMem = true;
            CooperativeLaunch = true;
            CooperativeMultiDeviceLaunch = false;
            MaxSharedMemoryPerMultiprocessor = 65536;
            PageableMemoryAccessUsesHostPageTables = false;
            DirectManagedMemAccessFromHost = true;

            // Initialize new properties with sensible defaults
            TensorCoreCount = CalculateTensorCoreCount(ComputeCapabilityMajor, ComputeCapabilityMinor, StreamingMultiprocessorCount);
            SupportsNVLink = false; // Most consumer GPUs don't support NVLink
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaDeviceInfo"/> class for testing/P2P operations.
        /// </summary>
        public CudaDeviceInfo()
        {
            _device = null;

            // Initialize new properties with sensible defaults for parameterless constructor

            TensorCoreCount = 0;
            SupportsNVLink = false;
        }

        /// <summary>
        /// Gets or sets the device ID.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compute capability (e.g., "8.9").
        /// </summary>
        public string ComputeCapability { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compute capability major version.
        /// </summary>
        public int ComputeCapabilityMajor { get; set; }

        /// <summary>
        /// Gets or sets the compute capability minor version.
        /// </summary>
        public int ComputeCapabilityMinor { get; set; }

        /// <summary>
        /// Gets the compute capability major version (alias for ComputeCapabilityMajor).
        /// </summary>
        public int Major => ComputeCapabilityMajor;

        /// <summary>
        /// Gets the compute capability minor version (alias for ComputeCapabilityMinor).
        /// </summary>
        public int Minor => ComputeCapabilityMinor;

        /// <summary>
        /// Gets or sets the total memory in bytes.
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets the total memory in bytes (alias for compatibility).
        /// </summary>
        public long TotalMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of streaming multiprocessors.
        /// </summary>
        public int StreamingMultiprocessorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of multiprocessors (alias for compatibility).
        /// </summary>
        public int MultiprocessorCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum threads per block.
        /// </summary>
        public int MaxThreadsPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the maximum threads per multiprocessor.
        /// </summary>
        public int MaxThreadsPerMultiprocessor { get; set; }

        /// <summary>
        /// Gets or sets the warp size.
        /// </summary>
        public int WarpSize { get; set; }

        /// <summary>
        /// Gets or sets the memory clock rate in kHz.
        /// </summary>
        public int MemoryClockRate { get; set; }

        /// <summary>
        /// Gets or sets the GPU core clock rate in kHz.
        /// </summary>
        public int ClockRate { get; set; }

        /// <summary>
        /// Gets or sets the memory bus width in bits.
        /// </summary>
        public int MemoryBusWidth { get; set; }

        /// <summary>
        /// Gets or sets the L2 cache size in bytes.
        /// </summary>
        public int L2CacheSize { get; set; }

        /// <summary>
        /// Gets or sets whether unified addressing is supported.
        /// </summary>
        public bool UnifiedAddressing { get; set; }

        /// <summary>
        /// Gets or sets whether the device can map host memory.
        /// </summary>
        public bool CanMapHostMemory { get; set; }

        /// <summary>
        /// Gets or sets whether concurrent kernels are supported.
        /// </summary>
        public bool ConcurrentKernels { get; set; }

        /// <summary>
        /// Gets or sets the memory bandwidth in GB/s.
        /// </summary>
        public double MemoryBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the architecture generation.
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the PCI domain ID.
        /// </summary>
        public int PciDomainId { get; set; }

        /// <summary>
        /// Gets or sets the PCI bus ID.
        /// </summary>
        public int PciBusId { get; set; }

        /// <summary>
        /// Gets or sets the PCI device ID.
        /// </summary>
        public int PciDeviceId { get; set; }

        /// <summary>
        /// Gets the estimated number of CUDA cores based on compute capability and SM count.
        /// </summary>
        public int CudaCores
        {
            get
            {
                var smCount = StreamingMultiprocessorCount;
                return ComputeCapabilityMajor switch
                {
                    2 => smCount * 32,  // Fermi
                    3 => smCount * 192, // Kepler
                    5 => smCount * 128, // Maxwell
                    6 => smCount * (ComputeCapabilityMinor == 0 ? 64 : 128), // Pascal
                    7 => smCount * 64,  // Volta/Turing
                    8 => smCount * 64,  // Ampere
                    9 => smCount * 128, // Ada Lovelace/Hopper
                    _ => smCount * 64   // Default estimate
                };
            }
        }

        /// <summary>
        /// Gets whether this is an RTX series GPU.
        /// </summary>
        public bool IsRTX => Name.Contains("RTX", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Calculates the estimated number of Tensor Cores based on compute capability and SM count.
        /// </summary>
        /// <param name="major">Compute capability major version</param>
        /// <param name="minor">Compute capability minor version</param>
        /// <param name="smCount">Number of streaming multiprocessors</param>
        /// <returns>Estimated Tensor Core count</returns>
        private static int CalculateTensorCoreCount(int major, int minor, int smCount)
        {
            // Tensor Cores were introduced in Volta (SM 7.0)
            return major switch
            {
                7 when minor >= 0 => smCount * 8,   // Volta: 8 Tensor Cores per SM
                7 when minor >= 5 => smCount * 8,   // Turing: 8 Tensor Cores per SM (RT Cores variant)
                8 when minor >= 0 => smCount * 4,   // Ampere: 4 3rd gen Tensor Cores per SM
                8 when minor >= 6 => smCount * 4,   // Ada Lovelace: 4 4th gen Tensor Cores per SM
                9 when minor >= 0 => smCount * 4,   // Hopper: 4 4th gen Tensor Cores per SM
                _ => 0  // No Tensor Cores for older architectures
            };
        }

        /// <summary>
        /// Gets or sets whether this device is an integrated GPU.
        /// Integrated GPUs share system memory and are typically lower performance than discrete GPUs.
        /// </summary>
        public bool IntegratedGpu { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports managed memory.
        /// </summary>
        public bool ManagedMemory { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports concurrent managed access.
        /// </summary>
        public bool ConcurrentManagedAccess { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports pageable memory access.
        /// </summary>
        public bool PageableMemoryAccess { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports managed memory (alias for compatibility).
        /// </summary>
        public bool SupportsManagedMemory { get; set; }

        /// <summary>
        /// Gets or sets the number of multiprocessors (alternative naming for compatibility).
        /// </summary>
        public int MultiProcessorCount { get; set; }

        /// <summary>
        /// Gets or sets the shared memory per block in bytes.
        /// </summary>
        public int SharedMemoryPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the number of registers per block.
        /// </summary>
        public int RegistersPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the maximum X dimension of a block.
        /// </summary>
        public int MaxBlockDimX { get; set; }

        /// <summary>
        /// Gets or sets the maximum Y dimension of a block.
        /// </summary>
        public int MaxBlockDimY { get; set; }

        /// <summary>
        /// Gets or sets the maximum Z dimension of a block.
        /// </summary>
        public int MaxBlockDimZ { get; set; }

        /// <summary>
        /// Gets the PCI bus information as a formatted string.
        /// </summary>
        public string PciInfo => $"{PciDomainId:X4}:{PciBusId:X2}:{PciDeviceId:X2}";

        // Missing grid dimension properties


        /// <summary>
        /// Gets or sets the maximum X dimension of a grid.
        /// </summary>
        public int MaxGridDimX { get; set; }


        /// <summary>
        /// Gets or sets the maximum Y dimension of a grid.
        /// </summary>
        public int MaxGridDimY { get; set; }


        /// <summary>
        /// Gets or sets the maximum Z dimension of a grid.
        /// </summary>
        public int MaxGridDimZ { get; set; }

        // Missing texture properties


        /// <summary>
        /// Gets or sets the maximum 1D texture size.
        /// </summary>
        public int MaxTexture1DSize { get; set; }


        /// <summary>
        /// Gets or sets the maximum 2D texture width.
        /// </summary>
        public int MaxTexture2DWidth { get; set; }


        /// <summary>
        /// Gets or sets the maximum 2D texture height.
        /// </summary>
        public int MaxTexture2DHeight { get; set; }


        /// <summary>
        /// Gets or sets the maximum 3D texture width.
        /// </summary>
        public int MaxTexture3DWidth { get; set; }


        /// <summary>
        /// Gets or sets the maximum 3D texture height.
        /// </summary>
        public int MaxTexture3DHeight { get; set; }


        /// <summary>
        /// Gets or sets the maximum 3D texture depth.
        /// </summary>
        public int MaxTexture3DDepth { get; set; }

        // Missing execution and feature properties


        /// <summary>
        /// Gets or sets whether kernel execution has timeout enabled.
        /// </summary>
        public bool KernelExecutionTimeout { get; set; }


        /// <summary>
        /// Gets or sets whether ECC (error correction) is enabled.
        /// </summary>
        public bool EccEnabled { get; set; }


        /// <summary>
        /// Gets or sets whether TCC driver is enabled.
        /// </summary>
        public bool TccDriver { get; set; }


        /// <summary>
        /// Gets or sets the number of asynchronous copy engines.
        /// </summary>
        public int AsyncEngineCount { get; set; }


        /// <summary>
        /// Gets or sets the maximum threads per multiprocessor.
        /// </summary>
        public int MaxThreadsPerMultiProcessor { get; set; }


        /// <summary>
        /// Gets or sets whether stream priorities are supported.
        /// </summary>
        public bool StreamPrioritiesSupported { get; set; }


        /// <summary>
        /// Gets or sets whether global L1 cache is supported.
        /// </summary>
        public bool GlobalL1CacheSupported { get; set; }


        /// <summary>
        /// Gets or sets whether local L1 cache is supported.
        /// </summary>
        public bool LocalL1CacheSupported { get; set; }


        /// <summary>
        /// Gets or sets whether this is a multi-GPU board.
        /// </summary>
        public bool IsMultiGpuBoard { get; set; }


        /// <summary>
        /// Gets or sets the multi-GPU board group identifier.
        /// </summary>
        public int MultiGpuBoardGroupId { get; set; }


        /// <summary>
        /// Gets or sets whether host native atomic operations are supported.
        /// </summary>
        public bool HostNativeAtomicSupported { get; set; }


        /// <summary>
        /// Gets or sets the single to double precision performance ratio.
        /// </summary>
        public int SingleToDoublePrecisionPerfRatio { get; set; }


        /// <summary>
        /// Gets or sets whether compute preemption is supported.
        /// </summary>
        public bool ComputePreemptionSupported { get; set; }


        /// <summary>
        /// Gets or sets whether host pointers can be used for registered memory.
        /// </summary>
        public bool CanUseHostPointerForRegisteredMem { get; set; }


        /// <summary>
        /// Gets or sets whether cooperative kernel launches are supported.
        /// </summary>
        public bool CooperativeLaunch { get; set; }


        /// <summary>
        /// Gets or sets whether cooperative multi-device launches are supported.
        /// </summary>
        public bool CooperativeMultiDeviceLaunch { get; set; }


        /// <summary>
        /// Gets or sets the maximum shared memory per multiprocessor.
        /// </summary>
        public int MaxSharedMemoryPerMultiprocessor { get; set; }


        /// <summary>
        /// Gets or sets whether pageable memory access uses host page tables.
        /// </summary>
        public bool PageableMemoryAccessUsesHostPageTables { get; set; }


        /// <summary>
        /// Gets or sets whether direct managed memory access from host is supported.
        /// </summary>
        public bool DirectManagedMemAccessFromHost { get; set; }

        /// <summary>
        /// Gets or sets the number of Tensor Cores available on this device.
        /// Tensor Cores provide accelerated mixed-precision matrix operations for AI workloads.
        /// </summary>
        public int TensorCoreCount { get; set; }

        /// <summary>
        /// Gets or sets the total global memory size in bytes.
        /// This is an alias for TotalMemory to provide consistency with CUDA terminology.
        /// </summary>
        public long TotalGlobalMemory
        {

            get => TotalMemory;

            set => TotalMemory = value;

        }

        /// <summary>
        /// Gets or sets whether this device supports NVLink for high-speed inter-GPU communication.
        /// NVLink provides much higher bandwidth than PCIe for multi-GPU systems.
        /// </summary>
        public bool SupportsNVLink { get; set; }
    }
}