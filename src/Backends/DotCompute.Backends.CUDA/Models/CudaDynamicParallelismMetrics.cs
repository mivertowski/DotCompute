// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// Metrics for dynamic parallelism functionality.
    /// </summary>
    public sealed class CudaDynamicParallelismMetrics
    {
        /// <summary>
        /// Gets or sets the efficiency score (0.0 to 1.0).
        /// </summary>
        public double EfficiencyScore { get; set; }

        /// <summary>
        /// Gets or sets the number of child kernel launches.
        /// </summary>
        public int ChildKernelLaunches { get; set; }

        /// <summary>
        /// Gets or sets the launch overhead in milliseconds.
        /// </summary>
        public double LaunchOverhead { get; set; }
    }
}