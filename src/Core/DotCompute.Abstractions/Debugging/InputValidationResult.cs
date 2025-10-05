// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents the result of input validation for a kernel.
/// </summary>
public sealed class InputValidationResult
{
    /// <summary>
    /// Gets whether the input validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the list of validation issues found.
    /// </summary>
    public IReadOnlyList<string> Issues { get; set; } = [];

    /// <summary>
    /// Gets the number of inputs validated.
    /// </summary>
    public int InputCount { get; init; }

    /// <summary>
    /// Gets validation warnings that don't prevent execution.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets validation recommendations for improvement.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets the time taken for validation.
    /// </summary>
    public TimeSpan ValidationTime { get; init; }

    /// <summary>
    /// Gets detailed validation metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
