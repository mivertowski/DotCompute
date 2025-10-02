// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Models
{
    /// <summary>
    /// Profiling statistics container for kernel execution timing.
    /// </summary>
    public sealed class ProfilingStatistics
    {
        /// <summary>
        /// Gets or sets the average execution time in milliseconds.
        /// </summary>
        public double AverageTime { get; set; }

        /// <summary>
        /// Gets or sets the minimum execution time in milliseconds.
        /// </summary>
        public double MinTime { get; set; }

        /// <summary>
        /// Gets or sets the maximum execution time in milliseconds.
        /// </summary>
        public double MaxTime { get; set; }

        /// <summary>
        /// Gets or sets the median execution time in milliseconds.
        /// </summary>
        public double MedianTime { get; set; }

        /// <summary>
        /// Gets or sets the standard deviation of execution times.
        /// </summary>
        public double StandardDeviation { get; set; }

        /// <summary>
        /// Gets or sets the percentile timings (e.g., 50th, 90th, 95th, 99th).
        /// </summary>
        public Dictionary<int, double> Percentiles { get; } = [];
    }
}