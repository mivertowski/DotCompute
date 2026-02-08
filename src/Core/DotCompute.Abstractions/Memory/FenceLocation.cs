// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Specifies where memory fences should be inserted in kernel code.
/// </summary>
/// <remarks>
/// <para>
/// Fence location controls precise placement of memory ordering operations.
/// This enables fine-grained performance tuning by inserting fences only where needed.
/// </para>
/// <para>
/// <strong>Usage Example:</strong>
/// <code>
/// // Insert fence after all writes, before reads
/// var location = new FenceLocation
/// {
///     AfterWrites = true,
///     BeforeReads = true
/// };
/// provider.InsertFence(FenceType.Device, location);
/// </code>
/// </para>
/// <para>
/// <strong>Performance Optimization:</strong>
/// Strategic fence placement can reduce overhead by 30-50% compared to
/// pervasive fencing. Profile your kernel to identify critical synchronization points.
/// </para>
/// </remarks>
public sealed class FenceLocation
{
    /// <summary>
    /// Gets or sets the specific instruction index where the fence should be inserted.
    /// </summary>
    /// <value>
    /// The zero-based instruction index, or null to use other location properties.
    /// </value>
    /// <remarks>
    /// When specified, the fence is inserted at the exact instruction offset
    /// in the kernel's intermediate representation (PTX, SPIR-V, etc.).
    /// This provides maximum control but requires deep knowledge of compiled code.
    /// </remarks>
    public int? InstructionIndex { get; init; }

    /// <summary>
    /// Gets or sets whether the fence should be inserted at kernel entry.
    /// </summary>
    /// <value>
    /// True to insert fence at the very beginning of kernel execution, false otherwise.
    /// </value>
    /// <remarks>
    /// <para>
    /// Entry fences ensure that all threads observe the same initial memory state.
    /// Useful for kernels that depend on data prepared by previous kernels or host code.
    /// </para>
    /// <para>
    /// <strong>Overhead:</strong> ~10-100ns per kernel launch depending on fence type.
    /// </para>
    /// </remarks>
    public bool AtEntry { get; init; }

    /// <summary>
    /// Gets or sets whether the fence should be inserted at kernel exit.
    /// </summary>
    /// <value>
    /// True to insert fence at the very end of kernel execution, false otherwise.
    /// </value>
    /// <remarks>
    /// <para>
    /// Exit fences ensure that all memory operations complete before kernel termination.
    /// Guarantees that host code or subsequent kernels observe all updates.
    /// </para>
    /// <para>
    /// <strong>Overhead:</strong> ~10-100ns per kernel launch depending on fence type.
    /// </para>
    /// </remarks>
    public bool AtExit { get; init; }

    /// <summary>
    /// Gets or sets whether the fence should be inserted after all write operations.
    /// </summary>
    /// <value>
    /// True to insert fence after stores/writes, false otherwise.
    /// </value>
    /// <remarks>
    /// <para>
    /// Post-write fences implement release semantics: all prior writes become visible
    /// to other threads after the fence.
    /// </para>
    /// <para>
    /// <strong>Use Case:</strong> Producer threads releasing data to consumers.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// data[tid] = compute_value();  // Write
    /// __threadfence();              // Post-write fence
    /// flag[tid] = READY;            // Signal ready
    /// </code>
    /// </para>
    /// </remarks>
    public bool AfterWrites { get; init; }

    /// <summary>
    /// Gets or sets whether the fence should be inserted before all read operations.
    /// </summary>
    /// <value>
    /// True to insert fence before loads/reads, false otherwise.
    /// </value>
    /// <remarks>
    /// <para>
    /// Pre-read fences implement acquire semantics: all subsequent reads observe
    /// values written by other threads before the fence.
    /// </para>
    /// <para>
    /// <strong>Use Case:</strong> Consumer threads acquiring data from producers.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// <code>
    /// while (flag[producer] != READY) { }  // Wait
    /// __threadfence();                     // Pre-read fence
    /// value = data[producer];              // Read
    /// </code>
    /// </para>
    /// </remarks>
    public bool BeforeReads { get; init; }

    /// <summary>
    /// Gets a fence location configured for release semantics (post-write fence).
    /// </summary>
    /// <remarks>
    /// Release semantics ensure all prior writes are visible before subsequent operations.
    /// Use this for producer threads publishing data.
    /// </remarks>
    public static FenceLocation Release => new() { AfterWrites = true };

    /// <summary>
    /// Gets a fence location configured for acquire semantics (pre-read fence).
    /// </summary>
    /// <remarks>
    /// Acquire semantics ensure all subsequent reads observe prior writes.
    /// Use this for consumer threads reading published data.
    /// </remarks>
    public static FenceLocation Acquire => new() { BeforeReads = true };

    /// <summary>
    /// Gets a fence location configured for full barrier semantics (post-write + pre-read).
    /// </summary>
    /// <remarks>
    /// Full barriers provide both release and acquire semantics, ensuring complete
    /// memory consistency. Highest overhead but strongest guarantee.
    /// </remarks>
    public static FenceLocation FullBarrier => new() { AfterWrites = true, BeforeReads = true };

    /// <summary>
    /// Gets a fence location at kernel entry.
    /// </summary>
    /// <remarks>
    /// Entry fences ensure consistent initial memory state across all threads.
    /// </remarks>
    public static FenceLocation KernelEntry => new() { AtEntry = true };

    /// <summary>
    /// Gets a fence location at kernel exit.
    /// </summary>
    /// <remarks>
    /// Exit fences ensure all memory operations complete before kernel termination.
    /// </remarks>
    public static FenceLocation KernelExit => new() { AtExit = true };
}
