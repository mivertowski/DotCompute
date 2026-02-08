// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Atomics;

/// <summary>
/// Specifies the memory ordering constraints for atomic operations.
/// </summary>
/// <remarks>
/// <para>
/// Memory ordering determines how atomic operations interact with other memory operations
/// in a multi-threaded environment. The ordering constraints affect both compiler and
/// hardware reordering of memory operations.
/// </para>
/// <para>
/// <b>Backend Translations:</b>
/// </para>
/// <list type="table">
/// <listheader>
///     <term>Order</term>
///     <description>CUDA</description>
///     <description>OpenCL</description>
///     <description>.NET</description>
/// </listheader>
/// <item>
///     <term>Relaxed</term>
///     <description>(default)</description>
///     <description>memory_order_relaxed</description>
///     <description>Interlocked.*</description>
/// </item>
/// <item>
///     <term>Acquire</term>
///     <description>__threadfence() before read</description>
///     <description>memory_order_acquire</description>
///     <description>Volatile.Read + barrier</description>
/// </item>
/// <item>
///     <term>Release</term>
///     <description>__threadfence() after write</description>
///     <description>memory_order_release</description>
///     <description>barrier + Volatile.Write</description>
/// </item>
/// <item>
///     <term>AcquireRelease</term>
///     <description>__threadfence() before/after</description>
///     <description>memory_order_acq_rel</description>
///     <description>Full barriers</description>
/// </item>
/// <item>
///     <term>SequentiallyConsistent</term>
///     <description>__threadfence_system()</description>
///     <description>memory_order_seq_cst</description>
///     <description>Thread.MemoryBarrier()</description>
/// </item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Acquire semantics - ensures all prior stores are visible before the load
/// var value = Atomics.Load(ref data, MemoryOrder.Acquire);
///
/// // Release semantics - ensures all stores are complete before this store
/// Atomics.Store(ref ready, true, MemoryOrder.Release);
/// </code>
/// </example>
public enum MemoryOrder
{
    /// <summary>
    /// No memory ordering constraints beyond atomicity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The operation is guaranteed to be atomic, but no guarantees are made about
    /// ordering with respect to other memory operations.
    /// </para>
    /// <para>
    /// Use when synchronization is handled by other means (barriers, mutexes) or
    /// when the atomic is only accessed by a single thread.
    /// </para>
    /// </remarks>
    Relaxed = 0,

    /// <summary>
    /// Acquire semantics: no reads/writes can be reordered before this load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A load operation with acquire semantics ensures that all memory operations
    /// that appear after the acquire in program order will observe any stores
    /// that occurred before a corresponding release operation.
    /// </para>
    /// <para>
    /// Typically used when reading a flag that indicates data is ready.
    /// </para>
    /// </remarks>
    Acquire = 1,

    /// <summary>
    /// Release semantics: no reads/writes can be reordered after this store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A store operation with release semantics ensures that all memory operations
    /// that appear before the release in program order are visible to other threads
    /// that perform an acquire operation on the same atomic.
    /// </para>
    /// <para>
    /// Typically used when writing a flag that indicates data is ready.
    /// </para>
    /// </remarks>
    Release = 2,

    /// <summary>
    /// Both acquire and release semantics combined.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Combines the guarantees of both acquire and release ordering.
    /// </para>
    /// <para>
    /// Typically used for read-modify-write operations (like compare-exchange)
    /// that both consume and produce synchronization.
    /// </para>
    /// </remarks>
    AcquireRelease = 3,

    /// <summary>
    /// Full sequential consistency: all threads see all operations in the same order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The strongest memory ordering guarantee. All sequentially consistent operations
    /// appear to execute in a single total order that is consistent with the program
    /// order of each individual thread.
    /// </para>
    /// <para>
    /// <b>Performance:</b> This is the most expensive ordering and should only be used
    /// when strictly necessary. Consider acquire-release semantics first.
    /// </para>
    /// </remarks>
    SequentiallyConsistent = 4
}
