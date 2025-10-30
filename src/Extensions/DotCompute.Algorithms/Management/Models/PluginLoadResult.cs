
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Loading;

namespace DotCompute.Algorithms.Management.Models
{
    /// <summary>
    /// Result of plugin loading operation.
    /// </summary>
    public sealed class PluginLoadResult
    {
        /// <summary>
        /// Gets or sets whether the loading was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets the loaded plugins.
        /// </summary>
        public IList<IAlgorithmPlugin> Plugins { get; } = [];

        /// <summary>
        /// Gets or sets the security validation result.
        /// </summary>
        public SecurityValidationResult UnifiedValidationResult { get; set; } = new();

        /// <summary>
        /// Gets or sets the error message if loading failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the load context (if successful).
        /// </summary>
        public PluginAssemblyLoadContext? LoadContext { get; set; }

        /// <summary>
        /// Gets or sets the loaded assembly (if successful).
        /// </summary>
        public Assembly? Assembly { get; set; }
    }
}
