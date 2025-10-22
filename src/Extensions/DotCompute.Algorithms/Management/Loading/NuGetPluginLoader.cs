
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Loads plugins from NuGet packages with modern .NET 9 async patterns.
/// </summary>
/// <param name="logger">Logger instance for diagnostics.</param>
/// <param name="cacheDirectory">Directory for caching extracted packages.</param>
public sealed partial class NuGetPluginLoader(ILogger<NuGetPluginLoader> logger, string? cacheDirectory = null) : IDisposable
{
    private readonly ILogger<NuGetPluginLoader> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _cacheDirectory = cacheDirectory ?? Path.Combine(Path.GetTempPath(), "DotCompute", "NuGetPluginCache");
    private bool _disposed;

    /// <summary>
    /// Loads a plugin from a NuGet package asynchronously.
    /// </summary>
    /// <param name="packagePath">Path to the .nupkg file.</param>
    /// <param name="targetFramework">Target framework moniker (e.g., net9.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Load result containing extracted assemblies and metadata.</returns>
    public async Task<NuGetPackageLoadResult> LoadPackageAsync(
        string packagePath,
        string targetFramework,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);

        LogLoadingPackage(packagePath, targetFramework);

        try
        {
            // Extract package to cache directory
            var extractPath = Path.Combine(_cacheDirectory, Path.GetFileNameWithoutExtension(packagePath));
            _ = Directory.CreateDirectory(extractPath);

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(packagePath);
                archive.ExtractToDirectory(extractPath, overwriteFiles: true);
            }, cancellationToken);

            // Find assemblies for target framework
            var libPath = Path.Combine(extractPath, "lib", targetFramework);
            var assemblies = Directory.Exists(libPath)
                ? Directory.GetFiles(libPath, "*.dll", SearchOption.TopDirectoryOnly)
                : [];

            // Parse .nuspec for metadata
            var nuspecPath = Directory.GetFiles(extractPath, "*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();
            var packageId = nuspecPath != null ? Path.GetFileNameWithoutExtension(nuspecPath) : Path.GetFileNameWithoutExtension(packagePath);

            LogLoadedPackage(packageId, assemblies.Length);

            return new NuGetPackageLoadResult
            {
                PackageId = packageId,
                LoadedAssemblyPaths = assemblies,
                ExtractedPath = extractPath,
                Success = true
            };
        }
        catch (Exception ex)
        {
            LogFailedToLoadPackage(ex, packagePath);
            return new NuGetPackageLoadResult
            {
                PackageId = Path.GetFileNameWithoutExtension(packagePath),
                LoadedAssemblyPaths = [],
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Clears the NuGet cache directory.
    /// </summary>
    public void ClearCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
                LogClearedCache(_cacheDirectory);
            }
        }
        catch (Exception ex)
        {
            LogFailedToClearCache(ex, _cacheDirectory);
        }
    }

    /// <summary>
    /// Asynchronously clears the NuGet cache directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, recursive: true);
                    LogClearedCache(_cacheDirectory);
                }
            }
            catch (Exception ex)
            {
                LogFailedToClearCache(ex, _cacheDirectory);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the list of cached package directories.
    /// </summary>
    /// <returns>List of cached package paths.</returns>
    public IReadOnlyList<string> GetCachedPackages()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                return Directory.GetDirectories(_cacheDirectory)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList()!;
            }
        }
        catch (Exception ex)
        {
            LogFailedToEnumeratePackages(ex, _cacheDirectory);
        }

        return [];
    }

    #region LoggerMessage Delegates

    [LoggerMessage(Level = LogLevel.Information, Message = "Loading NuGet package from {PackagePath} for {Framework}")]
    private partial void LogLoadingPackage(string packagePath, string framework);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded NuGet package {PackageId} with {AssemblyCount} assemblies")]
    private partial void LogLoadedPackage(string packageId, int assemblyCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load NuGet package from {PackagePath}")]
    private partial void LogFailedToLoadPackage(Exception ex, string packagePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleared NuGet cache at {CacheDirectory}")]
    private partial void LogClearedCache(string cacheDirectory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to clear NuGet cache at {CacheDirectory}")]
    private partial void LogFailedToClearCache(Exception ex, string cacheDirectory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to enumerate cached packages at {CacheDirectory}")]
    private partial void LogFailedToEnumeratePackages(Exception ex, string cacheDirectory);

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Cleanup temporary files on dispose
        try
        {
            ClearCache();
        }
        catch
        {
            // Best effort cleanup
        }

        _disposed = true;
    }
}

/// <summary>
/// Represents a NuGet package identity with ID and version.
/// </summary>
/// <param name="Id">The package identifier.</param>
/// <param name="Version">The package version.</param>
public sealed record PackageIdentity(string Id, Version Version);

/// <summary>
/// Result of loading a NuGet package.
/// </summary>
public sealed class NuGetPackageLoadResult
{
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Gets or sets the package identity (ID and version combined).
    /// </summary>
    public PackageIdentity? PackageIdentity { get; init; }

    /// <summary>
    /// Gets the resolved dependencies for this package.
    /// </summary>
    public IReadOnlyList<string> ResolvedDependencies { get; init; } = [];

    /// <summary>
    /// Gets or sets the security validation result for this package.
    /// </summary>
    public Models.SecurityValidationResult? SecurityValidationResult { get; init; }

    /// <summary>
    /// Gets the warnings generated during package load.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets or sets the time taken to load the package.
    /// </summary>
    public TimeSpan? LoadTime { get; init; }

    /// <summary>
    /// Gets or sets the paths to loaded assemblies.
    /// </summary>
    public required string[] LoadedAssemblyPaths { get; init; }

    /// <summary>
    /// Gets or sets the path where the package was extracted.
    /// </summary>
    public string? ExtractedPath { get; init; }

    /// <summary>
    /// Gets or sets whether the load operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets or sets the error message if the load failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets whether this package was loaded from cache.
    /// </summary>
    public bool FromCache { get; init; }

    /// <summary>
    /// Gets or sets the total size of the package in bytes.
    /// </summary>
    public long TotalSize { get; init; }
}
