// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Optimization;

namespace DotCompute.Linq.CodeGeneration;

/// <summary>
/// Describes the characteristics of a computational workload for backend selection.
/// </summary>
public sealed class WorkloadCharacteristics
{
    /// <summary>
    /// Gets or sets the total number of elements to process.
    /// Larger datasets (>1M elements) benefit more from GPU acceleration.
    /// </summary>
    public int DataSize { get; set; }

    /// <summary>
    /// Gets or sets the computational intensity of the operation.
    /// Higher intensity favors GPU execution.
    /// </summary>
    public ComputeIntensity Intensity { get; set; }

    /// <summary>
    /// Gets or sets whether multiple operations can be fused into a single kernel.
    /// Fusion reduces memory transfers and improves GPU efficiency.
    /// </summary>
    public bool IsFusible { get; set; }

    /// <summary>
    /// Gets or sets the primary operation type being performed.
    /// Some operations (e.g., reductions, sorts) benefit more from GPU.
    /// </summary>
    public OperationType PrimaryOperation { get; set; }

    /// <summary>
    /// Gets or sets the estimated operations per element.
    /// Higher values increase compute-to-memory ratio, favoring GPU.
    /// </summary>
    public int OperationsPerElement { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the operation has high memory bandwidth requirements.
    /// Memory-bound operations may not benefit from GPU if bandwidth is saturated.
    /// </summary>
    public bool IsMemoryBound { get; set; }

    /// <summary>
    /// Gets or sets whether the operation requires random memory access patterns.
    /// Random access reduces GPU cache efficiency.
    /// </summary>
    public bool HasRandomAccess { get; set; }

    /// <summary>
    /// Gets or sets the degree of parallelism achievable.
    /// Higher parallelism (>1000 threads) benefits from GPU execution.
    /// </summary>
    public int ParallelismDegree { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the data is already resident on the GPU.
    /// Eliminates H2D transfer overhead if true.
    /// </summary>
    public bool IsDataAlreadyOnDevice { get; set; }

    /// <summary>
    /// Gets or sets whether the result will be consumed on the GPU.
    /// Eliminates D2H transfer overhead if true.
    /// </summary>
    public bool IsResultConsumedOnDevice { get; set; }

    /// <summary>
    /// Creates workload characteristics for a simple map operation.
    /// </summary>
    public static WorkloadCharacteristics CreateMapWorkload(int dataSize, ComputeIntensity intensity = ComputeIntensity.Medium)
    {
        return new WorkloadCharacteristics
        {
            DataSize = dataSize,
            Intensity = intensity,
            IsFusible = true,
            PrimaryOperation = OperationType.Map,
            OperationsPerElement = 1,
            IsMemoryBound = intensity == ComputeIntensity.Low,
            HasRandomAccess = false,
            ParallelismDegree = dataSize
        };
    }

    /// <summary>
    /// Creates workload characteristics for a reduction operation.
    /// </summary>
    public static WorkloadCharacteristics CreateReduceWorkload(int dataSize, ComputeIntensity intensity = ComputeIntensity.Medium)
    {
        return new WorkloadCharacteristics
        {
            DataSize = dataSize,
            Intensity = intensity,
            IsFusible = false, // Reductions typically don't fuse well
            PrimaryOperation = OperationType.Reduce,
            OperationsPerElement = (int)Math.Log2(dataSize), // Tree reduction depth
            IsMemoryBound = false,
            HasRandomAccess = false,
            ParallelismDegree = dataSize / 2 // Parallel tree reduction
        };
    }

    /// <summary>
    /// Creates workload characteristics for a filter operation.
    /// </summary>
    public static WorkloadCharacteristics CreateFilterWorkload(int dataSize, double selectivity = 0.5)
    {
        return new WorkloadCharacteristics
        {
            DataSize = dataSize,
            Intensity = ComputeIntensity.Low,
            IsFusible = true,
            PrimaryOperation = OperationType.Filter,
            OperationsPerElement = 1,
            IsMemoryBound = true, // Stream compaction is memory-bound
            HasRandomAccess = false,
            ParallelismDegree = dataSize
        };
    }

    /// <summary>
    /// Estimates the computational cost in arbitrary units for backend selection.
    /// Higher scores indicate more compute-intensive workloads that benefit from GPU.
    /// </summary>
    public double EstimateComputationalCost()
    {
        double baseCost = DataSize * OperationsPerElement;

        // Apply intensity multiplier
        var intensityMultiplier = Intensity switch
        {
            ComputeIntensity.Low => 0.5,
            ComputeIntensity.Medium => 1.0,
            ComputeIntensity.High => 2.0,
            ComputeIntensity.VeryHigh => 4.0,
            _ => 1.0
        };

        // Apply parallelism benefit
        var parallelismFactor = Math.Min(ParallelismDegree / 1000.0, 10.0);

        // Memory-bound workloads don't scale as well
        var memoryPenalty = IsMemoryBound ? 0.6 : 1.0;

        // Random access reduces efficiency
        var accessPenalty = HasRandomAccess ? 0.7 : 1.0;

        return baseCost * intensityMultiplier * parallelismFactor * memoryPenalty * accessPenalty;
    }

    /// <summary>
    /// Returns a string representation of the workload characteristics.
    /// </summary>
    public override string ToString()
    {
        return $"Workload[Size={DataSize}, Intensity={Intensity}, Op={PrimaryOperation}, " +
               $"OpsPerElement={OperationsPerElement}, Fusible={IsFusible}, Cost={EstimateComputationalCost():F2}]";
    }
}
