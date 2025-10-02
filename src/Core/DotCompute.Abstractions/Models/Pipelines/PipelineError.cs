// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Models.Pipelines;

/// <summary>
/// Represents an error that occurred during pipeline execution
/// </summary>
public class PipelineError
{
    /// <summary>
    /// Gets or sets the error code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the pipeline stage where the error occurred
    /// </summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier of the pipeline stage where the error occurred
    /// </summary>
    public string StageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the severity level of the error
    /// </summary>
    public Types.ErrorSeverity Severity { get; set; } = Types.ErrorSeverity.Error;

    /// <summary>
    /// Gets or sets the type of error that occurred
    /// </summary>
    public PipelineErrorType ErrorType { get; set; } = PipelineErrorType.ExecutionError;

    /// <summary>
    /// Gets or sets the timestamp when the error occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the exception that caused this error, if any
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets additional context information
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this error is recoverable
    /// </summary>
    public bool IsRecoverable { get; set; } = true;

    /// <summary>
    /// Gets or sets the suggested recovery actions
    /// </summary>
    public List<string> SuggestedActions { get; set; } = [];
}

