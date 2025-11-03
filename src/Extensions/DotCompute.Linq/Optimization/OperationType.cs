using System;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Defines the types of operations in a compute query.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Used for workload characterization and optimization decisions.
/// </remarks>
public enum OperationType
{
    /// <summary>
    /// Map operation that transforms each element independently.
    /// </summary>
    Map = 0,

    /// <summary>
    /// Filter operation that selects elements based on a predicate.
    /// </summary>
    Filter = 1,

    /// <summary>
    /// Reduce operation that combines elements into a single result.
    /// </summary>
    Reduce = 2,

    /// <summary>
    /// Scan operation that computes prefix sums or cumulative operations.
    /// </summary>
    Scan = 3,

    /// <summary>
    /// Join operation that combines two sequences based on key equality.
    /// </summary>
    Join = 4,

    /// <summary>
    /// GroupBy operation that partitions elements by key.
    /// </summary>
    GroupBy = 5,

    /// <summary>
    /// OrderBy operation that sorts elements.
    /// </summary>
    OrderBy = 6,

    /// <summary>
    /// Aggregate operation that computes a summary value.
    /// </summary>
    Aggregate = 7
}
