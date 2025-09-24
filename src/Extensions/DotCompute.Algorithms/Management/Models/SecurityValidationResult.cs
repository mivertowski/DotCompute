// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Models
{
    /// <summary>
    /// Result of assembly security validation.
    /// </summary>
    public sealed class SecurityValidationResult
    {
        /// <summary>
        /// Gets or sets whether the assembly passed validation.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets the validation errors.
        /// </summary>
        public List<string> Errors { get; } = [];

        /// <summary>
        /// Gets the validation warnings.
        /// </summary>
        public List<string> Warnings { get; } = [];

        /// <summary>
        /// Gets additional validation metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; } = [];
    }
}