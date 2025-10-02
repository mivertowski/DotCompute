// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Models
{
    /// <summary>
    /// Result of profiling a CUDA operation with statistical analysis.
    /// </summary>
    public sealed class CudaProfilingResult
    {
        /// <summary>
        /// Gets or sets the unique session identifier.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the name of the operation that was profiled.
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stream the operation was executed on.
        /// </summary>
        public IntPtr Stream { get; set; }

        /// <summary>
        /// Gets or sets the planned number of iterations.
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// Gets or sets the actual number of iterations completed.
        /// </summary>
        public int ActualIterations { get; set; }

        /// <summary>
        /// Gets or sets the average GPU execution time in milliseconds.
        /// </summary>
        public double AverageGpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum GPU execution time in milliseconds.
        /// </summary>
        public double MinGpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum GPU execution time in milliseconds.
        /// </summary>
        public double MaxGpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the median GPU execution time in milliseconds.
        /// </summary>
        public double MedianGpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the standard deviation of GPU execution times in milliseconds.
        /// </summary>
        public double StdDevGpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average CPU time including overhead in milliseconds.
        /// </summary>
        public double AverageCpuTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the average overhead (CPU - GPU) in milliseconds.
        /// </summary>
        public double AverageOverheadMs { get; set; }

        /// <summary>
        /// Gets or sets the percentile values for GPU execution times.
        /// </summary>
        public Dictionary<int, double> Percentiles { get; } = [];

        /// <summary>
        /// Gets or sets the coefficient of variation (relative standard deviation).
        /// </summary>
        public double CoefficientOfVariation { get; set; }

        /// <summary>
        /// Gets or sets the number of outlier measurements.
        /// </summary>
        public int OutlierCount { get; set; }

        /// <summary>
        /// Gets or sets the throughput in operations per second.
        /// </summary>
        public double ThroughputOpsPerSecond { get; set; }

        /// <summary>
        /// Gets or sets the total session duration in milliseconds.
        /// </summary>
        public double SessionDurationMs { get; set; }
    }
}