// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Nodes
{
    /// <summary>
    /// Represents a pair of kernels that are candidates for fusion optimization.
    /// Contains the analysis results for potential kernel fusion including estimated performance benefits.
    /// </summary>
    /// <remarks>
    /// Kernel fusion combines multiple compatible kernels into a single kernel to reduce
    /// memory bandwidth requirements and kernel launch overhead. This class provides the
    /// analysis framework for identifying and evaluating fusion opportunities.
    /// </remarks>
    public sealed class KernelFusionCandidate
    {
        /// <summary>
        /// Gets or sets the first kernel in the fusion candidate pair.
        /// </summary>
        /// <value>A <see cref="CudaKernelOperation"/> representing the first kernel to be fused.</value>
        /// <remarks>
        /// This kernel typically produces output that is consumed by the second kernel,
        /// making them suitable candidates for fusion optimization.
        /// </remarks>
        public CudaKernelOperation FirstKernel { get; set; } = null!;

        /// <summary>
        /// Gets or sets the second kernel in the fusion candidate pair.
        /// </summary>
        /// <value>A <see cref="CudaKernelOperation"/> representing the second kernel to be fused.</value>
        /// <remarks>
        /// This kernel typically consumes the output of the first kernel, creating a
        /// data dependency that can benefit from fusion optimization.
        /// </remarks>
        public CudaKernelOperation SecondKernel { get; set; } = null!;

        /// <summary>
        /// Gets or sets the estimated performance benefit of fusing these kernels.
        /// </summary>
        /// <value>A double representing the estimated performance improvement as a ratio (e.g., 0.2 = 20% improvement).</value>
        /// <remarks>
        /// This value is calculated based on factors such as:
        /// - Memory bandwidth savings from eliminating intermediate storage
        /// - Reduced kernel launch overhead
        /// - Improved data locality and cache utilization
        /// - Register usage and occupancy considerations
        /// </remarks>
        public double EstimatedBenefit { get; set; }
    }
}