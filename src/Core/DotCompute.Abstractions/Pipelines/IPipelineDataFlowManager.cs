// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Manages data flow and buffer optimization between pipeline stages with zero-copy optimizations.
/// Provides intelligent data routing, transformation, and memory management for complex pipeline topologies.
/// </summary>
public interface IPipelineDataFlowManager : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this data flow manager instance.
    /// </summary>
    Guid ManagerId { get; }
    
    /// <summary>
    /// Gets current data flow statistics and metrics.
    /// </summary>
    IDataFlowMetrics Metrics { get; }

    #region Data Flow Planning
    
    /// <summary>
    /// Analyzes a pipeline and creates an optimized data flow plan with zero-copy optimizations.
    /// </summary>
    /// <param name="pipeline">The pipeline to analyze for data flow patterns</param>
    /// <param name="constraints">Constraints that must be respected in the data flow plan</param>
    /// <returns>Comprehensive data flow plan with routing and optimization strategies</returns>
    Task<IDataFlowPlan> PlanDataFlowAsync(
        IKernelPipeline pipeline,
        DataFlowConstraints constraints);
    
    /// <summary>
    /// Creates a data flow plan for streaming pipelines with continuous data processing.
    /// </summary>
    /// <param name="pipeline">The streaming pipeline to plan data flow for</param>
    /// <param name="streamCharacteristics">Characteristics of the expected data stream</param>
    /// <param name="constraints">Streaming-specific data flow constraints</param>
    /// <returns>Streaming-optimized data flow plan</returns>
    Task<IStreamingDataFlowPlan> PlanStreamingDataFlowAsync(
        IKernelPipeline pipeline,
        IStreamCharacteristics streamCharacteristics,
        StreamingDataFlowConstraints constraints);
    
    /// <summary>
    /// Optimizes an existing data flow plan based on runtime performance data.
    /// </summary>
    /// <param name="currentPlan">The current data flow plan to optimize</param>
    /// <param name="performanceData">Runtime performance data for optimization</param>
    /// <param name="optimizationGoals">Specific optimization objectives</param>
    /// <returns>Optimized data flow plan</returns>
    Task<IDataFlowPlan> OptimizeDataFlowAsync(
        IDataFlowPlan currentPlan,
        IDataFlowPerformanceData performanceData,
        DataFlowOptimizationGoals optimizationGoals);
    
    /// <summary>
    /// Validates a data flow plan for correctness and compatibility.
    /// </summary>
    /// <param name="plan">The data flow plan to validate</param>
    /// <returns>Validation result with any issues found</returns>
    Task<IDataFlowValidationResult> ValidateDataFlowAsync(IDataFlowPlan plan);
    
    #endregion

    #region Buffer Management
    
    /// <summary>
    /// Creates a buffer manager for executing a data flow plan with optimized memory usage.
    /// </summary>
    /// <param name="plan">The data flow plan to create a buffer manager for</param>
    /// <param name="strategy">Memory allocation strategy for buffer management</param>
    /// <returns>Buffer manager configured for the data flow plan</returns>
    Task<IBufferManager> CreateBufferManagerAsync(
        IDataFlowPlan plan,
        MemoryAllocationStrategy strategy = MemoryAllocationStrategy.Optimal);
    
    /// <summary>
    /// Creates a specialized buffer manager for streaming data processing.
    /// </summary>
    /// <param name="streamingPlan">The streaming data flow plan</param>
    /// <param name="bufferConfiguration">Configuration for streaming buffers</param>
    /// <returns>Streaming buffer manager with flow control capabilities</returns>
    Task<IStreamingBufferManager> CreateStreamingBufferManagerAsync(
        IStreamingDataFlowPlan streamingPlan,
        StreamingBufferConfiguration bufferConfiguration);
    
    /// <summary>
    /// Manages buffer lifecycle across multiple pipeline executions with pooling.
    /// </summary>
    /// <param name="bufferPoolConfiguration">Configuration for buffer pooling</param>
    /// <returns>Buffer pool manager for reusing memory allocations</returns>
    Task<IBufferPoolManager> CreateBufferPoolManagerAsync(
        BufferPoolConfiguration bufferPoolConfiguration);
    
    #endregion

    #region Data Transformation
    
    /// <summary>
    /// Gets or creates a data transformer for converting between incompatible stage types.
    /// </summary>
    /// <typeparam name="TInput">The source data type</typeparam>
    /// <typeparam name="TOutput">The target data type</typeparam>
    /// <param name="transformationRules">Rules for the data transformation</param>
    /// <returns>Data transformer for the specified type conversion</returns>
    Task<IDataTransformer<TInput, TOutput>> GetTransformerAsync<TInput, TOutput>(
        ITransformationRules? transformationRules = null) where TInput : unmanaged where TOutput : unmanaged;
    
    /// <summary>
    /// Creates a data transformer pipeline for complex multi-step transformations.
    /// </summary>
    /// <param name="transformationChain">Chain of transformations to apply</param>
    /// <returns>Composite data transformer for the transformation chain</returns>
    Task<ICompositeDataTransformer> CreateTransformerPipelineAsync(
        IEnumerable<ITransformationStep> transformationChain);
    
    /// <summary>
    /// Gets a data transformer optimized for specific hardware characteristics.
    /// </summary>
    /// <typeparam name="TInput">The source data type</typeparam>
    /// <typeparam name="TOutput">The target data type</typeparam>
    /// <param name="hardwareProfile">Target hardware characteristics</param>
    /// <param name="transformationRules">Transformation configuration</param>
    /// <returns>Hardware-optimized data transformer</returns>
    Task<IHardwareOptimizedTransformer<TInput, TOutput>> GetHardwareOptimizedTransformerAsync<TInput, TOutput>(
        IHardwareProfile hardwareProfile,
        ITransformationRules transformationRules) where TInput : unmanaged where TOutput : unmanaged;
    
    #endregion

    #region Streaming Data Flow
    
    /// <summary>
    /// Creates a streaming data flow for large dataset processing with backpressure handling.
    /// </summary>
    /// <typeparam name="TInput">The input stream element type</typeparam>
    /// <typeparam name="TOutput">The output stream element type</typeparam>
    /// <param name="input">The input data stream</param>
    /// <param name="pipeline">The pipeline to process the stream</param>
    /// <param name="options">Streaming processing options</param>
    /// <param name="cancellationToken">Cancellation token for the stream processing</param>
    /// <returns>Processed data stream with flow control</returns>
    IAsyncEnumerable<TOutput> CreateDataStreamAsync<TInput, TOutput>(
        IAsyncEnumerable<TInput> input,
        IKernelPipeline pipeline,
        StreamingOptions options,
        CancellationToken cancellationToken = default) where TInput : unmanaged where TOutput : unmanaged;
    
    /// <summary>
    /// Creates a parallel data stream that processes multiple streams concurrently.
    /// </summary>
    /// <typeparam name="TInput">The input stream element type</typeparam>
    /// <typeparam name="TOutput">The output stream element type</typeparam>
    /// <param name="inputStreams">Multiple input streams to process in parallel</param>
    /// <param name="pipeline">The pipeline to process each stream</param>
    /// <param name="options">Parallel streaming options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Merged output stream from parallel processing</returns>
    IAsyncEnumerable<TOutput> CreateParallelDataStreamAsync<TInput, TOutput>(
        IEnumerable<IAsyncEnumerable<TInput>> inputStreams,
        IKernelPipeline pipeline,
        ParallelStreamingOptions options,
        CancellationToken cancellationToken = default) where TInput : unmanaged where TOutput : unmanaged;
    
    /// <summary>
    /// Creates a windowed data stream for processing data in batches or time windows.
    /// </summary>
    /// <typeparam name="TInput">The input stream element type</typeparam>
    /// <typeparam name="TOutput">The output stream element type</typeparam>
    /// <param name="input">The input data stream</param>
    /// <param name="windowStrategy">Strategy for creating data windows</param>
    /// <param name="pipeline">Pipeline to process each window</param>
    /// <param name="options">Windowed streaming options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of processed window results</returns>
    IAsyncEnumerable<TOutput> CreateWindowedDataStreamAsync<TInput, TOutput>(
        IAsyncEnumerable<TInput> input,
        IWindowStrategy<TInput> windowStrategy,
        IKernelPipeline pipeline,
        WindowedStreamingOptions options,
        CancellationToken cancellationToken = default) where TInput : unmanaged where TOutput : unmanaged;
    
    #endregion

    #region Data Flow Monitoring and Control
    
    /// <summary>
    /// Monitors data flow performance in real-time during pipeline execution.
    /// </summary>
    /// <param name="dataFlowId">Identifier for the data flow to monitor</param>
    /// <param name="monitoringOptions">Configuration for data flow monitoring</param>
    /// <returns>Data flow monitor for real-time statistics</returns>
    Task<IDataFlowMonitor> StartMonitoringAsync(
        Guid dataFlowId,
        DataFlowMonitoringOptions monitoringOptions);
    
    /// <summary>
    /// Controls data flow rate and backpressure for streaming operations.
    /// </summary>
    /// <param name="streamId">Identifier for the data stream</param>
    /// <param name="flowControlSettings">Flow control configuration</param>
    /// <returns>Flow control manager for the data stream</returns>
    Task<IStreamFlowControl> GetFlowControlAsync(
        Guid streamId,
        FlowControlSettings flowControlSettings);
    
    /// <summary>
    /// Gets an observable stream of data flow events for monitoring and debugging.
    /// </summary>
    IObservable<DataFlowEvent> DataFlowEvents { get; }
    
    /// <summary>
    /// Analyzes data flow bottlenecks and provides optimization recommendations.
    /// </summary>
    /// <param name="dataFlowId">The data flow to analyze</param>
    /// <param name="analysisOptions">Configuration for bottleneck analysis</param>
    /// <returns>Analysis results with bottleneck identification and recommendations</returns>
    Task<IDataFlowBottleneckAnalysis> AnalyzeBottlenecksAsync(
        Guid dataFlowId,
        BottleneckAnalysisOptions analysisOptions);
    
    #endregion

    #region Advanced Data Flow Features
    
    /// <summary>
    /// Creates a fault-tolerant data flow with automatic recovery capabilities.
    /// </summary>
    /// <param name="basePlan">The base data flow plan to make fault-tolerant</param>
    /// <param name="faultToleranceConfig">Configuration for fault tolerance mechanisms</param>
    /// <returns>Fault-tolerant data flow plan with recovery strategies</returns>
    Task<IFaultTolerantDataFlowPlan> CreateFaultTolerantDataFlowAsync(
        IDataFlowPlan basePlan,
        FaultToleranceConfiguration faultToleranceConfig);
    
    /// <summary>
    /// Creates a data flow with automatic load balancing across multiple execution paths.
    /// </summary>
    /// <param name="pipeline">The pipeline to create load-balanced data flow for</param>
    /// <param name="loadBalancingStrategy">Strategy for distributing load</param>
    /// <returns>Load-balanced data flow plan</returns>
    Task<ILoadBalancedDataFlowPlan> CreateLoadBalancedDataFlowAsync(
        IKernelPipeline pipeline,
        ILoadBalancingStrategy loadBalancingStrategy);
    
    /// <summary>
    /// Creates a data flow with intelligent caching of intermediate results.
    /// </summary>
    /// <param name="basePlan">The base data flow plan</param>
    /// <param name="cachingStrategy">Strategy for caching intermediate results</param>
    /// <returns>Data flow plan with integrated result caching</returns>
    Task<ICachedDataFlowPlan> CreateCachedDataFlowAsync(
        IDataFlowPlan basePlan,
        IDataFlowCachingStrategy cachingStrategy);
    
    /// <summary>
    /// Creates a data flow that adapts to changing runtime conditions.
    /// </summary>
    /// <param name="basePlan">The base data flow plan</param>
    /// <param name="adaptationRules">Rules for adapting the data flow</param>
    /// <returns>Adaptive data flow plan that can modify itself at runtime</returns>
    Task<IAdaptiveDataFlowPlan> CreateAdaptiveDataFlowAsync(
        IDataFlowPlan basePlan,
        IDataFlowAdaptationRules adaptationRules);
    
    #endregion
}

