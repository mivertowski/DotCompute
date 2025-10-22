
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using MSLogger = Microsoft.Extensions.Logging.ILogger;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Packaging.Core;
using DotCompute.Algorithms.Management.Models;
using DotCompute.Algorithms.Management.Types;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Handles NuGet package resolution including version resolution and dependency analysis.
    /// Provides comprehensive package resolution with caching and security validation.
    /// </summary>
    internal sealed partial class NuGetPackageResolver : IDisposable
    {
        private readonly MSLogger _logger;
        private readonly NuGetPluginLoaderOptions _options;
        private readonly SourceRepositoryProvider _sourceRepositoryProvider;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the NuGetPackageResolver class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="options">The options.</param>

        public NuGetPackageResolver(MSLogger logger, NuGetPluginLoaderOptions options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            var settings = Settings.LoadDefaultSettings(null);
            _sourceRepositoryProvider = new SourceRepositoryProvider(
                new PackageSourceProvider(settings),
                Repository.Provider.GetCoreV3());
        }

        /// <summary>
        /// Resolves the latest version of a package from configured NuGet sources.
        /// </summary>
        public async Task<NuGetVersion> ResolveLatestVersionAsync(string packageId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

            LogResolvingLatestVersion(packageId);

            foreach (var sourceRepository in _sourceRepositoryProvider.GetRepositories())
            {
                try
                {
                    var metadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>(cancellationToken)
                        .ConfigureAwait(false);

                    if (metadataResource == null)
                    {
                        continue;
                    }


                    var packages = await metadataResource.GetMetadataAsync(
                        packageId,
                        includePrerelease: _options.IncludePrereleaseVersions,
                        includeUnlisted: false,
                        sourceCacheContext: new SourceCacheContext(),
                        log: new NuGetLogger(_logger),
                        token: cancellationToken).ConfigureAwait(false);

                    var latestPackage = packages
                        .Where(p => !p.Identity.Version.IsPrerelease || _options.IncludePrereleaseVersions)
                        .OrderByDescending(p => p.Identity.Version)
                        .FirstOrDefault();

                    if (latestPackage != null)
                    {
                        LogResolvedLatestVersion(packageId, latestPackage.Identity.Version!);
                        return latestPackage.Identity.Version;
                    }
                }
                catch (Exception ex)
                {
                    LogFailedToResolveVersionFromSource(ex, sourceRepository.PackageSource.Source);
                    // Continue to next source
                }
            }

            throw new InvalidOperationException($"Package '{packageId}' not found in any configured source.");
        }

        /// <summary>
        /// Resolves all dependencies for a given package identity.
        /// </summary>
        public async Task<PackageDependency[]> ResolveDependenciesAsync(PackageIdentity identity, string extractedPath)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentException.ThrowIfNullOrWhiteSpace(extractedPath);

            LogResolvingDependencies(identity.Id, identity.Version!);

            var dependencies = new List<PackageDependency>();

            // Load dependencies from .nuspec file in extracted package
            var nuspecFile = Directory.GetFiles(extractedPath, "*.nuspec", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (nuspecFile != null)
            {
                try
                {
                    var nuspecContent = await File.ReadAllTextAsync(nuspecFile).ConfigureAwait(false);
                    var nuspecDoc = System.Xml.Linq.XDocument.Parse(nuspecContent);
                    var ns = nuspecDoc.Root?.GetDefaultNamespace();

                    if (ns != null)
                    {
                        var dependencyGroups = nuspecDoc.Root?
                            .Element(ns + "metadata")?
                            .Element(ns + "dependencies")?
                            .Elements(ns + "group");

                        if (dependencyGroups != null)
                        {
                            foreach (var group in dependencyGroups)
                            {
                                var targetFramework = group.Attribute("targetFramework")?.Value;
                                var dependencyElements = group.Elements(ns + "dependency");

                                foreach (var dep in dependencyElements)
                                {
                                    var id = dep.Attribute("id")?.Value;
                                    var version = dep.Attribute("version")?.Value;
                                    var exclude = dep.Attribute("exclude")?.Value?.Split(',');
                                    var include = dep.Attribute("include")?.Value?.Split(',');

                                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(version))
                                    {
                                        dependencies.Add(new PackageDependency(
                                            id,
                                            VersionRange.Parse(version),
                                            exclude,
                                            include
                                        ));
                                    }
                                }
                            }
                        }
                    }

                    LogResolvedDependencies(dependencies.Count, identity.Id);
                }
                catch (Exception ex)
                {
                    LogFailedToParseNuspecFile(ex, nuspecFile);
                }
            }

            return [.. dependencies];
        }

        /// <summary>
        /// Parses a package source string to extract package ID and version.
        /// </summary>
        public static (string PackageId, NuGetVersion? Version) ParsePackageSource(string packageSource)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packageSource);

            // Handle various formats:
            // - "PackageId" -> latest version
            // - "PackageId:1.0.0" -> specific version
            // - "PackageId/1.0.0" -> specific version

            var separators = new[] { ':', '/' };
            var parts = packageSource.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
            {
                return (parts[0].Trim(), null);
            }

            if (parts.Length == 2 && NuGetVersion.TryParse(parts[1].Trim(), out var version))
            {
                return (parts[0].Trim(), version);
            }

            // If parsing fails, treat entire string as package ID
            return (packageSource.Trim(), null);
        }

        /// <summary>
        /// Validates that a package identity meets security requirements.
        /// </summary>
        public async Task<(bool IsValid, string? ValidationMessage)> ValidatePackageSecurityAsync(
            PackageIdentity identity,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);

            try
            {
                // Check against security policy if configured
                if (!string.IsNullOrEmpty(_options.PackageSecurityPolicy))
                {
                    var securityPolicy = DotCompute.Algorithms.Types.Security.SecurityPolicy.FromFile(_options.PackageSecurityPolicy);

                    if (securityPolicy.IsPackageBlocked(identity.Id))
                    {
                        return (false, $"Package {identity.Id} is blocked by security policy");
                    }

                    if (!securityPolicy.IsPackageAllowed(identity.Id))
                    {
                        return (false, $"Package {identity.Id} is not in allowed packages list");
                    }
                }

                // Additional security validations can be added here
                // - Check for known vulnerabilities
                // - Validate publisher certificates
                // - Check package reputation

                await Task.CompletedTask; // Placeholder for async security checks

                return (true, null);
            }
            catch (Exception ex)
            {
                LogSecurityValidationFailed(ex, identity.Id, identity.Version!);
                return (false, $"Security validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a cache key for package identity and target framework combination.
        /// </summary>
        public static string GetCacheKey(PackageIdentity identity, string targetFramework)
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);

            return $"{identity.Id}_{identity.Version}_{targetFramework}".ToUpperInvariant();
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                // SourceRepositoryProvider doesn't implement IDisposable in modern NuGet versions
                // No explicit disposal needed
                _disposed = true;
            }
        }

        #region LoggerMessage Delegates

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Resolving latest version for package: {PackageId}")]
        private partial void LogResolvingLatestVersion(string packageId);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Resolved latest version for {PackageId}: {Version}")]
        private partial void LogResolvedLatestVersion(string packageId, NuGetVersion? version);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Failed to resolve version from source: {Source}")]
        private partial void LogFailedToResolveVersionFromSource(Exception exception, string source);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Resolving dependencies for package: {PackageId} {Version}")]
        private partial void LogResolvingDependencies(string packageId, NuGetVersion? version);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Resolved {Count} dependencies for {PackageId}")]
        private partial void LogResolvedDependencies(int count, string packageId);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Failed to parse nuspec file for dependency resolution: {NuspecFile}")]
        private partial void LogFailedToParseNuspecFile(Exception exception, string nuspecFile);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Security validation failed for package: {PackageId} {Version}")]
        private partial void LogSecurityValidationFailed(Exception exception, string packageId, NuGetVersion? version);

        #endregion
    }
}