// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Models;

/// <summary>
/// Result of pipeline stage execution in the Core pipeline system.
/// Composes the base StageExecutionResult with additional Core-specific functionality.
/// </summary>
public sealed class StageExecutionResult
{
    /// <summary>
    /// Gets the base stage execution result.
    /// </summary>
    public required AbstractionsMemory.Models.Pipelines.StageExecutionResult BaseResult { get; init; }

    /// <summary>
    /// Gets whether the stage execution was successful.
    /// </summary>
    public bool Success => BaseResult.Success;

    /// <summary>
    /// Gets the stage identifier.
    /// </summary>
    public string StageId => BaseResult.StageId;

    /// <summary>
    /// Gets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime => BaseResult.ExecutionTime;

    /// <summary>
    /// Gets the output data produced by the stage.
    /// </summary>
    public Dictionary<string, object> OutputData => BaseResult.OutputData;

    /// <summary>
    /// Gets the amount of memory used during execution.
    /// </summary>
    public long MemoryUsed => BaseResult.MemoryUsed;

    /// <summary>
    /// Gets any error that occurred during execution.
    /// </summary>
    public Exception? Error => BaseResult.Error;

    /// <summary>
    /// Gets whether this stage was skipped during execution.
    /// </summary>
    public bool Skipped => BaseResult.Skipped;

    /// <summary>
    /// Gets additional execution metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata => BaseResult.Metadata;

    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    public string? StageName { get; set; }

    /// <summary>
    /// Gets or sets the input data processed by the stage.
    /// </summary>
    public Dictionary<string, object>? InputData { get; set; }

    /// <summary>
    /// Gets or sets stage-specific metrics.
    /// </summary>
    public StageMetrics? CoreMetrics { get; set; }

    /// <summary>
    /// Gets or sets the execution start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the execution end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Gets or sets the number of items processed.
    /// </summary>
    public long ItemsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the throughput in items per second.
    /// </summary>
    public double ThroughputItemsPerSecond { get; set; }

    /// <summary>
    /// Creates a successful stage execution result.
    /// </summary>
    /// <param name="stageId">The stage identifier</param>
    /// <param name="outputData">The output data</param>
    /// <param name="executionTime">The execution time</param>
    /// <returns>A successful result</returns>
    public static StageExecutionResult CreateSuccess(
        string stageId,
        Dictionary<string, object> outputData,
        TimeSpan executionTime)
    {
        var now = DateTimeOffset.UtcNow;
        var baseResult = new AbstractionsMemory.Models.Pipelines.StageExecutionResult
        {
            Success = true,
            StageId = stageId,
            ExecutionTime = executionTime,
            OutputData = outputData
        };

        return new StageExecutionResult
        {
            BaseResult = baseResult,
            EndTime = now,
            StartTime = now.Subtract(executionTime)
        };
    }

    /// <summary>
    /// Creates a failed stage execution result.
    /// </summary>
    /// <param name="stageId">The stage identifier</param>
    /// <param name="error">The error that occurred</param>
    /// <param name="executionTime">The execution time</param>
    /// <param name="partialOutputs">Any partial outputs</param>
    /// <returns>A failed result</returns>
    public static StageExecutionResult CreateFailure(
        string stageId,
        Exception error,
        TimeSpan executionTime,
        Dictionary<string, object>? partialOutputs = null)
    {
        var now = DateTimeOffset.UtcNow;
        var baseResult = new AbstractionsMemory.Models.Pipelines.StageExecutionResult
        {
            Success = false,
            StageId = stageId,
            ExecutionTime = executionTime,
            Error = error,
            OutputData = partialOutputs ?? []
        };

        return new StageExecutionResult
        {
            BaseResult = baseResult,
            EndTime = now,
            StartTime = now.Subtract(executionTime)
        };
    }
}