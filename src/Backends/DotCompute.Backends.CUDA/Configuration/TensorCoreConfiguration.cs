using DotCompute.Backends.CUDA.Types;

namespace DotCompute.Backends.CUDA.Configuration
{
    /// <summary>
    /// Comprehensive configuration for Tensor Core operations on NVIDIA GPUs.
    /// Tensor Cores provide specialized hardware acceleration for matrix operations.
    /// </summary>
    public class TensorCoreConfiguration
    {
        /// <summary>
        /// Gets or sets whether to enable automatic mixed precision.
        /// </summary>
        public bool EnableAutoMixedPrecision { get; set; } = true;

        /// <summary>
        /// Gets or sets the preferred data type for computations.
        /// </summary>
        public DataType PreferredDataType { get; set; } = Types.DataType.Float16;

        /// <summary>
        /// Gets or sets the data type string for Tensor Core operations.
        /// Supported types include TF32, FP16, BF16, INT8, and FP8.
        /// </summary>
        public string DataType { get; set; } = "TF32";

        /// <summary>
        /// Gets or sets the precision mode for Tensor Core operations.
        /// Options include "High" for maximum precision, "Medium" for balanced performance,
        /// and "Fast" for maximum throughput with reduced precision.
        /// </summary>
        public string Precision { get; set; } = "High";

        /// <summary>
        /// Gets or sets whether to enable sparsity acceleration.
        /// </summary>
        public bool EnableSparsity { get; set; }


        /// <summary>
        /// Gets or sets the minimum matrix size to use tensor cores.
        /// </summary>
        public int MinMatrixSize { get; set; } = 16;

        /// <summary>
        /// Gets or sets whether to use the Transformer Engine on Hopper.
        /// </summary>
        public bool UseTransformerEngine { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable performance profiling.
        /// </summary>
        public bool EnableProfiling { get; set; }


        /// <summary>
        /// Gets a default configuration optimized for general use.
        /// </summary>
        public static TensorCoreConfiguration Default => new();

        /// <summary>
        /// Gets a configuration optimized for AI/ML workloads.
        /// </summary>
        public static TensorCoreConfiguration ForAI => new()
        {
            EnableAutoMixedPrecision = true,
            PreferredDataType = Types.DataType.Float16,
            DataType = "FP16",
            Precision = "Fast",
            EnableSparsity = true,
            UseTransformerEngine = true
        };

        /// <summary>
        /// Gets a configuration optimized for HPC workloads.
        /// </summary>
        public static TensorCoreConfiguration ForHPC => new()
        {
            EnableAutoMixedPrecision = false,
            PreferredDataType = Types.DataType.Float64,
            DataType = "FP64",
            Precision = "High",
            EnableSparsity = false,
            UseTransformerEngine = false
        };
    }
}