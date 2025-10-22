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

        /// <summary>
        /// Gets or sets the default target framework for package resolution.
        /// </summary>
        public string DefaultTargetFramework { get; set; } = "net9.0";

        /// <summary>
        /// Gets or sets whether security validation is enabled.
        /// </summary>
        public bool EnableSecurityValidation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether package signatures are required.
        /// </summary>
        public bool RequirePackageSignature { get; set; }

        /// <summary>
        /// Gets or sets whether malware scanning is enabled.
        /// </summary>
        public bool EnableMalwareScanning { get; set; }

        /// <summary>
        /// Gets or sets the maximum assembly size in bytes.
        /// </summary>
        public long MaxAssemblySize { get; set; } = 100 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the minimum security level required.
        /// </summary>
        public DotCompute.Abstractions.Security.SecurityLevel MinimumSecurityLevel { get; set; } = DotCompute.Abstractions.Security.SecurityLevel.Medium;

        /// <summary>
        /// Gets the allowed package prefixes for security filtering.
        /// </summary>
        public IList<string> AllowedPackagePrefixes { get; } = new List<string>();

        /// <summary>
        /// Gets or sets the cache expiration time.
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);
    }
}