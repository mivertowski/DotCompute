// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// Result of optimization operations.
    /// </summary>
    public sealed class CudaOptimizationResult
    {
        /// <summary>
        /// Gets or sets whether the optimization was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or initializes the list of optimizations that were applied.
        /// </summary>
        public IList<string> OptimizationsApplied { get; init; } = [];

        /// <summary>
        /// Gets or sets the time taken to perform optimizations.
        /// </summary>
        public TimeSpan OptimizationTime { get; set; }

        /// <summary>
        /// Gets or sets the estimated performance gain (1.0 = no gain, 2.0 = 2x faster).
        /// </summary>
        public double PerformanceGain { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets recommendations for further optimization.
        /// </summary>
        public IList<string> Recommendations { get; } = [];

        /// <summary>
        /// Gets or sets an error message if optimization failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}