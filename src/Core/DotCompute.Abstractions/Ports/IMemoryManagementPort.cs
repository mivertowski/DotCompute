// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Ports;

/// <summary>
/// Port interface for memory management operations.
/// Part of hexagonal architecture - defines the contract that backend adapters must implement.
/// </summary>
/// <remarks>
/// <para>
/// This port abstracts memory allocation, transfer, and synchronization from backend-specific implementations.
/// Each backend (CUDA, Metal, OpenCL, CPU) provides an adapter that implements this interface.
/// </para>
/// </remarks>
public interface IMemoryManagementPort
{
    /// <summary>
    /// Allocates a buffer of the specified size.
    /// </summary>
    /// <typeparam name="T">The element type (must be unmanaged).</typeparam>
    /// <param name="length">Number of elements to allocate.</param>
    /// <param name="flags">Buffer allocation flags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The allocated buffer.</returns>
    public ValueTask<IPortBuffer<T>> AllocateAsync<T>(
        int length,
        BufferAllocationFlags flags = BufferAllocationFlags.None,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Copies data between buffers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">Source buffer.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask CopyAsync<T>(
        IPortBuffer<T> source,
        IPortBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Copies a range of data between buffers.
    /// </summary>
    public ValueTask CopyRangeAsync<T>(
        IPortBuffer<T> source,
        int sourceOffset,
        IPortBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default) where T : unmanaged;

    /// <summary>
    /// Gets the memory management capabilities of this backend.
    /// </summary>
    public MemoryCapabilities Capabilities { get; }

    /// <summary>
    /// Gets current memory statistics.
    /// </summary>
    public MemoryStatistics GetStatistics();
}

/// <summary>
/// A buffer allocated through the memory management port.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public interface IPortBuffer<T> : IAsyncDisposable, IDisposable where T : unmanaged
{
    /// <summary>Number of elements in the buffer.</summary>
    public int Length { get; }

    /// <summary>Size in bytes.</summary>
    public long SizeInBytes { get; }

    /// <summary>Buffer location.</summary>
    public BufferLocation Location { get; }

    /// <summary>Gets a span for CPU access (may require synchronization).</summary>
    public Span<T> AsSpan();

    /// <summary>Gets a read-only span.</summary>
    public ReadOnlySpan<T> AsReadOnlySpan();

    /// <summary>Copies data from a source span to this buffer.</summary>
    public ValueTask CopyFromAsync(ReadOnlySpan<T> source, CancellationToken cancellationToken = default);

    /// <summary>Copies data from this buffer to a destination span.</summary>
    public ValueTask CopyToAsync(Span<T> destination, CancellationToken cancellationToken = default);

    /// <summary>Synchronizes the buffer between host and device.</summary>
    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Flags for buffer allocation.
/// </summary>
[Flags]
public enum BufferAllocationFlags
{
    /// <summary>Default allocation.</summary>
    None = 0,

    /// <summary>Allocate from memory pool if available.</summary>
    Pooled = 1 << 0,

    /// <summary>Use unified/managed memory.</summary>
    Unified = 1 << 1,

    /// <summary>Pin memory for faster transfers.</summary>
    Pinned = 1 << 2,

    /// <summary>Read-only buffer.</summary>
    ReadOnly = 1 << 3,

    /// <summary>Write-only buffer.</summary>
    WriteOnly = 1 << 4,

    /// <summary>Zero-initialize the buffer.</summary>
    ZeroInitialize = 1 << 5
}

/// <summary>
/// Where a buffer is located.
/// </summary>
public enum BufferLocation
{
    /// <summary>Host (CPU) memory.</summary>
    Host,

    /// <summary>Device (GPU) memory.</summary>
    Device,

    /// <summary>Unified memory accessible from both.</summary>
    Unified
}

/// <summary>
/// Memory management capabilities.
/// </summary>
public sealed record MemoryCapabilities
{
    /// <summary>Supports unified/managed memory.</summary>
    public bool SupportsUnifiedMemory { get; init; }

    /// <summary>Supports memory pooling.</summary>
    public bool SupportsPooling { get; init; }

    /// <summary>Supports peer-to-peer transfers.</summary>
    public bool SupportsP2P { get; init; }

    /// <summary>Maximum allocation size in bytes.</summary>
    public long MaxAllocationSize { get; init; }

    /// <summary>Total available memory in bytes.</summary>
    public long TotalMemory { get; init; }

    /// <summary>Memory alignment requirement in bytes.</summary>
    public int AlignmentRequirement { get; init; } = 256;
}

/// <summary>
/// Current memory usage statistics.
/// </summary>
public sealed record MemoryStatistics
{
    /// <summary>Total allocated bytes.</summary>
    public long AllocatedBytes { get; init; }

    /// <summary>Available bytes.</summary>
    public long AvailableBytes { get; init; }

    /// <summary>Number of active allocations.</summary>
    public int AllocationCount { get; init; }

    /// <summary>Pool hit rate (0.0-1.0).</summary>
    public double PoolHitRate { get; init; }

    /// <summary>Peak allocated bytes.</summary>
    public long PeakAllocatedBytes { get; init; }
}
