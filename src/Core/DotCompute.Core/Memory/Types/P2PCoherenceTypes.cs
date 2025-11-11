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
    public Guid BufferId { get; init; }
    public IAccelerator? OwnerDevice { get; init; }
    public ConcurrentDictionary<string, BufferCopy> DeviceCopies { get; init; } = new();
    public ConcurrentDictionary<string, DeviceCoherenceState> DeviceStates { get; init; } = new();
    public CoherenceLevel CurrentCoherenceLevel { get; set; }
    public AccessPattern DetectedAccessPattern { get; set; }
    public DateTimeOffset LastSyncTime { get; set; }
    public long TotalAccesses { get; set; }
    public long SyncOperations { get; set; }

    // Additional properties used in implementation
    public object? SourceBuffer { get; set; }
    public IAccelerator? SourceDevice { get; set; }
    public IAccelerator? TargetDevice { get; set; }
    public long Offset { get; set; }
    public long ElementCount { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public bool IsCoherent { get; set; }
    public CoherenceLevel CoherenceLevel { get; set; }
    public P2PConnectionCapability? P2PCapability { get; set; }
    public AccessPattern AccessPattern { get; set; }
    public IList<BufferCopy> Copies { get; set; } = [];
}

/// <summary>
/// Represents a buffer copy on a specific device.
/// </summary>
internal sealed class BufferCopy
{
    public string DeviceId { get; init; } = string.Empty;
    public IntPtr DevicePointer { get; init; }
    public long SizeInBytes { get; init; }
    public bool IsValid { get; set; }
    public bool IsDirty { get; set; }
    public DateTimeOffset LastAccessTime { get; set; }
    public long AccessCount { get; set; }
    public AccessType LastAccessType { get; set; }

    // Additional properties used in implementation
    public IAccelerator? Device { get; set; }
    public object? Buffer { get; set; }
    public DateTimeOffset LastAccessed { get; set; }
    public bool IsWritten { get; set; }
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

    // Additional properties used in implementation
    public int CoherentBuffers { get; set; }
    public int IncoherentBuffers { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>
/// Analysis of optimal buffer placement across devices.
/// </summary>
internal sealed class BufferPlacementAnalysis
{
    public Guid BufferId { get; init; }
    public string CurrentOwner { get; init; } = string.Empty;
    public Dictionary<string, int> AccessCountsByDevice { get; init; } = new();
    public Dictionary<string, TimeSpan> AccessLatencyByDevice { get; init; } = new();
    public string? RecommendedOwner { get; set; }
    public double EstimatedGain { get; set; }

    // Additional properties used in implementation
    public Dictionary<string, int> DeviceDistribution { get; set; } = [];
    public IList<string> HotspotDevices { get; set; } = [];
    public IList<string> UnderutilizedDevices { get; set; } = [];
}

/// <summary>
/// Optimization recommendation for buffer coherence.
/// </summary>
internal sealed class PlacementOptimization
{
    public Guid BufferId { get; init; }
    public OptimizationType OptimizationType { get; init; }
    public string TargetDevice { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double EstimatedBenefit { get; init; }
    public TimeSpan EstimatedMigrationCost { get; init; }
    public double NetBenefit => EstimatedBenefit - EstimatedMigrationCost.TotalMilliseconds;

    // Additional properties used in implementation
    public string? SourceDeviceId { get; set; }
    public string? TargetDeviceId { get; set; }
    public P2PConnectionCapability? P2PCapability { get; set; }
    public double ExpectedBenefit { get; set; }
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

    /// <summary>Gets or sets total tracked buffers (alias).</summary>
    public int TotalTrackedBuffers { get; set; }

    /// <summary>Gets or sets total synchronization operations.</summary>
    public long TotalSyncOperations { get; set; }

    /// <summary>Gets or sets synchronization operations (alias).</summary>
    public long SynchronizationOperations { get; set; }

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

    /// <summary>Gets or sets number of coherent buffers.</summary>
    public int CoherentBuffers { get; set; }

    /// <summary>Gets or sets number of incoherent buffers.</summary>
    public int IncoherentBuffers { get; set; }

    /// <summary>Gets or sets number of read operations.</summary>
    public long ReadOperations { get; set; }

    /// <summary>Gets or sets number of write operations.</summary>
    public long WriteOperations { get; set; }

    /// <summary>Gets or sets coherence efficiency score (0.0-1.0).</summary>
    public double CoherenceEfficiency { get; set; }

    /// <summary>Gets average sync time per operation.</summary>
    public TimeSpan AverageSyncTime { get; set; }

    /// <summary>Gets coherence hit rate (0.0-1.0).</summary>
    public double CoherenceHitRate => (CoherenceHits + CoherenceMisses) > 0 ? (double)CoherenceHits / (CoherenceHits + CoherenceMisses) : 0.0;

    /// <summary>Gets lazy sync ratio (0.0-1.0).</summary>
    public double LazySyncRatio => TotalSyncOperations > 0 ? (double)LazySyncOperations / TotalSyncOperations : 0.0;
}
