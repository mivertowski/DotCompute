// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Defines the memory consistency model for GPU kernel execution.
/// </summary>
/// <remarks>
/// <para>
/// Memory consistency models specify the ordering guarantees for memory operations
/// performed by different threads. Stronger models provide more intuitive semantics
/// but impose higher performance costs.
/// </para>
/// <para>
/// <strong>Model Comparison:</strong>
/// <list type="table">
/// <item>
/// <term>Relaxed</term>
/// <description>
/// No ordering guarantees. Threads may observe operations in any order.
/// Maximum performance (1.0× baseline), minimal synchronization.
/// </description>
/// </item>
/// <item>
/// <term>ReleaseAcquire</term>
/// <description>
/// Causal ordering: release stores become visible to acquire loads.
/// Good balance (0.85× baseline, 15% overhead), suitable for most algorithms.
/// </description>
/// </item>
/// <item>
/// <term>Sequential</term>
/// <description>
/// Total order: all threads observe operations in the same order.
/// Strongest guarantees (0.60× baseline, 40% overhead), simplest reasoning.
/// </description>
/// </item>
/// </list>
/// </para>
/// <para>
/// <strong>Choosing a Model:</strong>
/// <list type="bullet">
/// <item><description><strong>Relaxed:</strong> Data-parallel algorithms with no inter-thread dependencies</description></item>
/// <item><description><strong>ReleaseAcquire:</strong> Producer-consumer patterns, message passing (recommended default)</description></item>
/// <item><description><strong>Sequential:</strong> Complex algorithms requiring total order visibility</description></item>
/// </list>
/// </para>
/// </remarks>
public enum MemoryConsistencyModel
{
    /// <summary>
    /// Relaxed memory consistency: no ordering guarantees between threads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the relaxed model, threads may observe memory operations in any order unless
    /// explicitly synchronized with fences or atomic operations. This is the default
    /// GPU memory model.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// Thread 1: A = 1; B = 2;
    /// Thread 2: r1 = B; r2 = A;  // May see r1=2, r2=0 (reordering)
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> 1.0× baseline (no overhead).
    /// </para>
    /// <para>
    /// <strong>Use When:</strong> Data-parallel algorithms with independent operations,
    /// no inter-thread communication, or manual fence management.
    /// </para>
    /// </remarks>
    Relaxed = 0,

    /// <summary>
    /// Release-Acquire memory consistency: causal ordering for synchronized operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Release-Acquire semantics ensure that:
    /// <list type="bullet">
    /// <item><description><strong>Release Store:</strong> All prior writes become visible before the store</description></item>
    /// <item><description><strong>Acquire Load:</strong> All subsequent reads see values after the load</description></item>
    /// <item><description><strong>Causality:</strong> If Thread A releases X and Thread B acquires X, all of A's prior writes are visible to B</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// Thread 1: A = 1; B = 2; release_store(&amp;flag, 1);  // Release
    /// Thread 2: if (acquire_load(&amp;flag)) r1 = A;       // Acquire, sees A=1
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Implementation:</strong>
    /// <list type="bullet">
    /// <item><description>Release: Fence before atomic store</description></item>
    /// <item><description>Acquire: Fence after atomic load</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> 0.85× baseline (15% overhead from fences).
    /// </para>
    /// <para>
    /// <strong>Use When:</strong> Producer-consumer patterns, message passing,
    /// distributed data structures, actor systems (Orleans.GpuBridge.Core).
    /// </para>
    /// </remarks>
    ReleaseAcquire = 1,

    /// <summary>
    /// Sequential consistency: total order visible to all threads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sequential consistency (SC) provides the strongest guarantee: all threads
    /// observe memory operations in the same global order, as if operations were
    /// interleaved on a single processor.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// Thread 1: A = 1; B = 2;
    /// Thread 2: r1 = B; r2 = A;
    /// // SC guarantees: if r1=2, then r2=1 (never r2=0)
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Implementation:</strong> Fence before and after every memory operation.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> 0.60× baseline (40% overhead from pervasive fencing).
    /// </para>
    /// <para>
    /// <strong>Use When:</strong> Algorithm correctness requires total order visibility,
    /// performance is secondary, or debugging relaxed-model race conditions.
    /// </para>
    /// <para>
    /// <strong>⚠️ Warning:</strong> Sequential consistency significantly impacts performance.
    /// Only use when absolutely necessary. Consider Release-Acquire first.
    /// </para>
    /// </remarks>
    Sequential = 2
}
