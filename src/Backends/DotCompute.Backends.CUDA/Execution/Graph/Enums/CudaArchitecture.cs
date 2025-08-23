// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Enums
{
    /// <summary>
    /// Specifies the target NVIDIA GPU architecture for optimization.
    /// Enables architecture-specific optimizations for maximum performance.
    /// </summary>
    /// <remarks>
    /// Each GPU architecture has unique features and capabilities that can be leveraged
    /// for performance optimization. Specifying the correct architecture enables the
    /// graph compiler to apply the most effective optimization strategies.
    /// </remarks>
    public enum CudaArchitecture
    {
        /// <summary>
        /// NVIDIA Turing architecture (RTX 20-series, GTX 16-series).
        /// </summary>
        /// <remarks>
        /// Introduced RT cores for ray tracing and first-generation tensor cores.
        /// Optimizations focus on mixed precision computation and improved memory bandwidth.
        /// </remarks>
        Turing,

        /// <summary>
        /// NVIDIA Ampere architecture (RTX 30-series, A100).
        /// </summary>
        /// <remarks>
        /// Features second-generation tensor cores, improved RT cores, and enhanced
        /// memory hierarchy. Optimizations leverage sparsity support and improved
        /// tensor core utilization.
        /// </remarks>
        Ampere,

        /// <summary>
        /// NVIDIA Ada Lovelace architecture (RTX 40-series).
        /// </summary>
        /// <remarks>
        /// Latest consumer architecture featuring third-generation tensor cores,
        /// enhanced RT cores, and advanced memory optimization. Default target
        /// for RTX 2000 Ada professional graphics cards.
        /// </remarks>
        Ada,

        /// <summary>
        /// NVIDIA Hopper architecture (H100, professional compute).
        /// </summary>
        /// <remarks>
        /// Data center architecture with advanced tensor cores, enhanced memory
        /// bandwidth, and specialized compute capabilities for AI workloads.
        /// </remarks>
        Hopper
    }
}