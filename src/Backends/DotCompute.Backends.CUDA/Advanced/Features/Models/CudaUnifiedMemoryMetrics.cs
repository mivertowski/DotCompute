// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// Metrics for unified memory functionality.
    /// </summary>
    public sealed class CudaUnifiedMemoryMetrics
    {
        /// <summary>
        /// Gets or sets the efficiency score (0.0 to 1.0).
        /// </summary>
        public double EfficiencyScore { get; set; }

        /// <summary>
        /// Gets or sets the number of page faults.
        /// </summary>
        public ulong PageFaults { get; set; }

        /// <summary>
        /// Gets or sets the migration overhead in milliseconds.
        /// </summary>
        public double MigrationOverhead { get; set; }
    }
}