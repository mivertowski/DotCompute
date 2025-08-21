// <copyright file="NuGetPackageInfo.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// Information about a NuGet package for caching purposes.
/// Stores package metadata and caching information.
/// </summary>
public class NuGetPackageInfo
{
    /// <summary>
    /// Gets or sets the package ID.
    /// Unique identifier of the NuGet package.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the package version.
    /// Semantic version of the package.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets the package path.
    /// Local file system path to the extracted package.
    /// </summary>
    public string? PackagePath { get; set; }

    /// <summary>
    /// Gets or sets the package dependencies.
    /// List of other packages required by this package.
    /// </summary>
    public List<NuGetPackageDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the package metadata.
    /// Additional package information and properties.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets when the package was cached.
    /// Timestamp for cache validity checking.
    /// </summary>
    public DateTimeOffset CachedAt { get; set; }

    /// <summary>
    /// Gets or sets whether the package has been verified.
    /// Indicates if security and integrity checks have passed.
    /// </summary>
    public bool IsVerified { get; set; }
}