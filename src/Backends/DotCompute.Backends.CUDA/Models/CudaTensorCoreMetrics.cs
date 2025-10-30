// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// Metrics for tensor core functionality.
    /// </summary>
    public sealed class CudaTensorCoreMetrics
    {
        /// <summary>
        /// Gets or sets the efficiency score (0.0 to 1.0).
        /// </summary>
        public double EfficiencyScore { get; set; }

        /// <summary>
        /// Gets or sets the utilization percentage (0.0 to 1.0).
        /// </summary>
        public double Utilization { get; set; }

        /// <summary>
        /// Gets or sets the throughput in TFLOPS.
        /// </summary>
        public double ThroughputTFLOPS { get; set; }
    }
}
