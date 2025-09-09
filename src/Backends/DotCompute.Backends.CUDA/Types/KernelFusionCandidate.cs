// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Kernel fusion candidate.
    /// </summary>
    public sealed class KernelFusionCandidate
    {
        public string KernelA { get; set; } = string.Empty;
        public string KernelB { get; set; } = string.Empty;
        public double FusionBenefit { get; set; }
        public bool IsEligible { get; set; }
    }
}