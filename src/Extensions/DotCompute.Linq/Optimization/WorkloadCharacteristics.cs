using System;
using DotCompute.Abstractions.Types;

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
    public long DataSize { get; init; }

    /// <summary>
    /// Gets the computational intensity of the workload.
    /// </summary>
    public ComputeIntensity ComputeIntensity { get; init; }

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

    /// <summary>
    /// Gets a value indicating whether the data is already on the compute device.
    /// </summary>
    public bool IsDataAlreadyOnDevice { get; init; }

    /// <summary>
    /// Gets a value indicating whether the result will be consumed on the device.
    /// </summary>
    public bool IsResultConsumedOnDevice { get; init; }

    /// <summary>
    /// Gets the degree of parallelism available in the workload.
    /// </summary>
    public int ParallelismDegree { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workload can be fused with other operations.
    /// </summary>
    public bool IsFusible { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workload has random memory access patterns.
    /// </summary>
    public bool HasRandomAccess { get; init; }

    /// <summary>
    /// Gets the memory access pattern of the workload.
    /// </summary>
    public required MemoryAccessPattern MemoryPattern { get; init; }
}
