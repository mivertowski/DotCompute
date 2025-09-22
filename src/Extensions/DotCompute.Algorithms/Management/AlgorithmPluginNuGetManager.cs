// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Metadata;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Manages NuGet package operations for algorithm plugins including
/// downloading, caching, validation, and updating.
/// </summary>
public class AlgorithmPluginNuGetManager
{
    private readonly ILogger<AlgorithmPluginNuGetManager> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly string _packageCacheDirectory;
    private readonly HttpClient _httpClient;

    public AlgorithmPluginNuGetManager(
        ILogger<AlgorithmPluginNuGetManager> logger,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _packageCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotCompute", "PluginCache", "NuGet");

        Directory.CreateDirectory(_packageCacheDirectory);
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Downloads and extracts a NuGet package containing algorithm plugins.
    /// </summary>
    public async Task<string> DownloadPackageAsync(
        string packageId,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading NuGet package: {PackageId} {Version} from {Source}",
            packageId, version ?? "latest", source ?? "nuget.org");

        source ??= "https://api.nuget.org/v3/index.json";

        var repository = Repository.Factory.GetCoreV3(source);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        // Determine version to download
        NuGetVersion targetVersion;
        if (string.IsNullOrEmpty(version))
        {
            var versions = await resource.GetAllVersionsAsync(packageId, NullSourceCacheContext.Instance, NullLogger.Instance, cancellationToken);
            targetVersion = versions.OrderByDescending(v => v).FirstOrDefault()
                ?? throw new InvalidOperationException($"Package {packageId} not found");
        }
        else
        {
            targetVersion = NuGetVersion.Parse(version);
        }

        // Download package
        var packagePath = Path.Combine(_packageCacheDirectory, $"{packageId}.{targetVersion}.nupkg");

        if (!File.Exists(packagePath))
        {
            using var packageStream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var success = await resource.CopyNupkgToStreamAsync(
                packageId,
                targetVersion,
                packageStream,
                NullSourceCacheContext.Instance,
                NullLogger.Instance,
                cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to download package {packageId} {targetVersion}");
            }
        }

        // Extract package
        var extractPath = Path.Combine(_packageCacheDirectory, $"{packageId}.{targetVersion}");
        if (!Directory.Exists(extractPath))
        {
            using var packageReader = new PackageArchiveReader(packagePath);
            await packageReader.CopyFilesAsync(
                extractPath,
                packageReader.GetFiles(),
                ExtractPackageFilesAsync,
                NullLogger.Instance,
                cancellationToken);
        }

        _logger.LogInformation("Successfully downloaded and extracted package to: {Path}", extractPath);
        return extractPath;
    }

    /// <summary>
    /// Updates an installed NuGet package to the latest version.
    /// </summary>
    public async Task<bool> UpdatePackageAsync(
        string packageId,
        string currentVersion,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking for updates to package: {PackageId} (current: {CurrentVersion})",
            packageId, currentVersion);

        source ??= "https://api.nuget.org/v3/index.json";

        var repository = Repository.Factory.GetCoreV3(source);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        // Get all versions
        var versions = await resource.GetAllVersionsAsync(packageId, NullSourceCacheContext.Instance, NullLogger.Instance, cancellationToken);
        var latestVersion = versions.OrderByDescending(v => v).FirstOrDefault();

        if (latestVersion == null)
        {
            _logger.LogWarning("No versions found for package: {PackageId}", packageId);
            return false;
        }

        var currentNuGetVersion = NuGetVersion.Parse(currentVersion);
        if (latestVersion <= currentNuGetVersion)
        {
            _logger.LogInformation("Package {PackageId} is already up to date", packageId);
            return false;
        }

        _logger.LogInformation("Updating package {PackageId} from {CurrentVersion} to {NewVersion}",
            packageId, currentVersion, latestVersion);

