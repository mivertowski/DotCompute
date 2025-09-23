// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;
using DotCompute.Abstractions.Execution;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Pipelines;

#region Core Pipeline Types

/// <summary>
/// Represents the current execution state of a pipeline.
/// </summary>
public enum PipelineState
{
    /// <summary>Pipeline has been created but not yet configured.</summary>
    Created,
    
    /// <summary>Pipeline is configured and ready for execution.</summary>
    Ready,
    
    /// <summary>Pipeline is currently executing.</summary>
    Executing,
    
    /// <summary>Pipeline execution has been paused.</summary>
    Paused,
    
    /// <summary>Pipeline execution completed successfully.</summary>
    Completed,
    
    /// <summary>Pipeline execution failed with errors.</summary>
    Failed,
    
    /// <summary>Pipeline execution was cancelled.</summary>
    Cancelled,
    
    /// <summary>Pipeline is being optimized.</summary>
    Optimizing,
    
    /// <summary>Pipeline has been disposed.</summary>
    Disposed
}

/// <summary>
/// Configuration options for individual pipeline stages.
/// </summary>
public class PipelineStageOptions
{
    /// <summary>Preferred backend for this stage execution.</summary>
    public string? PreferredBackend { get; set; }
    
    /// <summary>Maximum execution time for this stage.</summary>
    public TimeSpan? Timeout { get; set; }
    
    /// <summary>Priority level for resource allocation.</summary>
    public ExecutionPriority Priority { get; set; } = ExecutionPriority.Normal;
    
    /// <summary>Whether this stage can be executed in parallel with others.</summary>
    public bool AllowParallelExecution { get; set; } = true;
    
    /// <summary>Memory allocation hints for this stage.</summary>
    public MemoryAllocationHints? MemoryHints { get; set; }
    
    /// <summary>Custom metadata for stage configuration.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>Whether to enable detailed profiling for this stage.</summary>
    public bool EnableProfiling { get; set; } = false;
    
    /// <summary>Retry configuration for stage execution.</summary>
    public RetryConfiguration? RetryConfig { get; set; }
    
    /// <summary>Whether to enable optimization for this stage.</summary>
    public bool EnableOptimization { get; set; } = true;
    
    /// <summary>Whether to enable memory optimization for this stage.</summary>
    public bool EnableMemoryOptimization { get; set; } = true;
}

// Use the canonical ExecutionPriority from DotCompute.Abstractions.Execution
// This local enum has been replaced with the unified type

/// <summary>
/// Memory allocation hints for optimizing stage execution.
/// </summary>
public class MemoryAllocationHints
{
    /// <summary>Expected memory usage in bytes.</summary>
    public long? ExpectedMemoryUsage { get; set; }
    
    /// <summary>Whether the stage benefits from memory pooling.</summary>
    public bool PreferMemoryPooling { get; set; } = true;
    
    /// <summary>Preferred memory location (host, device, unified).</summary>
    public MemoryLocation PreferredLocation { get; set; } = MemoryLocation.Auto;
    
    /// <summary>Memory access pattern for optimization.</summary>
    public MemoryAccessPattern AccessPattern { get; set; } = MemoryAccessPattern.Sequential;
    
    /// <summary>Whether the stage can work with pinned memory.</summary>
    public bool SupportsPinnedMemory { get; set; } = false;
}

/// <summary>
/// Retry configuration for handling transient failures.
/// </summary>
public class RetryConfiguration
{
    /// <summary>Maximum number of retry attempts.</summary>
    public int MaxAttempts { get; set; } = 3;
    
    /// <summary>Base delay between retry attempts.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    
    /// <summary>Backoff strategy for increasing delays.</summary>
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;
    
    /// <summary>Maximum delay between retries.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>Condition to determine if an exception should trigger retry.</summary>
    public Func<Exception, bool>? ShouldRetry { get; set; }
    
    /// <summary>Action to execute before each retry attempt.</summary>
    public Func<int, Exception, Task>? OnRetry { get; set; }
}

