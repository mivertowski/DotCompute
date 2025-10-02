// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Memory optimization hints for pipeline execution.
/// These hints guide memory allocation and management strategies.
/// </summary>
[Flags]
public enum MemoryHint
{
    /// <summary>
    /// No specific memory hints.
    /// </summary>
    None = 0,

    /// <summary>
    /// Prefer sequential memory access patterns for better cache performance.
    /// </summary>
    SequentialAccess = 1 << 0,

    /// <summary>
    /// Expect random memory access patterns, optimize accordingly.
    /// </summary>
    RandomAccess = 1 << 1,

    /// <summary>
    /// Data will be read multiple times, optimize for read performance.
    /// </summary>
    ReadHeavy = 1 << 2,

    /// <summary>
    /// Data will be written frequently, optimize for write performance.
    /// </summary>
    WriteHeavy = 1 << 3,

    /// <summary>
    /// Data will be used only once, optimize for single-use access.
    /// </summary>
    SingleUse = 1 << 4,

    /// <summary>
    /// Data will be reused multiple times, optimize for caching.
    /// </summary>
    Reusable = 1 << 5,

    /// <summary>
    /// Large dataset that may not fit in cache, use streaming access.
    /// </summary>
    LargeDataset = 1 << 6,

    /// <summary>
    /// Small dataset that should fit in cache, optimize for locality.
    /// </summary>
    SmallDataset = 1 << 7,

    /// <summary>
    /// Memory will be shared between host and device, optimize for unified access.
    /// </summary>
    SharedMemory = 1 << 8,

    /// <summary>
    /// Memory will be used only on device, optimize for device-specific access.
    /// </summary>
    DeviceOnly = 1 << 9,

    /// <summary>
    /// Memory will be used only on host, optimize for host access.
    /// </summary>
    HostOnly = 1 << 10,

    /// <summary>
    /// Use pinned memory for faster host-device transfers.
    /// </summary>
    PinnedMemory = 1 << 11,

    /// <summary>
    /// Use memory pooling to reduce allocation overhead.
    /// </summary>
    UseMemoryPool = 1 << 12,

    /// <summary>
    /// Avoid memory pooling, use direct allocation.
    /// </summary>
    AvoidMemoryPool = 1 << 13,

    /// <summary>
    /// Optimize for low memory usage, prefer compression where possible.
    /// </summary>
    LowMemoryUsage = 1 << 14,

    /// <summary>
    /// Optimize for high bandwidth memory access.
    /// </summary>
    HighBandwidth = 1 << 15,

    /// <summary>
    /// Optimize for low latency memory access.
    /// </summary>
    LowLatency = 1 << 16,

    /// <summary>
    /// Memory will be accessed in a streaming fashion, optimize for throughput.
    /// </summary>
    StreamingAccess = 1 << 17,

    /// <summary>
    /// Memory layout should be optimized for vectorized operations.
    /// </summary>
    VectorizedAccess = 1 << 18,

    /// <summary>
    /// Memory should be aligned for optimal SIMD performance.
    /// </summary>
    AlignedAccess = 1 << 19,

    /// <summary>
    /// Use write-combining memory for better write performance.
    /// </summary>
    WriteCombining = 1 << 20,

    /// <summary>
    /// Use non-cached memory for better cache utilization of other data.
    /// </summary>
    NonCached = 1 << 21,

    /// <summary>
    /// Prefer structure-of-arrays layout for better vectorization.
    /// </summary>
    StructureOfArrays = 1 << 22,

    /// <summary>
    /// Prefer array-of-structures layout for better locality.
    /// </summary>
    ArrayOfStructures = 1 << 23,

    /// <summary>
    /// Memory will be accessed by multiple threads, optimize for concurrent access.
    /// </summary>
    ConcurrentAccess = 1 << 24,

    /// <summary>
    /// Memory will be accessed by a single thread, optimize accordingly.
    /// </summary>
    SingleThreadAccess = 1 << 25
}