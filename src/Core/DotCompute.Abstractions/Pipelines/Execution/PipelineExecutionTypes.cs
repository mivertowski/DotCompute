// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Configuration;

namespace DotCompute.Abstractions.Pipelines.Execution;

/// <summary>
/// Execution context providing environment and configuration for pipeline execution.
/// </summary>
/// <remarks>
/// <para>
/// The execution context encapsulates all runtime state and services needed
/// for pipeline execution including configuration, resource management, caching,
/// telemetry, cancellation, logging, dependency injection, and ambient properties.
/// </para>
/// <para>
/// Contexts are scoped to a single pipeline execution and should be disposed
/// properly to release resources.
/// </para>
/// </remarks>
public interface IPipelineExecutionContext
{
    /// <summary>Unique identifier for this execution context.</summary>
    public Guid ContextId { get; }

    /// <summary>Configuration for this execution.</summary>
    public IPipelineConfiguration Configuration { get; }

    // TODO: Define missing type
    /* <summary>Resource manager for this execution.</summary>
    IPipelineResourceManager ResourceManager { get; } */

    // TODO: Define missing type
    /* <summary>Cache manager for storing intermediate results.</summary>
    IPipelineCacheManager CacheManager { get; } */

    // TODO: Define missing type
    /* <summary>Telemetry collector for execution metrics.</summary>
    ITelemetryCollector TelemetryCollector { get; } */

    /// <summary>Cancellation token for the execution.</summary>
    /// <remarks>
    /// Pipelines should honor cancellation requests promptly to support
    /// graceful shutdown and timeout enforcement.
    /// </remarks>
    public CancellationToken CancellationToken { get; }

    // TODO: Define missing type
    /* <summary>Logger for execution events and diagnostics.</summary>
    ILogger Logger { get; } */

    /// <summary>Service provider for dependency injection.</summary>
    /// <remarks>
    /// Allows pipeline stages to resolve services dynamically.
    /// Prefer constructor injection when possible.
    /// </remarks>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>Ambient properties available to all pipeline stages.</summary>
    /// <remarks>
    /// Provides a dictionary for passing contextual data between stages
    /// without explicit parameter passing.
    /// </remarks>
    public IReadOnlyDictionary<string, object> Properties { get; }
}

/// <summary>
/// Detailed result from pipeline execution with comprehensive metrics and diagnostics.
/// </summary>
/// <typeparam name="TOutput">Type of the pipeline output result.</typeparam>
/// <remarks>
/// <para>
/// Encapsulates execution outcomes including the result value, success status,
/// exceptions, timing, resource utilization, caching statistics, and
/// performance insights.
/// </para>
/// <para>
/// Provides rich diagnostics for performance analysis, debugging, and
/// optimization feedback.
/// </para>
/// </remarks>
public interface IPipelineExecutionResult<TOutput>
{
    /// <summary>The output result from the pipeline execution.</summary>
    public TOutput Result { get; }

    /// <summary>Whether the execution completed successfully.</summary>
    /// <remarks>
    /// False indicates failures that prevented successful completion.
    /// Check Exceptions collection for details.
    /// </remarks>
    public bool IsSuccess { get; }

    /// <summary>Any exceptions that occurred during execution.</summary>
    /// <remarks>
    /// May contain multiple exceptions if ContinueOnError strategy was used.
    /// </remarks>
    public IReadOnlyList<Exception> Exceptions { get; }

    /// <summary>Total execution time for the pipeline.</summary>
    /// <remarks>
    /// Includes all stages, overhead, and any wait times.
    /// For detailed stage breakdowns, see StageMetrics.
    /// </remarks>
    public TimeSpan ExecutionTime { get; }

    // TODO: Define missing type
    /* <summary>Detailed metrics for each pipeline stage.</summary>
    IReadOnlyList<IStageExecutionMetrics> StageMetrics { get; } */

    // TODO: Define missing type
    /* <summary>Resource utilization during execution.</summary>
    IResourceUtilizationMetrics ResourceMetrics { get; } */

    // TODO: Define missing type
    /* <summary>Cache utilization statistics.</summary>
    ICacheUtilizationMetrics CacheMetrics { get; } */

    // TODO: Define missing type
    /* <summary>Performance insights and recommendations.</summary>
    IPerformanceInsights PerformanceInsights { get; } */

    /// <summary>Execution context that was used.</summary>
    /// <remarks>
    /// Provides access to configuration and ambient properties used during execution.
    /// </remarks>
    public IPipelineExecutionContext ExecutionContext { get; }

    /// <summary>Timestamp when execution started.</summary>
    public DateTimeOffset StartTime { get; }

    /// <summary>Timestamp when execution completed.</summary>
    public DateTimeOffset EndTime { get; }
}

/// <summary>
/// Memory allocation strategies for pipeline operations.
/// </summary>
/// <remarks>
/// <para>
/// Different allocation strategies optimize for different scenarios:
/// OnDemand minimizes upfront cost, PreAllocated minimizes runtime overhead,
/// Pooled amortizes allocation cost, Adaptive learns from patterns, and
/// Optimal uses cost-based analysis.
/// </para>
/// </remarks>
public enum MemoryAllocationStrategy
{
    /// <summary>Allocate memory as needed during execution.</summary>
    /// <remarks>Minimal upfront cost, but higher runtime allocation overhead.</remarks>
    OnDemand,

    /// <summary>Pre-allocate based on estimated requirements.</summary>
    /// <remarks>Reduces runtime overhead but may waste memory if overestimated.</remarks>
    PreAllocated,

    /// <summary>Use memory pooling for frequent allocations.</summary>
    /// <remarks>Best for repeated executions with predictable sizes.</remarks>
    Pooled,

    /// <summary>Adaptive allocation based on runtime patterns.</summary>
    /// <remarks>Learns from execution history to optimize allocation strategy.</remarks>
    Adaptive,

    /// <summary>Optimal allocation strategy based on performance analysis.</summary>
    /// <remarks>Uses cost model to select best strategy for current workload.</remarks>
    Optimal
}
