using System;
using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.Stubs;

/// <summary>
/// Stub performance profiler for Phase 2 testing.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Throws NotImplementedException. Real implementation in Phase 6.
/// </remarks>
public class PerformanceProfilerStub : IPerformanceProfiler
{
    /// <inheritdoc/>
    public PerformanceProfile Profile(OperationGraph graph)
        => throw new NotImplementedException("Phase 6: Query Optimization");

    /// <inheritdoc/>
    public void RecordExecution(string operationId, double timeMs)
        => throw new NotImplementedException("Phase 6: Query Optimization");
}
