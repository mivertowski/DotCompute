// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Advanced.Models
{
    /// <summary>
    /// Validation result for Ada generation GPU configurations.
    /// </summary>
    public sealed class AdaValidationResult
    {
        /// <summary>
        /// Gets or sets whether the configuration is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the list of validation errors.
        /// </summary>
        public IList<string> Errors { get; } = [];

        /// <summary>
        /// Gets or sets the list of validation warnings.
        /// </summary>
        public IList<string> Warnings { get; } = [];

        /// <summary>
        /// Gets or sets the calculated occupancy ratio (0.0 to 1.0).
        /// </summary>
        public double Occupancy { get; set; }

        /// <summary>
        /// Gets or sets the number of blocks per streaming multiprocessor.
        /// </summary>
        public int BlocksPerSM { get; set; }
    }
}
