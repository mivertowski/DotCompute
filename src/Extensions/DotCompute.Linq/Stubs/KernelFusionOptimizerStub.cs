using System;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub kernel fusion optimizer for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 6.
/// </remarks>
public class KernelFusionOptimizerStub : IKernelFusionOptimizer
{
    /// <inheritdoc/>
    public OperationGraph FuseOperations(OperationGraph graph)
        => throw new NotImplementedException("Phase 6: Query Optimization");

    /// <inheritdoc/>
    public bool CanFuse(Operation op1, Operation op2) => false;
}
