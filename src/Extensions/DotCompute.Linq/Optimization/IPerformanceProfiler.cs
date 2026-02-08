using System;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Interface for performance profiling and execution time tracking.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// Profiling data guides adaptive optimization decisions.
/// </remarks>
[Experimental("DOTCOMPUTE0004", UrlFormat = "https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{0}.md")]
public interface IPerformanceProfiler
{
    /// <summary>
    /// Profiles an operation graph to estimate execution characteristics.
    /// </summary>
    /// <param name="graph">The operation graph to profile.</param>
    /// <returns>Performance profile with timing estimates.</returns>
    public PerformanceProfile Profile(OperationGraph graph);

    /// <summary>
    /// Records actual execution time for an operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="timeMs">Execution time in milliseconds.</param>
    public void RecordExecution(string operationId, double timeMs);
}
