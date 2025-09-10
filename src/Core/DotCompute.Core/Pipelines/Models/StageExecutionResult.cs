// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Models;

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
    /// Gets or sets any error that occurred during execution.
    /// </summary>
    public Exception? Error { get; set; }

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
}

/// <summary>
/// Interface for stage performance metrics.
/// </summary>
public interface IStageMetrics
{
    /// <summary>
    /// Gets the stage identifier.
    /// </summary>
    string StageId { get; }

    /// <summary>
    /// Gets the stage name.
    /// </summary>
    string StageName { get; }

    /// <summary>
    /// Gets the total number of executions.
    /// </summary>
    int ExecutionCount { get; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the average execution time.
    /// </summary>
    TimeSpan AverageExecutionTime { get; }

    /// <summary>
    /// Gets the memory usage in bytes.
    /// </summary>
    long MemoryUsage { get; }

    /// <summary>
    /// Gets the throughput in items per second.
    /// </summary>
    double ThroughputItemsPerSecond { get; }
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
    public int ExecutionCount { get; set; }

    /// <inheritdoc />
    public TimeSpan TotalExecutionTime { get; set; }

    /// <inheritdoc />
    public TimeSpan AverageExecutionTime { get; set; }

    /// <inheritdoc />
    public long MemoryUsage { get; set; }

    /// <inheritdoc />
    public double ThroughputItemsPerSecond { get; set; }
}

// Note: PipelineError is already defined in PipelineError.cs

/// <summary>
/// Pipeline execution metrics.
/// </summary>
public sealed class PipelineExecutionMetrics
{
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
    /// Gets or sets the number of parallel stages.
    /// </summary>
    public int ParallelStageCount { get; set; }

    /// <summary>
    /// Gets or sets the average stage execution time.
    /// </summary>
    public TimeSpan AverageStageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the pipeline efficiency score.
    /// </summary>
    public double EfficiencyScore { get; set; }

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
    string PipelineId { get; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    TimeSpan TotalExecutionTime { get; }

    /// <summary>
    /// Gets the peak memory usage.
    /// </summary>
    long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets the number of stages.
    /// </summary>
    int StageCount { get; }

    /// <summary>
    /// Gets stage-specific metrics.
    /// </summary>
    IReadOnlyList<IStageMetrics> StageMetrics { get; }
}