/// <summary>
/// Constraints and requirements for data flow planning.
/// </summary>
public class DataFlowConstraints
{
    /// <summary>Maximum memory that can be used for data flow buffers.</summary>
    public long? MaxMemoryUsage { get; set; }
    
    /// <summary>Preferred memory locations for data storage.</summary>
    public List<MemoryLocation> PreferredMemoryLocations { get; set; } = new();
    
    /// <summary>Whether zero-copy optimizations are allowed.</summary>
    public bool AllowZeroCopyOptimizations { get; set; } = true;
    
    /// <summary>Whether data can be transformed in-place.</summary>
    public bool AllowInPlaceTransformations { get; set; } = true;
    
    /// <summary>Maximum number of intermediate buffers to create.</summary>
    public int? MaxIntermediateBuffers { get; set; }
    
    /// <summary>Required data alignment for memory buffers.</summary>
    public int RequiredAlignment { get; set; } = 32;
    
    /// <summary>Whether to prioritize memory usage or execution speed.</summary>
    public DataFlowOptimizationTarget OptimizationTarget { get; set; } = DataFlowOptimizationTarget.Balanced;
    
    /// <summary>Custom constraints specific to the application domain.</summary>
    public Dictionary<string, object> CustomConstraints { get; set; } = new();
}

