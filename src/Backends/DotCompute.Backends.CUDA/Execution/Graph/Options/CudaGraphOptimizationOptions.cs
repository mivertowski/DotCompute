// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Options
{
    /// <summary>
    /// Configures optimization settings for CUDA graph compilation and execution.
    /// Provides fine-grained control over graph optimization strategies targeting RTX 2000 Ada architecture.
    /// </summary>
    /// <remarks>
    /// These options control various optimization passes including kernel fusion, memory access optimization,
    /// and architecture-specific optimizations for maximum performance on modern GPU architectures.
    /// </remarks>
    public sealed class CudaGraphOptimizationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether graph optimization should be enabled.
        /// </summary>
        /// <value><c>true</c> to enable graph optimization passes; otherwise, <c>false</c>. Default is <c>true</c>.</value>
        /// <remarks>
        /// When enabled, the graph will undergo various optimization passes to improve execution performance.
        /// Disabling optimization may be useful for debugging or when deterministic behavior is required.
        /// </remarks>
        public bool EnableOptimization { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether kernel fusion should be attempted.
        /// </summary>
        /// <value><c>true</c> to enable kernel fusion optimization; otherwise, <c>false</c>. Default is <c>true</c>.</value>
        /// <remarks>
        /// Kernel fusion combines multiple compatible kernels into a single kernel to reduce
        /// memory bandwidth requirements and kernel launch overhead.
        /// </remarks>
        public bool EnableKernelFusion { get; set; } = true;

        /// <summary>
        /// Gets or sets the optimization level to apply to the graph.
        /// </summary>
        /// <value>A <see cref="CudaGraphOptimizationLevel"/> value specifying the optimization intensity. Default is <see cref="CudaGraphOptimizationLevel.Balanced"/>.</value>
        /// <remarks>
        /// Higher optimization levels may provide better performance but increase compilation time.
        /// Choose the level based on your performance requirements and compilation time constraints.
        /// </remarks>
        public CudaGraphOptimizationLevel OptimizationLevel { get; set; } = CudaGraphOptimizationLevel.Balanced;

        /// <summary>
        /// Gets or sets the target GPU architecture for optimization.
        /// </summary>
        /// <value>A <see cref="CudaArchitecture"/> value specifying the target architecture. Default is <see cref="CudaArchitecture.Ada"/>.</value>
        /// <remarks>
        /// Architecture-specific optimizations can significantly improve performance by leveraging
        /// features unique to specific GPU generations, such as tensor cores and memory hierarchies.
        /// </remarks>
        public CudaArchitecture TargetArchitecture { get; set; } = CudaArchitecture.Ada;
    }
}