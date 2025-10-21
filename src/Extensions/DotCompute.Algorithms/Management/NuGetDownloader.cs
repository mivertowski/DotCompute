// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MSLogger = Microsoft.Extensions.Logging.ILogger;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging.Core;
using DotCompute.Algorithms.Management.Models;
using System;

namespace DotCompute.Algorithms.Management
{
    /// <summary>
    /// Handles NuGet package downloading, extraction, and caching with comprehensive security validation.
    /// Provides download management with integrity verification and proper cleanup.
    /// </summary>
    internal sealed partial class NuGetDownloader : IDisposable
    {
        private readonly MSLogger _logger;
        private readonly NuGetPluginLoaderOptions _options;
        private readonly SourceRepositoryProvider _sourceRepositoryProvider;
        private readonly SemaphoreSlim _downloadSemaphore;
        private readonly ConcurrentDictionary<string, string> _downloadCache;
        private bool _disposed;
        /// <summary>
        /// Initializes a new instance of the NuGetDownloader class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="options">The options.</param>
        /// <param name="maxConcurrentDownloads">The max concurrent downloads.</param>

        public NuGetDownloader(MSLogger logger, NuGetPluginLoaderOptions options, int maxConcurrentDownloads = 4)
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
                LogUsingCachedDownload(identity.Id, identity.Version.ToString());
                return cachedPath;
            }