/// <summary>
/// Optimization targets for data flow planning.
/// </summary>
public enum DataFlowOptimizationTarget
{
    /// <summary>Minimize memory usage at the cost of some performance.</summary>
    MinimizeMemory,
    
    /// <summary>Maximize execution speed with higher memory usage.</summary>
    MaximizeSpeed,
    
    /// <summary>Balance memory usage and execution speed.</summary>
    Balanced,
    
    /// <summary>Optimize for streaming throughput.</summary>
    MaximizeThroughput,
    
    /// <summary>Optimize for low latency operations.</summary>
    MinimizeLatency,
    
    /// <summary>Optimize for energy efficiency.</summary>
    EnergyEfficient
}

/// <summary>
/// Specialized constraints for streaming data flow operations.
/// </summary>
public class StreamingDataFlowConstraints : DataFlowConstraints
{
    /// <summary>Maximum acceptable latency for stream processing.</summary>
    public TimeSpan? MaxLatency { get; set; }
    
    /// <summary>Required minimum throughput for the stream.</summary>
    public double? MinThroughput { get; set; }
    
    /// <summary>Buffer size limits for streaming operations.</summary>
    public BufferSizeLimits BufferLimits { get; set; } = new();
    
    /// <summary>Backpressure handling strategy.</summary>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Buffer;
    
