// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Barriers;

/// <summary>
/// Represents a GPU barrier synchronization primitive for coordinating thread execution.
/// </summary>
/// <remarks>
/// <para>
/// A barrier handle provides a typed reference to a GPU synchronization barrier, enabling
/// thread groups to coordinate execution and ensure memory consistency. Barriers are critical
/// for algorithms requiring phased computation where all threads must complete one phase
/// before any thread can proceed to the next.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Barrier handles are thread-safe and can be used by multiple
/// threads simultaneously. However, the barrier itself has specific synchronization semantics
/// that must be respected (see <see cref="Sync"/> method).
/// </para>
/// <para>
/// <strong>Lifetime:</strong> Barriers remain valid until explicitly disposed. For grid-level
/// barriers, all participating threads must complete before disposal.
/// </para>
/// <para>
/// <strong>Usage Example:</strong>
/// <code>
/// var barrier = provider.CreateBarrier(BarrierScope.ThreadBlock, capacity: 256);
/// // In kernel: barrier.Sync(); // All 256 threads wait here
/// barrier.Dispose(); // Cleanup when done
/// </code>
/// </para>
/// </remarks>
public interface IBarrierHandle : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this barrier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Barrier IDs are unique within the lifetime of the barrier provider. Multiple barriers
    /// can exist simultaneously, each with a distinct ID.
    /// </para>
    /// <para>
    /// <strong>ID Assignment:</strong>
    /// <list type="bullet">
    /// <item><description>ThreadBlock barriers: ID corresponds to barrier register index (0-15)</description></item>
    /// <item><description>Grid barriers: ID is runtime-generated unique identifier</description></item>
    /// <item><description>Named barriers: ID matches user-specified name hash</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int BarrierId { get; }

    /// <summary>
    /// Gets the synchronization scope of this barrier.
    /// </summary>
    /// <remarks>
    /// The scope determines which threads participate in synchronization:
    /// <see cref="BarrierScope.ThreadBlock"/>, <see cref="BarrierScope.Grid"/>,
    /// <see cref="BarrierScope.Warp"/>, or <see cref="BarrierScope.Tile"/>.
    /// </remarks>
    /// <seealso cref="BarrierScope"/>
    public BarrierScope Scope { get; }

    /// <summary>
    /// Gets the maximum number of threads that can synchronize on this barrier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Capacity constraints vary by scope:
    /// <list type="table">
    /// <item>
    /// <term>ThreadBlock</term>
    /// <description>≤ block size (typically 1024 max)</description>
    /// </item>
    /// <item>
    /// <term>Grid</term>
    /// <description>≤ grid size (all threads in kernel)</description>
    /// </item>
    /// <item>
    /// <term>Warp</term>
    /// <description>Fixed at 32 (warp size)</description>
    /// </item>
    /// <item>
    /// <term>Tile</term>
    /// <description>≤ block size, user-specified</description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Important:</strong> Exactly <see cref="Capacity"/> threads must call <see cref="Sync"/>
    /// for the barrier to proceed. Calling sync with fewer threads causes deadlock.
    /// </para>
    /// </remarks>
    public int Capacity { get; }

    /// <summary>
    /// Gets the current number of threads waiting at the barrier.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property enables monitoring barrier utilization and detecting deadlocks. Reading
    /// this value requires synchronization with the GPU and may introduce overhead (~1μs).
    /// </para>
    /// <para>
    /// <strong>States:</strong>
    /// <list type="bullet">
    /// <item><description>0: No threads waiting (idle barrier)</description></item>
    /// <item><description>0 &lt; n &lt; Capacity: Some threads blocked, waiting for others</description></item>
    /// <item><description>n = Capacity: All threads arrived, barrier about to release</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Debugging:</strong> If <c>ThreadsWaiting</c> remains &lt; <c>Capacity</c> indefinitely,
    /// a thread may have missed the barrier call (deadlock condition).
    /// </para>
    /// </remarks>
    public int ThreadsWaiting { get; }

    /// <summary>
    /// Gets whether this barrier is currently active (has waiting threads).
    /// </summary>
    /// <remarks>
    /// A barrier is active when 0 &lt; <see cref="ThreadsWaiting"/> &lt; <see cref="Capacity"/>.
    /// Active barriers should not be disposed or reconfigured.
    /// </remarks>
    public bool IsActive { get; }

    /// <summary>
    /// Synchronizes threads at the barrier, blocking until all threads arrive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is called from GPU kernel code to synchronize threads. All threads
    /// in the barrier's scope must call <c>Sync()</c> for any thread to proceed.
    /// </para>
    /// <para>
    /// <strong>Semantics:</strong>
    /// <list type="number">
    /// <item><description>Thread arrives at barrier and increments arrival counter</description></item>
    /// <item><description>Thread blocks until counter reaches <see cref="Capacity"/></description></item>
    /// <item><description>All threads released simultaneously when capacity reached</description></item>
    /// <item><description>Counter resets to 0 for next barrier cycle</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Memory Ordering:</strong>
    /// Barriers provide acquire-release semantics:
    /// <list type="bullet">
    /// <item><description>All memory operations before sync() are visible to all threads after sync()</description></item>
    /// <item><description>Shared memory reads after sync() see all writes before sync()</description></item>
    /// <item><description>Equivalent to memory fence + synchronization</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Deadlock Prevention:</strong>
    /// <list type="bullet">
    /// <item><description>Ensure all threads execute barrier call (no conditional skipping)</description></item>
    /// <item><description>Match barrier calls across all divergent control paths</description></item>
    /// <item><description>Use same <see cref="BarrierId"/> for all threads in scope</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance:</strong>
    /// <list type="bullet">
    /// <item><description>ThreadBlock: ~10ns latency</description></item>
    /// <item><description>Grid: ~1-10μs latency</description></item>
    /// <item><description>Warp: ~1ns latency (lockstep execution)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when:
    /// <list type="bullet">
    /// <item><description>Barrier has been disposed</description></item>
    /// <item><description>Thread is not part of the barrier's scope</description></item>
    /// <item><description>Grid barrier used without cooperative launch</description></item>
    /// </list>
    /// </exception>
    /// <example>
    /// <code>
    /// // Shared memory synchronization pattern
    /// __shared__ float sharedData[256];
    ///
    /// sharedData[threadIdx.x] = input[threadIdx.x]; // Write phase
    /// barrier.Sync();                                // Ensure all writes complete
    /// float result = sharedData[neighbor];           // Read phase (safe)
    /// </code>
    /// </example>
    public void Sync();

    /// <summary>
    /// Resets the barrier to its initial state, clearing waiting threads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>⚠️ WARNING:</strong> Resetting an active barrier (with waiting threads) can cause
    /// deadlock or data races. Only reset when <see cref="IsActive"/> is false.
    /// </para>
    /// <para>
    /// Reset is typically used between kernel launches or when reinitializing a barrier
    /// for a new computation phase.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when barrier is currently active (<see cref="IsActive"/> is true).
    /// </exception>
    public void Reset();
}
