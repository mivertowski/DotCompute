// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Provides causal memory ordering primitives for GPU computation.
/// </summary>
/// <remarks>
/// <para>
/// Memory ordering controls the visibility and ordering of memory operations across threads.
/// By default, GPUs use a relaxed memory model where operations may be reordered for performance.
/// This provider enables explicit control over memory consistency for correctness-critical algorithms.
/// </para>
/// <para>
/// <strong>Why Memory Ordering Matters:</strong>
/// Without proper ordering, race conditions can cause:
/// <list type="bullet">
/// <item><description>Stale reads: Thread B reads old value even after Thread A writes new value</description></item>
/// <item><description>Causality violations: Effects observed before causes in distributed systems</description></item>
/// <item><description>Inconsistent state: Different threads see different snapshots of shared data</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Platform Support:</strong>
/// <list type="bullet">
/// <item><description>CUDA 9.0+: Full release-acquire semantics via __threadfence_* intrinsics</description></item>
/// <item><description>OpenCL 2.0+: mem_fence() and atomic_work_item_fence()</description></item>
/// <item><description>Metal: threadgroup_barrier(), device_barrier()</description></item>
/// <item><description>CPU: Volatile + Interlocked operations</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Impact:</strong>
/// <list type="table">
/// <item><term>Relaxed</term><description>1.0× (baseline, no overhead)</description></item>
/// <item><term>Release-Acquire</term><description>0.85× (15% overhead, recommended)</description></item>
/// <item><description>Sequential</description><description>0.60× (40% overhead, use sparingly)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Use Cases:</strong>
/// <list type="bullet">
/// <item><description><strong>Message Passing:</strong> Producer-consumer patterns in Orleans.GpuBridge.Core</description></item>
/// <item><description><strong>Distributed Data Structures:</strong> Lock-free queues, hash tables</description></item>
/// <item><description><strong>Multi-GPU Coordination:</strong> Cross-device synchronization</description></item>
/// <item><description><strong>GPU-CPU Communication:</strong> Mapped memory consistency</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Example Usage:</strong>
/// <code>
/// var provider = accelerator.GetMemoryOrderingProvider();
/// if (provider != null)
/// {
///     // Enable causal ordering for distributed actor system
///     provider.SetConsistencyModel(MemoryConsistencyModel.ReleaseAcquire);
///     provider.EnableCausalOrdering(true);
///
///     // Insert release fence after write
///     provider.InsertFence(FenceType.System, FenceLocation.Release);
/// }
/// </code>
/// </para>
/// </remarks>
public interface IMemoryOrderingProvider
{
    /// <summary>
    /// Enables causal memory ordering (release-acquire semantics).
    /// </summary>
    /// <param name="enable">True to enable causal ordering, false to use relaxed model.</param>
    /// <remarks>
    /// <para>
    /// When enabled, memory operations use release-acquire semantics:
    /// <list type="bullet">
    /// <item><description><strong>Release (Write):</strong> All prior memory operations complete before the write is visible</description></item>
    /// <item><description><strong>Acquire (Read):</strong> All subsequent memory operations see values written before the read</description></item>
    /// <item><description><strong>Causality:</strong> If A writes and B reads, B observes all of A's prior writes</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Implementation:</strong>
    /// <list type="bullet">
    /// <item><description>CUDA: Automatic fence insertion before stores (release) and after loads (acquire)</description></item>
    /// <item><description>OpenCL: mem_fence() with acquire/release flags</description></item>
    /// <item><description>CPU: Volatile + Interlocked operations</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> ~15% overhead from fence insertion.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This setting affects all kernels launched after this call.
    /// Not thread-safe; call during initialization only.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Producer thread (release)
    /// data[tid] = compute();
    /// provider.EnableCausalOrdering(true);  // Ensures data is visible
    /// flag[tid] = READY;
    ///
    /// // Consumer thread (acquire)
    /// while (flag[producer] != READY) { }
    /// provider.EnableCausalOrdering(true);  // Ensures data is observed
    /// value = data[producer];
    /// </code>
    /// </example>
    public void EnableCausalOrdering(bool enable = true);

    /// <summary>
    /// Inserts a memory fence at the specified location in kernel code.
    /// </summary>
    /// <param name="type">The fence scope (ThreadBlock, Device, or System).</param>
    /// <param name="location">Optional fence location specification. If null, inserts at current location.</param>
    /// <remarks>
    /// <para>
    /// Memory fences provide explicit control over operation ordering and visibility.
    /// Fences ensure that all memory operations before the fence complete before any
    /// operations after the fence begin.
    /// </para>
    /// <para>
    /// <strong>Fence Types:</strong>
    /// <list type="bullet">
    /// <item><description><strong>ThreadBlock:</strong> Visibility within one block (~10ns)</description></item>
    /// <item><description><strong>Device:</strong> Visibility across all blocks on one GPU (~100ns)</description></item>
    /// <item><description><strong>System:</strong> Visibility across CPU, all GPUs (~200ns)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Strategic Placement:</strong>
    /// <list type="bullet">
    /// <item><description>After writes: Release semantics (producer)</description></item>
    /// <item><description>Before reads: Acquire semantics (consumer)</description></item>
    /// <item><description>Both: Full barrier (strongest guarantee)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>⚠️ Warning:</strong> Excessive fencing degrades performance. Profile your kernel
    /// to identify minimal fence placement that ensures correctness.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// Thrown when the specified fence type is not supported by the device.
    /// </exception>
    /// <example>
    /// <code>
    /// // Producer-consumer with explicit fences
    /// provider.InsertFence(FenceType.Device, FenceLocation.Release);  // After write
    /// provider.InsertFence(FenceType.Device, FenceLocation.Acquire);  // Before read
    /// </code>
    /// </example>
    public void InsertFence(FenceType type, FenceLocation? location = null);

