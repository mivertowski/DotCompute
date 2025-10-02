// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Options for CUDA graph optimization.
    /// </summary>
    public sealed class CudaGraphOptimizationOptions
    {
        /// <summary>
        /// Gets or sets the enable fusion.
        /// </summary>
        /// <value>The enable fusion.</value>
        public bool EnableFusion { get; set; } = true;
        /// <summary>
        /// Gets or sets the enable coalescing.
        /// </summary>
        /// <value>The enable coalescing.</value>
        public bool EnableCoalescing { get; set; } = true;
        /// <summary>
        /// Gets or sets the enable pipelining.
        /// </summary>
        /// <value>The enable pipelining.</value>
        public bool EnablePipelining { get; set; } = true;
        /// <summary>
        /// Gets or sets the max nodes per graph.
        /// </summary>
        /// <value>The max nodes per graph.</value>
        public int MaxNodesPerGraph { get; set; } = 1000;
        /// <summary>
        /// Gets or sets the use instantiated graphs.
        /// </summary>
        /// <value>The use instantiated graphs.</value>
        public bool UseInstantiatedGraphs { get; set; } = true;
        /// <summary>
        /// Gets or sets the enable optimization.
        /// </summary>
        /// <value>The enable optimization.</value>
        public bool EnableOptimization { get; set; } = true;
        /// <summary>
        /// Gets or sets the target architecture.
        /// </summary>
        /// <value>The target architecture.</value>
        public CudaArchitecture TargetArchitecture { get; set; } = CudaArchitecture.Ada;
        /// <summary>
        /// Gets or sets the enable kernel fusion.
        /// </summary>
        /// <value>The enable kernel fusion.</value>
        public bool EnableKernelFusion { get; set; } = true;
        /// <summary>
        /// Gets or sets the optimization level.
        /// </summary>
        /// <value>The optimization level.</value>
        public CudaGraphOptimizationLevel OptimizationLevel { get; set; } = CudaGraphOptimizationLevel.Balanced;
    }
}