// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Advanced;

/// <summary>
/// RTX 2000 Ada Lovelace generation specific optimizations and utilities
/// </summary>
public static class AdaOptimizations
{
    /// <summary>
    /// RTX 2000 Ada device specifications
    /// </summary>
    public static class RTX2000Specs
    {
        public const int StreamingMultiprocessors = 24;
        public const int ThreadsPerSM = 1536;
        public const int WarpsPerSM = 48; // 32 threads per warp
        public const int MaxThreadsPerBlock = 1024;
        public const int SharedMemoryPerSM = 102400; // 100KB
        public const int RegistersPerSM = 65536;
        public const int L2CacheSize = 32 * 1024 * 1024; // 32MB
        public const double BaseClockMHz = 2610.0;
        public const double BoostClockMHz = 2850.0;
        public const int MemoryBusWidth = 192; // bits
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
        
        // Ada's memory hierarchy optimizations
        if (totalSize <= RTX2000Specs.SharedMemoryPerSM)
        {
            return MemoryAccessPattern.SharedMemory;
        }
        else return totalSize <= RTX2000Specs.L2CacheSize ? MemoryAccessPattern.L2Cache : MemoryAccessPattern.GlobalMemoryCoalesced;
    }

    /// <summary>
    /// Validates configuration against RTX 2000 Ada capabilities
    /// </summary>
    public static ValidationResult ValidateForAda(int gridX, int gridY, int gridZ, 
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

        return new ValidationResult
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
    public int BytesPerBlock { get; set; }
    public int BytesPerThread { get; set; }
    public bool CanUse100KB { get; set; }
    public SharedMemoryCarveout RecommendedCarveout { get; set; }
}

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
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int TotalBlocks { get; set; }
    public int ElementsPerBlock { get; set; }
    public double Occupancy { get; set; }
}

/// <summary>
/// Memory access patterns for optimization
/// </summary>
public enum MemoryAccessPattern
{
    SharedMemory,
    L2Cache,
    GlobalMemoryCoalesced,
    GlobalMemoryStrided
}

/// <summary>
/// Validation result for Ada configurations
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public double Occupancy { get; set; }
    public int BlocksPerSM { get; set; }
}