// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory.Types;

/// <summary>
/// Coherence level indicating synchronization state across devices.
/// </summary>
public enum CoherenceLevel
{
    /// <summary>Data is coherent across all devices.</summary>
    FullyCoherent,
    /// <summary>Data may be stale on some devices.</summary>
    PartiallyCoherent,
    /// <summary>Data is incoherent and requires synchronization.</summary>
    Incoherent,
    /// <summary>Buffer is write-locked by one device.</summary>
    Exclusive,
    /// <summary>Buffer is being actively synchronized.</summary>
    Syncing
}

/// <summary>
/// Memory access pattern for optimization hints.
/// </summary>
public enum AccessPattern
{
    /// <summary>Random access with no clear pattern.</summary>
    Random,
    /// <summary>Sequential access in order.</summary>
    Sequential,
    /// <summary>Strided access with fixed offset.</summary>
    Strided,
    /// <summary>Broadcast pattern (one write, many reads).</summary>
    Broadcast,
    /// <summary>Reduction pattern (many writes, one read).</summary>
    Reduction,
    /// <summary>Unknown or mixed pattern.</summary>
    Unknown
}

/// <summary>
/// Type of memory access operation.
/// </summary>
public enum AccessType
{
    /// <summary>Read-only access.</summary>
    Read,
    /// <summary>Write access modifying data.</summary>
    Write,
    /// <summary>Read-modify-write atomic operation.</summary>
    ReadWrite,
    /// <summary>No access (buffer transfer only).</summary>
    None
}

/// <summary>
/// Strategy for buffer synchronization.
/// </summary>
public enum SyncStrategy
{
    /// <summary>Eager synchronization on every access.</summary>
    Eager,
    /// <summary>Lazy synchronization on demand.</summary>
    Lazy,
    /// <summary>Invalidate remote copies instead of updating.</summary>
    Invalidate,
    /// <summary>Write-through to all copies immediately.</summary>
    WriteThrough,
    /// <summary>Write-back with deferred synchronization.</summary>
    WriteBack
}

/// <summary>
/// Type of coherence optimization applied.
/// </summary>
public enum OptimizationType
{
    /// <summary>No optimization applied.</summary>
    None,
    /// <summary>Prefetch data to target device.</summary>
    Prefetch,
    /// <summary>Migrate buffer to optimal device.</summary>
    Migration,
    /// <summary>Replicate buffer across devices.</summary>
    Replication,
    /// <summary>Partition buffer for distributed access.</summary>
    Partitioning
}
