// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Service for managing unified memory across different accelerators
/// </summary>
public interface IUnifiedMemoryService
{
    /// <summary>
    /// Allocates unified memory that can be accessed by multiple accelerators
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes</param>
    /// <param name="acceleratorIds">The accelerator IDs that will access this memory</param>
    /// <returns>The allocated unified memory buffer</returns>
    public Task<IUnifiedMemoryBuffer> AllocateUnifiedAsync(long sizeInBytes, params string[] acceleratorIds);

    /// <summary>
    /// Migrates data between accelerators
    /// </summary>
    /// <param name="buffer">The memory buffer to migrate</param>
    /// <param name="sourceAcceleratorId">The source accelerator ID</param>
    /// <param name="targetAcceleratorId">The target accelerator ID</param>
    /// <returns>A task representing the migration operation</returns>
    public Task MigrateAsync(IUnifiedMemoryBuffer buffer, string sourceAcceleratorId, string targetAcceleratorId);

    /// <summary>
    /// Synchronizes memory coherence across accelerators
    /// </summary>
    /// <param name="buffer">The memory buffer to synchronize</param>
    /// <param name="acceleratorIds">The accelerator IDs to synchronize</param>
    /// <returns>A task representing the synchronization operation</returns>
    public Task SynchronizeCoherenceAsync(IUnifiedMemoryBuffer buffer, params string[] acceleratorIds);

    /// <summary>
    /// Gets memory coherence status for a buffer
    /// </summary>
    /// <param name="buffer">The memory buffer</param>
    /// <returns>The coherence status</returns>
    public MemoryCoherenceStatus GetCoherenceStatus(IUnifiedMemoryBuffer buffer);
}

/// <summary>
/// Memory coherence status
/// </summary>
public enum MemoryCoherenceStatus
{
    /// <summary>
    /// Memory is coherent across all accelerators
    /// </summary>
    Coherent,

    /// <summary>
    /// Memory is incoherent and needs synchronization
    /// </summary>
    Incoherent,

    /// <summary>
    /// Memory coherence state is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// Memory is being synchronized
    /// </summary>
    Synchronizing
}