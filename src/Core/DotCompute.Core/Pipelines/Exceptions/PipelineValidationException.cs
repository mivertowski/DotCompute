// <copyright file="PipelineValidationException.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Core.Pipelines.Validation;

namespace DotCompute.Core.Pipelines.Exceptions;

/// <summary>
/// Exception thrown when pipeline validation fails.
/// Contains detailed validation errors and warnings that prevented pipeline execution.
/// </summary>
public sealed class PipelineValidationException : PipelineException
{
    /// <summary>
    /// Gets the validation errors that caused the exception.
    /// These errors must be resolved before the pipeline can execute.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Gets the validation warnings encountered during validation.
    /// Warnings don't prevent execution but indicate potential issues.
    /// </summary>
    public IReadOnlyList<ValidationWarning>? Warnings { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineValidationException class.
    /// </summary>
    public PipelineValidationException()
        : base("Pipeline validation failed", "PIPELINE_VALIDATION_FAILED")
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the PipelineValidationException class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public PipelineValidationException(string message) 
        : base(message, "PIPELINE_VALIDATION_FAILED")
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the PipelineValidationException class with message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public PipelineValidationException(string message, Exception innerException) 
        : base(message, "PIPELINE_VALIDATION_FAILED", innerException)
    {
        Errors = [];
    }

    /// <summary>
    /// Initializes a new instance of the PipelineValidationException class with validation results.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errors">The validation errors.</param>
    /// <param name="warnings">The validation warnings.</param>
    public PipelineValidationException(
        string message,
        IReadOnlyList<ValidationError> errors,
        IReadOnlyList<ValidationWarning>? warnings = null)
        : base(message, "PIPELINE_VALIDATION_FAILED")
    {
        Errors = errors;
        Warnings = warnings;
    }
}