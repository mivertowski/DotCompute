using System;
using System.Diagnostics.CodeAnalysis;
using MemoryPattern = DotCompute.Abstractions.Types.MemoryAccessPattern;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Interface for memory access pattern optimization to improve cache utilization and coalescing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// Memory optimization is critical for GPU performance.
/// </remarks>
[Experimental("DOTCOMPUTE0004", UrlFormat = "https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{0}.md")]
public interface IMemoryOptimizer
{
    /// <summary>
    /// Analyzes memory access patterns in an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to analyze.</param>
    /// <returns>Memory access pattern information.</returns>
    public MemoryPattern AnalyzeAccess(OperationGraph graph);

    /// <summary>
    /// Optimizes the operation graph for better cache utilization and memory coalescing.
    /// </summary>
    /// <param name="graph">The operation graph to optimize.</param>
    /// <returns>An optimized operation graph with improved memory access patterns.</returns>
    public OperationGraph OptimizeForCache(OperationGraph graph);
}
