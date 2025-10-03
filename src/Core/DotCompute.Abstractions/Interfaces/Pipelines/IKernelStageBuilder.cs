// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Models;

namespace DotCompute.Abstractions.Interfaces.Pipelines;

/// <summary>
/// Builder interface for configuring individual kernel stages within a pipeline.
/// Provides fluent API for setting stage-specific parameters, constraints, and behaviors.
/// </summary>
public interface IKernelStageBuilder
{
    /// <summary>
    /// Sets the parameters for the kernel stage.
    /// </summary>
    /// <param name="parameters">Array of parameters to pass to the kernel</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithParameters(params object[] parameters);

    /// <summary>
    /// Configures the backend preference for this stage.
    /// </summary>
    /// <param name="backendName">Preferred backend name (e.g., "CUDA", "CPU", "Metal")</param>
    /// <param name="fallbackStrategy">Strategy if preferred backend is unavailable</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder PreferBackend(string backendName, BackendFallbackStrategy fallbackStrategy = BackendFallbackStrategy.Auto);

    /// <summary>
    /// Sets execution timeout for this specific stage.
    /// </summary>
    /// <param name="timeout">Maximum execution time for the stage</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Configures memory hints for buffer allocation in this stage.
    /// </summary>
    /// <param name="hints">Memory optimization hints</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithMemoryHints(params MemoryHint[] hints);

    /// <summary>
    /// Sets retry policy for this stage in case of failures.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <param name="retryStrategy">Strategy for retry timing and conditions</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithRetryPolicy(int maxRetries, RetryStrategy retryStrategy = RetryStrategy.ExponentialBackoff);

    /// <summary>
    /// Configures caching for this stage's results.
    /// </summary>
    /// <param name="cacheKey">Key for storing/retrieving cached results</param>
    /// <param name="ttl">Time-to-live for cached results</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithCaching(string cacheKey, TimeSpan? ttl = null);

    /// <summary>
    /// Sets the execution priority for this stage.
    /// </summary>
    /// <param name="priority">Execution priority level</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithPriority(ExecutionPriority priority);

    /// <summary>
    /// Configures input validation for this stage.
    /// </summary>
    /// <param name="validator">Validator function for input parameters</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithInputValidation(Func<object[], ValidationResult> validator);

    /// <summary>
    /// Sets output transformation for this stage's results.
    /// </summary>
    /// <param name="transformer">Function to transform stage output</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithOutputTransform(Func<object, object> transformer);

    /// <summary>
    /// Configures profiling and metrics collection for this stage.
    /// </summary>
    /// <param name="enableProfiling">Whether to enable detailed profiling</param>
    /// <param name="customMetrics">Custom metrics to collect</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithProfiling(bool enableProfiling = true, params string[] customMetrics);

    /// <summary>
    /// Sets dependencies that must complete before this stage can execute.
    /// </summary>
    /// <param name="dependencies">Stage names or identifiers that must complete first</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithDependencies(params string[] dependencies);

    /// <summary>
    /// Configures resource requirements for this stage.
    /// </summary>
    /// <param name="requirements">Resource requirements specification</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithResourceRequirements(ResourceRequirements requirements);

    /// <summary>
    /// Sets a custom error handler for this stage.
    /// </summary>
    /// <param name="errorHandler">Function to handle stage-specific errors</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WithErrorHandler(Func<Exception, ErrorHandlingResult> errorHandler);

    /// <summary>
    /// Configures the stage to run conditionally based on runtime evaluation.
    /// </summary>
    /// <param name="condition">Condition to evaluate before execution</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder WhenCondition(Func<PipelineExecutionMetrics, bool> condition);

    /// <summary>
    /// Sets the stage type for specialized processing.
    /// </summary>
    /// <param name="stageType">Type of pipeline stage</param>
    /// <returns>The stage builder for fluent configuration</returns>
    public IKernelStageBuilder OfType(PipelineStageType stageType);
}
