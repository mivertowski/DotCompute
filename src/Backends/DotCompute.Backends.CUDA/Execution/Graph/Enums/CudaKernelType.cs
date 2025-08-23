// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums
{
    /// <summary>
    /// Classifies CUDA kernels by their computational pattern and optimization characteristics.
    /// Enables type-specific optimizations and fusion strategies.
    /// </summary>
    /// <remarks>
    /// Kernel type classification allows the graph optimizer to apply appropriate
    /// optimization strategies based on the computational characteristics of each kernel.
    /// This improves both individual kernel performance and fusion opportunities.
    /// </remarks>
    public enum CudaKernelType
    {
        /// <summary>
        /// Element-wise operations that process arrays element by element.
        /// </summary>
        /// <remarks>
        /// These kernels typically have simple memory access patterns and are excellent
        /// candidates for kernel fusion. Examples include vector addition, scaling,
        /// and element-wise mathematical functions. Often memory-bandwidth bound.
        /// </remarks>
        ElementWise,

        /// <summary>
        /// Matrix multiplication and related linear algebra operations.
        /// </summary>
        /// <remarks>
        /// Computationally intensive kernels that can benefit significantly from
        /// tensor core acceleration on modern GPU architectures. These kernels
        /// typically have high arithmetic intensity and complex memory access patterns.
        /// </remarks>
        MatrixMultiply,

        /// <summary>
        /// Reduction operations that aggregate data across multiple elements.
        /// </summary>
        /// <remarks>
        /// These kernels reduce large datasets to smaller results through operations
        /// like sum, max, min, or average. They require careful optimization for
        /// memory access patterns and inter-thread communication via shared memory.
        /// </remarks>
        Reduction,

        /// <summary>
        /// Matrix transpose and data layout transformation operations.
        /// </summary>
        /// <remarks>
        /// Memory-intensive operations that benefit from coalesced memory access
        /// optimization and shared memory tiling strategies. Critical for many
        /// linear algebra and signal processing applications.
        /// </remarks>
        Transpose,

        /// <summary>
        /// Custom kernel implementations with application-specific logic.
        /// </summary>
        /// <remarks>
        /// User-defined kernels that don't fit standard patterns. These kernels
        /// receive general optimizations but may not benefit from type-specific
        /// optimization strategies applied to other kernel types.
        /// </remarks>
        Custom,

        /// <summary>
        /// Fused kernel created by combining multiple compatible kernels.
        /// </summary>
        /// <remarks>
        /// These kernels are created by the graph optimizer through kernel fusion
        /// to reduce memory bandwidth requirements and kernel launch overhead.
        /// They represent optimized versions of multiple original kernel operations.
        /// </remarks>
        Fused
    }
}