/// <summary>
/// Backoff strategies for retry delays.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>Fixed delay between retries.</summary>
    Fixed,
    
    /// <summary>Linearly increasing delay.</summary>
    Linear,
    
    /// <summary>Exponentially increasing delay.</summary>
    Exponential,
    
    /// <summary>Exponential with jitter to prevent thundering herd.</summary>
    ExponentialWithJitter
}

#endregion

#region Pipeline Configuration

/// <summary>
/// Comprehensive configuration for pipeline behavior and execution.
/// </summary>
public interface IPipelineConfiguration
{
    /// <summary>Unique name for the pipeline.</summary>
    string Name { get; }
    
    /// <summary>Description of the pipeline's purpose.</summary>
    string? Description { get; }
    
    /// <summary>Version of the pipeline configuration.</summary>
    Version Version { get; }
    
    /// <summary>Global timeout for the entire pipeline execution.</summary>
    TimeSpan? GlobalTimeout { get; }
    
    /// <summary>Default execution priority for all stages.</summary>
    ExecutionPriority DefaultPriority { get; }
    
    /// <summary>Whether to enable pipeline-wide caching.</summary>
    bool EnableCaching { get; }
    
    /// <summary>Whether to enable detailed performance profiling.</summary>
    bool EnableProfiling { get; }
    
    /// <summary>Error handling strategy for the pipeline.</summary>
    ErrorHandlingStrategy ErrorHandling { get; }
    
    /// <summary>Resource allocation preferences.</summary>
    ResourceAllocationPreferences ResourcePreferences { get; }
    
    /// <summary>Pipeline-specific metadata.</summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Error handling strategies for pipeline execution.
/// </summary>
public enum ErrorHandlingStrategy
{
    /// <summary>Stop pipeline execution on first error.</summary>
    StopOnFirstError,
    
    /// <summary>Continue execution and collect all errors.</summary>
    ContinueOnError,
    
    /// <summary>Attempt to recover from errors automatically.</summary>
    AutoRecover,
    
    /// <summary>Use custom error handling logic.</summary>
    Custom
}

/// <summary>
/// Resource allocation preferences for pipeline execution.
/// </summary>
public class ResourceAllocationPreferences
{
    /// <summary>Preferred compute backends in order of preference.</summary>
    public List<string> PreferredBackends { get; set; } = new();
    
    /// <summary>Maximum memory usage for the pipeline.</summary>
    public long? MaxMemoryUsage { get; set; }
    
    /// <summary>Maximum CPU cores to use.</summary>
    public int? MaxCpuCores { get; set; }
    
    /// <summary>Whether to allow GPU acceleration.</summary>
    public bool AllowGpuAcceleration { get; set; } = true;
    
    /// <summary>Resource sharing policy with other pipelines.</summary>
    public ResourceSharingPolicy SharingPolicy { get; set; } = ResourceSharingPolicy.Fair;
}

/// <summary>
/// Resource sharing policies for multiple concurrent pipelines.
/// </summary>
public enum ResourceSharingPolicy
{
    /// <summary>Fair sharing of resources among all pipelines.</summary>
    Fair,
    
    /// <summary>Priority-based resource allocation.</summary>
    PriorityBased,
    
    /// <summary>Exclusive resource usage when possible.</summary>
    Exclusive,
    
    /// <summary>Adaptive sharing based on workload characteristics.</summary>
    Adaptive
}

#endregion

#region Execution Context and Results

/// <summary>
/// Execution context providing environment and configuration for pipeline execution.
/// </summary>
public interface IPipelineExecutionContext
{
    /// <summary>Unique identifier for this execution context.</summary>
    Guid ContextId { get; }
    
    /// <summary>Configuration for this execution.</summary>
    IPipelineConfiguration Configuration { get; }
    
    /// <summary>Resource manager for this execution.</summary>
    IPipelineResourceManager ResourceManager { get; }
    
    /// <summary>Cache manager for storing intermediate results.</summary>
    IPipelineCacheManager CacheManager { get; }
    
    /// <summary>Telemetry collector for execution metrics.</summary>
    ITelemetryCollector TelemetryCollector { get; }
    
    /// <summary>Cancellation token for the execution.</summary>
    CancellationToken CancellationToken { get; }
    
