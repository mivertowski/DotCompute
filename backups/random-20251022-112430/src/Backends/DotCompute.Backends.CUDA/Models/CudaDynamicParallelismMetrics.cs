// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file is DEPRECATED - CudaDynamicParallelismMetrics is now defined in
// /src/Backends/DotCompute.Backends.CUDA/Execution/Metrics/CudaExecutionMetrics.cs
// This file is kept for compatibility during migration and will be removed.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// DEPRECATED: Use DotCompute.Backends.CUDA.Execution.Metrics.CudaDynamicParallelismMetrics instead.
    /// This class is kept for backward compatibility during migration.
    /// </summary>
    [Obsolete("Use DotCompute.Backends.CUDA.Execution.Metrics.CudaDynamicParallelismMetrics instead.", false)]
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