// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Analysis
{
    /// <summary>
    /// Contains analysis results for a workload, providing metrics that help
    /// optimize execution planning and resource allocation.
    /// </summary>
    public sealed class WorkloadAnalysis
    {
        /// <summary>
        /// Gets or initializes the type of workload being analyzed.
        /// </summary>
        public WorkloadType WorkloadType { get; init; }

        /// <summary>
        /// Gets or initializes the total data size in bytes for the workload.
        /// This includes input data, output data, and any intermediate results.
        /// </summary>
        public long DataSize { get; init; }

        /// <summary>
        /// Gets or initializes the compute intensity of the workload.
        /// A value between 0.0 and 1.0 representing how compute-intensive the workload is.
        /// </summary>
        public double ComputeIntensity { get; init; }

        /// <summary>
        /// Gets or initializes the memory intensity of the workload.
        /// A value between 0.0 and 1.0 representing how memory-intensive the workload is.
        /// </summary>
        public double MemoryIntensity { get; init; }

        /// <summary>
        /// Gets or initializes the parallelization potential of the workload.
        /// A value between 0.0 and 1.0 representing how well the workload can be parallelized.
        /// </summary>
        public double ParallelizationPotential { get; init; }

        /// <summary>
        /// Gets or initializes the dependency complexity of the workload.
        /// A value between 0.0 and 1.0 representing how complex the dependencies are.
        /// Higher values indicate more complex synchronization requirements.
        /// </summary>
        public double DependencyComplexity { get; init; }
    }
}
