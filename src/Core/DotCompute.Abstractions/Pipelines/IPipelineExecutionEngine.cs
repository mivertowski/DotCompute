// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Core execution engine for kernel pipelines with advanced scheduling, optimization, and monitoring capabilities.
/// Manages the complete lifecycle of pipeline execution from analysis through completion.
/// </summary>
public interface IPipelineExecutionEngine : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this execution engine instance.
    /// </summary>
    Guid EngineId { get; }
    
    /// <summary>
    /// Gets the current status of the execution engine.
    /// </summary>
    EngineStatus Status { get; }
    
    /// <summary>
    /// Gets comprehensive performance metrics for the execution engine.
    /// </summary>
    IPipelineEngineMetrics Metrics { get; }

    #region Pipeline Execution
    
    /// <summary>
    /// Executes a pipeline with comprehensive monitoring and optimization.
    /// </summary>
    /// <typeparam name="TOutput">The expected output type from the pipeline</typeparam>
    /// <param name="pipeline">The pipeline to execute</param>
    /// <param name="context">Execution context with configuration and monitoring</param>
    /// <param name="cancellationToken">Cancellation token for the execution</param>
    /// <returns>The pipeline execution result</returns>
    Task<TOutput> ExecuteAsync<TOutput>(
        IKernelPipeline pipeline,
        IPipelineExecutionContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a pipeline with explicit input data and context.
    /// </summary>
    /// <typeparam name="TInput">The input data type</typeparam>
    /// <typeparam name="TOutput">The expected output type</typeparam>
    /// <param name="pipeline">The pipeline to execute</param>
    /// <param name="input">The input data for the pipeline</param>
    /// <param name="context">Execution context with configuration</param>
    /// <param name="cancellationToken">Cancellation token for the execution</param>
    /// <returns>The pipeline execution result</returns>
    Task<TOutput> ExecuteAsync<TInput, TOutput>(
        IKernelPipeline pipeline,
        TInput input,
        IPipelineExecutionContext context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a pipeline in streaming mode for continuous data processing.
    /// </summary>
    /// <typeparam name="TInput">The input stream element type</typeparam>
    /// <typeparam name="TOutput">The output stream element type</typeparam>
    /// <param name="pipeline">The pipeline to execute in streaming mode</param>
    /// <param name="inputStream">The input data stream</param>
    /// <param name="context">Streaming execution context</param>
    /// <param name="cancellationToken">Cancellation token for the stream processing</param>
    /// <returns>An async enumerable of processed results</returns>
    IAsyncEnumerable<TOutput> ExecuteStreamAsync<TInput, TOutput>(
        IKernelPipeline pipeline,
        IAsyncEnumerable<TInput> inputStream,
        IStreamingExecutionContext context,
        CancellationToken cancellationToken = default);
    
    #endregion

    #region Pipeline Scheduling
    
    /// <summary>
    /// Schedules pipeline execution with priority and resource constraints.
    /// </summary>
    /// <typeparam name="TOutput">The expected output type</typeparam>
    /// <param name="pipeline">The pipeline to schedule for execution</param>
    /// <param name="options">Scheduling options including priority and constraints</param>
    /// <param name="cancellationToken">Cancellation token for the scheduling operation</param>
    /// <returns>A scheduled pipeline execution that can be awaited</returns>
    Task<IPipelineExecution<TOutput>> ScheduleAsync<TOutput>(
        IKernelPipeline pipeline,
        PipelineSchedulingOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Schedules multiple pipelines for coordinated execution with dependency management.
    /// </summary>
    /// <param name="pipelineGroup">Group of pipelines with their dependencies</param>
    /// <param name="coordinationStrategy">Strategy for coordinating the executions</param>
    /// <param name="cancellationToken">Cancellation token for the group scheduling</param>
    /// <returns>A coordinated execution manager for the pipeline group</returns>
    Task<IPipelineGroupExecution> ScheduleGroupAsync(
        IPipelineGroup pipelineGroup,
        ICoordinationStrategy coordinationStrategy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets information about currently scheduled and running pipeline executions.
    /// </summary>
    /// <returns>Collection of active pipeline executions</returns>
    Task<IReadOnlyList<IPipelineExecutionInfo>> GetActiveExecutionsAsync();
    
    /// <summary>
    /// Cancels a specific pipeline execution by its identifier.
    /// </summary>
    /// <param name="executionId">The unique identifier of the execution to cancel</param>
    /// <param name="reason">Reason for the cancellation</param>
    /// <returns>True if the execution was successfully cancelled</returns>
    Task<bool> CancelExecutionAsync(Guid executionId, string? reason = null);
    
    #endregion

    #region Pipeline Optimization
    
    /// <summary>
    /// Optimizes pipeline structure for improved performance characteristics.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="context">Optimization context with performance goals and constraints</param>
    /// <returns>An optimized version of the pipeline</returns>
    Task<IKernelPipeline> OptimizeAsync(
        IKernelPipeline pipeline,
        OptimizationContext context);
    
    /// <summary>
    /// Applies machine learning-based optimization using historical execution data.
    /// </summary>
    /// <param name="pipeline">The pipeline to optimize</param>
    /// <param name="executionHistory">Historical performance data</param>
    /// <param name="mlOptions">Machine learning optimization configuration</param>
    /// <returns>ML-optimized pipeline with performance predictions</returns>
    Task<IMLOptimizedPipeline> OptimizeWithMLAsync(
        IKernelPipeline pipeline,
        IExecutionHistory executionHistory,
        IMLOptimizationOptions mlOptions);
    
    /// <summary>
    /// Performs real-time optimization during pipeline execution.
    /// </summary>
    /// <param name="executionId">The execution to optimize in real-time</param>
    /// <param name="optimizationTriggers">Conditions that trigger optimization</param>
    /// <returns>Real-time optimization controller</returns>
    Task<IRealTimeOptimizer> EnableRealTimeOptimizationAsync(
        Guid executionId,
        IOptimizationTriggers optimizationTriggers);
    
    #endregion

    #region Pipeline Analysis
    
    /// <summary>
    /// Analyzes pipeline for bottlenecks and optimization opportunities.
    /// </summary>
    /// <param name="pipeline">The pipeline to analyze</param>
    /// <returns>Comprehensive pipeline analysis results</returns>
    Task<IPipelineAnalysis> AnalyzeAsync(IKernelPipeline pipeline);
    
    /// <summary>
    /// Performs static analysis of pipeline structure and dependencies.
    /// </summary>
    /// <param name="pipeline">The pipeline to analyze statically</param>
    /// <returns>Static analysis results with structural insights</returns>
    Task<IStaticPipelineAnalysis> StaticAnalyzeAsync(IKernelPipeline pipeline);
    
    /// <summary>
    /// Simulates pipeline execution to predict performance characteristics.
    /// </summary>
    /// <param name="pipeline">The pipeline to simulate</param>
    /// <param name="simulationParameters">Parameters for the simulation</param>
    /// <returns>Simulation results with performance predictions</returns>
    Task<IPipelineSimulationResult> SimulateAsync(
        IKernelPipeline pipeline,
        ISimulationParameters simulationParameters);
    
    /// <summary>
    /// Benchmarks pipeline performance with various input characteristics.
    /// </summary>
    /// <param name="pipeline">The pipeline to benchmark</param>
    /// <param name="benchmarkSuite">Suite of benchmarks to run</param>
    /// <returns>Comprehensive benchmark results</returns>
    Task<IPipelineBenchmarkResult> BenchmarkAsync(
        IKernelPipeline pipeline,
        IBenchmarkSuite benchmarkSuite);
    
    #endregion

    #region Resource Management
    
    /// <summary>
    /// Gets current resource utilization across all active pipeline executions.
    /// </summary>
    /// <returns>Current resource utilization snapshot</returns>
    Task<IResourceUtilizationSnapshot> GetResourceUtilizationAsync();
    
    /// <summary>
    /// Configures resource allocation policies for pipeline executions.
    /// </summary>
    /// <param name="allocationPolicy">Resource allocation policy configuration</param>
    /// <returns>Resource allocation manager with the configured policy</returns>
    Task<IResourceAllocationManager> ConfigureResourceAllocationAsync(
        IResourceAllocationPolicy allocationPolicy);
    
    /// <summary>
    /// Handles resource contention between competing pipeline executions.
    /// </summary>
    /// <param name="contentionResolver">Strategy for resolving resource contention</param>
    /// <returns>Contention resolution manager</returns>
    Task<IContentionResolver> EnableContentionResolutionAsync(
        IContentionResolutionStrategy contentionResolver);
    
    /// <summary>
    /// Monitors resource usage patterns and provides optimization recommendations.
    /// </summary>
    /// <param name="monitoringOptions">Configuration for resource monitoring</param>
    /// <returns>Resource monitoring service</returns>
    Task<IResourceMonitor> StartResourceMonitoringAsync(
        IResourceMonitoringOptions monitoringOptions);
    
    #endregion

    #region Event Handling and Monitoring
    
    /// <summary>
    /// Gets an observable stream of pipeline execution events for real-time monitoring.
    /// </summary>
    IObservable<PipelineExecutionEvent> ExecutionEvents { get; }
    
    /// <summary>
    /// Gets an observable stream of engine performance events.
    /// </summary>
    IObservable<EnginePerformanceEvent> PerformanceEvents { get; }
    
    /// <summary>
    /// Gets an observable stream of resource utilization events.
    /// </summary>
    IObservable<ResourceUtilizationEvent> ResourceEvents { get; }
    
    /// <summary>
    /// Subscribes to specific pipeline execution events with filtering.
    /// </summary>
    /// <param name="eventFilter">Filter for specific event types and sources</param>
    /// <param name="handler">Event handler for filtered events</param>
    /// <returns>Subscription that can be disposed to stop listening</returns>
    IDisposable SubscribeToEvents(
        IEventFilter eventFilter,
        Func<PipelineExecutionEvent, Task> handler);
    
    #endregion

    #region Configuration and Management
    
    /// <summary>
    /// Configures the execution engine with custom settings.
    /// </summary>
    /// <param name="configuration">Engine configuration options</param>
    /// <returns>Task representing the configuration operation</returns>
    Task ConfigureAsync(IExecutionEngineConfiguration configuration);
    
    /// <summary>
    /// Gets the current configuration of the execution engine.
    /// </summary>
    /// <returns>Current engine configuration</returns>
    Task<IExecutionEngineConfiguration> GetConfigurationAsync();
    
    /// <summary>
    /// Performs health checks on the execution engine and its components.
    /// </summary>
    /// <returns>Health check results with component status</returns>
    Task<IEngineHealthCheck> PerformHealthCheckAsync();
    
    /// <summary>
    /// Gracefully shuts down the execution engine with cleanup of active executions.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for graceful shutdown</param>
    /// <param name="cancellationToken">Cancellation token for the shutdown operation</param>
    /// <returns>Shutdown result indicating success or issues</returns>
    Task<IShutdownResult> ShutdownAsync(
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default);
    
    #endregion

    #region Advanced Features
    
    /// <summary>
    /// Creates a pipeline execution sandbox for isolated and secure execution.
    /// </summary>
    /// <param name="sandboxConfiguration">Configuration for the execution sandbox</param>
    /// <returns>Isolated execution environment</returns>
    Task<IPipelineSandbox> CreateSandboxAsync(ISandboxConfiguration sandboxConfiguration);
    
    /// <summary>
    /// Enables distributed execution across multiple compute nodes.
    /// </summary>
    /// <param name="distributionConfiguration">Configuration for distributed execution</param>
    /// <returns>Distributed execution coordinator</returns>
    Task<IDistributedExecutionCoordinator> EnableDistributedExecutionAsync(
        IDistributionConfiguration distributionConfiguration);
    
    /// <summary>
    /// Creates a pipeline execution checkpoint for fault tolerance.
    /// </summary>
    /// <param name="executionId">The execution to checkpoint</param>
    /// <param name="checkpointOptions">Checkpointing configuration options</param>
    /// <returns>Checkpoint handle for recovery operations</returns>
    Task<IExecutionCheckpoint> CreateCheckpointAsync(
        Guid executionId,
        ICheckpointOptions checkpointOptions);
    
    /// <summary>
    /// Restores pipeline execution from a previous checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to restore from</param>
    /// <param name="recoveryOptions">Options for the recovery process</param>
    /// <returns>Restored pipeline execution</returns>
    Task<IPipelineExecution> RestoreFromCheckpointAsync(
        IExecutionCheckpoint checkpoint,
        IRecoveryOptions recoveryOptions);
    
    #endregion
}

/// <summary>
/// Represents the execution status of the pipeline engine.
/// </summary>
public enum EngineStatus
{
    /// <summary>Engine is initializing and not ready for execution.</summary>
    Initializing,
    
    /// <summary>Engine is ready and available for pipeline execution.</summary>
    Ready,
    
    /// <summary>Engine is actively executing pipelines.</summary>
    Executing,
    
    /// <summary>Engine is under high load with limited capacity.</summary>
    HighLoad,
    
    /// <summary>Engine is experiencing resource contention.</summary>
    ResourceConstrained,
    
    /// <summary>Engine is in maintenance mode with limited functionality.</summary>
    Maintenance,
    
    /// <summary>Engine is shutting down gracefully.</summary>
    ShuttingDown,
    
    /// <summary>Engine has encountered a critical error.</summary>
    Faulted,
    
    /// <summary>Engine has been shut down and is not operational.</summary>
    Shutdown
}

/// <summary>
/// Comprehensive performance metrics for the pipeline execution engine.
/// </summary>
public interface IPipelineEngineMetrics
{
    /// <summary>Total number of pipelines executed since engine start.</summary>
    long TotalPipelinesExecuted { get; }
    
    /// <summary>Number of pipelines currently executing.</summary>
    int ActiveExecutions { get; }
    
    /// <summary>Number of pipelines waiting in the execution queue.</summary>
    int QueuedExecutions { get; }
    
    /// <summary>Average pipeline execution time over the last measurement period.</summary>
    TimeSpan AverageExecutionTime { get; }
    
    /// <summary>Total execution time across all pipelines.</summary>
    TimeSpan TotalExecutionTime { get; }
    
    /// <summary>Current throughput in pipelines per second.</summary>
    double ThroughputPipelinesPerSecond { get; }
    
    /// <summary>Peak throughput achieved by the engine.</summary>
    double PeakThroughput { get; }
    
    /// <summary>Number of successful pipeline executions.</summary>
    long SuccessfulExecutions { get; }
    
    /// <summary>Number of failed pipeline executions.</summary>
    long FailedExecutions { get; }
    
    /// <summary>Success rate as a percentage.</summary>
    double SuccessRate { get; }
    
    /// <summary>Current CPU utilization of the engine.</summary>
    double CpuUtilization { get; }
    
    /// <summary>Current memory utilization in bytes.</summary>
    long MemoryUtilization { get; }
    
    /// <summary>Number of optimization operations performed.</summary>
    long OptimizationCount { get; }
    
    /// <summary>Number of cache hits for pipeline results.</summary>
    long CacheHits { get; }
    
    /// <summary>Number of cache misses for pipeline results.</summary>
    long CacheMisses { get; }
    
    /// <summary>Cache hit rate as a percentage.</summary>
    double CacheHitRate { get; }
    
    /// <summary>Engine uptime since last restart.</summary>
    TimeSpan Uptime { get; }
    
    /// <summary>Timestamp of when metrics were last updated.</summary>
    DateTimeOffset LastUpdated { get; }
}