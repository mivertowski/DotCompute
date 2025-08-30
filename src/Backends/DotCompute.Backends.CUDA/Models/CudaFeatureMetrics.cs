// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Features.Models
{
    /// <summary>
    /// Comprehensive metrics for advanced CUDA features.
    /// </summary>
    public sealed class CudaFeatureMetrics
    {
        /// <summary>
        /// Gets or sets the cooperative groups metrics.
        /// </summary>
        public CudaCooperativeGroupsMetrics CooperativeGroupsMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the dynamic parallelism metrics.
        /// </summary>
        public DotCompute.Backends.CUDA.Execution.Metrics.CudaDynamicParallelismMetrics DynamicParallelismMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the unified memory metrics.
        /// </summary>
        public CudaUnifiedMemoryMetrics UnifiedMemoryMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the tensor core metrics.
        /// </summary>
        public CudaTensorCoreMetrics TensorCoreMetrics { get; set; } = new();

        /// <summary>
        /// Gets or sets the overall efficiency score (0.0 to 1.0).
        /// </summary>
        public double OverallEfficiency { get; set; }
    }
}
