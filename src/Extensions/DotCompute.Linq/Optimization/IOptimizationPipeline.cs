using System;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Interface for a pipeline of optimization passes that transform operation graphs.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// </remarks>
[Experimental("DOTCOMPUTE0004", UrlFormat = "https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{0}.md")]
public interface IOptimizationPipeline
{
    /// <summary>
    /// Processes an operation graph through all registered optimizers.
    /// </summary>
    /// <param name="graph">The operation graph to process.</param>
    /// <returns>The optimized operation graph.</returns>
    public OperationGraph Process(OperationGraph graph);

    /// <summary>
    /// Adds an optimizer to the pipeline.
    /// </summary>
    /// <param name="optimizer">The optimizer to add.</param>
    public void AddOptimizer(IOptimizer optimizer);
}

/// <summary>
/// Base interface for optimization passes.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// </remarks>
[Experimental("DOTCOMPUTE0004", UrlFormat = "https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{0}.md")]
public interface IOptimizer
{
    /// <summary>
    /// Optimizes an operation graph.
    /// </summary>
    /// <param name="graph">The operation graph to optimize.</param>
    /// <returns>The optimized operation graph.</returns>
    public OperationGraph Optimize(OperationGraph graph);
}
