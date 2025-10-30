namespace DotCompute.Backends.CUDA.Advanced.Types
{
    /// <summary>
    /// Defines NVIDIA GPU architectures with tensor core support.
    /// </summary>
    public enum TensorCoreArchitecture
    {
        /// <summary>
        /// No tensor core support available.
        /// </summary>
        None = 0,

        /// <summary>
        /// Volta architecture (SM 7.0) - First generation tensor cores.
        /// Supports FP16 accumulation to FP16/FP32.
        /// </summary>
        Volta = 70,

        /// <summary>
        /// Turing architecture (SM 7.5) - Enhanced tensor cores.
        /// Adds INT8/INT4 support.
        /// </summary>
        Turing = 75,

        /// <summary>
        /// Ampere architecture (SM 8.0/8.6) - Third generation tensor cores.
        /// Adds BF16, TF32, and sparsity support.
        /// </summary>
        Ampere = 80,

        /// <summary>
        /// Ada Lovelace architecture (SM 8.9) - Fourth generation tensor cores.
        /// Enhanced FP8 support and improved throughput.
        /// </summary>
        Ada = 89,

        /// <summary>
        /// Hopper architecture (SM 9.0) - Fifth generation tensor cores.
        /// Transformer Engine with FP8, improved sparsity.
        /// </summary>
        Hopper = 90
    }
}
