// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Compute.Types
{
    /// <summary>
    /// Contains parsed information about a kernel.
    /// Represents the analyzed structure of a kernel including its type, source code, and parameters.
    /// </summary>
    internal class KernelInfo
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the detected kernel type for optimization selection.
        /// </summary>
        public KernelType Type { get; set; }

        /// <summary>
        /// Gets or sets the kernel source code.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of kernel parameters.
        /// </summary>
        public List<KernelParameter> Parameters { get; set; } = [];
    }
}