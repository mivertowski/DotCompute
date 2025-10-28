// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Services.Types;

/// <summary>
/// Result of kernel validation operation
/// </summary>
public sealed class KernelValidationResult
{
    /// <summary>
    /// Gets or sets whether the kernel is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the kernel name
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets validation messages
    /// </summary>
    public IList<string> Messages { get; } = [];

    /// <summary>
    /// Gets or sets validation warnings
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets or sets validation errors
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets or sets additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];

    /// <summary>
    /// Gets or sets the validation timestamp
    /// </summary>
    public DateTimeOffset ValidationTime { get; set; } = DateTimeOffset.UtcNow;
}