    /// <summary>Whether to preserve order in streaming results.</summary>
    public bool PreserveOrder { get; set; } = true;
}

/// <summary>
/// Buffer size limits for streaming operations.
/// </summary>
public class BufferSizeLimits
{
    /// <summary>Minimum buffer size in bytes.</summary>
    public long MinBufferSize { get; set; } = 1024;
    
    /// <summary>Maximum buffer size in bytes.</summary>
    public long MaxBufferSize { get; set; } = 64 * 1024 * 1024; // 64 MB
    
    /// <summary>Preferred buffer size in bytes.</summary>
    public long PreferredBufferSize { get; set; } = 1024 * 1024; // 1 MB
    
    /// <summary>Maximum number of buffers to allocate.</summary>
    public int MaxBufferCount { get; set; } = 16;
}

/// <summary>
/// Goals for optimizing data flow performance.
/// </summary>
public class DataFlowOptimizationGoals
{
    /// <summary>Target reduction in memory usage (percentage).</summary>
    public double? TargetMemoryReduction { get; set; }
    
    /// <summary>Target improvement in throughput (percentage).</summary>
    public double? TargetThroughputImprovement { get; set; }
    
    /// <summary>Target reduction in latency (percentage).</summary>
    public double? TargetLatencyReduction { get; set; }
    
    /// <summary>Target improvement in cache utilization (percentage).</summary>
    public double? TargetCacheUtilizationImprovement { get; set; }
    
    /// <summary>Relative weights for different optimization aspects.</summary>
    public DataFlowOptimizationWeights Weights { get; set; } = new();
}

/// <summary>
/// Weights for balancing different data flow optimization aspects.
/// </summary>
public class DataFlowOptimizationWeights
{
    /// <summary>Weight for memory usage optimization (0.0 to 1.0).</summary>
    public double Memory { get; set; } = 0.3;
    
    /// <summary>Weight for throughput optimization (0.0 to 1.0).</summary>
    public double Throughput { get; set; } = 0.4;
    
    /// <summary>Weight for latency optimization (0.0 to 1.0).</summary>
    public double Latency { get; set; } = 0.2;
    
    /// <summary>Weight for cache efficiency optimization (0.0 to 1.0).</summary>
    public double CacheEfficiency { get; set; } = 0.1;
}

/// <summary>
/// Comprehensive data flow plan with routing and optimization strategies.
/// </summary>
public interface IDataFlowPlan
{
    /// <summary>Unique identifier for this data flow plan.</summary>
    Guid PlanId { get; }
    
    /// <summary>Pipeline this plan was created for.</summary>
    IKernelPipeline Pipeline { get; }
    
    /// <summary>Data flow graph representing stage connections.</summary>
    IDataFlowGraph FlowGraph { get; }
    
    /// <summary>Memory allocation plan for buffers.</summary>
    IMemoryAllocationPlan MemoryPlan { get; }
    
    /// <summary>Optimization strategies applied to the data flow.</summary>
    IReadOnlyList<IDataFlowOptimization> Optimizations { get; }
    
    /// <summary>Estimated performance characteristics.</summary>
    IDataFlowPerformanceEstimate PerformanceEstimate { get; }
    
