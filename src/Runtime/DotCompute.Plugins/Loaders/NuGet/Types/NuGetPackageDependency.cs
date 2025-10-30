// <copyright file="NuGetPackageDependency.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Types;

/// <summary>
/// Represents a NuGet package dependency.
/// Defines a package that must be available for the plugin to function correctly.
/// </summary>
public class NuGetPackageDependency
{
    /// <summary>
    /// Gets or sets the package ID.
    /// The unique identifier of the dependent package.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the version range.
    /// Specifies acceptable versions (e.g., "[1.0.0,2.0.0)").
    /// </summary>
    public required string VersionRange { get; set; }

    /// <summary>
    /// Gets or sets the target framework for this dependency.
    /// The .NET framework version this dependency targets.
    /// </summary>
    public string? TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets whether this dependency is optional.
    /// Indicates if the plugin can function without this dependency.
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Gets or sets the dependency exclusions.
    /// List of transitive dependencies to exclude.
    /// </summary>
    public IList<string> Exclude { get; } = [];
}
