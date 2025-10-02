// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Kernel fusion candidate.
    /// </summary>
    public sealed class KernelFusionCandidate
    {
        /// <summary>
        /// Gets or sets the kernel a.
        /// </summary>
        /// <value>The kernel a.</value>
        public string KernelA { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the kernel b.
        /// </summary>
        /// <value>The kernel b.</value>
        public string KernelB { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the fusion benefit.
        /// </summary>
        /// <value>The fusion benefit.</value>
        public double FusionBenefit { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether eligible.
        /// </summary>
        /// <value>The is eligible.</value>
        public bool IsEligible { get; set; }
    }
}