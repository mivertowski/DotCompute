// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Models
{
    /// <summary>
    /// CUDA device properties relevant for occupancy calculations.
    /// </summary>
    internal class DeviceProperties
    {
        /// <summary>
        /// Gets or sets the device ID.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of threads per block.
        /// </summary>
        public int MaxThreadsPerBlock { get; set; }

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
        /// Gets or sets the maximum grid size.
        /// </summary>
        public int MaxGridSize { get; set; }

        /// <summary>
        /// Gets or sets the warp size.
        /// </summary>
        public int WarpSize { get; set; }

        /// <summary>
        /// Gets or sets the number of registers per block.
        /// </summary>
        public int RegistersPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the number of registers per multiprocessor.
        /// </summary>
        public int RegistersPerMultiprocessor { get; set; }

        /// <summary>
        /// Gets or sets the shared memory per block in bytes.
        /// </summary>
        public int SharedMemoryPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the shared memory per multiprocessor in bytes.
        /// </summary>
        public int SharedMemoryPerMultiprocessor { get; set; }

        /// <summary>
        /// Gets or sets the number of multiprocessors.
        /// </summary>
        public int MultiprocessorCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of blocks per multiprocessor.
        /// </summary>
        public int MaxBlocksPerMultiprocessor { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of warps per multiprocessor.
        /// </summary>
        public int MaxWarpsPerMultiprocessor { get; set; }

        /// <summary>
        /// Gets or sets the compute capability (major.minor combined).
        /// </summary>
        public int ComputeCapability { get; set; }

        /// <summary>
        /// Gets or sets the maximum device depth for dynamic parallelism.
        /// </summary>
        public int MaxDeviceDepth { get; set; }
    }
}
