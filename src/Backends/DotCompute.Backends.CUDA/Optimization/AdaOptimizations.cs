// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.CUDA.Advanced
{

    /// <summary>
    /// RTX 2000 Ada Lovelace generation specific optimizations and utilities
    /// </summary>
    public static class AdaOptimizations
    {
        /// <summary>
        /// RTX 2000 Ada device specifications
        /// </summary>
        internal static class RTX2000Specs
        {
            /// <summary>
            /// The streaming multiprocessors.
            /// </summary>
            public const int StreamingMultiprocessors = 24;
            /// <summary>
            /// The threads per s m.
            /// </summary>
            public const int ThreadsPerSM = 1536;
            /// <summary>
            /// The warps per s m.
            /// </summary>
            public const int WarpsPerSM = 48; // 32 threads per warp
            /// <summary>
            /// The max threads per block.
            /// </summary>
            public const int MaxThreadsPerBlock = 1024;
            /// <summary>
            /// The shared memory per s m.
            /// </summary>
            public const int SharedMemoryPerSM = 102400; // 100KB
            /// <summary>
            /// The registers per s m.
            /// </summary>
            public const int RegistersPerSM = 65536;
            /// <summary>
            /// The l2 cache size.
            /// </summary>
            public const int L2CacheSize = 32 * 1024 * 1024; // 32MB
            /// <summary>
            /// The base clock m hz.
            /// </summary>
            public const double BaseClockMHz = 2610.0;
            /// <summary>
            /// The boost clock m hz.
            /// </summary>
            public const double BoostClockMHz = 2850.0;
            /// <summary>
            /// The memory bus width.
            /// </summary>
            public const int MemoryBusWidth = 192; // bits
            /// <summary>
            /// The memory bandwidth g bs.
            /// </summary>
            public const int MemoryBandwidthGBs = 288; // GB/s
        }

        /// <summary>
        /// Gets optimal block size for RTX 2000 Ada based on workload characteristics
        /// </summary>
        public static int GetOptimalBlockSize(WorkloadType workloadType)
        {
            return workloadType switch
            {
                WorkloadType.ComputeIntensive => 512, // Maximize ALU utilization
                WorkloadType.MemoryBandwidthBound => 256, // Balance memory access
                WorkloadType.SharedMemoryIntensive => 384, // Optimize for 100KB shared memory
                WorkloadType.TensorOperations => 512, // Utilize tensor cores efficiently
                WorkloadType.Mixed => 512, // Default optimal for Ada
                _ => 256
            };
        }

        /// <summary>
        /// Calculates optimal shared memory configuration for Ada generation
        /// </summary>
        public static SharedMemoryConfig GetOptimalSharedMemoryConfig(int blockSize, int dataSize)
        {
            var totalThreads = blockSize;
            var sharedMemPerThread = Math.Min(dataSize, RTX2000Specs.SharedMemoryPerSM / totalThreads);

            // Ada can use up to 100KB shared memory with carveout configuration
            var optimalSharedMem = Math.Min(
                totalThreads * sharedMemPerThread,
                RTX2000Specs.SharedMemoryPerSM
            );

            return new SharedMemoryConfig
            {
                BytesPerBlock = optimalSharedMem,
                BytesPerThread = sharedMemPerThread,
                CanUse100KB = optimalSharedMem > 49152, // > 48KB
                RecommendedCarveout = optimalSharedMem > 65536 ?
                    SharedMemoryCarveout.Prefer100KB : SharedMemoryCarveout.Default
            };
        }

        /// <summary>
        /// Gets optimal grid configuration for RTX 2000 Ada
        /// </summary>
        public static GridConfig GetOptimalGridConfig(int totalElements, int blockSize)
        {
            var blocksNeeded = (totalElements + blockSize - 1) / blockSize;

            // Ada has 24 SMs, aim for 2-3 blocks per SM for optimal occupancy
            var optimalBlocks = Math.Min(blocksNeeded, RTX2000Specs.StreamingMultiprocessors * 3);

            // Distribute across grid dimensions for better load balancing
            var gridX = Math.Min(optimalBlocks, 65535);
            var gridY = Math.Min((optimalBlocks + gridX - 1) / gridX, 65535);
            var gridZ = Math.Min((optimalBlocks + gridX * gridY - 1) / (gridX * gridY), 65535);

            return new GridConfig
            {
                X = gridX,
                Y = gridY,
                Z = gridZ,
                TotalBlocks = gridX * gridY * gridZ,
                ElementsPerBlock = blockSize,
                Occupancy = CalculateOccupancy(blockSize)
            };
        }

        /// <summary>
        /// Calculates theoretical occupancy for Ada generation
        /// </summary>
        public static double CalculateOccupancy(int blockSize)
        {
            var blocksPerSM = RTX2000Specs.ThreadsPerSM / blockSize;
            var activeWarps = blocksPerSM * (blockSize / 32); // 32 threads per warp
            var maxWarps = RTX2000Specs.WarpsPerSM;

            return Math.Min(1.0, (double)activeWarps / maxWarps);
        }

        /// <summary>
        /// Gets optimal memory access pattern for Ada generation
        /// </summary>
        public static MemoryAccessPattern GetOptimalMemoryPattern(int elements, int elementSize)
        {
            var totalSize = elements * elementSize;

            // Ada's memory hierarchy optimizations - map to canonical patterns
            if (totalSize <= RTX2000Specs.SharedMemoryPerSM)
            {
                // Small data fits in shared memory - use tiled pattern
                return MemoryAccessPattern.Tiled;
            }
            else if (totalSize <= RTX2000Specs.L2CacheSize)
            {
                // Data fits in L2 cache - use sequential pattern
                return MemoryAccessPattern.Sequential;
            }
            else
            {
                // Large data - use coalesced pattern for optimal bandwidth
                return MemoryAccessPattern.Coalesced;
            }
        }

        /// <summary>
        /// Validates configuration against RTX 2000 Ada capabilities
        /// </summary>
        public static UnifiedValidationResult ValidateForAda(int gridX, int gridY, int gridZ,
            int blockX, int blockY, int blockZ, int sharedMem, ILogger? logger = null)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            var blockSize = blockX * blockY * blockZ;
            var totalBlocks = gridX * gridY * gridZ;

            // Check block size limits
            if (blockSize > RTX2000Specs.MaxThreadsPerBlock)
            {
                errors.Add($"Block size {blockSize} exceeds Ada limit of {RTX2000Specs.MaxThreadsPerBlock}");
            }

            // Check shared memory limits
            if (sharedMem > RTX2000Specs.SharedMemoryPerSM)
            {
                errors.Add($"Shared memory {sharedMem} bytes exceeds Ada limit of {RTX2000Specs.SharedMemoryPerSM}");
            }

            // Performance warnings
            var occupancy = CalculateOccupancy(blockSize);
            if (occupancy < 0.5)
            {
                warnings.Add($"Low occupancy {occupancy:P1} detected. Consider adjusting block size");
            }

            if (blockSize % 32 != 0)
            {
                warnings.Add("Block size is not a multiple of warp size (32), may reduce efficiency");
            }

            var blocksPerSM = totalBlocks / RTX2000Specs.StreamingMultiprocessors;
            if (blocksPerSM < 2)
            {
                warnings.Add("Less than 2 blocks per SM may not fully utilize Ada's resources");
            }

            logger?.LogInformation("Ada validation: Occupancy={Occupancy:P1}, BlocksPerSM={BlocksPerSM}",
                occupancy, blocksPerSM);

            return new UnifiedValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                Occupancy = occupancy,
                BlocksPerSM = blocksPerSM
            };
        }
    }
    /// <summary>
    /// An workload type enumeration.
    /// </summary>

    /// <summary>
    /// Workload types for optimization decisions
    /// </summary>
    public enum WorkloadType
    {
        ComputeIntensive,
        MemoryBandwidthBound,
        SharedMemoryIntensive,
        TensorOperations,
        Mixed
    }

    /// <summary>
    /// Shared memory configuration for Ada generation
    /// </summary>
    public sealed class SharedMemoryConfig
    {
        /// <summary>
        /// Gets or sets the bytes per block.
        /// </summary>
        /// <value>The bytes per block.</value>
        public int BytesPerBlock { get; set; }
        /// <summary>
        /// Gets or sets the bytes per thread.
        /// </summary>
        /// <value>The bytes per thread.</value>
        public int BytesPerThread { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether use100 k b.
        /// </summary>
        /// <value>The can use100 k b.</value>
        public bool CanUse100KB { get; set; }
        /// <summary>
        /// Gets or sets the recommended carveout.
        /// </summary>
        /// <value>The recommended carveout.</value>
        public SharedMemoryCarveout RecommendedCarveout { get; set; }
    }
    /// <summary>
    /// An shared memory carveout enumeration.
    /// </summary>

    /// <summary>
    /// Shared memory carveout preferences for Ada
    /// </summary>
    public enum SharedMemoryCarveout
    {
        Default,      // 48KB shared, 16KB L1
        Prefer100KB,  // 100KB shared, minimal L1
        PreferL1      // 16KB shared, 48KB L1
    }

    /// <summary>
    /// Grid configuration for optimal execution
    /// </summary>
    public sealed class GridConfig
    {
        /// <summary>
        /// Gets or sets the x.
        /// </summary>
        /// <value>The x.</value>
        public int X { get; set; }
        /// <summary>
        /// Gets or sets the y.
        /// </summary>
        /// <value>The y.</value>
        public int Y { get; set; }
        /// <summary>
        /// Gets or sets the z.
        /// </summary>
        /// <value>The z.</value>
        public int Z { get; set; }
        /// <summary>
        /// Gets or sets the total blocks.
        /// </summary>
        /// <value>The total blocks.</value>
        public int TotalBlocks { get; set; }
        /// <summary>
        /// Gets or sets the elements per block.
        /// </summary>
        /// <value>The elements per block.</value>
        public int ElementsPerBlock { get; set; }
        /// <summary>
        /// Gets or sets the occupancy.
        /// </summary>
        /// <value>The occupancy.</value>
        public double Occupancy { get; set; }
    }


    /// <summary>
    /// Validation result for Ada configurations
    /// </summary>
    public sealed class UnifiedValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether valid.
        /// </summary>
        /// <value>The is valid.</value>
        public bool IsValid { get; set; }
        /// <summary>
        /// Gets or initializes the errors.
        /// </summary>
        /// <value>The errors.</value>
        public IList<string> Errors { get; init; } = [];
        /// <summary>
        /// Gets or initializes the warnings.
        /// </summary>
        /// <value>The warnings.</value>
        public IList<string> Warnings { get; init; } = [];
        /// <summary>
        /// Gets or sets the occupancy.
        /// </summary>
        /// <value>The occupancy.</value>
        public double Occupancy { get; set; }
        /// <summary>
        /// Gets or sets the blocks per s m.
        /// </summary>
        /// <value>The blocks per s m.</value>
        public int BlocksPerSM { get; set; }
    }
}
