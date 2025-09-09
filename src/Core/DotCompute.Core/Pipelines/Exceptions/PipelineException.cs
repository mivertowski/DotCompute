// <copyright file="PipelineException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Pipelines.Exceptions;

/// <summary>
/// Base exception class for all pipeline-related errors.
/// Provides comprehensive error context including pipeline and stage identification.
/// </summary>
public class PipelineException : Exception
{
    /// <summary>
    /// Gets the error code for categorization and handling.
    /// Used to identify specific error conditions programmatically.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the pipeline identifier if available.
    /// Identifies which pipeline instance encountered the error.
    /// </summary>
    public string? PipelineId { get; }

    /// <summary>
    /// Gets the stage identifier if available.
    /// Identifies the specific pipeline stage where the error occurred.
    /// </summary>
    public string? StageId { get; }

    /// <summary>
    /// Gets additional error context as key-value pairs.
    /// Contains runtime information relevant to understanding the error.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineException class.
    /// </summary>
    public PipelineException()
    {
        ErrorCode = "UNKNOWN_ERROR";
    }

    /// <summary>
    /// Initializes a new instance of the PipelineException class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PipelineException(string message) : base(message)
    {
        ErrorCode = "UNKNOWN_ERROR";
    }

    /// <summary>
    /// Initializes a new instance of the PipelineException class with message and error code.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The specific error code.</param>
    public PipelineException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the PipelineException class with message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineException(string message, Exception innerException)

        : base(message, innerException)
    {
        ErrorCode = "UNKNOWN_ERROR";
    }

    /// <summary>
    /// Initializes a new instance of the PipelineException class with message, error code, and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The specific error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the PipelineException class with full context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The specific error code.</param>
    /// <param name="pipelineId">The pipeline identifier.</param>
    /// <param name="stageId">The stage identifier.</param>
    /// <param name="context">Additional error context.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineException(
        string message,
        string errorCode,
        string? pipelineId = null,
        string? stageId = null,
        IReadOnlyDictionary<string, object>? context = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        PipelineId = pipelineId;
        StageId = stageId;
        Context = context;
    }
}