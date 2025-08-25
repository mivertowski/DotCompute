// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;
using DotCompute.Abstractions;

namespace DotCompute.Core.Execution.Workload
{
    /// <summary>
    /// Data parallel workload specification for operations that can be executed
    /// independently across multiple data elements.
    /// </summary>
    /// <typeparam name="T">The unmanaged element type for the workload data</typeparam>
    public sealed class DataParallelWorkload<T> : ExecutionWorkload<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or initializes the name of the kernel to be executed.
        /// </summary>
        public required string KernelName { get; init; }

        /// <summary>
        /// Gets or initializes the input buffers for the data parallel operation.
        /// </summary>
        public required IUnifiedMemoryBuffer<T>[] InputBuffers { get; init; }

        /// <summary>
        /// Gets or initializes the output buffers for the data parallel operation.
        /// </summary>
        public required IUnifiedMemoryBuffer<T>[] OutputBuffers { get; init; }

        /// <summary>
        /// Gets the workload type.
        /// </summary>
        public override WorkloadType WorkloadType => WorkloadType.DataParallel;

        /// <summary>
        /// Calculates the total data size including both input and output buffers.
        /// </summary>
        /// <returns>The total data size in bytes</returns>
        public override long GetTotalDataSize()
            => InputBuffers.Sum(b => b.SizeInBytes) + OutputBuffers.Sum(b => b.SizeInBytes);

        /// <summary>
        /// Gets the compute intensity for data parallel operations.
        /// Data parallel workloads typically have moderate compute intensity.
        /// </summary>
        /// <returns>The compute intensity (0.5)</returns>
        public override double GetComputeIntensity() => 0.5; // Default moderate compute intensity

        /// <summary>
        /// Gets the memory intensity for data parallel operations.
        /// Data parallel workloads typically have moderate memory intensity.
        /// </summary>
        /// <returns>The memory intensity (0.3)</returns>
        public override double GetMemoryIntensity() => 0.3; // Default moderate memory intensity

        /// <summary>
        /// Gets the parallelization potential for data parallel operations.
        /// Data parallel workloads have high parallelization potential by definition.
        /// </summary>
        /// <returns>The parallelization potential (0.9)</returns>
        public override double GetParallelizationPotential() => 0.9; // High parallelization potential

        /// <summary>
        /// Gets the dependency complexity for data parallel operations.
        /// Data parallel workloads typically have low dependency complexity.
        /// </summary>
        /// <returns>The dependency complexity (0.1)</returns>
        public override double GetDependencyComplexity() => 0.1; // Low dependency complexity
    }
}