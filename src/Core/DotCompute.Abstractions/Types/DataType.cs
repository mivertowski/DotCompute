namespace DotCompute.Backends.CUDA.Advanced.Types
{
    /// <summary>
    /// Defines data types supported by tensor core operations.
    /// </summary>
    public enum DataType
    {
        /// <summary>
        /// 8-bit floating point with E4M3 format.
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores - Required for CUDA/GPU naming convention
        FP8_E4M3,
#pragma warning restore CA1707


        /// <summary>
        /// 8-bit floating point with E5M2 format.
        /// </summary>
#pragma warning disable CA1707 // Identifiers should not contain underscores - Required for CUDA/GPU naming convention
        FP8_E5M2,
#pragma warning restore CA1707

        /// <summary>
        /// 16-bit floating point (half precision).
        /// </summary>
        FP16,

        /// <summary>
        /// 16-bit brain floating point.
        /// </summary>
        BF16,

        /// <summary>
        /// TensorFloat-32 format.
        /// </summary>
        TF32,

        /// <summary>
        /// 32-bit floating point (single precision).
        /// </summary>
        FP32,

        /// <summary>
        /// 64-bit floating point (double precision).
        /// </summary>
        FP64,


        /// <summary>
        /// 8-bit signed integer.
        /// </summary>
#pragma warning disable CA1720 // Identifier contains type name - Required for CUDA/GPU naming convention
        INT8,
#pragma warning restore CA1720

        /// <summary>
        /// 4-bit integer.
        /// </summary>
        INT4,

        /// <summary>
        /// 1-bit binary.
        /// </summary>
        INT1
    }
}
