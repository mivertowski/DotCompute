// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Loading;

/// <summary>
/// Interface for NuGet package plugin operations.
/// </summary>
public interface INuGetPluginService
{
    /// <summary>
    /// Loads plugins from a NuGet package.
    /// </summary>
    /// <param name="packageSource">The path to the .nupkg file or package ID (with optional version).</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded.</returns>
    public Task<int> LoadPluginsFromNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates plugins from a NuGet package to the latest version.
    /// </summary>
    /// <param name="packageId">The package ID to update.</param>
    /// <param name="targetFramework">Target framework for assembly selection (optional, defaults to current framework).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of plugins loaded from the updated package.</returns>
    public Task<int> UpdateNuGetPackageAsync(
        string packageId,
        string? targetFramework = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the NuGet package cache.
    /// </summary>
    /// <param name="olderThan">Optional age filter - only clear packages older than this timespan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the cache clearing operation.</returns>
    public Task ClearNuGetCacheAsync(TimeSpan? olderThan = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about cached NuGet packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of cached NuGet package information.</returns>
    public Task<CachedPackageInfo[]> GetCachedNuGetPackagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a NuGet package before loading it.
    /// </summary>
    /// <param name="packageSource">The package source to validate.</param>
    /// <param name="targetFramework">Target framework for validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with details about the package.</returns>
    public Task<NuGetValidationResult> ValidateNuGetPackageAsync(
        string packageSource,
        string? targetFramework = null,
        CancellationToken cancellationToken = default);
}