        await DownloadPackageAsync(packageId, latestVersion.ToString(), source, cancellationToken);
        return true;
    }

    /// <summary>
    /// Clears the NuGet package cache.
    /// </summary>
    public async Task ClearCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing NuGet package cache (older than: {OlderThan})",
            olderThan?.ToString() ?? "all");

        await Task.Run(() =>
        {
            var cutoffTime = olderThan.HasValue
                ? DateTime.UtcNow - olderThan.Value
                : DateTime.MinValue;

            var files = Directory.GetFiles(_packageCacheDirectory, "*.nupkg", SearchOption.AllDirectories);
            var deletedCount = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoffTime)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogDebug("Deleted cached package: {File}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cached package: {File}", file);
                    }
                }
            }

            _logger.LogInformation("Deleted {Count} cached packages", deletedCount);
        }, cancellationToken);
    }

    /// <summary>
    /// Gets information about cached NuGet packages.
    /// </summary>
    public async Task<IEnumerable<CachedPackageInfo>> GetCachedPackagesAsync(CancellationToken cancellationToken = default)
    {
        var packages = new List<CachedPackageInfo>();

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(_packageCacheDirectory, "*.nupkg", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(file);
                    using var packageReader = new PackageArchiveReader(file);
                    var identity = packageReader.GetIdentity();

                    packages.Add(new CachedPackageInfo
                    {
                        PackageId = identity.Id,
                        Version = identity.Version.ToString(),
                        FilePath = file,
                        FileSize = fileInfo.Length,
                        CachedDate = fileInfo.CreationTimeUtc
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read package info from: {File}", file);
                }
            }
        }, cancellationToken);

        return packages.OrderBy(p => p.PackageId).ThenBy(p => p.Version);
    }

    /// <summary>
    /// Validates a NuGet package for security and integrity.
    /// </summary>
    public async Task<InternalNuGetValidationResult> ValidatePackageAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating NuGet package: {PackagePath}", packagePath);

        var result = new InternalNuGetValidationResult
        {
            IsValid = true,
            PackagePath = packagePath
        };

        try
        {
            using var packageReader = new PackageArchiveReader(packagePath);

            // Check package identity
            var identity = packageReader.GetIdentity();
            result.PackageId = identity.Id;
            result.Version = identity.Version.ToString();

            // Check for required DLLs
            var libItems = await packageReader.GetLibItemsAsync(cancellationToken);
            result.HasPluginAssemblies = libItems.Any(group => group.Items.Any(item => item.EndsWith(".dll")));

            if (!result.HasPluginAssemblies)
            {
                result.IsValid = false;
                result.ValidationErrors.Add("Package does not contain any assemblies");
            }

            // Check package size
            var fileInfo = new FileInfo(packagePath);
            if (fileInfo.Length > _options.MaxAssemblySize)
            {
                result.IsValid = false;
                result.ValidationErrors.Add($"Package exceeds maximum size of {_options.MaxAssemblySize} bytes");
            }

            _logger.LogInformation("Package validation completed. Valid: {IsValid}", result.IsValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate package: {PackagePath}", packagePath);
            result.IsValid = false;
            result.ValidationErrors.Add($"Validation failed: {ex.Message}");
        }

        return result;
    }

    private static string ExtractPackageFilesAsync(
        string sourceFile,
        string targetPath,
        Stream fileStream)
    {
        var targetFile = Path.Combine(targetPath, sourceFile);
        var targetDir = Path.GetDirectoryName(targetFile);

        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        using var targetStream = File.Create(targetFile);
        fileStream.CopyTo(targetStream);

        return targetFile;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Information about a cached NuGet package.
/// </summary>
public class CachedPackageInfo
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public required string FilePath { get; init; }
    public long FileSize { get; init; }
    public DateTime CachedDate { get; init; }
}

/// <summary>
/// Result of NuGet package validation for internal use.
/// </summary>
public class InternalNuGetValidationResult
{
    public bool IsValid { get; set; }
    public string? PackageId { get; set; }
    public string? Version { get; set; }
    public required string PackagePath { get; init; }
    public bool HasPluginAssemblies { get; set; }
    public List<string> ValidationErrors { get; } = new();
}