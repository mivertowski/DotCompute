using DotCompute.Backends.CUDA.Advanced.Types;

namespace DotCompute.Backends.CUDA.Advanced.Configuration
{
    /// <summary>
    /// Configuration options for tensor core operations.
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
        public DataType PreferredDataType { get; set; } = DataType.FP16;

        /// <summary>
        /// Gets or sets whether to enable sparsity acceleration.
        /// </summary>
        public bool EnableSparsity { get; set; } = false;

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
        public bool EnableProfiling { get; set; } = false;

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
            PreferredDataType = DataType.FP16,
            EnableSparsity = true,
            UseTransformerEngine = true
        };

        /// <summary>
        /// Gets a configuration optimized for HPC workloads.
        /// </summary>
        public static TensorCoreConfiguration ForHPC => new()
        {
            EnableAutoMixedPrecision = false,
            PreferredDataType = DataType.FP64,
            EnableSparsity = false,
            UseTransformerEngine = false
        };
    }
}