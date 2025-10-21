// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Threading.NUMA;

/// <summary>
/// Constants and enums for NUMA operations.
/// </summary>
/// <remarks>
/// Note: Nested constant classes have been moved to separate files:
/// - <see cref="NumaDistances"/> for distance values
/// - <see cref="NumaSizes"/> for memory and cache sizes
/// - <see cref="NumaLimits"/> for platform limits
/// </remarks>
public static class NumaConstants
{
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