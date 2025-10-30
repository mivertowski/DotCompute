// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Represents CUDA device information.
    /// </summary>
    public sealed class DeviceInfo
    {
        /// <summary>
        /// Gets or sets the device index.
        /// </summary>
        public int DeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the device ID.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compute capability (major.minor).
        /// </summary>
        public (int Major, int Minor) ComputeCapability { get; set; }

        /// <summary>
        /// Gets or sets the architecture generation name (e.g., "Ada Lovelace", "Ampere", "Turing").
        /// </summary>
        public string ArchitectureGeneration { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this device is an RTX 2000 Ada generation GPU.
        /// </summary>
        public bool IsRTX2000Ada { get; set; }

        /// <summary>
        /// Gets or sets the total global memory in bytes.
        /// </summary>
        public long GlobalMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the shared memory per block in bytes.
        /// </summary>
        public int SharedMemoryPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the number of multiprocessors.
        /// </summary>
        public int MultiprocessorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of streaming multiprocessors.
        /// </summary>
        public int StreamingMultiprocessors { get; set; }

        /// <summary>
        /// Gets or sets the estimated number of CUDA cores based on the architecture and SM count.
        /// </summary>
        public int EstimatedCudaCores { get; set; }

        /// <summary>
        /// Gets or sets the maximum threads per block.
        /// </summary>
        public int MaxThreadsPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the warp size.
        /// </summary>
        public int WarpSize { get; set; } = 32;

        /// <summary>
        /// Gets or sets whether the device supports unified memory.
        /// </summary>
        public bool SupportsUnifiedMemory { get; set; }

        /// <summary>
        /// Gets or sets the memory bandwidth in GB/s.
        /// </summary>
        public double MemoryBandwidthGBps { get; set; }

        /// <summary>
        /// Gets or sets the L2 cache size in bytes.
        /// </summary>
        public int L2CacheSize { get; set; }

        /// <summary>
        /// Gets or sets the GPU core clock rate in kHz.
        /// </summary>
        public int ClockRate { get; set; }

        /// <summary>
        /// Gets or sets the memory clock rate in kHz.
        /// </summary>
        public int MemoryClockRate { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports unified addressing.
        /// </summary>
        public bool SupportsUnifiedAddressing { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports managed memory.
        /// </summary>
        public bool SupportsManagedMemory { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports concurrent kernel execution.
        /// </summary>
        public bool SupportsConcurrentKernels { get; set; }

        /// <summary>
        /// Gets or sets whether ECC (Error-Correcting Code) memory is enabled on the device.
        /// </summary>
        public bool IsECCEnabled { get; set; }

        /// <summary>
        /// Gets or sets the total memory available on the device in bytes.
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets the currently available memory on the device in bytes.
        /// </summary>
        public long AvailableMemory { get; set; }
    }
}
