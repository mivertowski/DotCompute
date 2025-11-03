using System;

namespace DotCompute.Linq.Optimization;

/// <summary>
/// Describes the characteristics of a compute workload for optimization decisions.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 6: Query Optimization.
/// Used by adaptive backend selector and optimization engine.
/// </remarks>
public class WorkloadCharacteristics
{
    /// <summary>
    /// Gets the size of the input data in elements.
    /// </summary>
    public int DataSize { get; init; }

    /// <summary>
    /// Gets the computational intensity of the workload.
    /// </summary>
    public ComputeIntensity Intensity { get; init; }

    /// <summary>
    /// Gets the primary operation type in the workload.
    /// </summary>
    public OperationType PrimaryOperation { get; init; }

    /// <summary>
    /// Gets the total number of operations in the workload.
    /// </summary>
    public int OperationCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workload is memory-bound.
    /// </summary>
    public bool IsMemoryBound { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workload requires inter-element communication.
    /// </summary>
    public bool RequiresCommunication { get; init; }
}
