// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Defines the types of memory recovery strategies available.
/// </summary>
public enum MemoryRecoveryStrategyType
{
    /// <summary>
    /// Simple garbage collection to free memory.
    /// </summary>
    SimpleGarbageCollection,

    /// <summary>
    /// Memory defragmentation with garbage collection.
    /// </summary>
    DefragmentationWithGC,

    /// <summary>
    /// Aggressive cleanup of all memory pools and caches.
    /// </summary>
    AggressiveCleanup,

    /// <summary>
    /// Emergency recovery using all available methods including reserve release.
    /// </summary>
    EmergencyRecovery,

    /// <summary>
    /// Pool-specific cleanup targeting individual memory pools.
    /// </summary>
    PoolSpecificCleanup,

    /// <summary>
    /// Large object heap compaction.
    /// </summary>
    LargeObjectHeapCompaction,

    /// <summary>
    /// Generation-specific garbage collection.
    /// </summary>
    GenerationSpecificGC,

    /// <summary>
    /// Cache eviction to free memory.
    /// </summary>
    CacheEviction,

    /// <summary>
    /// Memory-mapped file cleanup.
    /// </summary>
    MemoryMappedFileCleanup,

    /// <summary>
    /// Native memory cleanup for unmanaged resources.
    /// </summary>
    NativeMemoryCleanup
}
