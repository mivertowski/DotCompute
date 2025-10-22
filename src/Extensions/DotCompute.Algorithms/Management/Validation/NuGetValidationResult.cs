
// <copyright file="NuGetValidationResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Algorithms.Management.Validation;

/// <summary>
/// Contains the results of NuGet package validation for algorithm plugins.
/// Provides comprehensive validation status including security, dependencies, and integrity checks.
/// </summary>
public sealed class NuGetValidationResult
{
    /// <summary>
    /// Gets or sets the NuGet package identifier.
    /// The unique name of the package in the NuGet repository.
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// Gets or sets the package version string.
    /// The semantic version of the validated package.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets or sets whether the package passed all validation checks.
    /// True if the package is safe to load and use.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets or sets the validation error message if validation failed.
    /// Null if the package is valid, otherwise contains the failure reason.
    /// </summary>
    public string? ValidationIssue { get; init; }

    /// <summary>
    /// Gets or sets the number of assemblies found in the package.
    /// Used to verify package contents match expectations.
    /// </summary>
    public int AssemblyCount { get; init; }

    /// <summary>
    /// Gets or sets the number of dependencies required by the package.
    /// Helps assess package complexity and potential conflicts.
    /// </summary>
    public int DependencyCount { get; init; }

    /// <summary>
    /// Gets or sets whether security validation passed.
    /// Includes signature verification, malware scanning, and permission checks.
    /// </summary>
    public bool SecurityValidationPassed { get; init; }

    /// <summary>
    /// Gets or sets detailed security validation information.
    /// Provides specifics about security checks performed and their results.
    /// </summary>
    public required string SecurityDetails { get; init; }

    /// <summary>
    /// Gets or sets validation warnings that don't prevent package use.
    /// Non-critical issues that should be reviewed but don't block loading.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Gets or sets the time taken to perform validation.
    /// Used for performance monitoring and optimization.
    /// </summary>
    public TimeSpan ValidationTime { get; init; }

    /// <summary>
    /// Gets or sets the package size in bytes.
    /// Used to enforce size limits and estimate resource requirements.
    /// </summary>
    public long PackageSize { get; init; }

    /// <summary>
    /// Gets whether the package has any warnings.
    /// Convenience property for checking warning presence.
    /// </summary>
    public bool HasWarnings => Warnings?.Count > 0;

    /// <summary>
    /// Gets a summary of the validation result.
    /// Provides a human-readable description of the validation outcome.
    /// </summary>
    public string Summary
        => IsValid
            ? $"Package {PackageId} v{Version} validated successfully"
            : $"Package {PackageId} v{Version} validation failed: {ValidationIssue}";
}