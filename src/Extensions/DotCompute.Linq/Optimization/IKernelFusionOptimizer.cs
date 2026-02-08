using System;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Interface for kernel fusion optimization that combines multiple operations into single kernels.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// Kernel fusion reduces memory transfers and improves GPU utilization.
/// </remarks>
[Experimental("DOTCOMPUTE0004", UrlFormat = "https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{0}.md")]
public interface IKernelFusionOptimizer
{
    /// <summary>
    /// Analyzes and fuses compatible operations in the graph to reduce kernel launches.
    /// </summary>
    /// <param name="graph">The operation graph to analyze for fusion opportunities.</param>
    /// <returns>An optimized graph with fused operations.</returns>
    public OperationGraph FuseOperations(OperationGraph graph);

    /// <summary>
    /// Determines if two operations can be fused into a single kernel.
    /// </summary>
    /// <param name="op1">The first operation to check.</param>
    /// <param name="op2">The second operation to check.</param>
    /// <returns>True if the operations can be fused; otherwise, false.</returns>
    public bool CanFuse(Operation op1, Operation op2);
}
