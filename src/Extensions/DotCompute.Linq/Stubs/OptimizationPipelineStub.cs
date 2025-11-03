using System;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub optimization pipeline for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 6.
/// </remarks>
public class OptimizationPipelineStub : IOptimizationPipeline
{
    /// <inheritdoc/>
    public OperationGraph Process(OperationGraph graph)
        => throw new NotImplementedException("Phase 6: Query Optimization");

    /// <inheritdoc/>
    public void AddOptimizer(IOptimizer optimizer)
        => throw new NotImplementedException("Phase 6: Query Optimization");
}
