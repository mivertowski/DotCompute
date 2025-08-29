// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Execution.Metrics
{
    /// <summary>
    /// Base class for CUDA execution metrics.
    /// </summary>
    public abstract class CudaExecutionMetrics
    {
        /// <summary>
        /// Gets the timestamp when the metrics were captured.
        /// </summary>
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets the total number of operations executed.
        /// </summary>
        public long OperationCount { get; init; }

        /// <summary>
        /// Gets the total execution time in milliseconds.
        /// </summary>
        public double TotalExecutionTimeMs { get; init; }

        /// <summary>
        /// Gets the average execution time per operation in milliseconds.
        /// </summary>
        public double AverageExecutionTimeMs => OperationCount > 0 ? TotalExecutionTimeMs / OperationCount : 0;

        /// <summary>
        /// Gets the number of errors encountered during execution.
        /// </summary>
        public long ErrorCount { get; init; }

        /// <summary>
        /// Gets the success rate as a percentage.
        /// </summary>
        public double SuccessRate => OperationCount > 0 ? ((double)(OperationCount - ErrorCount) / OperationCount) * 100 : 100;
    }

    /// <summary>
    /// Performance metrics for CUDA cooperative groups operations.
    /// </summary>
    public sealed class CudaCooperativeGroupsMetrics : CudaExecutionMetrics
    {
        /// <summary>
        /// Gets the number of cooperative launches executed.
        /// </summary>
        public long CooperativeLaunches { get; init; }

        /// <summary>
        /// Gets the total number of threads in cooperative groups.
        /// </summary>
        public long TotalThreads { get; init; }

        /// <summary>
        /// Gets the average group size.
        /// </summary>
        public double AverageGroupSize => CooperativeLaunches > 0 ? (double)TotalThreads / CooperativeLaunches : 0;

        /// <summary>
        /// Gets the number of synchronization points hit.
        /// </summary>
        public long SynchronizationPoints { get; init; }

        /// <summary>
        /// Gets the memory bandwidth utilization percentage.
        /// </summary>
        public double MemoryBandwidthUtilization { get; init; }

        /// <summary>
        /// Gets the compute utilization percentage.
        /// </summary>
        public double ComputeUtilization { get; init; }

        /// <summary>
        /// Gets or sets the number of active groups currently executing.
        /// </summary>
        public long ActiveGroups { get; set; }
    }

    /// <summary>
    /// Performance metrics for CUDA dynamic parallelism operations.
    /// </summary>
    public sealed class CudaDynamicParallelismMetrics : CudaExecutionMetrics
    {
        /// <summary>
        /// Gets the number of device-side kernel launches.
        /// </summary>
        public long DeviceKernelLaunches { get; init; }

        /// <summary>
        /// Gets the maximum nesting depth achieved.
        /// </summary>
        public int MaxNestingDepth { get; init; }

        /// <summary>
        /// Gets the average nesting depth.
        /// </summary>
        public double AverageNestingDepth { get; init; }

        /// <summary>
        /// Gets the number of runtime API calls from device.
        /// </summary>
        public long DeviceRuntimeCalls { get; init; }

        /// <summary>
        /// Gets the memory allocation overhead in bytes.
        /// </summary>
        public long MemoryAllocationOverhead { get; init; }

        /// <summary>
        /// Gets the launch overhead in milliseconds.
        /// </summary>
        public double LaunchOverheadMs { get; init; }

        /// <summary>
        /// Gets the synchronization overhead in milliseconds.
        /// </summary>
        public double SynchronizationOverheadMs { get; init; }

        /// <summary>
        /// Gets the number of child kernel launches from device.
        /// </summary>
        public long ChildKernelLaunches { get; init; }

        /// <summary>
        /// Gets the efficiency score as a percentage (0-100).
        /// </summary>
        public double EfficiencyScore { get; init; }
    }

    /// <summary>
    /// Performance metrics for CUDA Tensor Core operations.
    /// </summary>
    public sealed class CudaTensorCoreMetrics : CudaExecutionMetrics
    {
        /// <summary>
        /// Gets the number of Tensor Core operations executed.
        /// </summary>
        public long TensorCoreOps { get; init; }

        /// <summary>
        /// Gets the achieved throughput in TOPS (Tera Operations Per Second).
        /// </summary>
        public double ThroughputTOPS { get; init; }

        /// <summary>
        /// Gets the utilization percentage of Tensor Cores.
        /// </summary>
        public double TensorCoreUtilization { get; init; }

        /// <summary>
        /// Gets the mixed precision operation count.
        /// </summary>
        public long MixedPrecisionOps { get; init; }

        /// <summary>
        /// Gets the FP16 operation count.
        /// </summary>
        public long Fp16Ops { get; init; }

        /// <summary>
        /// Gets the INT8 operation count.
        /// </summary>
        public long Int8Ops { get; init; }

        /// <summary>
        /// Gets the BF16 operation count.
        /// </summary>
        public long Bf16Ops { get; init; }

        /// <summary>
        /// Gets the sparsity utilization percentage.
        /// </summary>
        public double SparsityUtilization { get; init; }

        /// <summary>
        /// Gets the memory bandwidth utilization for Tensor operations.
        /// </summary>
        public double MemoryBandwidthUtilization { get; init; }
    }
}