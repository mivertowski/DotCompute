// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Models;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Stub implementation of NuGetPluginService for compilation.
/// </summary>
public sealed class NuGetPluginService : INuGetPluginService, IDisposable
{
    private readonly ILogger<NuGetPluginService> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginService"/> class.
    /// </summary>
    public NuGetPluginService(ILogger<NuGetPluginService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads plugins from NuGet packages asynchronously.
    /// </summary>
    public static Task<IEnumerable<IAlgorithmPlugin>> LoadPluginsFromPackagesAsync(
        IEnumerable<string> packageIds,

        CancellationToken cancellationToken = default)
        // Stub implementation - returns empty collection
        => Task.FromResult(Enumerable.Empty<IAlgorithmPlugin>());

    /// <summary>
    /// Loads plugins from a NuGet package.
    /// </summary>
    public Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
        // Stub implementation - returns 0
        => Task.FromResult(0);

    /// <summary>
    /// Updates plugins from a NuGet package to the latest version.
    /// </summary>
    public Task<int> UpdateNuGetPackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
        // Stub implementation - returns 0
        => Task.FromResult(0);

    /// <summary>
    /// Clears the NuGet package cache.
    /// </summary>
    public Task ClearNuGetCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default)
        // Stub implementation - returns completed task
        => Task.CompletedTask;

    /// <summary>
    /// Gets information about cached NuGet packages.
    /// </summary>
    public Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default)
        // Stub implementation - returns empty array
        => Task.FromResult(Array.Empty<CachedPackageInfo>());

    /// <summary>
    /// Validates a NuGet package before loading it.
    /// </summary>
    public Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        // Stub implementation - returns valid result
        return Task.FromResult(new NuGetValidationResult
        {
            IsValid = true,
            PackageId = packageSource,
            Version = "1.0.0",
            ValidationIssue = null
        });
    }

    /// <summary>
    /// Disposes the service.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}