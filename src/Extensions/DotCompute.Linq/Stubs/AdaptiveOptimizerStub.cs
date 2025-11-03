using System;
using DotCompute.Linq.Compilation;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub adaptive optimizer for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 6.
/// </remarks>
public class AdaptiveOptimizerStub : IAdaptiveOptimizer
{
    /// <inheritdoc/>
    public ComputeBackend SelectBackend(WorkloadCharacteristics workload)
        => ComputeBackend.Cpu;

    /// <inheritdoc/>
    public void TrainFromHistory(ExecutionHistory history)
        => throw new NotImplementedException("Phase 6: Query Optimization");

    /// <inheritdoc/>
    public OptimizationStrategy PredictOptimalStrategy(OperationGraph graph)
        => OptimizationStrategy.Conservative;
}
