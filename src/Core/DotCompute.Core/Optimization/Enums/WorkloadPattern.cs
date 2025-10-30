// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Enums;
/// <summary>
/// An workload pattern enumeration.
/// </summary>

/// <summary>
/// Classification of workload patterns for backend optimization.
/// </summary>
public enum WorkloadPattern
{
    /// <summary>Sequential access pattern.</summary>
    Sequential,
    /// <summary>Compute-intensive workload pattern.</summary>
    ComputeIntensive,
    /// <summary>Memory-intensive workload pattern.</summary>
    MemoryIntensive,
    /// <summary>Highly parallel workload pattern.</summary>
    HighlyParallel,
    /// <summary>Balanced workload pattern.</summary>
    Balanced,
    /// <summary>I/O-bound workload pattern.</summary>
    IOBound,
    /// <summary>Mixed workload pattern.</summary>
    Mixed
}
