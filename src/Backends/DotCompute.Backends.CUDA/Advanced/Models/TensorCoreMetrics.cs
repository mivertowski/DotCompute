namespace DotCompute.Backends.CUDA.Advanced.Models
{
    /// <summary>
    /// Performance metrics for tensor core operations.
    /// </summary>
    public class TensorCoreMetrics
    {
        /// <summary>
        /// Gets or sets the number of tensor core operations performed.
        /// </summary>
        public long OperationCount { get; set; }

        /// <summary>
        /// Gets or sets the memory bandwidth utilized in GB/s.
        /// </summary>
        public double MemoryBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the tensor core utilization percentage.
        /// </summary>
        public double TensorCoreUtilization { get; set; }

        /// <summary>
        /// Gets or sets the number of warps that used tensor cores.
        /// </summary>
        public int ActiveTensorWarps { get; set; }

        /// <summary>
        /// Gets or sets whether sparsity acceleration was used.
        /// </summary>
        public bool SparsityEnabled { get; set; }

        /// <summary>
        /// Gets or sets the sparsity ratio if sparsity was enabled.
        /// </summary>
        public double SparsityRatio { get; set; }
    }
}