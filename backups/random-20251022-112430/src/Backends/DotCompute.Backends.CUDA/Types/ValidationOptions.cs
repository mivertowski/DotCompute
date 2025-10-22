// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Validation options for CUDA operations.
    /// </summary>
    public sealed class ValidationOptions
    {
        /// <summary>
        /// Gets or sets the validate memory access.
        /// </summary>
        /// <value>The validate memory access.</value>
        public bool ValidateMemoryAccess { get; set; } = true;
        /// <summary>
        /// Gets or sets the validate launch parameters.
        /// </summary>
        /// <value>The validate launch parameters.</value>
        public bool ValidateLaunchParameters { get; set; } = true;
        /// <summary>
        /// Gets or sets the validate kernel existence.
        /// </summary>
        /// <value>The validate kernel existence.</value>
        public bool ValidateKernelExistence { get; set; } = true;
        /// <summary>
        /// Gets or sets the enable bounds checking.
        /// </summary>
        /// <value>The enable bounds checking.</value>
        public bool EnableBoundsChecking { get; set; }
        /// <summary>
        /// Gets or sets the enable nan detection.
        /// </summary>
        /// <value>The enable nan detection.</value>
        public bool EnableNanDetection { get; set; }
    }
}