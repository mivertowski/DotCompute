#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Models
{
    /// <summary>
    /// Local configuration options for NuGet plugin loading functionality.
    /// This is a simplified version for DotCompute.Algorithms project use.
    /// </summary>
    internal sealed class NuGetPluginLoaderOptions
    {
        /// <summary>
        /// Gets or sets the cache directory for downloaded packages.
        /// </summary>
        public string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "DotCompute", "NuGetCache");

        /// <summary>
        /// Gets or sets whether to include prerelease versions in package resolution.
        /// </summary>
        public bool IncludePrereleaseVersions { get; set; }

        /// <summary>
        /// Gets or sets the path to the package security policy file.
        /// </summary>
        public string? PackageSecurityPolicy { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent downloads allowed.
        /// </summary>
        public int MaxConcurrentDownloads { get; set; } = 4;

        /// <summary>
        /// Gets or sets the timeout for package operations in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;
    }
}