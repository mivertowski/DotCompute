using System;

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Represents the result of a tensor core operation.
    /// </summary>
    public class TensorCoreResult
    {
        /// <summary>
        /// Gets or sets whether the operation completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the execution time for the operation.
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the theoretical TFLOPS achieved.
        /// </summary>
        public double TFlops { get; set; }

        /// <summary>
        /// Gets or sets the percentage of peak performance achieved.
        /// </summary>
        public double EfficiencyPercentage { get; set; }

        /// <summary>
        /// Gets or sets the output data pointer.
        /// </summary>
        public IntPtr OutputData { get; set; }

        /// <summary>
        /// Gets or sets any error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets performance metrics for the operation.
        /// </summary>
        public TensorCoreMetrics? Metrics { get; set; }
    }
}