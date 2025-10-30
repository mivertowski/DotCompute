// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Pipeline;
using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Workload
{
    /// <summary>
    /// Pipeline workload specification for streaming execution patterns
    /// where data flows through multiple processing stages.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type for the workload data</typeparam>
    public sealed class PipelineWorkload<T> : ExecutionWorkload<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or initializes the pipeline definition that describes the stages and data flow.
        /// </summary>
        public required PipelineDefinition<T> PipelineDefinition { get; init; }

        /// <summary>
        /// Gets the workload type.
        /// </summary>
        public override WorkloadType WorkloadType => WorkloadType.Pipeline;

        /// <summary>
        /// Calculates the total data size based on the pipeline input specification.
        /// </summary>
        /// <returns>The total data size in bytes</returns>
        public override long GetTotalDataSize()
            => PipelineDefinition.InputSpec.Tensors.Sum(t => t.SizeInBytes);

        /// <summary>
        /// Gets the compute intensity for pipeline operations.
        /// Pipeline workloads are often compute-intensive due to multi-stage processing.
        /// </summary>
        /// <returns>The compute intensity (0.6)</returns>
        public override double GetComputeIntensity() => 0.6; // Pipelines often compute-intensive

        /// <summary>
        /// Gets the memory intensity for pipeline operations.
        /// Pipeline workloads have moderate memory intensity.
        /// </summary>
        /// <returns>The memory intensity (0.4)</returns>
        public override double GetMemoryIntensity() => 0.4; // Moderate memory intensity

        /// <summary>
        /// Gets the parallelization potential for pipeline operations.
        /// Pipeline workloads have high parallelization potential across stages.
        /// </summary>
        /// <returns>The parallelization potential (0.8)</returns>
        public override double GetParallelizationPotential() => 0.8; // High pipeline parallelization

        /// <summary>
        /// Gets the dependency complexity for pipeline operations.
        /// Pipeline workloads have moderate dependency complexity due to stage dependencies.
        /// </summary>
        /// <returns>The dependency complexity (0.6)</returns>
        public override double GetDependencyComplexity() => 0.6; // Moderate dependency complexity
    }
}
