// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Configuration
{
    /// <summary>
    /// Configures tensor core usage for matrix operations in CUDA kernels.
    /// Provides settings for optimizing tensor core performance on RTX 2000 Ada architecture.
    /// </summary>
    /// <remarks>
    /// Tensor cores are specialized matrix processing units available on modern NVIDIA GPUs
    /// that can dramatically accelerate matrix multiplication operations when properly configured.
    /// This configuration class allows fine-tuning of tensor core behavior for optimal performance.
    /// </remarks>
    public sealed class TensorCoreConfig
    {
        /// <summary>
        /// Gets or sets the data type to use for tensor core operations.
        /// </summary>
        /// <value>A string representing the tensor core data type. Default is "TF32".</value>
        /// <remarks>
        /// Common values include:
        /// - "TF32": TensorFloat-32 format for high performance with good precision
        /// - "FP16": Half precision for maximum throughput
        /// - "BF16": Brain floating point for better numerical stability
        /// - "INT8": Integer format for inference workloads
        /// </remarks>
        public string DataType { get; set; } = "TF32";

        /// <summary>
        /// Gets or sets the precision level for tensor core computations.
        /// </summary>
        /// <value>A string representing the precision level. Default is "High".</value>
        /// <remarks>
        /// Precision levels:
        /// - "High": Maximum precision with potential performance trade-off
        /// - "Medium": Balanced precision and performance
        /// - "Fast": Maximum performance with reduced precision
        /// The actual behavior depends on the GPU architecture and CUDA runtime version.
        /// </remarks>
        public string Precision { get; set; } = "High";
    }
}