    /// <summary>
    /// Configures the memory consistency model for kernel execution.
    /// </summary>
    /// <param name="model">The desired consistency model.</param>
    /// <remarks>
    /// <para>
    /// The consistency model determines default ordering guarantees for all memory operations:
    /// <list type="bullet">
    /// <item><description><strong>Relaxed:</strong> No ordering (1.0× performance, default GPU model)</description></item>
    /// <item><description><strong>ReleaseAcquire:</strong> Causal ordering (0.85× performance, recommended)</description></item>
    /// <item><description><strong>Sequential:</strong> Total order (0.60× performance, strongest guarantee)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Model Selection Guide:</strong>
    /// <list type="bullet">
    /// <item><description><strong>Use Relaxed:</strong> Data-parallel algorithms with no inter-thread communication</description></item>
    /// <item><description><strong>Use ReleaseAcquire:</strong> Message passing, actor systems, distributed data structures (default for Orleans.GpuBridge)</description></item>
    /// <item><description><strong>Use Sequential:</strong> Complex algorithms requiring total order, or debugging race conditions</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> This setting affects all kernels launched after this call.
    /// Not thread-safe; call during initialization only.
    /// </para>
    /// <para>
    /// <strong>Performance Tip:</strong> Start with Relaxed and add explicit fences only where
    /// needed. This often outperforms pervasive models (ReleaseAcquire/Sequential).
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// Thrown when the specified consistency model is not supported by the device.
    /// </exception>
    public void SetConsistencyModel(MemoryConsistencyModel model);

    /// <summary>
    /// Gets the current memory consistency model.
    /// </summary>
    /// <value>
    /// The active consistency model, or <see cref="MemoryConsistencyModel.Relaxed"/> if not explicitly set.
    /// </value>
    public MemoryConsistencyModel ConsistencyModel { get; }

    /// <summary>
    /// Gets whether the device supports acquire-release memory ordering.
    /// </summary>
    /// <value>
    /// True if release-acquire semantics are supported in hardware, false otherwise.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Hardware Support:</strong>
    /// <list type="bullet">
    /// <item><description>CUDA 9.0+ (Volta CC 7.0+): Native support</description></item>
    /// <item><description>OpenCL 2.0+: Via atomic_work_item_fence()</description></item>
    /// <item><description>Older GPUs: Software emulation via fences (higher overhead)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If false, the provider may emulate acquire-release using pervasive fences,
    /// increasing overhead from 15% to 30-40%.
    /// </para>
    /// </remarks>
    public bool IsAcquireReleaseSupported { get; }

    /// <summary>
    /// Gets whether causal memory ordering is currently enabled.
    /// </summary>
    /// <value>
    /// True if release-acquire semantics are active, false if using relaxed model.
    /// </value>
    /// <remarks>
    /// Indicates whether <see cref="EnableCausalOrdering"/> has been called with true.
    /// </remarks>
    public bool IsCausalOrderingEnabled { get; }

    /// <summary>
    /// Gets whether system-wide memory fences are supported.
    /// </summary>
    /// <value>
    /// True if FenceType.System is supported (CPU+GPU visibility), false otherwise.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Requirements:</strong>
    /// <list type="bullet">
    /// <item><description>CUDA: Compute Capability 2.0+ with Unified Virtual Addressing (UVA)</description></item>
    /// <item><description>OpenCL: Shared virtual memory (SVM) support</description></item>
    /// <item><description>Metal: Shared memory resources enabled</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// If false, system fences will throw <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    public bool SupportsSystemFences { get; }

    /// <summary>
    /// Gets the overhead multiplier for the current consistency model.
    /// </summary>
    /// <value>
    /// Performance multiplier (1.0 = no overhead). Lower values indicate higher overhead.
    /// </value>
    /// <remarks>
    /// <para>
    /// Use this to estimate performance impact of memory ordering:
    /// <list type="bullet">
    /// <item><description>Relaxed: 1.0× (baseline)</description></item>
    /// <item><description>ReleaseAcquire: 0.85× (15% overhead)</description></item>
    /// <item><description>Sequential: 0.60× (40% overhead)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Actual overhead depends on memory access patterns. Compute-bound kernels
    /// see minimal impact, memory-bound kernels see higher impact.
    /// </para>
    /// </remarks>
    public double GetOverheadMultiplier();
}
