using System;
using System.Collections.Generic;

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Represents an error that occurred during pipeline execution.
/// </summary>
public sealed class PipelineError
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the stage where the error occurred.
    /// </summary>
    public string? StageId { get; init; }

    /// <summary>
    /// Gets the error severity.
    /// </summary>
    public required ErrorSeverity Severity { get; init; }

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the exception if available.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets additional error context.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    /// <summary>
    /// Gets suggested remediation steps.
    /// </summary>
    public IReadOnlyList<string>? RemediationSteps { get; init; }
}

/// <summary>
/// Error severity levels.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Informational, not an actual error.
    /// </summary>
    Information,

    /// <summary>
    /// Warning that doesn't prevent execution.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that affects results but allows continuation.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that prevents execution.
    /// </summary>
    Critical,

    /// <summary>
    /// Fatal error that corrupts state.
    /// </summary>
    Fatal
}

/// <summary>
/// Validation error for pipeline configuration.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets the validation error code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the path to the invalid configuration.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the invalid value if available.
    /// </summary>
    public object? InvalidValue { get; init; }

    /// <summary>
    /// Gets expected value or constraint.
    /// </summary>
    public string? ExpectedValue { get; init; }
}

/// <summary>
/// Validation warning for pipeline configuration.
/// </summary>
public sealed class ValidationWarning
{
    /// <summary>
    /// Gets the warning code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the path to the configuration.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the recommendation.
    /// </summary>
    public string? Recommendation { get; init; }

    /// <summary>
    /// Gets the potential impact.
    /// </summary>
    public string? PotentialImpact { get; init; }
}

/// <summary>
/// Exception thrown during pipeline operations.
/// </summary>
public class PipelineException : Exception
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the pipeline ID if available.
    /// </summary>
    public string? PipelineId { get; }

    /// <summary>
    /// Gets the stage ID if available.
    /// </summary>
    public string? StageId { get; }

    /// <summary>
    /// Gets additional error context.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineException class.
    /// </summary>
    public PipelineException(string message, string errorCode) 
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the PipelineException class with inner exception.
    /// </summary>
    public PipelineException(string message, string errorCode, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Initializes a new instance of the PipelineException class with full context.
    /// </summary>
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

/// <summary>
/// Exception thrown when pipeline validation fails.
/// </summary>
public sealed class PipelineValidationException : PipelineException
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public IReadOnlyList<ValidationWarning>? Warnings { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineValidationException class.
    /// </summary>
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

/// <summary>
/// Exception thrown when pipeline execution fails.
/// </summary>
public sealed class PipelineExecutionException : PipelineException
{
    /// <summary>
    /// Gets the execution errors.
    /// </summary>
    public IReadOnlyList<PipelineError> Errors { get; }

    /// <summary>
    /// Gets partial results if available.
    /// </summary>
    public PipelineExecutionResult? PartialResult { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineExecutionException class.
    /// </summary>
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

/// <summary>
/// Exception thrown when pipeline optimization fails.
/// </summary>
public sealed class PipelineOptimizationException : PipelineException
{
    /// <summary>
    /// Gets the optimization that failed.
    /// </summary>
    public OptimizationType FailedOptimization { get; }

    /// <summary>
    /// Gets the reason for failure.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Initializes a new instance of the PipelineOptimizationException class.
    /// </summary>
    public PipelineOptimizationException(
        string message,
        OptimizationType failedOptimization,
        string reason,
        string? pipelineId = null,
        Exception? innerException = null)
        : base(message, "PIPELINE_OPTIMIZATION_FAILED", pipelineId, null, null, innerException)
    {
        FailedOptimization = failedOptimization;
        Reason = reason;
    }
}