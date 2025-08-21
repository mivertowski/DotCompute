// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Compute.Types
{
    /// <summary>
    /// Represents a kernel parameter with type and access information.
    /// Contains metadata about individual parameters in a kernel function signature.
    /// </summary>
    internal class KernelParameter
    {
        /// <summary>
        /// Gets or sets the parameter name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parameter type (e.g., "float*", "int", "const char*").
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this parameter is in global memory space.
        /// </summary>
        public bool IsGlobal { get; set; }
    }
}