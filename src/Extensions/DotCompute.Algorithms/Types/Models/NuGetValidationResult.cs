
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.Models;

/// <summary>
/// Represents the result of NuGet package validation.
/// </summary>
public sealed class NuGetValidationResult
{
    /// <summary>
    /// Gets or sets the package identifier.
    /// </summary>
    public required string PackageId { get; set; }

    /// <summary>
    /// Gets or sets the package version.
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the package is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation issue description if validation failed.
    /// </summary>
    public string? ValidationIssue { get; set; }

    /// <summary>
    /// Gets or sets the number of assemblies in the package.
    /// </summary>
    public int AssemblyCount { get; set; }

    /// <summary>
    /// Gets or sets the number of dependencies.
    /// </summary>
    public int DependencyCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether security validation passed.
    /// </summary>
    public bool SecurityValidationPassed { get; set; }

    /// <summary>
    /// Gets or sets detailed security validation information.
    /// </summary>
    public string SecurityDetails { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets the time taken for validation.
    /// </summary>
    public TimeSpan ValidationTime { get; set; }

    /// <summary>
    /// Gets or sets the total size of the package in bytes.
    /// </summary>
    public long PackageSize { get; set; }
}