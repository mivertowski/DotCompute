using DotCompute.Linq.CodeGeneration;
using System;
using System.Collections.Generic;
using DotCompute.Linq.Compilation;

using DotCompute.Abstractions;
namespace DotCompute.Linq.Optimization;

/// <summary>
/// Contains performance profiling data for a compute operation.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// Used for adaptive optimization and backend selection.
/// </remarks>
public class PerformanceProfile
{
    /// <summary>
    /// Gets the total execution time in milliseconds.
    /// </summary>
    public double TotalExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets the kernel compilation time in milliseconds.
    /// </summary>
    public double CompilationTimeMs { get; init; }

    /// <summary>
    /// Gets the optimal compute backend for this operation.
    /// </summary>
    public ComputeBackend OptimalBackend { get; init; }

    /// <summary>
    /// Gets execution times for individual operations.
    /// </summary>
    public Dictionary<string, double> OperationTimes { get; init; } = new();

    /// <summary>
    /// Gets memory transfer time in milliseconds.
    /// </summary>
    public double MemoryTransferTimeMs { get; init; }

    /// <summary>
    /// Gets the number of kernel launches.
    /// </summary>
    public int KernelLaunchCount { get; init; }

    /// <summary>
    /// Gets the GPU occupancy percentage (0-100).
    /// </summary>
    public double GpuOccupancy { get; init; }
}
