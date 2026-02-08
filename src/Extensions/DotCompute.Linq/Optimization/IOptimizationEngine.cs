using System;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Interface for query optimization engine that analyzes and transforms operation graphs.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// </remarks>
[Experimental("DOTCOMPUTE0004", UrlFormat = "https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{0}.md")]
public interface IOptimizationEngine
{
    /// <summary>
    /// Optimizes an operation graph using various optimization strategies.
    /// </summary>
    /// <param name="graph">The operation graph to optimize.</param>
    /// <returns>An optimized operation graph.</returns>
    public OperationGraph Optimize(OperationGraph graph);

    /// <summary>
    /// Selects the most appropriate optimization strategy for the given workload.
    /// </summary>
    /// <param name="workload">The workload characteristics to analyze.</param>
    /// <returns>The recommended optimization strategy.</returns>
    public OptimizationStrategy SelectStrategy(WorkloadCharacteristics workload);
}
