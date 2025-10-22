// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Optimization.Types
{
    /// <summary>
    /// CUDA device attributes for querying device capabilities.
    /// </summary>
    internal enum CudaDeviceAttribute
    {
        /// <summary>
        /// Maximum number of threads per block.
        /// </summary>
        MaxThreadsPerBlock = 1,

        /// <summary>
        /// Maximum X dimension of a block.
        /// </summary>
        MaxBlockDimX = 2,

        /// <summary>
        /// Maximum Y dimension of a block.
        /// </summary>
        MaxBlockDimY = 3,

        /// <summary>
        /// Maximum Z dimension of a block.
        /// </summary>
        MaxBlockDimZ = 4,

        /// <summary>
        /// Maximum X dimension of a grid.
        /// </summary>
        MaxGridDimX = 5,

        /// <summary>
        /// Maximum Y dimension of a grid.
        /// </summary>
        MaxGridDimY = 6,

        /// <summary>
        /// Maximum Z dimension of a grid.
        /// </summary>
        MaxGridDimZ = 7,

        /// <summary>
        /// Maximum shared memory available per block in bytes.
        /// </summary>
        MaxSharedMemoryPerBlock = 8,

        /// <summary>
        /// Total constant memory available on the device in bytes.
        /// </summary>
        TotalConstantMemory = 9,

        /// <summary>
        /// Warp size in threads.
        /// </summary>
        WarpSize = 10,

        /// <summary>
        /// Maximum pitch in bytes allowed by memory copies.
        /// </summary>
        MaxPitch = 11,

        /// <summary>
        /// Maximum number of 32-bit registers available per block.
        /// </summary>
        MaxRegistersPerBlock = 12,

        /// <summary>
        /// Clock frequency in kilohertz.
        /// </summary>
        ClockRate = 13,

        /// <summary>
        /// Alignment requirement for textures.
        /// </summary>
        TextureAlignment = 14,

        /// <summary>
        /// Number of multiprocessors on the device.
        /// </summary>
        MultiprocessorCount = 16,

        /// <summary>
        /// Maximum number of 32-bit registers available per multiprocessor.
        /// </summary>
        MaxRegistersPerMultiprocessor = 82,

        /// <summary>
        /// Maximum shared memory available per multiprocessor in bytes.
        /// </summary>
        MaxSharedMemoryPerMultiprocessor = 81,

        /// <summary>
        /// Maximum number of blocks per multiprocessor.
        /// </summary>
        MaxBlocksPerMultiprocessor = 106,

        /// <summary>
        /// Maximum number of warps per multiprocessor.
        /// </summary>
        MaxWarpsPerMultiprocessor = 67,

        /// <summary>
        /// Major compute capability version number.
        /// </summary>
        ComputeCapabilityMajor = 75,

        /// <summary>
        /// Minor compute capability version number.
        /// </summary>
        ComputeCapabilityMinor = 76,

        /// <summary>
        /// Maximum device depth for dynamic parallelism.
        /// </summary>
        MaxDeviceDepth = 111
    }
}