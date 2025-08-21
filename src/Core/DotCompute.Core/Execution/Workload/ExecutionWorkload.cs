// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution.Workload
{
    /// <summary>
    /// Base class for different types of execution workloads.
    /// Provides a common interface for workload analysis and optimization.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type for the workload data</typeparam>
    public abstract class ExecutionWorkload<T> where T : unmanaged
    {
        /// <summary>
        /// Gets the type of workload represented by this instance.
        /// </summary>
        public abstract WorkloadType WorkloadType { get; }

        /// <summary>
        /// Calculates the total data size in bytes for this workload.
        /// </summary>
        /// <returns>The total data size in bytes</returns>
        public abstract long GetTotalDataSize();

        /// <summary>
        /// Gets the compute intensity of this workload.
        /// </summary>
        /// <returns>A value between 0.0 and 1.0 representing compute intensity</returns>
        public abstract double GetComputeIntensity();

        /// <summary>
        /// Gets the memory intensity of this workload.
        /// </summary>
        /// <returns>A value between 0.0 and 1.0 representing memory intensity</returns>
        public abstract double GetMemoryIntensity();

        /// <summary>
        /// Gets the parallelization potential of this workload.
        /// </summary>
        /// <returns>A value between 0.0 and 1.0 representing parallelization potential</returns>
        public abstract double GetParallelizationPotential();

        /// <summary>
        /// Gets the dependency complexity of this workload.
        /// </summary>
        /// <returns>A value between 0.0 and 1.0 representing dependency complexity</returns>
        public abstract double GetDependencyComplexity();
    }
}