// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// CUDA kernel fusion options.
    /// </summary>
    public sealed class CudaKernelFusionOptions
    {
        public bool EnableAutoFusion { get; set; } = true;
        public int MaxFusedKernels { get; set; } = 4;
        public double MinimumBenefitThreshold { get; set; } = 0.2;
    }
}