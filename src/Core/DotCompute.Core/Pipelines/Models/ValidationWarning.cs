// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Models;

/// <summary>
/// Represents a validation warning that occurred during pipeline validation
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Gets or sets the validation rule that generated the warning
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the warning message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the property or field that generated the warning
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the value that triggered the warning
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the warning code for programmatic handling
    /// </summary>
    public string? WarningCode { get; set; }

    /// <summary>
    /// Gets or sets additional context information
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the warning was generated
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets recommended actions to address the warning
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this warning can be safely ignored
    /// </summary>
    public bool CanIgnore { get; set; } = true;

    /// <summary>
    /// Gets or sets the potential impact if the warning is ignored
    /// </summary>
    public string? PotentialImpact { get; set; }
}