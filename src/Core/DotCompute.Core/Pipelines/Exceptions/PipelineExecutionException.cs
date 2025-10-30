// <copyright file="PipelineExecutionException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Abstractions.Pipelines.Results;

namespace DotCompute.Core.Pipelines.Exceptions;

/// <summary>
/// Exception thrown when pipeline execution fails.
/// Contains execution errors and any partial results that were produced before failure.
/// </summary>
public sealed class PipelineExecutionException : PipelineException
{
    /// <summary>
    /// Gets the execution errors encountered during pipeline execution.
    /// Provides detailed information about each error that occurred.
    /// </summary>
    public IReadOnlyList<PipelineError> Errors { get; }

    /// <summary>
    /// Gets partial results if available.
    /// Contains any successfully computed results before the failure occurred.
    /// </summary>
    public PipelineExecutionResult? PartialResult { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineExecutionException class.
    /// </summary>
    public PipelineExecutionException()
        : base("Pipeline execution failed", "PIPELINE_EXECUTION_FAILED")
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the PipelineExecutionException class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PipelineExecutionException(string message)

        : base(message, "PIPELINE_EXECUTION_FAILED")
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the PipelineExecutionException class with message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineExecutionException(string message, Exception innerException)

        : base(message, "PIPELINE_EXECUTION_FAILED", innerException)
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the PipelineExecutionException class with full context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="pipelineId">The pipeline identifier.</param>
    /// <param name="errors">The execution errors.</param>
    /// <param name="partialResult">Any partial results produced before failure.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineExecutionException(
        string message,
        string pipelineId,
        IReadOnlyList<PipelineError> errors,
        PipelineExecutionResult? partialResult = null,
        Exception? innerException = null)
        : base(message, "PIPELINE_EXECUTION_FAILED", pipelineId, null, null, innerException)
    {
        Errors = errors;
        PartialResult = partialResult;
    }
}
