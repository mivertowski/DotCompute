// <copyright file="PipelineError.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Core.Pipelines.Types;

namespace DotCompute.Core.Pipelines.Errors;

/// <summary>
/// Represents an error that occurred during pipeline execution.
/// Contains comprehensive error information including context, severity, and remediation guidance.
/// </summary>
public sealed class PipelineError
{
    /// <summary>
    /// Gets the error code for categorization and identification.
    /// Used for programmatic error handling and documentation references.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// Provides detailed description of the error condition.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the identifier of the stage where the error occurred.
    /// Null if the error is not associated with a specific stage.
    /// </summary>
    public string? StageId { get; init; }

    /// <summary>
    /// Gets the error severity level.
    /// Determines the impact and handling priority of the error.
    /// </summary>
    public required ErrorSeverity Severity { get; init; }

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// Used for error sequence analysis and debugging.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the underlying exception if available.
    /// Provides access to the original exception for detailed diagnostics.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets additional error context as key-value pairs.
    /// Contains runtime values and state information relevant to the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Gets suggested remediation steps to resolve the error.
    /// Provides actionable guidance for error recovery.
    /// </summary>
    public IReadOnlyList<string>? RemediationSteps { get; init; }
}