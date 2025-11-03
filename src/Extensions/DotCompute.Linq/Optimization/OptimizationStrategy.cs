using System;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Defines optimization strategies for query execution.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// </remarks>
public enum OptimizationStrategy
{
    /// <summary>
    /// No optimization applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// Conservative optimization that prioritizes correctness over performance.
    /// </summary>
    Conservative = 1,

    /// <summary>
    /// Balanced optimization that trades off between correctness and performance.
    /// </summary>
    Balanced = 2,

    /// <summary>
    /// Aggressive optimization that maximizes performance with controlled risk.
    /// </summary>
    Aggressive = 3,

    /// <summary>
    /// Adaptive optimization that learns from execution history and adjusts strategy.
    /// </summary>
    Adaptive = 4
}
