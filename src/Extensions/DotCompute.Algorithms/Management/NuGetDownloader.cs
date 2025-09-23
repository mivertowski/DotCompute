// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.IO.Compression;
using global::System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging.Core;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Handles NuGet package downloading, extraction, and caching with comprehensive security validation.
    /// Provides download management with integrity verification and proper cleanup.
    /// </summary>
    internal sealed class NuGetDownloader : IDisposable
    {
        private readonly ILogger _logger;
        private readonly NuGetPluginLoaderOptions _options;
        private readonly SourceRepositoryProvider _sourceRepositoryProvider;
        private readonly SemaphoreSlim _downloadSemaphore;
        private readonly ConcurrentDictionary<string, string> _downloadCache;
        private bool _disposed;

        public NuGetDownloader(ILogger logger, NuGetPluginLoaderOptions options, int maxConcurrentDownloads = 4)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            var settings = Settings.LoadDefaultSettings(null);
            _sourceRepositoryProvider = new SourceRepositoryProvider(
                new PackageSourceProvider(settings),
                Repository.Provider.GetCoreV3());

            _downloadSemaphore = new SemaphoreSlim(maxConcurrentDownloads, maxConcurrentDownloads);
            _downloadCache = new ConcurrentDictionary<string, string>();
        }

        /// <summary>
        /// Downloads a package from configured NuGet sources with caching and integrity verification.
        /// </summary>
        public async Task<string> DownloadPackageAsync(PackageIdentity identity, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identity);

            var cacheKey = $"{identity.Id}_{identity.Version}";

            // Check if already downloaded and cached
            if (_downloadCache.TryGetValue(cacheKey, out var cachedPath) && File.Exists(cachedPath))
            {
                _logger.LogDebug("Using cached download for package: {PackageId} {Version}",
                    identity.Id, identity.Version);
                return cachedPath;
            }

            await _downloadSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Downloading package: {PackageId} {Version}", identity.Id, identity.Version);

                var downloadPath = Path.Combine(_options.CacheDirectory, $"{identity.Id}.{identity.Version}.nupkg");

                // Ensure cache directory exists
                Directory.CreateDirectory(_options.CacheDirectory);

                foreach (var sourceRepository in _sourceRepositoryProvider.GetRepositories())
                {
                    try
                    {
                        var downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(cancellationToken)
                            .ConfigureAwait(false);

                        if (downloadResource == null)
                        {
                            continue;
                        }


                        using var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                            identity,
                            new PackageDownloadContext(NullSourceCacheContext.Instance),
                            _options.CacheDirectory,
                            new NuGetLogger(_logger),
                            cancellationToken).ConfigureAwait(false);

                        if (downloadResult.Status == DownloadResourceResultStatus.Available && downloadResult.PackageStream != null)
                        {
                            // Download to temporary file first
                            var tempPath = downloadPath + ".tmp";

                            try
                            {
                                using (var fileStream = File.Create(tempPath))
                                {
                                    await downloadResult.PackageStream.CopyToAsync(fileStream, cancellationToken)
                                        .ConfigureAwait(false);
                                }

                                // Verify downloaded package integrity
                                if (await ValidatePackageIntegrityAsync(tempPath, cancellationToken))
                                {
                                    // Move to final location
                                    if (File.Exists(downloadPath))
                                    {
                                        File.Delete(downloadPath);
                                    }
                                    File.Move(tempPath, downloadPath);

                                    var fileInfo = new FileInfo(downloadPath);
                                    _logger.LogInformation("Successfully downloaded package: {PackageId} {Version} ({Size} bytes)",
                                        identity.Id, identity.Version, fileInfo.Length);

                                    // Cache the download path
                                    _downloadCache.TryAdd(cacheKey, downloadPath);
                                    return downloadPath;
                                }
                                else
                                {
                                    _logger.LogWarning("Package integrity validation failed: {PackageId} {Version}",
                                        identity.Id, identity.Version);
                                    // Continue to next source
                                }
                            }
                            finally
                            {
                                // Clean up temporary file
                                if (File.Exists(tempPath))
                                {
                                    File.Delete(tempPath);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download package {PackageId} {Version} from source: {Source}",
                            identity.Id, identity.Version, sourceRepository.PackageSource.Source);
                        // Continue to next source
                    }
                }

                throw new InvalidOperationException($"Failed to download package {identity} from any configured source.");
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }

        /// <summary>
        /// Extracts a .nupkg file to the cache directory with security validation.
        /// </summary>
        public async Task<string> ExtractPackageAsync(
            string packagePath,
            PackageIdentity identity,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
            ArgumentNullException.ThrowIfNull(identity);

            var extractPath = Path.Combine(
                _options.CacheDirectory,
                "extracted",
                $"{identity.Id}.{identity.Version}");

            if (Directory.Exists(extractPath))
            {
                // Check if extraction is up to date
                var packageTime = File.GetLastWriteTimeUtc(packagePath);
                var extractTime = Directory.GetLastWriteTimeUtc(extractPath);

                if (extractTime >= packageTime)
                {
                    _logger.LogDebug("Package already extracted: {PackageId} {Version}",
                        identity.Id, identity.Version);
                    return extractPath;
                }

                // Clean up old extraction
                Directory.Delete(extractPath, true);
            }

            Directory.CreateDirectory(extractPath);

            _logger.LogDebug("Extracting package: {PackageId} {Version} to {ExtractPath}",
                identity.Id, identity.Version, extractPath);

            try
            {
                await Task.Run(() =>
                {
                    // Extract using ZipFile for better control
                    using var archive = ZipFile.OpenRead(packagePath);

                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Skip directory entries
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            continue;
                        }

                        // Security check: prevent directory traversal

                        var entryPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
                        if (!entryPath.StartsWith(extractPath, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("Security violation: Directory traversal detected in entry {EntryName} for package {PackageId}",
                                entry.FullName, identity.Id);
                            continue;
                        }

                        // Create directory if needed
                        var directory = Path.GetDirectoryName(entryPath);
                        if (directory != null && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        // Extract file
                        entry.ExtractToFile(entryPath, true);
                    }

                    _logger.LogDebug("Successfully extracted package: {PackageId} {Version} ({EntryCount} entries)",
                        identity.Id, identity.Version, archive.Entries.Count);

                }, cancellationToken);

                return extractPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract package: {PackageId} {Version}",
                    identity.Id, identity.Version);

                // Clean up on failure
                if (Directory.Exists(extractPath))
                {
                    try
                    {
                        Directory.Delete(extractPath, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Calculates SHA256 hash of a package file for integrity verification.
        /// </summary>
        public static async Task<string> CalculatePackageHashAsync(string packagePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

            using var stream = File.OpenRead(packagePath);
            using var sha256 = SHA256.Create();

            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Validates package integrity by checking file format and basic structure.
        /// </summary>
        private async Task<bool> ValidatePackageIntegrityAsync(string packagePath, CancellationToken cancellationToken)
        {
            try
            {
                // Basic file existence and size check
                var fileInfo = new FileInfo(packagePath);
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    return false;
                }

                // Validate ZIP structure
                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(packagePath);

                    // Check for required files
                    var hasNuspec = archive.Entries.Any(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
                    if (!hasNuspec)
                    {
                        _logger.LogWarning("Package missing required .nuspec file: {PackagePath}", packagePath);
                        return false;
                    }

                    // Basic entry validation
                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Check for suspicious entry names
                        if (entry.FullName.Contains("..") ||
                            entry.FullName.StartsWith("/") ||
                            entry.FullName.Contains(":"))
                        {
                            _logger.LogWarning("Suspicious entry found in package: {EntryName}", entry.FullName);
                            return false;
                        }
                    }

                }, cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Package integrity validation failed: {PackagePath}", packagePath);
                return false;
            }
        }

        /// <summary>
        /// Clears the download cache for packages older than the specified time.
        /// </summary>
        public async Task ClearCacheAsync(TimeSpan? olderThan = null)
        {
            var cutoffTime = DateTime.UtcNow - (olderThan ?? TimeSpan.FromDays(30));

            await Task.Run(() =>
            {
                var cachesToRemove = new List<string>();

                foreach (var kvp in _downloadCache)
                {
                    try
                    {
                        if (File.Exists(kvp.Value))
                        {
                            var fileInfo = new FileInfo(kvp.Value);
                            if (fileInfo.LastWriteTimeUtc < cutoffTime)
                            {
                                File.Delete(kvp.Value);
                                cachesToRemove.Add(kvp.Key);
                                _logger.LogDebug("Removed cached package: {CacheKey}", kvp.Key);
                            }
                        }
                        else
                        {
                            // File doesn't exist, remove from cache
                            cachesToRemove.Add(kvp.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean cache entry: {CacheKey}", kvp.Key);
                    }
                }

                // Remove cache entries
                foreach (var key in cachesToRemove)
                {
                    _downloadCache.TryRemove(key, out _);
                }

                _logger.LogInformation("Cache cleanup completed. Removed {Count} entries.", cachesToRemove.Count);
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _downloadSemaphore?.Dispose();
                _sourceRepositoryProvider?.Dispose();
                _disposed = true;
            }
        }
    }
}