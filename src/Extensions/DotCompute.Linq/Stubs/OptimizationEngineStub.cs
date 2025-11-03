using System;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub optimization engine for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 6.
/// </remarks>
public class OptimizationEngineStub : IOptimizationEngine
{
    /// <inheritdoc/>
    public OperationGraph Optimize(OperationGraph graph)
        => throw new NotImplementedException("Phase 6: Query Optimization");

    /// <inheritdoc/>
    public OptimizationStrategy SelectStrategy(WorkloadCharacteristics workload)
        => OptimizationStrategy.Conservative;
}
