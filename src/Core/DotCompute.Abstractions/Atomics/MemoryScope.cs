// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Atomics;

/// <summary>
/// Specifies the memory scope for atomic operations and fences.
/// </summary>
/// <remarks>
/// <para>
/// Memory scope determines which threads are guaranteed to observe the effects
/// of memory operations. Choosing the appropriate scope is critical for both
/// correctness and performance.
/// </para>
/// <para>
/// <b>Backend Translations:</b>
/// </para>
/// <list type="table">
/// <listheader>
///     <term>Scope</term>
///     <description>CUDA</description>
///     <description>OpenCL</description>
///     <description>Metal</description>
/// </listheader>
/// <item>
///     <term>Workgroup</term>
///     <description>__threadfence_block()</description>
///     <description>CLK_LOCAL_MEM_FENCE</description>
///     <description>threadgroup_barrier</description>
/// </item>
/// <item>
///     <term>Device</term>
///     <description>__threadfence()</description>
///     <description>CLK_GLOBAL_MEM_FENCE</description>
///     <description>device_barrier</description>
/// </item>
/// <item>
///     <term>System</term>
///     <description>__threadfence_system()</description>
///     <description>memory_scope_all_svm_devices</description>
///     <description>N/A (fallback to device)</description>
/// </item>
/// </list>
/// <para>
/// <b>Performance Guidelines:</b>
/// </para>
/// <list type="bullet">
/// <item><description><b>Workgroup:</b> ~1-10ns latency, use when threads in same block communicate</description></item>
/// <item><description><b>Device:</b> ~10-100ns latency, use when blocks on same GPU communicate</description></item>
/// <item><description><b>System:</b> ~100-1000ns latency, use for host-GPU communication</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Synchronize within a workgroup
/// Atomics.ThreadFence(MemoryScope.Workgroup);
///
/// // Ensure all device threads see the update
/// Atomics.Store(ref globalFlag, 1, MemoryOrder.Release);
/// Atomics.ThreadFence(MemoryScope.Device);
///
/// // System-wide fence for host communication (unified memory)
/// Atomics.ThreadFence(MemoryScope.System);
/// </code>
/// </example>
public enum MemoryScope
{
    /// <summary>
    /// Memory operations are visible within the current workgroup/block only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fastest scope, ensuring visibility only among threads in the same
    /// thread block (CUDA), workgroup (OpenCL), or threadgroup (Metal).
    /// </para>
    /// <para>
    /// Use when coordinating threads that share local/shared memory within a block.
    /// </para>
    /// </remarks>
    Workgroup = 0,

    /// <summary>
    /// Memory operations are visible to all threads on the current device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ensures visibility across all thread blocks on the same GPU device.
    /// This is the appropriate scope for most inter-block coordination using
    /// global memory.
    /// </para>
    /// <para>
    /// Use when coordinating threads across different blocks via global memory.
    /// </para>
    /// </remarks>
    Device = 1,

    /// <summary>
    /// Memory operations are visible across the entire system (all devices and host CPU).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The most expensive scope, ensuring visibility to the host CPU and all
    /// other devices in the system. Required for unified memory coordination
    /// between host and device.
    /// </para>
    /// <para>
    /// <b>Requirements:</b> Unified memory or peer-to-peer access must be enabled.
    /// </para>
    /// <para>
    /// <b>WSL2 Limitation:</b> System-scope atomics may not work reliably in WSL2
    /// due to GPU virtualization. Use EventDriven patterns instead of polling
    /// for host-GPU communication.
    /// </para>
    /// </remarks>
    System = 2
}
