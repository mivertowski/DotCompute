// <copyright file="NuGetPluginValidationResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Plugins.Loaders.NuGet.Results;

/// <summary>
/// Result of plugin validation.
/// Contains the outcome of plugin verification checks.
/// </summary>
public class NuGetPluginValidationResult
{
    /// <summary>
    /// Gets or sets whether the plugin is valid.
    /// True if all validation checks passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors.
    /// List of issues that prevent plugin usage.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets the validation warnings.
    /// Non-critical issues that should be addressed.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets the security validation result.
    /// Outcome of security checks on the plugin.
    /// </summary>
    public bool SecurityCheckPassed { get; set; }

    /// <summary>
    /// Gets or sets the compatibility validation result.
    /// Whether the plugin is compatible with the current system.
    /// </summary>
    public bool CompatibilityCheckPassed { get; set; }

    /// <summary>
    /// Gets or sets the dependency validation result.
    /// Whether all plugin dependencies can be satisfied.
    /// </summary>
    public bool DependencyCheckPassed { get; set; }

    /// <summary>
    /// Gets or sets the validation timestamp.
    /// When the validation was performed.
    /// </summary>
    public DateTimeOffset ValidatedAt { get; set; } = DateTimeOffset.UtcNow;
}