// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Specifies the scope of memory fence operations for GPU synchronization.
/// </summary>
/// <remarks>
/// <para>
/// Memory fences control the visibility of memory operations across different levels
/// of the GPU memory hierarchy. The fence type determines which threads observe the
/// memory consistency guarantees.
/// </para>
/// <para>
/// <strong>Platform Mapping:</strong>
/// <list type="bullet">
/// <item><description>CUDA: __threadfence_block(), __threadfence(), __threadfence_system()</description></item>
/// <item><description>OpenCL: mem_fence(CLK_LOCAL_MEM_FENCE | CLK_GLOBAL_MEM_FENCE)</description></item>
/// <item><description>Metal: threadgroup_barrier(), device_barrier(), system_barrier()</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance vs. Scope Trade-off:</strong>
/// <list type="table">
/// <item><term>ThreadBlock</term><description>Fastest (~10ns), limited visibility</description></item>
/// <item><term>Device</term><description>Medium (~100ns), intra-device visibility</description></item>
/// <item><term>System</term><description>Slowest (~200ns), inter-device visibility</description></item>
/// </list>
/// </para>
/// </remarks>
public enum FenceType
{
    /// <summary>
    /// Thread-block scope fence ensuring memory consistency within a single thread block.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>CUDA:</strong> <c>__threadfence_block()</c>
    /// </para>
    /// <para>
    /// <strong>Visibility:</strong> All threads in the same thread block see memory updates
    /// after this fence. Does not guarantee visibility to threads in other blocks.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Producer-consumer patterns within a block</description></item>
    /// <item><description>Shared memory synchronization</description></item>
    /// <item><description>Block-local data structure updates</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> ~10ns latency (fastest fence type).
    /// </para>
    /// </remarks>
    ThreadBlock = 0,

    /// <summary>
    /// Device-wide fence ensuring memory consistency across all thread blocks on a single GPU.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>CUDA:</strong> <c>__threadfence()</c>
    /// </para>
    /// <para>
    /// <strong>Visibility:</strong> All threads on the same GPU see memory updates after this
    /// fence. Does not guarantee visibility to host CPU or other GPUs.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>Grid-wide producer-consumer patterns</description></item>
    /// <item><description>Device-global data structure updates</description></item>
    /// <item><description>Inter-block communication via global memory</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> ~100ns latency (medium overhead).
    /// </para>
    /// </remarks>
    Device = 1,

    /// <summary>
    /// System-wide fence ensuring memory consistency across CPU, GPU, and all devices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>CUDA:</strong> <c>__threadfence_system()</c>
    /// </para>
    /// <para>
    /// <strong>Visibility:</strong> All processors in the system (CPU, all GPUs) see memory
    /// updates after this fence. Strongest consistency guarantee, highest overhead.
    /// </para>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// <list type="bullet">
    /// <item><description>GPU-CPU communication via mapped/pinned memory</description></item>
    /// <item><description>Multi-GPU synchronization</description></item>
    /// <item><description>System-wide distributed data structures</description></item>
    /// <item><description>Causal message passing in Orleans.GpuBridge.Core</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> ~200ns latency (slowest, strongest guarantee).
    /// </para>
    /// <para>
    /// <strong>Requirements:</strong> Requires unified virtual addressing (UVA) on CUDA.
    /// </para>
    /// </remarks>
    System = 2
}
