using System;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub memory optimizer for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 6.
/// </remarks>
public class MemoryOptimizerStub : IMemoryOptimizer
{
    /// <inheritdoc/>
    public MemoryAccessPattern AnalyzeAccess(OperationGraph graph)
        => throw new NotImplementedException("Phase 6: Query Optimization");

    /// <inheritdoc/>
    public OperationGraph OptimizeForCache(OperationGraph graph)
        => throw new NotImplementedException("Phase 6: Query Optimization");
}
