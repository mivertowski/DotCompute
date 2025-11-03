using System;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Describes memory access patterns in a compute operation for optimization.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// Critical for GPU performance through coalesced memory access.
/// </remarks>
public class MemoryAccessPattern
{
    /// <summary>
    /// Gets a value indicating whether memory accesses are coalesced.
    /// </summary>
    /// <remarks>
    /// Coalesced access is critical for GPU memory bandwidth utilization.
    /// </remarks>
    public bool IsCoalesced { get; init; }

    /// <summary>
    /// Gets the stride size for memory accesses in bytes.
    /// </summary>
    public int StrideSize { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation requires caching.
    /// </summary>
    public bool RequiresCaching { get; init; }

    /// <summary>
    /// Gets a value indicating whether the access pattern is sequential.
    /// </summary>
    public bool IsSequential { get; init; }

    /// <summary>
    /// Gets the number of memory transactions required.
    /// </summary>
    public int TransactionCount { get; init; }
}
