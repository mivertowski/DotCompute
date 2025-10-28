// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Validation;

/// <summary>
/// Plugin validation result.
/// </summary>
public sealed class PluginValidationResult
{
    /// <summary>
    /// Gets or sets whether the plugin is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}