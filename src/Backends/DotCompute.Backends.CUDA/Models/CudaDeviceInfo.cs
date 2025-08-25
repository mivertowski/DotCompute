// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.P2P.Models
{
    /// <summary>
    /// Information about a CUDA device for P2P operations.
    /// </summary>
    public sealed class CudaDeviceInfo
    {
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
        /// Gets or sets the total memory in bytes.
        /// </summary>
        public ulong TotalMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of multiprocessors.
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
    }
}