// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Linq.Pipelines.Models;
namespace DotCompute.Linq.Pipelines.Interfaces;
{
/// <summary>
/// Interface for kernel execution pipelines that manage GPU/CPU execution flow.
/// </summary>
public interface IKernelPipeline : IAsyncEnumerable<object>
{
    /// <summary>
    /// Gets the pipeline ID.
    /// </summary>
    string PipelineId { get; }
    /// Gets the pipeline configuration.
    PipelineConfiguration Configuration { get; }
    /// Executes the pipeline asynchronously.
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Task representing the pipeline execution.</returns>
    Task<PipelineExecutionResult> ExecuteAsync(CancellationToken cancellationToken = default);
    /// Executes the pipeline asynchronously with typed result.
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>Task representing the pipeline execution with typed result.</returns>
    Task<TResult> ExecuteAsync<TResult>(CancellationToken cancellationToken = default);
    /// Adds a stage to the pipeline.
    /// <param name="stage">The stage to add.</param>
    /// <returns>The pipeline for fluent chaining.</returns>
    IKernelPipeline AddStage(IPipelineStage stage);
    /// Optimizes the pipeline for execution.
    /// <returns>The optimized pipeline.</returns>
    IKernelPipeline Optimize();
    /// Gets the pipeline execution plan.
    /// <returns>The execution plan.</returns>
    PipelineExecutionPlan GetExecutionPlan();
    /// Sets the input data for the pipeline.
    /// <param name="data">The input data.</param>
    IKernelPipeline WithInput<T>(IEnumerable<T> data);
    /// Configures pipeline options.
    /// <param name="options">The options to configure.</param>
    IKernelPipeline WithOptions(PipelineOptions options);
    /// Gets the current pipeline status.
    PipelineStatus Status { get; }
    /// Event raised when pipeline execution starts.
    event EventHandler<PipelineEventArgs>? ExecutionStarted;
    /// Event raised when pipeline execution completes.
    event EventHandler<PipelineEventArgs>? ExecutionCompleted;
    /// Event raised when a pipeline stage completes.
    event EventHandler<StageCompletedEventArgs>? StageCompleted;
}
/// Interface for pipeline stages.
public interface IPipelineStage
    {
    /// Gets the stage name.
    string Name { get; }
    /// Executes the stage.
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stage output.</returns>
    Task<object> ExecuteAsync(object input, CancellationToken cancellationToken = default);
    /// Gets whether the stage can be parallelized.
    bool CanParallelize { get; }
    /// Gets the stage configuration.
    StageConfiguration Configuration { get; }
/// Pipeline configuration settings.
public class PipelineConfiguration
    {
    /// Gets or sets the maximum degree of parallelism.
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    /// Gets or sets whether to enable GPU acceleration.
    public bool EnableGpuAcceleration { get; set; } = true;
    /// Gets or sets the memory limit in bytes.
    public long MemoryLimit { get; set; } = long.MaxValue;
    /// Gets or sets whether to enable optimization.
    public bool EnableOptimization { get; set; } = true;
    /// Gets or sets the execution timeout.
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(30);
/// Pipeline execution result.
public class PipelineExecutionResult
    {
    /// Gets or sets whether the execution was successful.
    public bool Success { get; set; }
    /// Gets or sets the output data.
    public object? Output { get; set; }
    /// Gets or sets the execution time.
    public TimeSpan ExecutionTime { get; set; }
    /// Gets or sets the stages executed.
    public List<StageExecutionInfo> StagesExecuted { get; set; } = [];
    /// Gets or sets any errors that occurred.
    public List<Exception> Errors { get; set; } = [];
    /// Gets or sets execution metrics.
    public Dictionary<string, object> Metrics { get; set; } = [];
/// Pipeline options.
public class PipelineOptions
    {
    /// Gets or sets whether to use caching.
    public bool UseCache { get; set; } = true;
    /// Gets or sets whether to enable profiling.
    public bool EnableProfiling { get; set; } = false;
    /// Gets or sets the batch size.
    public int BatchSize { get; set; } = 1000;
    /// Gets or sets custom options.
    public Dictionary<string, object> CustomOptions { get; set; } = [];
/// Pipeline execution status.
public enum PipelineStatus
    {
    /// Pipeline is idle.
    Idle,
    /// Pipeline is being configured.
    Configuring,
    /// Pipeline is being optimized.
    Optimizing,
    /// Pipeline is executing.
    Executing,
    /// Pipeline execution completed successfully.
    Completed,
    /// Pipeline execution failed.
    Failed,
    /// Pipeline execution was cancelled.
    Cancelled
/// Pipeline event arguments.
public class PipelineEventArgs : EventArgs
    {
    /// Gets or sets the pipeline ID.
    public string PipelineId { get; set; } = string.Empty;
    /// Gets or sets the timestamp.
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    /// Gets or sets additional data.
    public Dictionary<string, object> Data { get; set; } = [];
/// Stage completed event arguments.
public class StageCompletedEventArgs : PipelineEventArgs
    {
    /// Gets or sets the stage name.
    public string StageName { get; set; } = string.Empty;
    /// Gets or sets whether the stage succeeded.
/// Stage configuration.
public class StageConfiguration
    {
    /// Gets or sets the stage timeout.
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    /// Gets or sets the retry count.
    public int RetryCount { get; set; } = 0;
    /// Gets or sets whether to skip on error.
    public bool SkipOnError { get; set; } = false;
    /// Gets or sets custom configuration.
    public Dictionary<string, object> CustomConfig { get; set; } = [];
/// Stage execution information.
public class StageExecutionInfo
    {
    public string Name { get; set; } = string.Empty;
    /// Gets or sets the input size.
    public long InputSize { get; set; }
    /// Gets or sets the output size.
    public long OutputSize { get; set; }
    /// Gets or sets stage metrics.
