// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Barriers;

/// <summary>
/// Metal-specific barrier scope mapping to MSL primitives.
/// </summary>
public enum MetalBarrierScope
{
    /// <summary>
    /// Threadgroup-level barrier using threadgroup_barrier().
    /// Synchronizes all threads within a single threadgroup.
    /// </summary>
    Threadgroup,

    /// <summary>
    /// Simdgroup-level barrier using simdgroup_barrier().
    /// Synchronizes threads within a single simdgroup (32 threads on Apple GPUs).
    /// </summary>
    Simdgroup
}

/// <summary>
/// Metal memory fence flags for barrier synchronization.
/// Maps directly to MSL mem_flags enumeration.
/// </summary>
[Flags]
public enum MetalMemoryFenceFlags
{
    /// <summary>
    /// No memory fence (execution barrier only).
    /// Maps to mem_flags::mem_none in MSL.
    /// </summary>
    None = 0,

    /// <summary>
    /// Fence device (global) memory operations.
    /// Maps to mem_flags::mem_device in MSL.
    /// </summary>
    Device = 1 << 0,

    /// <summary>
    /// Fence threadgroup (shared) memory operations.
    /// Maps to mem_flags::mem_threadgroup in MSL.
    /// </summary>
    Threadgroup = 1 << 1,

    /// <summary>
    /// Fence texture memory operations (for read_write textures).
    /// Maps to mem_flags::mem_texture in MSL.
    /// </summary>
    Texture = 1 << 2,

    /// <summary>
    /// Combined device and threadgroup fence.
    /// Maps to mem_flags::mem_device_and_threadgroup in MSL.
    /// </summary>
    DeviceAndThreadgroup = Device | Threadgroup
}

/// <summary>
/// Statistics for Metal barrier operations.
/// </summary>
public sealed class MetalBarrierStatistics
{
    /// <summary>
    /// Gets or sets the total number of barriers created.
    /// </summary>
    public int TotalBarriersCreated { get; set; }

    /// <summary>
    /// Gets or sets the number of active barriers.
    /// </summary>
    public int ActiveBarriers { get; set; }

    /// <summary>
    /// Gets or sets the number of threadgroup barriers.
    /// </summary>
    public int ThreadgroupBarriers { get; set; }

    /// <summary>
    /// Gets or sets the number of simdgroup barriers.
    /// </summary>
    public int SimdgroupBarriers { get; set; }

    /// <summary>
    /// Gets or sets the number of barrier executions.
    /// </summary>
    public long TotalBarrierExecutions { get; set; }

    /// <summary>
    /// Gets or sets the average barrier execution time in microseconds.
    /// </summary>
    public double AverageExecutionTimeMicroseconds { get; set; }

    /// <summary>
    /// Gets when these statistics were last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configuration for Metal barrier provider.
/// </summary>
public sealed class MetalBarrierConfiguration
{
    /// <summary>
    /// Gets or sets whether to enable barrier performance profiling.
    /// Default: false (minimal overhead).
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets the default memory fence flags for threadgroup barriers.
    /// Default: DeviceAndThreadgroup (safest, ensures visibility across device).
    /// </summary>
    public MetalMemoryFenceFlags DefaultThreadgroupFenceFlags { get; set; } = MetalMemoryFenceFlags.DeviceAndThreadgroup;

    /// <summary>
    /// Gets or sets the default memory fence flags for simdgroup barriers.
    /// Default: None (execution barrier only, optimal for warp-level sync).
    /// </summary>
    public MetalMemoryFenceFlags DefaultSimdgroupFenceFlags { get; set; } = MetalMemoryFenceFlags.None;

    /// <summary>
    /// Gets or sets whether to validate barrier parameters at runtime.
    /// Default: true (ensures correctness).
    /// </summary>
    public bool ValidateBarrierParameters { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of barriers to cache.
    /// Default: 64 (reasonable for most workloads).
    /// </summary>
    public int MaxCachedBarriers { get; set; } = 64;
}
