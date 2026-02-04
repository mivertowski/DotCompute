// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Memory;

/// <summary>
/// Represents a fence injection request to be processed during kernel compilation.
/// </summary>
/// <remarks>
/// <para>
/// Fence requests are created by <see cref="IMemoryOrderingProvider.InsertFence"/>
/// and consumed by the kernel compiler to inject appropriate fence instructions
/// (e.g., PTX membar instructions for CUDA).
/// </para>
/// </remarks>
public sealed record FenceRequest
{
    /// <summary>
    /// Gets the fence type (scope) for this request.
    /// </summary>
    public required FenceType Type { get; init; }

    /// <summary>
    /// Gets the location specification for fence placement.
    /// </summary>
    public required FenceLocation Location { get; init; }

    /// <summary>
    /// Gets the timestamp when this request was created.
    /// </summary>
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the PTX instruction for this fence type.
    /// </summary>
    /// <returns>The PTX fence instruction string.</returns>
    /// <remarks>
    /// <para>
    /// PTX fence instructions:
    /// <list type="bullet">
    /// <item><description>ThreadBlock: <c>bar.sync 0;</c> (block-level synchronization barrier)</description></item>
    /// <item><description>Device: <c>membar.gl;</c> (global memory barrier)</description></item>
    /// <item><description>System: <c>membar.sys;</c> (system-wide memory barrier)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public string GetPtxInstruction() => Type switch
    {
        FenceType.ThreadBlock => "bar.sync 0;",
        FenceType.Device => "membar.gl;",
        FenceType.System => "membar.sys;",
        _ => "membar.gl;" // Default to device scope
    };

    /// <summary>
    /// Gets the CUDA C intrinsic for this fence type.
    /// </summary>
    /// <returns>The CUDA C fence intrinsic call.</returns>
    public string GetCudaCIntrinsic() => Type switch
    {
        FenceType.ThreadBlock => "__threadfence_block();",
        FenceType.Device => "__threadfence();",
        FenceType.System => "__threadfence_system();",
        _ => "__threadfence();" // Default to device scope
    };
}

/// <summary>
/// Service interface for fence injection during kernel compilation.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the bridge between <see cref="IMemoryOrderingProvider"/>
/// (which queues fence requests) and the kernel compiler (which injects fence instructions).
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// // 1. Application code requests fences via IMemoryOrderingProvider
/// orderingProvider.InsertFence(FenceType.Device, FenceLocation.Release);
///
/// // 2. Kernel compiler queries pending fences during compilation
/// var fences = fenceService.GetPendingFences();
/// foreach (var fence in fences)
/// {
///     InjectFence(ptxCode, fence);
/// }
///
/// // 3. Clear processed fences
/// fenceService.ClearPendingFences();
/// </code>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Implementations must be thread-safe as fence requests
/// may be queued from multiple threads while compilation proceeds concurrently.
/// </para>
/// </remarks>
public interface IFenceInjectionService
{
    /// <summary>
    /// Gets all pending fence requests that should be injected during the next compilation.
    /// </summary>
    /// <returns>A collection of pending fence requests, ordered by request time.</returns>
    /// <remarks>
    /// This method returns a snapshot of pending fences. The returned collection
    /// is not affected by subsequent calls to <see cref="QueueFence"/> or <see cref="ClearPendingFences"/>.
    /// </remarks>
    public IReadOnlyList<FenceRequest> GetPendingFences();

    /// <summary>
    /// Gets the number of pending fence requests.
    /// </summary>
    public int PendingFenceCount { get; }

    /// <summary>
    /// Queues a new fence request for injection during the next kernel compilation.
    /// </summary>
    /// <param name="request">The fence request to queue.</param>
    public void QueueFence(FenceRequest request);

    /// <summary>
    /// Clears all pending fence requests after they have been processed.
    /// </summary>
    /// <remarks>
    /// Call this after successfully compiling a kernel to avoid re-injecting
    /// the same fences in subsequent compilations.
    /// </remarks>
    public void ClearPendingFences();

    /// <summary>
    /// Gets fences appropriate for the specified location in kernel code.
    /// </summary>
    /// <param name="atEntry">Include fences marked for kernel entry.</param>
    /// <param name="atExit">Include fences marked for kernel exit.</param>
    /// <param name="afterWrites">Include fences marked for after write operations.</param>
    /// <param name="beforeReads">Include fences marked for before read operations.</param>
    /// <returns>Filtered collection of fence requests matching the criteria.</returns>
    public IReadOnlyList<FenceRequest> GetFencesForLocation(
        bool atEntry = false,
        bool atExit = false,
        bool afterWrites = false,
        bool beforeReads = false);
}