    /// <summary>Logger for execution events and diagnostics.</summary>
    ILogger Logger { get; }
    
    /// <summary>Service provider for dependency injection.</summary>
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>Ambient properties available to all pipeline stages.</summary>
    IReadOnlyDictionary<string, object> Properties { get; }
}

/// <summary>
/// Specialized execution context for streaming pipeline operations.
/// </summary>
public interface IStreamingExecutionContext : IPipelineExecutionContext
{
    /// <summary>Configuration for streaming behavior.</summary>
    StreamingConfiguration StreamingConfig { get; }
    
    /// <summary>Buffer manager for streaming data.</summary>
    IStreamingBufferManager BufferManager { get; }
    
    /// <summary>Flow control for managing streaming throughput.</summary>
    IStreamFlowControl FlowControl { get; }
    
    /// <summary>Event publisher for streaming events.</summary>
    IStreamEventPublisher EventPublisher { get; }
}

/// <summary>
/// Configuration for streaming pipeline execution.
/// </summary>
public class StreamingConfiguration
{
    /// <summary>Size of internal buffers for streaming operations.</summary>
    public int BufferSize { get; set; } = 1024;
    
    /// <summary>Maximum number of items to process in parallel.</summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    
    /// <summary>Timeout for individual item processing.</summary>
    public TimeSpan ItemTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>Whether to preserve order in streaming results.</summary>
    public bool PreserveOrder { get; set; } = true;
    
    /// <summary>Backpressure handling strategy.</summary>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Buffer;
    
    /// <summary>Error handling for streaming operations.</summary>
    public StreamingErrorHandling ErrorHandling { get; set; } = StreamingErrorHandling.Skip;
}

/// <summary>
/// Backpressure handling strategies for streaming operations.
/// </summary>
public enum BackpressureStrategy
{
    /// <summary>Buffer items when consumer is slower than producer.</summary>
    Buffer,
    
    /// <summary>Drop oldest items when buffer is full.</summary>
    DropOldest,
    
    /// <summary>Drop newest items when buffer is full.</summary>
    DropNewest,
    
    /// <summary>Block producer when buffer is full.</summary>
    Block,
    
    /// <summary>Throw exception when buffer is full.</summary>
    Fail
}

/// <summary>
/// Error handling strategies specific to streaming operations.
/// </summary>
public enum StreamingErrorHandling
{
    /// <summary>Skip items that cause errors and continue processing.</summary>
    Skip,
    
    /// <summary>Stop the entire stream on first error.</summary>
    Stop,
    
    /// <summary>Retry failed items with backoff.</summary>
    Retry,
    
    /// <summary>Send failed items to dead letter queue.</summary>
    DeadLetter
}

/// <summary>
/// Detailed result from pipeline execution with comprehensive metrics and diagnostics.
/// </summary>
public interface IPipelineExecutionResult<TOutput>
{
    /// <summary>The output result from the pipeline execution.</summary>
    TOutput Result { get; }
    
    /// <summary>Whether the execution completed successfully.</summary>
    bool IsSuccess { get; }
    
    /// <summary>Any exceptions that occurred during execution.</summary>
    IReadOnlyList<Exception> Exceptions { get; }
    
    /// <summary>Total execution time for the pipeline.</summary>
    TimeSpan ExecutionTime { get; }
    
    /// <summary>Detailed metrics for each pipeline stage.</summary>
    IReadOnlyList<IStageExecutionMetrics> StageMetrics { get; }
    
    /// <summary>Resource utilization during execution.</summary>
    IResourceUtilizationMetrics ResourceMetrics { get; }
    
    /// <summary>Cache utilization statistics.</summary>
    ICacheUtilizationMetrics CacheMetrics { get; }
    
    /// <summary>Performance insights and recommendations.</summary>
    IPerformanceInsights PerformanceInsights { get; }
    
    /// <summary>Execution context that was used.</summary>
    IPipelineExecutionContext ExecutionContext { get; }
    
    /// <summary>Timestamp when execution started.</summary>
    DateTimeOffset StartTime { get; }
    
