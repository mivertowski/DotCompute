// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Compilation;

namespace DotCompute.Backends.CUDA.Advanced.Profiling.Models
{
    /// <summary>
    /// Kernel profile data storage for performance analysis.
    /// </summary>
    internal sealed class KernelProfileData
    {
        /// <summary>
        /// Gets or sets the name of the profiled kernel.
        /// </summary>
        public string KernelName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the launch configuration used for profiling.
        /// </summary>
        public CudaLaunchConfig LaunchConfig { get; set; }

        /// <summary>
        /// Gets or sets the collected timing measurements in milliseconds.
        /// </summary>
        public IList<double> Timings { get; } = [];

        /// <summary>
        /// Gets or sets the calculated statistics from the timing data.
        /// </summary>
        public ProfilingStatistics Statistics { get; set; } = new();

        /// <summary>
        /// Gets or sets the occupancy metrics for the kernel.
        /// </summary>
        public OccupancyMetrics Occupancy { get; set; } = new();

        /// <summary>
        /// Gets or sets when the kernel was last profiled.
        /// </summary>
        public DateTime LastProfiled { get; set; }
    }
}