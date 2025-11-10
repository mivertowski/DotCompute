// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;

namespace DotCompute.Core.Memory.Types;

/// <summary>
/// Internal coherence information for a P2P buffer across devices.
/// </summary>
internal sealed class P2PBufferCoherenceInfo
{
    public required Guid BufferId { get; init; }
    public required IAccelerator OwnerDevice { get; init; }
    public required ConcurrentDictionary<string, BufferCopy> DeviceCopies { get; init; }
    public required ConcurrentDictionary<string, DeviceCoherenceState> DeviceStates { get; init; }
    public CoherenceLevel CurrentCoherenceLevel { get; set; }
    public AccessPattern DetectedAccessPattern { get; set; }
    public DateTimeOffset LastSyncTime { get; set; }
    public long TotalAccesses { get; set; }
    public long SyncOperations { get; set; }
}

/// <summary>
/// Represents a buffer copy on a specific device.
/// </summary>
internal sealed class BufferCopy
{
    public required string DeviceId { get; init; }
    public required IntPtr DevicePointer { get; init; }
    public required long SizeInBytes { get; init; }
    public bool IsValid { get; set; }
    public bool IsDirty { get; set; }
    public DateTimeOffset LastAccessTime { get; set; }
    public long AccessCount { get; set; }
    public AccessType LastAccessType { get; set; }
}

/// <summary>
/// Coherence state for a device holding buffer copy.
/// </summary>
internal sealed class DeviceCoherenceState
{
    public required string DeviceId { get; init; }
    public CoherenceLevel State { get; set; }
    public bool HasExclusiveAccess { get; set; }
    public bool IsPendingSync { get; set; }
    public DateTimeOffset StateChangedAt { get; set; }
    public List<Guid> PendingDependencies { get; init; } = [];
}

/// <summary>
/// Analysis of optimal buffer placement across devices.
/// </summary>
internal sealed class BufferPlacementAnalysis
{
    public required Guid BufferId { get; init; }
    public required string CurrentOwner { get; init; }
    public required Dictionary<string, int> AccessCountsByDevice { get; init; }
    public required Dictionary<string, TimeSpan> AccessLatencyByDevice { get; init; }
    public string? RecommendedOwner { get; set; }
    public double EstimatedGain { get; set; }
}

/// <summary>
/// Optimization recommendation for buffer coherence.
/// </summary>
internal sealed class PlacementOptimization
{
    public required Guid BufferId { get; init; }
    public required OptimizationType OptimizationType { get; init; }
    public required string TargetDevice { get; init; }
    public string Description { get; init; } = string.Empty;
    public double EstimatedBenefit { get; init; }
    public TimeSpan EstimatedMigrationCost { get; init; }
    public double NetBenefit => EstimatedBenefit - EstimatedMigrationCost.TotalMilliseconds;
}

/// <summary>
/// Statistics for P2P memory coherence operations.
/// </summary>
/// <remarks>
/// Tracks synchronization efficiency, access patterns, and optimization effectiveness
/// for multi-GPU memory coherence management.
/// </remarks>
public sealed class CoherenceStatistics
{
    /// <summary>Gets or sets total buffers tracked.</summary>
    public int TotalBuffersTracked { get; set; }

    /// <summary>Gets or sets total synchronization operations.</summary>
    public long TotalSyncOperations { get; set; }

    /// <summary>Gets or sets total memory accesses.</summary>
    public long TotalAccesses { get; set; }

    /// <summary>Gets or sets lazy sync operations (deferred).</summary>
    public long LazySyncOperations { get; set; }

    /// <summary>Gets or sets eager sync operations (immediate).</summary>
    public long EagerSyncOperations { get; set; }

    /// <summary>Gets or sets cache hits (already coherent).</summary>
    public long CoherenceHits { get; set; }

    /// <summary>Gets or sets cache misses (required sync).</summary>
    public long CoherenceMisses { get; set; }

    /// <summary>Gets or sets buffer migrations performed.</summary>
    public long MigrationsPerformed { get; set; }

    /// <summary>Gets or sets invalidations performed.</summary>
    public long InvalidationsPerformed { get; set; }

    /// <summary>Gets or sets total time spent synchronizing.</summary>
    public TimeSpan TotalSyncTime { get; set; }

    /// <summary>Gets average sync time per operation.</summary>
    public TimeSpan AverageSyncTime =>
        TotalSyncOperations > 0
            ? TimeSpan.FromTicks(TotalSyncTime.Ticks / TotalSyncOperations)
            : TimeSpan.Zero;

    /// <summary>Gets coherence hit rate (0.0-1.0).</summary>
    public double CoherenceHitRate =>
        (CoherenceHits + CoherenceMisses) > 0
            ? (double)CoherenceHits / (CoherenceHits + CoherenceMisses)
            : 0.0;

    /// <summary>Gets lazy sync ratio (0.0-1.0).</summary>
    public double LazySyncRatio =>
        TotalSyncOperations > 0
            ? (double)LazySyncOperations / TotalSyncOperations
            : 0.0;
}