    /// <summary>Timestamp when execution completed.</summary>
    DateTimeOffset EndTime { get; }
}

#endregion

#region Pipeline Events

/// <summary>
/// Base class for all pipeline-related events.
/// </summary>
public class PipelineEvent
{
    /// <summary>Unique identifier for the event.</summary>
    public Guid EventId { get; } = Guid.NewGuid();

    /// <summary>Timestamp when the event occurred.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Pipeline identifier associated with the event.</summary>
    public Guid PipelineId { get; set; }

    /// <summary>Event severity level.</summary>
    public EventSeverity Severity { get; set; } = EventSeverity.Information;

    /// <summary>Human-readable description of the event.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Event type identifier.</summary>
    public PipelineEventType Type { get; set; }

    /// <summary>Event message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Additional event data.</summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>Stage identifier associated with the event (if applicable).</summary>
    public string? StageId { get; set; }

    /// <summary>Additional event metadata.</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Event fired when pipeline execution starts.
/// </summary>
public class PipelineExecutionStartedEvent : PipelineEvent
{
    /// <summary>Execution context for the started pipeline.</summary>
    public IPipelineExecutionContext ExecutionContext { get; set; } = null!;
    
    /// <summary>Configuration used for the execution.</summary>
    public IPipelineConfiguration Configuration { get; set; } = null!;
}

/// <summary>
/// Event fired when pipeline execution completes.
/// </summary>
public class PipelineExecutionCompletedEvent : PipelineEvent
{
    /// <summary>Whether the execution completed successfully.</summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>Total execution time.</summary>
    public TimeSpan ExecutionTime { get; set; }
    
    /// <summary>Summary of execution results.</summary>
    public string? ResultSummary { get; set; }
    
    /// <summary>Any exceptions that occurred.</summary>
    public List<Exception> Exceptions { get; set; } = new();
}

/// <summary>
/// Event fired when a pipeline stage starts execution.
/// </summary>
public class StageExecutionStartedEvent : PipelineEvent
{
    /// <summary>Name of the stage that started.</summary>
    public string StageName { get; set; } = string.Empty;
    
    /// <summary>Kernel name being executed in the stage.</summary>
    public string KernelName { get; set; } = string.Empty;
    
    /// <summary>Backend selected for stage execution.</summary>
    public string? Backend { get; set; }
    
    /// <summary>Stage configuration options.</summary>
    public PipelineStageOptions? StageOptions { get; set; }
}

/// <summary>
/// Event fired when a pipeline stage completes execution.
/// </summary>
public class StageExecutionCompletedEvent : PipelineEvent
{
    /// <summary>Name of the stage that completed.</summary>
    public string StageName { get; set; } = string.Empty;
    
    /// <summary>Whether the stage completed successfully.</summary>
    public bool IsSuccess { get; set; }
    
    /// <summary>Stage execution time.</summary>
    public TimeSpan ExecutionTime { get; set; }
    
    /// <summary>Any exceptions from the stage.</summary>
    public Exception? Exception { get; set; }
    
    /// <summary>Performance metrics for the stage.</summary>
    public IStageExecutionMetrics? Metrics { get; set; }
}

/// <summary>
/// Event severity levels for pipeline events.
/// </summary>
public enum EventSeverity
{
    /// <summary>Verbose debugging information.</summary>
    Verbose,

    /// <summary>General informational messages.</summary>
    Information,

    /// <summary>Warning conditions that don't prevent execution.</summary>
    Warning,

    /// <summary>Error conditions that may affect execution.</summary>
    Error,

    /// <summary>Critical errors that prevent execution.</summary>
    Critical
}

/// <summary>
/// Types of pipeline events.
/// </summary>
public enum PipelineEventType
{
    /// <summary>Pipeline execution started.</summary>
    Started,

    /// <summary>Pipeline execution completed.</summary>
    Completed,

    /// <summary>Pipeline execution failed.</summary>
    Failed,

    /// <summary>Pipeline stage started.</summary>
    StageStarted,

    /// <summary>Pipeline stage completed.</summary>
    StageCompleted,

    /// <summary>Pipeline stage failed.</summary>
    StageFailed,

