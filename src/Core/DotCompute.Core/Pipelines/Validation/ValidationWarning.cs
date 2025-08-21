// <copyright file="ValidationWarning.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Pipelines.Validation;

/// <summary>
/// Represents a validation warning for pipeline configuration.
/// Indicates potential issues that don't prevent execution but may affect performance or behavior.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Gets the warning code for categorization.
    /// Used for filtering and suppressing specific warning types.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the warning message describing the potential issue.
    /// Explains what might be problematic with the current configuration.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the path to the configuration element that triggered the warning.
    /// Uses dot notation to identify the specific configuration property.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the recommendation for addressing the warning.
    /// Provides actionable advice to improve the configuration.
    /// </summary>
    public string? Recommendation { get; init; }

    /// <summary>
    /// Gets the description of potential impact if the warning is ignored.
    /// Helps assess the risk of proceeding without addressing the warning.
    /// </summary>
    public string? PotentialImpact { get; init; }
}