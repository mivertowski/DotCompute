// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA kernel fusion options.
    /// </summary>
    public sealed class CudaKernelFusionOptions
    {
        /// <summary>
        /// Gets or sets the enable auto fusion.
        /// </summary>
        /// <value>The enable auto fusion.</value>
        public bool EnableAutoFusion { get; set; } = true;
        /// <summary>
        /// Gets or sets the max fused kernels.
        /// </summary>
        /// <value>The max fused kernels.</value>
        public int MaxFusedKernels { get; set; } = 4;
        /// <summary>
        /// Gets or sets the minimum benefit threshold.
        /// </summary>
        /// <value>The minimum benefit threshold.</value>
        public double MinimumBenefitThreshold { get; set; } = 0.2;
    }
}