    /// <summary>Pipeline was optimized.</summary>
    Optimized,

    /// <summary>Pipeline validation occurred.</summary>
    Validated
}

#endregion

#region Optimization Types

/// <summary>
/// Optimization strategies for pipeline performance tuning.
/// </summary>
public enum OptimizationStrategy
{
    /// <summary>Conservative optimization focusing on reliability.</summary>
    Conservative,
    
    /// <summary>Balanced optimization between performance and reliability.</summary>
    Balanced,
    
    /// <summary>Aggressive optimization maximizing performance.</summary>
    Aggressive,
    
    /// <summary>Adaptive optimization based on runtime characteristics.</summary>
    Adaptive,
    
    /// <summary>Memory-focused optimization minimizing allocations.</summary>
    MemoryOptimal,
    
    /// <summary>Throughput-focused optimization maximizing data processing rate.</summary>
    ThroughputOptimal,
    
    /// <summary>Latency-focused optimization minimizing response time.</summary>
    LatencyOptimal,
    
    /// <summary>Energy-efficient optimization minimizing power consumption.</summary>
    EnergyEfficient
}

/// <summary>
/// Context information for optimization operations.
/// </summary>
public class OptimizationContext
{
    /// <summary>Performance goals for the optimization.</summary>
    public PerformanceGoals Goals { get; set; } = new();
    
    /// <summary>Constraints that must be respected during optimization.</summary>
    public OptimizationConstraints Constraints { get; set; } = new();
    
    /// <summary>Historical execution data for informed optimization.</summary>
    public IExecutionHistory? ExecutionHistory { get; set; }
    
    /// <summary>Target hardware characteristics.</summary>
    public IHardwareProfile? HardwareProfile { get; set; }
    
    /// <summary>Input data characteristics for optimization decisions.</summary>
    public IInputCharacteristics? InputCharacteristics { get; set; }
}

/// <summary>
/// Performance goals for pipeline optimization.
/// </summary>
public class PerformanceGoals
{
    /// <summary>Target execution time for the pipeline.</summary>
    public TimeSpan? TargetExecutionTime { get; set; }
    
    /// <summary>Target throughput in items per second.</summary>
    public double? TargetThroughput { get; set; }
    
    /// <summary>Maximum acceptable memory usage.</summary>
    public long? MaxMemoryUsage { get; set; }
    
    /// <summary>Target CPU utilization percentage.</summary>
    public double? TargetCpuUtilization { get; set; }
    
    /// <summary>Target energy efficiency in operations per watt.</summary>
    public double? TargetEnergyEfficiency { get; set; }
    
    /// <summary>Relative importance weights for different performance aspects.</summary>
    public PerformanceWeights Weights { get; set; } = new();
}

/// <summary>
/// Weights for balancing different performance aspects during optimization.
/// </summary>
public class PerformanceWeights
{
    /// <summary>Weight for execution time optimization (0.0 to 1.0).</summary>
    public double ExecutionTime { get; set; } = 0.4;
    
    /// <summary>Weight for memory usage optimization (0.0 to 1.0).</summary>
    public double MemoryUsage { get; set; } = 0.2;
    
    /// <summary>Weight for throughput optimization (0.0 to 1.0).</summary>
    public double Throughput { get; set; } = 0.3;
    
    /// <summary>Weight for energy efficiency optimization (0.0 to 1.0).</summary>
    public double EnergyEfficiency { get; set; } = 0.1;
}

/// <summary>
/// Constraints that limit optimization decisions.
/// </summary>
public class OptimizationConstraints
{
    /// <summary>Maximum execution time allowed.</summary>
    public TimeSpan? MaxExecutionTime { get; set; }
    
    /// <summary>Maximum memory that can be allocated.</summary>
    public long? MaxMemoryAllocation { get; set; }
    
    /// <summary>Maximum CPU cores that can be used.</summary>
    public int? MaxCpuCores { get; set; }
    
    /// <summary>Backends that are not allowed for execution.</summary>
    public HashSet<string> DisallowedBackends { get; set; } = new();
    
    /// <summary>Backends that must be used if available.</summary>
    public HashSet<string> RequiredBackends { get; set; } = new();
    
