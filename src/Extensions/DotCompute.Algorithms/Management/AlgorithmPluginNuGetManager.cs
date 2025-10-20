// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Management.Configuration;
using Microsoft.Extensions.Logging;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Manages NuGet package operations for algorithm plugins including
/// downloading, caching, validation, and updating.
/// </summary>
public partial class AlgorithmPluginNuGetManager
{
    private readonly ILogger<AlgorithmPluginNuGetManager> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly string _packageCacheDirectory;
    private readonly HttpClient _httpClient;
    /// <summary>
    /// Initializes a new instance of the AlgorithmPluginNuGetManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>

    public AlgorithmPluginNuGetManager(
        ILogger<AlgorithmPluginNuGetManager> logger,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _packageCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotCompute", "PluginCache", "NuGet");

        _ = Directory.CreateDirectory(_packageCacheDirectory);
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
        LogDownloadingPackage(packageId, version ?? "latest", source ?? "nuget.org");

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
            _ = await packageReader.CopyFilesAsync(
                extractPath,
                packageReader.GetFiles(),
                ExtractPackageFiles,
                NullLogger.Instance,
                cancellationToken);
        }

        LogPackageExtracted(extractPath);
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
        LogCheckingForUpdates(packageId, currentVersion);

        source ??= "https://api.nuget.org/v3/index.json";

        var repository = Repository.Factory.GetCoreV3(source);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        // Get all versions
        var versions = await resource.GetAllVersionsAsync(packageId, NullSourceCacheContext.Instance, NullLogger.Instance, cancellationToken);
        var latestVersion = versions.OrderByDescending(v => v).FirstOrDefault();

        if (latestVersion == null)
        {
            LogNoVersionsFound(packageId);
            return false;
        }

        var currentNuGetVersion = NuGetVersion.Parse(currentVersion);
        if (latestVersion <= currentNuGetVersion)
        {
            LogPackageUpToDate(packageId);
            return false;
        }

        LogUpdatingPackage(packageId, currentVersion, latestVersion.ToString());

        _ = await DownloadPackageAsync(packageId, latestVersion.ToString(), source, cancellationToken);
        return true;
    }

    /// <summary>
    /// Clears the NuGet package cache.
    /// </summary>
    public async Task ClearCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
    {
        LogClearingCache(olderThan?.ToString() ?? "all");

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
                        LogDeletedCachedPackage(file);
                    }
                    catch (Exception ex)
                    {
                        LogFailedToDeletePackage(ex, file);
                    }
                }
            }

            LogDeletedPackagesCount(deletedCount);
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
                    LogFailedToReadPackageInfo(ex, file);
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
        LogValidatingPackage(packagePath);

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
            result.HasPluginAssemblies = libItems.Any(group => group.Items.Any(item => item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));

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

            LogValidationCompleted(result.IsValid);
        }
        catch (Exception ex)
        {
            LogValidationFailed(ex, packagePath);
            result.IsValid = false;
            result.ValidationErrors.Add($"Validation failed: {ex.Message}");
        }

        return result;
    }

    private static string ExtractPackageFiles(
        string sourceFile,
        string targetPath,
        Stream fileStream)
    {
        var targetFile = Path.Combine(targetPath, sourceFile);
        var targetDir = Path.GetDirectoryName(targetFile);

        if (!string.IsNullOrEmpty(targetDir))
        {
            _ = Directory.CreateDirectory(targetDir);
        }

        using var targetStream = File.Create(targetFile);
        fileStream.CopyTo(targetStream);

        return targetFile;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose() => _httpClient?.Dispose();

    // LoggerMessage delegates
    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Downloading NuGet package: {PackageId} {Version} from {Source}")]
    partial void LogDownloadingPackage(string packageId, string version, string source);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Successfully downloaded and extracted package to: {Path}")]
    partial void LogPackageExtracted(string path);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Checking for updates to package: {PackageId} (current: {CurrentVersion})")]
    partial void LogCheckingForUpdates(string packageId, string currentVersion);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "No versions found for package: {PackageId}")]
    partial void LogNoVersionsFound(string packageId);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package {PackageId} is already up to date")]
    partial void LogPackageUpToDate(string packageId);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Updating package {PackageId} from {CurrentVersion} to {NewVersion}")]
    partial void LogUpdatingPackage(string packageId, string currentVersion, string newVersion);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Clearing NuGet package cache (older than: {OlderThan})")]
    partial void LogClearingCache(string olderThan);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Deleted cached package: {File}")]
    partial void LogDeletedCachedPackage(string file);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Failed to delete cached package: {File}")]
    partial void LogFailedToDeletePackage(Exception ex, string file);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Deleted {Count} cached packages")]
    partial void LogDeletedPackagesCount(int count);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Failed to read package info from: {File}")]
    partial void LogFailedToReadPackageInfo(Exception ex, string file);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Validating NuGet package: {PackagePath}")]
    partial void LogValidatingPackage(string packagePath);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Package validation completed. Valid: {IsValid}")]
    partial void LogValidationCompleted(bool isValid);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to validate package: {PackagePath}")]
    partial void LogValidationFailed(Exception ex, string packagePath);
}

/// <summary>
/// Information about a cached NuGet package.
/// </summary>
public class CachedPackageInfo
{
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    /// <value>The package id.</value>
    public required string PackageId { get; init; }
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    /// <value>The version.</value>
    public required string Version { get; init; }
    /// <summary>
    /// Gets or sets the file path.
    /// </summary>
    /// <value>The file path.</value>
    public required string FilePath { get; init; }
    /// <summary>
    /// Gets or sets the file size.
    /// </summary>
    /// <value>The file size.</value>
    public long FileSize { get; init; }
    /// <summary>
    /// Gets or sets the cached date.
    /// </summary>
    /// <value>The cached date.</value>
    public DateTime CachedDate { get; init; }
}

/// <summary>
/// Result of NuGet package validation for internal use.
/// </summary>
public class InternalNuGetValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; }
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    /// <value>The package id.</value>
    public string? PackageId { get; set; }
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    /// <value>The version.</value>
    public string? Version { get; set; }
    /// <summary>
    /// Gets or sets the package path.
    /// </summary>
    /// <value>The package path.</value>
    public required string PackagePath { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether plugin assemblies.
    /// </summary>
    /// <value>The has plugin assemblies.</value>
    public bool HasPluginAssemblies { get; set; }
    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    /// <value>The validation errors.</value>
    public IList<string> ValidationErrors { get; } = [];
}