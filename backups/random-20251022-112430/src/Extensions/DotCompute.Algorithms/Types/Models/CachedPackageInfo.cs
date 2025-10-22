#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Models;

/// <summary>
/// Represents information about a cached NuGet package.
/// </summary>
public sealed class CachedPackageInfo
{
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the package version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the path to the cached package.
    /// </summary>
    public required string CachePath { get; set; }

    /// <summary>
    /// Gets or sets the size of the cached package in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the package was cached.
    /// </summary>
    public DateTime CachedTime { get; set; }

    /// <summary>
    /// Gets or sets the last access time of the cached package.
    /// </summary>
    public DateTime LastAccessTime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the package is currently in use.
    /// </summary>
    public bool IsInUse { get; set; }

    /// <summary>
    /// Gets or sets the target framework for which the package was cached.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the number of assemblies in the cached package.
    /// </summary>
    public int AssemblyCount { get; set; }
}