    /// <summary>Validates the plan for correctness.</summary>
    Task<IDataFlowValidationResult> ValidateAsync();
    
    /// <summary>Creates an execution context for this plan.</summary>
    Task<IDataFlowExecutionContext> CreateExecutionContextAsync();
}

/// <summary>
/// Specialized data flow plan for streaming operations.
/// </summary>
public interface IStreamingDataFlowPlan : IDataFlowPlan
{
    /// <summary>Stream characteristics this plan was designed for.</summary>
    IStreamCharacteristics StreamCharacteristics { get; }
    
    /// <summary>Buffer management strategy for streaming.</summary>
    IStreamingBufferStrategy BufferStrategy { get; }
    
    /// <summary>Flow control configuration.</summary>
    IStreamFlowControlConfig FlowControlConfig { get; }
    
    /// <summary>Backpressure handling strategy.</summary>
    BackpressureStrategy BackpressureStrategy { get; }
}

/// <summary>
/// Current performance metrics for data flow operations.
/// </summary>
public interface IDataFlowMetrics
{
    /// <summary>Total number of data flow plans created.</summary>
    long TotalPlansCreated { get; }
    
    /// <summary>Number of currently active data flows.</summary>
    int ActiveDataFlows { get; }
    
    /// <summary>Total memory currently allocated for data flow buffers.</summary>
    long TotalMemoryAllocated { get; }
    
    /// <summary>Current data throughput in bytes per second.</summary>
    double CurrentThroughput { get; }
    
    /// <summary>Peak data throughput achieved.</summary>
    double PeakThroughput { get; }
    
    /// <summary>Average latency for data transformations.</summary>
    TimeSpan AverageTransformationLatency { get; }
    
    /// <summary>Number of zero-copy optimizations applied.</summary>
    long ZeroCopyOptimizations { get; }
    
    /// <summary>Buffer pool utilization percentage.</summary>
    double BufferPoolUtilization { get; }
    
    /// <summary>Number of data flow bottlenecks detected.</summary>
    int BottlenecksDetected { get; }
    
    /// <summary>Cache hit rate for data transformations.</summary>
    double TransformationCacheHitRate { get; }
}

/// <summary>
/// Events related to data flow operations and state changes.
/// </summary>
public abstract class DataFlowEvent
{
    /// <summary>Unique identifier for the event.</summary>
    public Guid EventId { get; } = Guid.NewGuid();
    
    /// <summary>Timestamp when the event occurred.</summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    
    /// <summary>Data flow identifier associated with the event.</summary>
    public Guid DataFlowId { get; set; }
    
    /// <summary>Event severity level.</summary>
    public EventSeverity Severity { get; set; } = EventSeverity.Information;
    
    /// <summary>Human-readable description of the event.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Event fired when a data flow bottleneck is detected.
/// </summary>
public class DataFlowBottleneckDetectedEvent : DataFlowEvent
{
    /// <summary>Location of the bottleneck in the data flow.</summary>
    public string BottleneckLocation { get; set; } = string.Empty;
    
    /// <summary>Type of bottleneck detected.</summary>
    public BottleneckType BottleneckType { get; set; }
    
    /// <summary>Severity of the bottleneck impact.</summary>
    public BottleneckSeverity BottleneckSeverity { get; set; }
    
    /// <summary>Recommended actions to resolve the bottleneck.</summary>
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Types of bottlenecks that can occur in data flow operations.
/// </summary>
public enum BottleneckType
{
    /// <summary>Memory bandwidth limitation.</summary>
    MemoryBandwidth,
    
    /// <summary>Buffer allocation/deallocation overhead.</summary>
    BufferAllocation,
    
    /// <summary>Data transformation overhead.</summary>
    DataTransformation,
    
    /// <summary>Inter-stage data transfer overhead.</summary>
    DataTransfer,
    
    /// <summary>Cache miss penalty.</summary>
    CacheMiss,
    
    /// <summary>Resource contention between stages.</summary>
    ResourceContention,
    
    /// <summary>Serialization/deserialization overhead.</summary>
    Serialization
}

/// <summary>
/// Severity levels for bottleneck impact assessment.
/// </summary>
public enum BottleneckSeverity
{
    /// <summary>Minor impact on performance.</summary>
    Minor,
    
    /// <summary>Moderate impact on performance.</summary>
    Moderate,
    
    /// <summary>Major impact on performance.</summary>
    Major,
    
    /// <summary>Critical impact preventing optimal performance.</summary>
    Critical
}