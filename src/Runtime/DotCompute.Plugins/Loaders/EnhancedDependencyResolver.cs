// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Plugins.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Loaders
{

    /// <summary>
    /// Simplified dependency resolver for plugin dependencies
    /// </summary>
    public sealed class EnhancedDependencyResolver(ILogger<EnhancedDependencyResolver> logger) : IDisposable
    {
        private readonly ILogger<EnhancedDependencyResolver> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ConcurrentDictionary<string, string> _packageCache = new();
        private bool _disposed;

        /// <summary>
        /// Simplified dependency resolution - just returns success for now
        /// </summary>
        public Task<bool> ResolveDependenciesAsync(string packageId, string version)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("Package ID cannot be null or empty", nameof(packageId));
            }

            _logger.LogInfoMessage("Resolving dependencies for package {PackageId} version {packageId, version}");

            // For now, just cache the package and return success
            _packageCache[packageId] = version;

            return Task.FromResult(true);
        }

        /// <summary>
        /// Check if package has been resolved
        /// </summary>
        public bool IsPackageResolved(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return false;
            }

            return _packageCache.ContainsKey(packageId);
        }

        /// <summary>
        /// Get resolved package version
        /// </summary>
        public string? GetResolvedVersion(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
            {
                return null;
            }

            _ = _packageCache.TryGetValue(packageId, out var version);
            return version;
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _packageCache.Clear();
            _disposed = true;
        }
    }

    /// <summary>
    /// Configuration options for the enhanced dependency resolver
    /// </summary>
    public class EnhancedDependencyResolverOptions
    {
        /// <summary>
        /// Target framework for dependency resolution
        /// </summary>
        public string TargetFramework { get; set; } = "net9.0";

        /// <summary>
        /// Configuration path for NuGet settings
        /// </summary>
        public string? ConfigurationPath { get; set; }

        /// <summary>
        /// Maximum number of concurrent resolutions
        /// </summary>
        public int MaxConcurrentResolutions { get; set; } = 4;
    }
}