            await _downloadSemaphore.WaitAsync(cancellationToken);
            try
            {
                LogDownloadingPackage(identity.Id, identity.Version.ToString());

                var downloadPath = Path.Combine(_options.CacheDirectory, $"{identity.Id}.{identity.Version}.nupkg");

                // Ensure cache directory exists
                _ = Directory.CreateDirectory(_options.CacheDirectory);

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
                            new NuGetLoggerAdapter(_logger),
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
                                    LogSuccessfullyDownloadedPackage(identity.Id, identity.Version.ToString(), fileInfo.Length);

                                    // Cache the download path
                                    _ = _downloadCache.TryAdd(cacheKey, downloadPath);
                                    return downloadPath;
                                }
                                else
                                {
                                    LogPackageIntegrityValidationFailed(identity.Id, identity.Version.ToString());
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
                        LogFailedToDownloadPackageFromSource(ex, identity.Id, identity.Version.ToString(), sourceRepository.PackageSource.Source);
                        // Continue to next source
                    }
                }

                throw new InvalidOperationException($"Failed to download package {identity} from any configured source.");
            }
            finally
            {
                _ = _downloadSemaphore.Release();
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
                    LogPackageAlreadyExtracted(identity.Id, identity.Version.ToString());
                    return extractPath;
                }

                // Clean up old extraction
                Directory.Delete(extractPath, true);
            }

            _ = Directory.CreateDirectory(extractPath);

            LogExtractingPackage(identity.Id, identity.Version.ToString(), extractPath);

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
                            LogSecurityViolationDirectoryTraversal(entry.FullName, identity.Id);
                            continue;
                        }

                        // Create directory if needed
                        var directory = Path.GetDirectoryName(entryPath);
                        if (directory != null && !Directory.Exists(directory))
                        {
                            _ = Directory.CreateDirectory(directory);
                        }

                        // Extract file
                        entry.ExtractToFile(entryPath, true);
                    }

                    LogSuccessfullyExtractedPackage(identity.Id, identity.Version.ToString(), archive.Entries.Count);

                }, cancellationToken);

                return extractPath;
            }
            catch (Exception ex)
            {
                LogFailedToExtractPackage(ex, identity.Id, identity.Version.ToString());

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
            return Convert.ToHexString(hashBytes).ToUpperInvariant();
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
                bool isValid = false;
                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(packagePath);

                    // Check for required files
                    var hasNuspec = archive.Entries.Any(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
                    if (!hasNuspec)
                    {
                        LogPackageMissingNuspec(packagePath);
                        isValid = false;
                        return;
                    }

                    // Basic entry validation
                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Check for suspicious entry names
                        if (entry.FullName.Contains("..", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.StartsWith("/", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.Contains(":", StringComparison.Ordinal))
                        {
                            LogSuspiciousEntryFound(entry.FullName);
                            isValid = false;
                            return;
                        }
                    }

                    isValid = true;
                }, cancellationToken);

                return isValid;
            }
            catch (Exception ex)
            {
                LogPackageIntegrityValidationFailedException(ex, packagePath);
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
                                LogRemovedCachedPackage(kvp.Key);
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
                        LogFailedToCleanCacheEntry(ex, kvp.Key);
                    }
                }

                // Remove cache entries
                foreach (var key in cachesToRemove)
                {
                    _ = _downloadCache.TryRemove(key, out _);
                }

                LogCacheCleanupCompleted(cachesToRemove.Count);
            });
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                _downloadSemaphore?.Dispose();
                // SourceRepositoryProvider doesn't implement IDisposable
                // _sourceRepositoryProvider?.Dispose();
                _disposed = true;
            }
        }

        #region LoggerMessage Delegates

        [LoggerMessage(Level = LogLevel.Debug, Message = "Using cached download for package: {packageId} {version}")]
        private partial void LogUsingCachedDownload(string packageId, string version);

        [LoggerMessage(Level = LogLevel.Information, Message = "Downloading package: {packageId} {version}")]
        private partial void LogDownloadingPackage(string packageId, string version);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully downloaded package: {packageId} {version} ({size} bytes)")]
        private partial void LogSuccessfullyDownloadedPackage(string packageId, string version, long size);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Package integrity validation failed: {packageId} {version}")]
        private partial void LogPackageIntegrityValidationFailed(string packageId, string version);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to download package {packageId} {version} from source: {source}")]
        private partial void LogFailedToDownloadPackageFromSource(Exception ex, string packageId, string version, string source);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Package already extracted: {packageId} {version}")]
        private partial void LogPackageAlreadyExtracted(string packageId, string version);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Extracting package: {packageId} {version} to {extractPath}")]
        private partial void LogExtractingPackage(string packageId, string version, string extractPath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Security violation: Directory traversal detected in entry {entryName} for package {packageId}")]
        private partial void LogSecurityViolationDirectoryTraversal(string entryName, string packageId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Successfully extracted package: {packageId} {version} ({entryCount} entries)")]
        private partial void LogSuccessfullyExtractedPackage(string packageId, string version, int entryCount);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to extract package: {packageId} {version}")]
        private partial void LogFailedToExtractPackage(Exception ex, string packageId, string version);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Package missing required .nuspec file: {packagePath}")]
        private partial void LogPackageMissingNuspec(string packagePath);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Suspicious entry found in package: {entryName}")]
        private partial void LogSuspiciousEntryFound(string entryName);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Package integrity validation failed: {packagePath}")]
        private partial void LogPackageIntegrityValidationFailedException(Exception ex, string packagePath);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Removed cached package: {cacheKey}")]
        private partial void LogRemovedCachedPackage(string cacheKey);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to clean cache entry: {cacheKey}")]
        private partial void LogFailedToCleanCacheEntry(Exception ex, string cacheKey);

        [LoggerMessage(Level = LogLevel.Information, Message = "Cache cleanup completed. Removed {count} entries.")]
        private partial void LogCacheCleanupCompleted(int count);

        #endregion
    }

    /// <summary>
    /// Adapter to bridge Microsoft.Extensions.Logging.ILogger to NuGet.Common.ILogger.
    /// </summary>
    internal sealed partial class NuGetLoggerAdapter(MSLogger logger) : NuGet.Common.ILogger
    {
        private readonly MSLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        /// <summary>
        /// Performs log debug.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogDebug(string data) => LogDebugData(data);
        /// <summary>
        /// Performs log verbose.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogVerbose(string data) => LogVerboseData(data);
        /// <summary>
        /// Performs log information.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogInformation(string data) => LogInformationData(data);
        /// <summary>
        /// Performs log minimal.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogMinimal(string data) => LogMinimalData(data);
        /// <summary>
        /// Performs log warning.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogWarning(string data) => LogWarningData(data);
        /// <summary>
        /// Performs log error.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogError(string data) => LogErrorData(data);
        /// <summary>
        /// Performs log information summary.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogInformationSummary(string data) => LogInformationSummaryData(data);
        /// <summary>
        /// Performs log error summary.
        /// </summary>
        /// <param name="data">The data.</param>

        public void LogErrorSummary(string data) => LogErrorSummaryData(data);
        /// <summary>
        /// Performs log.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="data">The data.</param>

        public void Log(NuGet.Common.LogLevel level, string data)
        {
            var msLogLevel = level switch
            {
                NuGet.Common.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                NuGet.Common.LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Trace,
                NuGet.Common.LogLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                NuGet.Common.LogLevel.Minimal => Microsoft.Extensions.Logging.LogLevel.Information,
                NuGet.Common.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                NuGet.Common.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                _ => Microsoft.Extensions.Logging.LogLevel.Information
            };

            LogGenericData(msLogLevel, data);
        }
        /// <summary>
        /// Gets log asynchronously.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <param name="data">The data.</param>
        /// <returns>The result of the operation.</returns>

        public async Task LogAsync(NuGet.Common.LogLevel level, string data)
        {
            Log(level, data);
            await Task.CompletedTask;
        }
        /// <summary>
        /// Performs log.
        /// </summary>
        /// <param name="message">The message.</param>

        public void Log(NuGet.Common.ILogMessage message) => Log(message.Level, message.Message);
        /// <summary>
        /// Gets log asynchronously.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The result of the operation.</returns>

        public async Task LogAsync(NuGet.Common.ILogMessage message)
        {
            Log(message);
            await Task.CompletedTask;
        }

        #region LoggerMessage Delegates

        [LoggerMessage(Level = LogLevel.Debug, Message = "{data}")]
        private partial void LogDebugData(string data);

        [LoggerMessage(Level = LogLevel.Trace, Message = "{data}")]
        private partial void LogVerboseData(string data);

        [LoggerMessage(Level = LogLevel.Information, Message = "{data}")]
        private partial void LogInformationData(string data);

        [LoggerMessage(Level = LogLevel.Information, Message = "{data}")]
        private partial void LogMinimalData(string data);

        [LoggerMessage(Level = LogLevel.Warning, Message = "{data}")]
        private partial void LogWarningData(string data);

        [LoggerMessage(Level = LogLevel.Error, Message = "{data}")]
        private partial void LogErrorData(string data);

        [LoggerMessage(Level = LogLevel.Information, Message = "{data}")]
        private partial void LogInformationSummaryData(string data);

        [LoggerMessage(Level = LogLevel.Error, Message = "{data}")]
        private partial void LogErrorSummaryData(string data);

        [LoggerMessage(Message = "{data}")]
        private partial void LogGenericData(LogLevel level, string data);

        #endregion
    }
}