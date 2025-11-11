// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Barriers;

/// <summary>
/// Defines the synchronization scope for GPU thread barriers.
/// </summary>
/// <remarks>
/// <para>
/// Barrier scope determines which threads participate in synchronization:
/// <list type="bullet">
/// <item><description><strong>ThreadBlock</strong>: All threads in the same CUDA thread block</description></item>
/// <item><description><strong>Grid</strong>: All threads across all blocks in the kernel grid (requires cooperative launch)</description></item>
/// <item><description><strong>Warp</strong>: All threads in the same 32-thread warp</description></item>
/// <item><description><strong>Tile</strong>: Arbitrary subset of threads within a block</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Characteristics:</strong>
/// <list type="table">
/// <item>
/// <term>ThreadBlock</term>
/// <description>~10ns latency, hardware support, most common</description>
/// </item>
/// <item>
/// <term>Grid</term>
/// <description>~1-10μs latency, requires cooperative launch, CC 6.0+</description>
/// </item>
/// <item>
/// <term>Warp</term>
/// <description>~1ns latency, implicit in lockstep execution</description>
/// </item>
/// <item>
/// <term>Tile</term>
/// <description>~20ns latency, flexible but slower than block</description>
/// </item>
/// </list>
/// </para>
/// <para>
/// <strong>Hardware Requirements:</strong>
/// <list type="bullet">
/// <item><description>ThreadBlock: All CUDA devices (CC 1.0+)</description></item>
/// <item><description>Grid: Pascal and newer (CC 6.0+)</description></item>
/// <item><description>Warp: All CUDA devices (CC 1.0+)</description></item>
/// <item><description>Tile: Volta and newer (CC 7.0+) for best performance</description></item>
/// </list>
/// </para>
/// </remarks>
public enum BarrierScope
{
    /// <summary>
    /// Synchronize all threads within a single thread block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the most common and efficient barrier scope, mapping directly to <c>__syncthreads()</c>
    /// in CUDA. All threads in the block wait until every thread reaches the barrier.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Shared memory synchronization</description></item>
    /// <item><description>Reduction operations within a block</description></item>
    /// <item><description>Stencil computations</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Constraints:</strong> Maximum block size varies by device (typically 1024 threads).
    /// </para>
    /// </remarks>
    ThreadBlock = 0,

    /// <summary>
    /// Synchronize all threads across all blocks in the entire kernel grid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Grid-wide barriers enable global synchronization for algorithms requiring inter-block communication.
    /// Requires cooperative kernel launch via <c>cudaLaunchCooperativeKernel</c>.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Global reductions to single value</description></item>
    /// <item><description>Iterative algorithms across entire dataset</description></item>
    /// <item><description>Multi-step computations requiring global state</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Requirements:</strong>
    /// <list type="bullet">
    /// <item><description>Compute Capability 6.0+ (Pascal or newer)</description></item>
    /// <item><description>Cooperative kernel launch</description></item>
    /// <item><description>Grid size ≤ maximum concurrent kernel limit</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Grid barriers have ~1-10μs latency depending on grid size
    /// and device generation. Use sparingly in tight loops.
    /// </para>
    /// </remarks>
    Grid = 1,

    /// <summary>
    /// Synchronize all threads within a single 32-thread warp (CUDA-specific).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Warp-level synchronization is implicit in lockstep execution but explicit barriers
    /// enable safe divergent execution patterns. Maps to <c>__syncwarp()</c>.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Warp-level reductions</description></item>
    /// <item><description>Ballot/shuffle operations</description></item>
    /// <item><description>Warp-synchronous programming patterns</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Warp size is 32 threads on NVIDIA GPUs. Other vendors may differ.
    /// </para>
    /// </remarks>
    Warp = 2,

    /// <summary>
    /// Synchronize an arbitrary subset of threads (tile) within a thread block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tile barriers enable flexible synchronization patterns where only a subset of threads
    /// need to wait. Particularly useful for irregular workloads.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Work-stealing algorithms</description></item>
    /// <item><description>Dynamic partitioning</description></item>
    /// <item><description>Hierarchical parallelism</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Tile barriers are more flexible but slightly slower than
    /// thread-block barriers (~20ns vs ~10ns).
    /// </para>
    /// </remarks>
    Tile = 3,

    /// <summary>
    /// Synchronize threads across multiple GPUs and the CPU in a system-wide barrier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// System-wide barriers enable synchronization across multiple GPUs and the host CPU.
    /// This is the most complex and slowest barrier type due to PCIe roundtrip latency.
    /// </para>
    /// <para>
    /// <strong>Architecture:</strong>
    /// System barriers operate in three phases:
    /// <list type="number">
    /// <item><description><strong>Device-Local Phase:</strong> Each GPU executes device-local barrier (__threadfence_system)</description></item>
    /// <item><description><strong>Cross-GPU Phase:</strong> CPU waits for all GPU events via CUDA events</description></item>
    /// <item><description><strong>Resume Phase:</strong> CPU signals all GPUs to continue via mapped memory</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Multi-GPU global reductions</description></item>
    /// <item><description>Distributed graph algorithms across GPUs</description></item>
    /// <item><description>Multi-GPU iterative solvers</description></item>
    /// <item><description>System-wide checkpoint synchronization</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Requirements:</strong>
    /// <list type="bullet">
    /// <item><description>Multiple CUDA devices (2-8 GPUs typical)</description></item>
    /// <item><description>Compute Capability 6.0+ for efficient P2P (Pascal or newer)</description></item>
    /// <item><description>P2P capability between all device pairs (recommended)</description></item>
    /// <item><description>Pinned host memory for cross-device signaling</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> System barriers have ~1-10ms latency due to PCIe roundtrip
    /// and CPU coordination. Use sparingly - typically once per iteration in multi-GPU algorithms.
    /// </para>
    /// <para>
    /// <strong>Limitations:</strong>
    /// <list type="bullet">
    /// <item><description>Maximum 8 GPUs typical (PCIe topology limit)</description></item>
    /// <item><description>Performance degrades with increased GPU count</description></item>
    /// <item><description>May not work reliably with integrated GPUs</description></item>
    /// <item><description>Requires careful error handling for multi-device scenarios</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    System = 4
}
