
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Models
{
    /// <summary>
    /// Sandbox configuration options for plugin execution.
    /// </summary>
    public sealed class PluginSandboxOptions
    {
        /// <summary>
        /// Gets or sets whether file system access is restricted.
        /// </summary>
        public bool RestrictFileSystemAccess { get; set; } = true;

        /// <summary>
        /// Gets or sets whether network access is restricted.
        /// </summary>
        public bool RestrictNetworkAccess { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum memory usage per plugin in bytes.
        /// </summary>
        public long MaxMemoryUsage { get; set; } = 256 * 1024 * 1024; // 256 MB

        /// <summary>
        /// Gets or sets the maximum execution time per operation.
        /// </summary>
        public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets the allowed file system paths.
        /// </summary>
        public IList<string> AllowedFilePaths { get; } = [];

        /// <summary>
        /// Gets the allowed network endpoints.
        /// </summary>
        public IList<string> AllowedNetworkEndpoints { get; } = [];
    }
}
