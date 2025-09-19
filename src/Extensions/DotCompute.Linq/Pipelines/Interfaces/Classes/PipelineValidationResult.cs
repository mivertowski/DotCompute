// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;

namespace DotCompute.Linq.Pipelines.Interfaces.Classes
{
    /// <summary>
    /// Pipeline validation result.
    /// </summary>
    public class PipelineValidationResult
    {
        /// <summary>Gets whether the pipeline is valid.</summary>
        public bool IsValid { get; set; }

        /// <summary>Gets validation errors if any.</summary>
        public List<string> Errors { get; set; } = [];

        /// <summary>Gets validation warnings if any.</summary>
        public List<string> Warnings { get; set; } = [];
    }
}