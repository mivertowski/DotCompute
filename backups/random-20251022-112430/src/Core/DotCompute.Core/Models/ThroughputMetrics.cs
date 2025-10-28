// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Throughput performance metrics for kernel execution.
    /// </summary>
    public sealed class ThroughputMetrics
    {
        /// <summary>
        /// Gets or sets the memory bandwidth in GB/s.
        /// </summary>
        public double MemoryBandwidth { get; set; }

        /// <summary>
        /// Gets or sets the compute performance in GFLOPS.
        /// </summary>
        public double ComputePerformance { get; set; }
    }
}