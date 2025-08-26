// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions;

/// <summary>
/// Unified memory manager interface that replaces all duplicate memory management interfaces.
/// This is the ONLY memory manager interface in the entire solution.
/// </summary>
public interface IUnifiedMemoryManager : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Gets the accelerator this memory manager is associated with.
    /// </summary>
    IAccelerator Accelerator { get; }
    
    /// <summary>
    /// Gets memory usage statistics.
    /// </summary>
    MemoryStatistics Statistics { get; }
    
    /// <summary>
    /// Gets the maximum memory allocation size in bytes.
    /// </summary>
    long MaxAllocationSize { get; }
    
    /// <summary>
    /// Gets the total available memory in bytes.
    /// </summary>
    long TotalAvailableMemory { get; }
    
    /// <summary>
    /// Gets the current allocated memory in bytes.
    /// </summary>
    long CurrentAllocatedMemory { get; }
    
    /// <summary>
    /// Allocates a memory buffer for a specific number of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="count">The number of elements to allocate.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A newly allocated memory buffer.</returns>
    ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Allocates memory and copies data from host.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source data to copy.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A newly allocated and initialized memory buffer.</returns>
    ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Allocates memory by size in bytes (for advanced scenarios).
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes to allocate.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A newly allocated memory buffer.</returns>
    ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a view over existing memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The offset in elements.</param>
    /// <param name="length">The length of the view in elements.</param>
    /// <returns>A view over the existing buffer.</returns>
    IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length) where T : unmanaged;
    
    /// <summary>
    /// Copies data between buffers.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source buffer.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Copies data between buffers with specified ranges.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source buffer.</param>
    /// <param name="sourceOffset">The offset in the source buffer.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="destinationOffset">The offset in the destination buffer.</param>
    /// <param name="count">The number of elements to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Copies data from host memory to a device buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source data.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Copies data from a device buffer to host memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The source buffer.</param>
    /// <param name="destination">The destination memory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the copy operation.</returns>
    ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <summary>
    /// Frees a memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to free.</param>
    void Free(IUnifiedMemoryBuffer buffer);
    
    /// <summary>
    /// Asynchronously frees a memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to free.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the free operation.</returns>
    ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Optimizes memory by defragmenting and releasing unused memory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the optimization operation.</returns>
    ValueTask OptimizeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears all allocated memory and resets the manager.
    /// </summary>
    void Clear();
}