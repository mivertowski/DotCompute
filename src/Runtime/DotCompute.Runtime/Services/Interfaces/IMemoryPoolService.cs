// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Interface for memory pool service that provides efficient buffer reuse and management.
/// </summary>
public interface IMemoryPoolService : IDisposable
{
    /// <summary>
    /// Gets statistics about pool performance.
    /// </summary>
    public MemoryPoolStatistics Statistics { get; }

    /// <summary>
    /// Tries to get a buffer from the pool for the specified size.
    /// </summary>
    /// <param name="sizeInBytes">The size of buffer needed.</param>
    /// <param name="options">Memory options for the buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pooled buffer if available, null otherwise.</returns>
    public ValueTask<IUnifiedMemoryBuffer?> TryGetBufferAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a buffer to the pool for reuse.
    /// </summary>
    /// <param name="buffer">The buffer to return to the pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask ReturnBufferAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new pooled buffer.
    /// </summary>
    /// <param name="sizeInBytes">The size of the buffer in bytes.</param>
    /// <param name="options">Memory options for the buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new pooled buffer.</returns>
    public ValueTask<IUnifiedMemoryBuffer> CreateBufferAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs cleanup of unused buffers and statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask PerformMaintenanceAsync(CancellationToken cancellationToken = default);
}