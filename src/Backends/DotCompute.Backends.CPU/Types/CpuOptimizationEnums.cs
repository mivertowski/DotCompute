// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU;

/// <summary>
/// CPU kernel optimization categories.
/// </summary>
/// <remarks>
/// <para>
/// Defines optimization strategies for CPU kernel execution. Used by
/// CPU kernel optimizer to analyze kernels and generate
/// performance improvement recommendations.
/// </para>
/// <para>
/// Each optimization type targets specific performance bottlenecks and
/// provides measurable speedup improvements when applied correctly.
/// </para>
/// </remarks>
public enum OptimizationType
{
    /// <summary>
    /// SIMD vectorization optimization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Utilizes CPU vector instructions (SSE, AVX2, AVX-512, NEON) to process
    /// multiple data elements simultaneously. Most effective for data-parallel
    /// operations like element-wise arithmetic, reductions, and transformations.
    /// </para>
    /// <para><b>Typical Speedup</b>: 2-8x depending on vector width and data types</para>
    /// <para><b>Requirements</b>: Aligned memory access, no data dependencies</para>
    /// <para><b>Best For</b>: Float/int arrays, matrix operations, image processing</para>
    /// <para><b>Instruction Sets</b>:</para>
    /// <list type="bullet">
    /// <item>SSE/AVX2: 4-8 floats per instruction (x86-64)</item>
    /// <item>AVX-512: 16 floats per instruction (Xeon, high-end Core)</item>
    /// <item>NEON: 4 floats per instruction (ARM64)</item>
    /// </list>
    /// </remarks>
    Vectorization,

    /// <summary>
    /// Multi-threading and task parallelization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Distributes workload across multiple CPU cores using thread pools or
    /// parallel loops. Scales with core count but has synchronization overhead.
    /// </para>
    /// <para><b>Typical Speedup</b>: Near-linear up to physical core count</para>
    /// <para><b>Overhead</b>: Thread creation, context switching, synchronization</para>
    /// <para><b>Sweet Spot</b>: Work items &gt; 10,000, minimal inter-thread communication</para>
    /// <para><b>Best For</b>: Large datasets, independent computations, embarrassingly parallel problems</para>
    /// </remarks>
    Parallelization,

    /// <summary>
    /// Memory access pattern optimization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optimizes memory access patterns to improve cache hit rates and reduce
    /// memory bandwidth pressure. Includes loop tiling, data layout transformations,
    /// and prefetching strategies.
    /// </para>
    /// <para><b>Typical Speedup</b>: 2-4x for memory-bound operations</para>
    /// <para><b>Techniques</b>: Structure-of-Arrays (SoA) layout, loop blocking, prefetching</para>
    /// <para><b>Metrics</b>: Cache miss rate, memory bandwidth utilization</para>
    /// <para><b>Best For</b>: Strided access, matrix operations, pointer chasing</para>
    /// </remarks>
    Memory,

    /// <summary>
    /// Cache hierarchy optimization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optimizes data structures and algorithms to maximize L1/L2/L3 cache
    /// utilization. Critical for workloads where data exceeds L1 cache size
    /// but fits in L3.
    /// </para>
    /// <para><b>Typical Speedup</b>: 3-10x when moving from DRAM to L1 cache</para>
    /// <para><b>Techniques</b>: Cache blocking/tiling, temporal locality exploitation</para>
    /// <para><b>Cache Sizes</b>:</para>
    /// <list type="bullet">
    /// <item>L1: 32-64 KB per core (1-2 cycles latency)</item>
    /// <item>L2: 256 KB - 1 MB per core (10-20 cycles)</item>
    /// <item>L3: 8-128 MB shared (40-75 cycles)</item>
    /// </list>
    /// <para><b>Best For</b>: Working sets 32KB-32MB, temporal data reuse</para>
    /// </remarks>
    Cache,

    /// <summary>
    /// Threading strategy and NUMA optimization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Optimizes thread affinity, NUMA node allocation, and synchronization
    /// primitives. Critical for multi-socket systems and large core counts.
    /// </para>
    /// <para><b>Typical Speedup</b>: 1.5-3x on NUMA systems when properly tuned</para>
    /// <para><b>Considerations</b>:</para>
    /// <list type="bullet">
    /// <item>Thread affinity to prevent thread migration</item>
    /// <item>NUMA-aware memory allocation (local vs. remote)</item>
    /// <item>False sharing prevention (64-byte cache line alignment)</item>
    /// <item>Lock-free algorithms where possible</item>
    /// </list>
    /// <para><b>Best For</b>: Multi-socket servers, workloads &gt; core count, shared data structures</para>
    /// </remarks>
    Threading
}
