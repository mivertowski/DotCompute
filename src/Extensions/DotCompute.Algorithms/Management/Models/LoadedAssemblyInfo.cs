
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Models
{
    /// <summary>
    /// Information about a loaded assembly.
    /// </summary>
    public sealed class LoadedAssemblyInfo
    {
        /// <summary>
        /// Gets or sets the assembly name.
        /// </summary>
        public required string AssemblyName { get; set; }

        /// <summary>
        /// Gets or sets the assembly path.
        /// </summary>
        public required string AssemblyPath { get; set; }

        /// <summary>
        /// Gets or sets the load time.
        /// </summary>
        public DateTime LoadTime { get; set; }

        /// <summary>
        /// Gets or sets whether the assembly is isolated.
        /// </summary>
        public bool IsIsolated { get; set; }

        /// <summary>
        /// Gets or sets the security validation result.
        /// </summary>
        public required SecurityValidationResult UnifiedValidationResult { get; set; }

        /// <summary>
        /// Gets or sets the number of plugins in the assembly.
        /// </summary>
        public int PluginCount { get; set; }

        /// <summary>
        /// Gets the plugin information.
        /// </summary>
        public IList<PluginInfo> Plugins { get; } = [];
    }
}
