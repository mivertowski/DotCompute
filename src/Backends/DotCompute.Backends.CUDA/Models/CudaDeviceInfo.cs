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
            TotalMemory = device.GlobalMemorySize;
            TotalMemoryBytes = device.GlobalMemorySize;
            StreamingMultiprocessorCount = device.StreamingMultiprocessorCount;
            MultiprocessorCount = device.StreamingMultiprocessorCount;
            MaxThreadsPerBlock = device.MaxThreadsPerBlock;
            MaxThreadsPerMultiprocessor = device.MaxThreadsPerMultiprocessor;
            WarpSize = device.WarpSize;
            MemoryClockRate = device.MemoryClockRate;
            MemoryBusWidth = device.MemoryBusWidth;
            L2CacheSize = device.L2CacheSize;
            UnifiedAddressing = device.UnifiedAddressing;
            CanMapHostMemory = device.CanMapHostMemory;
            ConcurrentKernels = device.ConcurrentManagedAccess;
            MemoryBandwidth = device.MemoryBandwidthGBps;
            Architecture = device.ArchitectureGeneration;
            PciDomainId = device.PciDomainId;
            PciBusId = device.PciBusId;
            PciDeviceId = device.PciDeviceId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaDeviceInfo"/> class for testing/P2P operations.
        /// </summary>
        public CudaDeviceInfo()
        {
            _device = null;
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
        /// Gets or sets the total memory in bytes.
        /// </summary>
        public ulong TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets the total memory in bytes (alias for compatibility).
        /// </summary>
        public ulong TotalMemoryBytes { get; set; }

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
        /// Gets or sets whether this device is an integrated GPU.
        /// Integrated GPUs share system memory and are typically lower performance than discrete GPUs.
        /// </summary>
        public bool IntegratedGpu { get; set; }

        /// <summary>
        /// Gets the PCI bus information as a formatted string.
        /// </summary>
        public string PciInfo => $"{PciDomainId:X4}:{PciBusId:X2}:{PciDeviceId:X2}";
    }
}