    /// <summary>Whether optimization can modify pipeline structure.</summary>
    public bool AllowStructuralChanges { get; set; } = true;
    
    /// <summary>Whether optimization can reorder independent operations.</summary>
    public bool AllowReordering { get; set; } = true;
}

#endregion

#region Cache Configuration

/// <summary>
/// Cache policies for managing pipeline result caching.
/// </summary>
public abstract class CachePolicy
{
    /// <summary>Default cache policy with reasonable defaults.</summary>
    public static CachePolicy Default => new LRUCachePolicy(maxSize: 100, maxAge: TimeSpan.FromHours(1));
    
    /// <summary>Determines if a cached item should be evicted.</summary>
    public abstract bool ShouldEvict(ICacheEntry entry);
    
    /// <summary>Gets the priority for cache eviction (higher = keep longer).</summary>
    public abstract int GetEvictionPriority(ICacheEntry entry);
}

/// <summary>
/// LRU (Least Recently Used) cache policy implementation.
/// </summary>
public class LRUCachePolicy : CachePolicy
{
    /// <summary>Maximum number of items to keep in cache.</summary>
    public int MaxSize { get; }
    
    /// <summary>Maximum age for cached items.</summary>
    public TimeSpan? MaxAge { get; }
    
    public LRUCachePolicy(int maxSize, TimeSpan? maxAge = null)
    {
        MaxSize = maxSize;
        MaxAge = maxAge;
    }
    
    public override bool ShouldEvict(ICacheEntry entry)
    {
        return MaxAge.HasValue && DateTimeOffset.UtcNow - entry.CreatedAt > MaxAge.Value;
    }
    
    public override int GetEvictionPriority(ICacheEntry entry)
    {
        var timeSinceAccess = DateTimeOffset.UtcNow - entry.LastAccessedAt;
        return -(int)timeSinceAccess.TotalSeconds; // Negative so older = lower priority
    }
}

/// <summary>
/// Time-to-live cache policy implementation.
/// </summary>
public class TTLCachePolicy : CachePolicy
{
    /// <summary>Time-to-live for cached items.</summary>
    public TimeSpan TimeToLive { get; }
    
    public TTLCachePolicy(TimeSpan timeToLive)
    {
        TimeToLive = timeToLive;
    }
    
    public override bool ShouldEvict(ICacheEntry entry)
    {
        return DateTimeOffset.UtcNow - entry.CreatedAt > TimeToLive;
    }
    
    public override int GetEvictionPriority(ICacheEntry entry)
    {
        var timeUntilExpiry = TimeToLive - (DateTimeOffset.UtcNow - entry.CreatedAt);
        return (int)timeUntilExpiry.TotalSeconds;
    }
}

/// <summary>
/// Represents a cache entry with metadata.
/// </summary>
public interface ICacheEntry
{
    /// <summary>Unique key for the cache entry.</summary>
    string Key { get; }
    
    /// <summary>Cached value.</summary>
    object Value { get; }
    
    /// <summary>Size of the cached value in bytes.</summary>
    long Size { get; }
    
    /// <summary>When the entry was created.</summary>
    DateTimeOffset CreatedAt { get; }
    
    /// <summary>When the entry was last accessed.</summary>
    DateTimeOffset LastAccessedAt { get; set; }
    
    /// <summary>Number of times the entry has been accessed.</summary>
    long AccessCount { get; set; }
    
    /// <summary>Metadata associated with the cache entry.</summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

#endregion

#region Missing Memory Types

/// <summary>
/// Memory allocation strategies for pipeline operations.
/// </summary>
public enum MemoryAllocationStrategy
{
    /// <summary>Allocate memory as needed during execution.</summary>
    OnDemand,
    
    /// <summary>Pre-allocate based on estimated requirements.</summary>
    PreAllocated,
    
    /// <summary>Use memory pooling for frequent allocations.</summary>
    Pooled,
    
    /// <summary>Adaptive allocation based on runtime patterns.</summary>
    Adaptive,
    
    /// <summary>Optimal allocation strategy based on performance analysis.</summary>
    Optimal
}

#endregion