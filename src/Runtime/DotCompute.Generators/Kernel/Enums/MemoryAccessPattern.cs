// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

/// <summary>
/// Memory access patterns for kernel optimization.
/// This is a netstandard2.0-compatible copy of the canonical version from
/// DotCompute.Abstractions.Types.MemoryAccessPattern. The values and semantics
/// must be kept in sync with the canonical version.
/// </summary>
/// <remarks>
/// This copy exists because the source generator targets netstandard2.0 and cannot
/// reference the main Abstractions assembly. Any changes to the canonical enum
/// should be reflected here to maintain compatibility.
/// </remarks>
public enum MemoryAccessPattern
{
    /// <summary>Sequential memory access with optimal cache utilization.</summary>
    Sequential = 0,

    /// <summary>Strided access with fixed step size between elements.</summary>
    Strided = 1,

    /// <summary>Coalesced access optimized for GPU memory architecture.</summary>
    Coalesced = 2,

    /// <summary>Random memory access with unpredictable patterns.</summary>
    Random = 3,

    /// <summary>Mixed patterns requiring runtime analysis.</summary>
    Mixed = 4,

    /// <summary>Scatter operation writing to non-contiguous locations.</summary>
    Scatter = 5,

    /// <summary>Gather operation reading from non-contiguous locations.</summary>
    Gather = 6,

    /// <summary>Combined scatter-gather operations.</summary>
    ScatterGather = 7,

    /// <summary>Broadcast operation from single source to multiple destinations.</summary>
    Broadcast = 8,

    /// <summary>Tiled access for optimized cache usage.</summary>
    Tiled = 9,

    /// <summary>Unknown or unanalyzed access pattern.</summary>
    Unknown = 10
}