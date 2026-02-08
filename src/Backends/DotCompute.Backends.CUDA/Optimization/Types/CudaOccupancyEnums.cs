// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Optimization;

/// <summary>
/// Optimization hint for CUDA kernel launch configuration.
/// </summary>
/// <remarks>
/// <para>
/// Provides guidance to the occupancy calculator on how to optimize kernel launch
/// parameters based on the dominant performance characteristic of the kernel.
/// </para>
/// <list type="bullet">
/// <item><b>MemoryBound</b>: Optimize for memory throughput with coalesced access patterns</item>
/// <item><b>ComputeBound</b>: Maximize occupancy for compute-intensive operations</item>
/// <item><b>Latency</b>: Minimize kernel launch overhead with larger blocks</item>
/// <item><b>Balanced</b>: Balance between occupancy and other performance factors</item>
/// </list>
/// </remarks>
public enum OptimizationHint
{
    /// <summary>No specific optimization hint provided.</summary>
    None,

    /// <summary>
    /// Balanced optimization between occupancy, memory throughput, and latency.
    /// </summary>
    /// <remarks>
    /// Recommended for kernels with mixed compute and memory access patterns.
    /// Uses default CUDA occupancy API recommendations.
    /// </remarks>
    Balanced,

    /// <summary>
    /// Optimize for memory-bound kernels with heavy data movement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Prefers block sizes that are multiples of 128 threads to maximize memory
    /// coalescing and cache line utilization. Best for kernels dominated by
    /// global memory access patterns.
    /// </para>
    /// <para><b>Performance Impact</b>: 2-4x speedup for memory-intensive kernels.</para>
    /// </remarks>
    MemoryBound,

    /// <summary>
    /// Optimize for compute-bound kernels with heavy arithmetic operations.
    /// </summary>
    /// <remarks>
    /// Maximizes occupancy to hide instruction latency through warp scheduling.
    /// Best for kernels with high arithmetic intensity and minimal memory access.
    /// </remarks>
    ComputeBound,

    /// <summary>
    /// Optimize for minimal kernel launch latency.
    /// </summary>
    /// <remarks>
    /// Uses larger block sizes to reduce grid dimensions and launch overhead.
    /// Recommended for frequently launched small kernels or interactive workloads.
    /// </remarks>
    Latency
}

/// <summary>
/// CUDA device attributes for occupancy calculation.
/// </summary>
/// <remarks>
/// Internal enumeration mapping to CUDA runtime API device attribute queries.
/// Values correspond to cudaDeviceAttr_t enumeration from CUDA runtime.
/// </remarks>
internal enum CudaDeviceAttribute
{
    /// <summary>Maximum number of threads per block.</summary>
    MaxThreadsPerBlock = 1,

    /// <summary>Maximum X dimension of block.</summary>
    MaxBlockDimX = 2,

    /// <summary>Maximum Y dimension of block.</summary>
    MaxBlockDimY = 3,

    /// <summary>Maximum Z dimension of block.</summary>
    MaxBlockDimZ = 4,

    /// <summary>Maximum X dimension of grid.</summary>
    MaxGridDimX = 5,

    /// <summary>Maximum Y dimension of grid.</summary>
    MaxGridDimY = 6,

    /// <summary>Maximum Z dimension of grid.</summary>
    MaxGridDimZ = 7,

    /// <summary>Maximum shared memory per block in bytes.</summary>
    MaxSharedMemoryPerBlock = 8,

    /// <summary>Total constant memory available in bytes.</summary>
    TotalConstantMemory = 9,

    /// <summary>Warp size in threads.</summary>
    WarpSize = 10,

    /// <summary>Maximum pitch in bytes for memory copies.</summary>
    MaxPitch = 11,

    /// <summary>Maximum number of 32-bit registers per block.</summary>
    MaxRegistersPerBlock = 12,

    /// <summary>Clock frequency in kilohertz.</summary>
    ClockRate = 13,

    /// <summary>Texture alignment requirement.</summary>
    TextureAlignment = 14,

    /// <summary>Number of multiprocessors on device.</summary>
    MultiprocessorCount = 16,

    /// <summary>Maximum warps per multiprocessor.</summary>
    MaxWarpsPerMultiprocessor = 67,

    /// <summary>Maximum shared memory per multiprocessor in bytes.</summary>
    MaxSharedMemoryPerMultiprocessor = 81,

    /// <summary>Maximum 32-bit registers per multiprocessor.</summary>
    MaxRegistersPerMultiprocessor = 82,

    /// <summary>Maximum resident blocks per multiprocessor.</summary>
    MaxBlocksPerMultiprocessor = 106,

    /// <summary>Major compute capability version number.</summary>
    ComputeCapabilityMajor = 75,

    /// <summary>Minor compute capability version number.</summary>
    ComputeCapabilityMinor = 76,

    /// <summary>Maximum depth of nested kernel launches (dynamic parallelism).</summary>
    MaxDeviceDepth = 111
}
