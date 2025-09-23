// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Models.Pipelines;

/// <summary>
/// Result of pipeline stage execution.
/// </summary>
public sealed class StageExecutionResult
{
    /// <summary>
    /// Gets or sets whether the stage execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the stage identifier.
    /// </summary>
    public required string StageId { get; set; }

    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the output data produced by the stage.
    /// </summary>
    public required Dictionary<string, object> OutputData { get; set; }

    /// <summary>
    /// Gets or sets the amount of memory used during execution.
    /// </summary>
    public long MemoryUsed { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets any error that occurred during execution.
    /// </summary>
    public Exception? Error { get; set; }

    /// <summary>
    /// Gets or sets whether this stage was skipped during execution.
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    /// Gets or sets additional execution metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Result of stage validation.
/// </summary>
public sealed class StageValidationResult
{
    /// <summary>
    /// Gets or sets whether the stage configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets validation issues.
    /// </summary>
    public List<DotCompute.Abstractions.Validation.ValidationIssue>? Issues { get; set; }

    /// <summary>
    /// Gets or sets validation errors.
    /// </summary>
    public IReadOnlyList<string>? Errors { get; set; }

    /// <summary>
    /// Gets or sets validation warnings.
    /// </summary>
    public IReadOnlyList<string>? Warnings { get; set; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful validation result</returns>
    public static StageValidationResult Success()
    {
        return new StageValidationResult { IsValid = true };
    }

    /// <summary>
    /// Creates a failed validation result with errors.
    /// </summary>
    /// <param name="errors">The validation errors</param>
    /// <returns>A failed validation result</returns>
    public static StageValidationResult Failure(params string[] errors)
    {
        return new StageValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }
}


/// <summary>
/// Default implementation of stage metrics.
/// </summary>
public sealed class DefaultStageMetrics : IStageMetrics
{
    /// <inheritdoc />
    public required string StageId { get; set; }

    /// <inheritdoc />
    public required string StageName { get; set; }

    /// <inheritdoc />
    public long ExecutionCount { get; set; }

    /// <inheritdoc />
    public TimeSpan AverageExecutionTime { get; set; }

    /// <inheritdoc />
    public TimeSpan MinExecutionTime { get; set; }

    /// <inheritdoc />
    public TimeSpan MaxExecutionTime { get; set; }

    /// <inheritdoc />
    public TimeSpan TotalExecutionTime { get; set; }

    /// <inheritdoc />
    public long AverageMemoryUsage { get; set; }

    /// <inheritdoc />
    public double SuccessRate { get; set; }

    /// <inheritdoc />
    public long ErrorCount { get; set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, double> CustomMetrics { get; set; } = new Dictionary<string, double>();
}

// Note: PipelineError is already defined in PipelineError.cs

/// <summary>
/// Pipeline execution metrics.
/// </summary>
public sealed class PipelineExecutionMetrics
{
    /// <summary>
    /// Gets or sets the unique identifier for this execution.
    /// </summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the pipeline that was executed.
    /// </summary>
    public string PipelineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start time of pipeline execution.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time of pipeline execution.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets the total execution duration.
    /// </summary>
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

    /// <summary>
    /// Gets or sets whether the execution completed successfully.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the peak memory usage.
    /// </summary>
    public long PeakMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the number of stages executed.
    /// </summary>
    public int StageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of stage executions.
    /// </summary>
    public int StageExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of parallel stages.
    /// </summary>
    public int ParallelStageCount { get; set; }

    /// <summary>
    /// Gets or sets the number of parallel executions.
    /// </summary>
    public int ParallelExecutions { get; set; }

    /// <summary>
    /// Gets or sets the average stage execution time.
    /// </summary>
    public TimeSpan AverageStageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the time spent in computation (excluding transfers and overhead).
    /// </summary>
    public TimeSpan ComputationTime { get; set; }

    /// <summary>
    /// Gets or sets the time spent in data transfers.
    /// </summary>
    public TimeSpan DataTransferTime { get; set; }

    /// <summary>
    /// Gets or sets the pipeline efficiency score.
    /// </summary>
    public double EfficiencyScore { get; set; }

    /// <summary>
    /// Gets or sets the execution status.
    /// </summary>
    public ExecutionStatus ExecutionStatus { get; set; }

    /// <summary>
    /// Gets or sets memory usage statistics.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets compute utilization percentage.
    /// </summary>
    public double ComputeUtilization { get; set; }

    /// <summary>
    /// Gets or sets memory bandwidth utilization percentage.
    /// </summary>
    public double MemoryBandwidthUtilization { get; set; }

    /// <summary>
    /// Gets or sets execution times for individual stages.
    /// </summary>
    public IDictionary<string, double> StageExecutionTimes { get; set; } = new Dictionary<string, double>();

    /// <summary>
    /// Gets or sets the throughput in operations per second.
    /// </summary>
    public double Throughput { get; set; }

    /// <summary>
    /// Gets or sets additional metrics.
    /// </summary>
    public Dictionary<string, object>? AdditionalMetrics { get; set; }
}

/// <summary>
/// Interface for pipeline metrics.
/// </summary>
public interface IPipelineMetrics
{
    /// <summary>
    /// Gets the pipeline identifier.
    /// </summary>
    public string PipelineId { get; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the peak memory usage.
    /// </summary>
    public long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets the number of stages.
    /// </summary>
    public int StageCount { get; }

    /// <summary>
    /// Gets stage-specific metrics.
    /// </summary>
    public IReadOnlyList<IStageMetrics> StageMetrics { get; }
}