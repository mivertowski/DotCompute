// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Constants and enums for NUMA operations.
/// </summary>
public static class NumaConstants
{
    /// <summary>
    /// Standard NUMA distance values.
    /// </summary>
    public static class Distances
    {
        /// <summary>Local node distance.</summary>
        public const int Local = 10;

        /// <summary>Remote node distance.</summary>
        public const int Remote = 20;

        /// <summary>Distant node distance.</summary>
        public const int Distant = 30;
    }

    /// <summary>
    /// Standard memory and cache sizes.
    /// </summary>
    public static class Sizes
    {
        /// <summary>Standard cache line size.</summary>
        public const int CacheLineSize = 64;

        /// <summary>Standard page size.</summary>
        public const int PageSize = 4096;

        /// <summary>Large page size (2MB).</summary>
        public const int LargePageSize = 2 * 1024 * 1024;

        /// <summary>Huge page size (1GB).</summary>
        public const int HugePageSize = 1024 * 1024 * 1024;
    }

    /// <summary>
    /// Platform-specific limits.
    /// </summary>
    public static class Limits
    {
        /// <summary>Maximum CPUs in a single mask (ulong limit).</summary>
        public const int MaxCpusInMask = 64;

        /// <summary>Maximum processors per NUMA node estimate.</summary>
        public const int MaxProcessorsPerNode = 128;

        /// <summary>Maximum NUMA nodes supported.</summary>
        public const int MaxNumaNodes = 256;
    }
}

/// <summary>
/// NUMA memory allocation policy.
/// </summary>
public enum NumaMemoryPolicy
{
    /// <summary>Default system policy.</summary>
    Default,

    /// <summary>Bind to specific nodes.</summary>
    Bind,

    /// <summary>Interleave across nodes.</summary>
    Interleave,

    /// <summary>Prefer specific nodes.</summary>
    Preferred,

    /// <summary>Local allocation only.</summary>
    Local
}

/// <summary>
/// NUMA optimization strategy.
/// </summary>
public enum NumaOptimizationStrategy
{
    /// <summary>No NUMA optimizations.</summary>
    None,

    /// <summary>Basic NUMA awareness.</summary>
    Basic,

    /// <summary>Aggressive NUMA optimizations.</summary>
    Aggressive,

    /// <summary>Adaptive based on workload.</summary>
    Adaptive
}

/// <summary>
/// NUMA node selection criteria.
/// </summary>
public enum NumaNodeSelectionCriteria
{
    /// <summary>Select by CPU affinity.</summary>
    CpuAffinity,

    /// <summary>Select by memory availability.</summary>
    MemoryAvailability,

    /// <summary>Select by distance minimization.</summary>
    Distance,

    /// <summary>Select by load balancing.</summary>
    LoadBalance,

    /// <summary>Round-robin selection.</summary>
    RoundRobin
}