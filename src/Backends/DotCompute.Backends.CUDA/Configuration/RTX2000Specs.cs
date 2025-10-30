// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Configuration
{
    /// <summary>
    /// RTX 2000 Ada device specifications.
    /// </summary>
    public static class RTX2000Specs
    {
        /// <summary>
        /// The streaming multiprocessors.
        /// </summary>
        /// <summary>
        /// Number of streaming multiprocessors.
        /// </summary>
        public const int StreamingMultiprocessors = 24;
        /// <summary>
        /// The threads per s m.
        /// </summary>

        /// <summary>
        /// Maximum threads per streaming multiprocessor.
        /// </summary>
        public const int ThreadsPerSM = 1536;
        /// <summary>
        /// The warps per s m.
        /// </summary>

        /// <summary>
        /// Maximum warps per streaming multiprocessor (32 threads per warp).
        /// </summary>
        public const int WarpsPerSM = 48;
        /// <summary>
        /// The max threads per block.
        /// </summary>

        /// <summary>
        /// Maximum threads per block.
        /// </summary>
        public const int MaxThreadsPerBlock = 1024;
        /// <summary>
        /// The shared memory per s m.
        /// </summary>

        /// <summary>
        /// Shared memory per streaming multiprocessor in bytes (100KB).
        /// </summary>
        public const int SharedMemoryPerSM = 102400;
        /// <summary>
        /// The registers per s m.
        /// </summary>

        /// <summary>
        /// Registers per streaming multiprocessor.
        /// </summary>
        public const int RegistersPerSM = 65536;
        /// <summary>
        /// The l2 cache size.
        /// </summary>

        /// <summary>
        /// L2 cache size in bytes (32MB).
        /// </summary>
        public const int L2CacheSize = 32 * 1024 * 1024;
        /// <summary>
        /// The base clock m hz.
        /// </summary>

        /// <summary>
        /// Base clock frequency in MHz.
        /// </summary>
        public const double BaseClockMHz = 2610.0;
        /// <summary>
        /// The boost clock m hz.
        /// </summary>

        /// <summary>
        /// Boost clock frequency in MHz.
        /// </summary>
        public const double BoostClockMHz = 2850.0;
        /// <summary>
        /// The memory bus width.
        /// </summary>

        /// <summary>
        /// Memory bus width in bits.
        /// </summary>
        public const int MemoryBusWidth = 192;
        /// <summary>
        /// The memory bandwidth g bs.
        /// </summary>

        /// <summary>
        /// Memory bandwidth in GB/s.
        /// </summary>
        public const int MemoryBandwidthGBs = 288;
    }
}
