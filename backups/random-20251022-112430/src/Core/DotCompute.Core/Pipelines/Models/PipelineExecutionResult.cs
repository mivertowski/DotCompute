// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Models.Pipelines;

namespace DotCompute.Core.Pipelines.Models;

/// <summary>
/// Result of pipeline execution in the Core pipeline system.
/// This is a simpler version compared to the comprehensive Abstractions version.
/// </summary>
public sealed class CorePipelineExecutionResult
{
    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the output data from the pipeline.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Outputs { get; init; }

    /// <summary>
    /// Gets execution metrics.
    /// </summary>
    public required PipelineExecutionMetrics Metrics { get; init; }

    /// <summary>
    /// Gets any errors that occurred during execution.
    /// </summary>
    public IReadOnlyList<PipelineError>? Errors { get; init; }

    /// <summary>
    /// Gets execution context information.
    /// </summary>
    public PipelineExecutionContext? Context { get; init; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracking.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the pipeline ID that was executed.
    /// </summary>
    public string? PipelineId { get; init; }

    /// <summary>
    /// Gets additional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Creates a successful pipeline execution result.
    /// </summary>
    /// <param name="outputs">The output data</param>
    /// <param name="metrics">Execution metrics</param>
    /// <param name="context">Execution context</param>
    /// <returns>A successful result</returns>
    public static CorePipelineExecutionResult CreateSuccess(
        IReadOnlyDictionary<string, object> outputs,
        PipelineExecutionMetrics metrics,
        PipelineExecutionContext? context = null)
    {
        return new CorePipelineExecutionResult
        {
            Success = true,
            Outputs = outputs,
            Metrics = metrics,
            Context = context,
            ExecutionTime = metrics.TotalExecutionTime,
            Errors = null
        };
    }

    /// <summary>
    /// Creates a failed pipeline execution result.
    /// </summary>
    /// <param name="errors">The errors that occurred</param>
    /// <param name="partialOutputs">Any partial outputs</param>
    /// <param name="metrics">Execution metrics</param>
    /// <param name="context">Execution context</param>
    /// <returns>A failed result</returns>
    public static CorePipelineExecutionResult CreateFailure(
        IReadOnlyList<PipelineError> errors,
        IReadOnlyDictionary<string, object>? partialOutputs = null,
        PipelineExecutionMetrics? metrics = null,
        PipelineExecutionContext? context = null)
    {
        return new CorePipelineExecutionResult
        {
            Success = false,
            Outputs = partialOutputs ?? new Dictionary<string, object>(),
            Metrics = metrics ?? new PipelineExecutionMetrics(),
            Context = context,
            ExecutionTime = metrics?.TotalExecutionTime ?? TimeSpan.Zero,
            Errors = errors
        };
    }
}