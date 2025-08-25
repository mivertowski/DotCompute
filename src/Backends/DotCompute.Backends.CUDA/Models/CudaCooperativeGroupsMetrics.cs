// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// Metrics for cooperative groups functionality.
    /// </summary>
    public sealed class CudaCooperativeGroupsMetrics
    {
        /// <summary>
        /// Gets or sets the efficiency score (0.0 to 1.0).
        /// </summary>
        public double EfficiencyScore { get; set; }

        /// <summary>
        /// Gets or sets the number of active cooperative groups.
        /// </summary>
        public int ActiveGroups { get; set; }

        /// <summary>
        /// Gets or sets the synchronization overhead in milliseconds.
        /// </summary>
        public double SynchronizationOverhead { get; set; }
    }
}