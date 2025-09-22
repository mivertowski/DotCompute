// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.Serialization;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.Logging;
namespace DotCompute.Linq.Pipelines.Core;
{
#region Exception Types
/// <summary>
/// Custom exception type for pipeline orchestration failures, providing detailed diagnostic information
/// and context for debugging complex pipeline execution issues.
/// </summary>
[Serializable]
public class PipelineOrchestrationException : Exception
{
    /// <summary>
    /// Gets the stage name where the error occurred.
    /// </summary>
    public string? StageName { get; }
    /// Gets the pipeline identifier associated with the failure.
    public Guid? PipelineId { get; }
    /// Gets the backend name where the error occurred.
    public string? BackendName { get; }
    /// Gets additional diagnostic information about the failure.
    public IReadOnlyDictionary<string, object> DiagnosticInfo { get; }
    /// Gets the execution context at the time of failure.
    public IPipelineExecutionContext? ExecutionContext { get; }
    /// Gets the error type classification for this exception.
    public PipelineErrorType ErrorType { get; }
    /// Initializes a new instance of the <see cref="PipelineOrchestrationException"/> class.
    public PipelineOrchestrationException() : base("Pipeline orchestration failed.")
    {
        DiagnosticInfo = new Dictionary<string, object>();
        ErrorType = PipelineErrorType.None;
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public PipelineOrchestrationException(string message) : base(message)
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public PipelineOrchestrationException(string message, Exception innerException) : base(message, innerException)
    /// <param name="diagnosticInfo">Additional diagnostic information.</param>
    public PipelineOrchestrationException(
        {
        string message,
        string? stageName = null,
        Guid? pipelineId = null,
        string? backendName = null,
        PipelineErrorType errorType = PipelineErrorType.None,
        IPipelineExecutionContext? executionContext = null,
        IReadOnlyDictionary<string, object>? diagnosticInfo = null,
        Exception? innerException = null) : base(message, innerException)
        StageName = stageName;
        PipelineId = pipelineId;
        BackendName = backendName;
        ErrorType = errorType;
        ExecutionContext = executionContext;
        DiagnosticInfo = diagnosticInfo ?? new Dictionary<string, object>();
    /// Initializes a new instance of the <see cref="PipelineOrchestrationException"/> class with serialized data.
    /// <param name="info">The serialization info.</param>
    /// <param name="context">The streaming context.</param>
    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    protected PipelineOrchestrationException(SerializationInfo info, StreamingContext context) : base(info, context)
        StageName = info.GetString(nameof(StageName));
        PipelineId = info.GetValue(nameof(PipelineId), typeof(Guid?)) as Guid?;
        BackendName = info.GetString(nameof(BackendName));
        ErrorType = (PipelineErrorType)(info.GetValue(nameof(ErrorType), typeof(PipelineErrorType)) ?? PipelineErrorType.None);
        DiagnosticInfo = info.GetValue(nameof(DiagnosticInfo), typeof(Dictionary<string, object>)) as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>();
    /// Sets the SerializationInfo with information about the exception.
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(StageName), StageName);
        info.AddValue(nameof(PipelineId), PipelineId);
        info.AddValue(nameof(BackendName), BackendName);
        info.AddValue(nameof(ErrorType), ErrorType);
        info.AddValue(nameof(DiagnosticInfo), DiagnosticInfo);
    /// Creates a detailed error message including all diagnostic information.
    /// <returns>A comprehensive error message with context.</returns>
    public override string ToString()
    {
        var details = new List<string> { base.ToString() };
        if (!string.IsNullOrEmpty(StageName))
        {
            details.Add($"Stage: {StageName}");
        }
        if (PipelineId.HasValue)
            details.Add($"Pipeline ID: {PipelineId.Value}");
        if (!string.IsNullOrEmpty(BackendName))
            details.Add($"Backend: {BackendName}");
        if (ErrorType != PipelineErrorType.None)
            details.Add($"Error Type: {ErrorType}");
        if (DiagnosticInfo.Count > 0)
            details.Add("Diagnostic Info:");
            foreach (var kvp in DiagnosticInfo)
            {
                details.Add($"  {kvp.Key}: {kvp.Value}");
            }
        return string.Join(Environment.NewLine, details);
}
#endregion
#region Optimization Types
/// Defines optimization strategies for pipeline execution, balancing performance, reliability, and resource utilization.
/// Each strategy represents a different approach to optimizing pipeline execution based on specific use cases and constraints.
public enum OptimizationStrategy
    {
    /// Conservative optimization focusing on reliability and predictable performance.
    /// Minimizes aggressive optimizations that could potentially cause instability.
    /// Best for: Production systems requiring high reliability.
    Conservative,
    /// Balanced optimization providing a good compromise between performance and reliability.
    /// Applies well-tested optimizations with moderate risk profiles.
    /// Best for: Most production workloads requiring good performance with reasonable stability.
    Balanced,
    /// Aggressive optimization focusing on maximum performance with higher risk tolerance.
    /// Applies advanced optimizations that may trade some stability for performance gains.
    /// Best for: Performance-critical applications where maximum speed is essential.
    Aggressive,
    /// AI-powered adaptive optimization that learns from execution patterns and runtime characteristics.
    /// Continuously optimizes based on workload patterns, resource availability, and historical performance.
    /// Best for: Long-running applications with varying workload patterns.
    Adaptive,
    /// Memory-focused optimization prioritizing efficient memory usage and reduced allocation overhead.
    /// Optimizes for scenarios where memory constraints are more critical than raw computational speed.
    /// Best for: Memory-constrained environments or large-scale batch processing.
    MemoryOptimized,
    /// Latency-focused optimization prioritizing low response times and minimal execution delays.
    /// Optimizes for interactive workloads where consistent low latency is more important than throughput.
    /// Best for: Real-time processing and interactive applications.
    LatencyOptimized,
    /// Throughput-focused optimization prioritizing maximum data processing capacity.
    /// Optimizes for batch processing scenarios where overall throughput is more important than individual operation latency.
    /// Best for: Large-scale data processing and ETL workloads.
    ThroughputOptimized
#region Cache Configuration Types
/// Comprehensive configuration options for adaptive caching in pipeline execution.
/// Provides fine-grained control over caching behavior, performance thresholds, and resource management.
public class AdaptiveCacheOptions
    {
    /// Gets or sets the maximum size of the cache in bytes.
    /// When the cache exceeds this size, the eviction policy will be applied to free space.
    /// Default: 100 MB.
    public long MaxCacheSize { get; set; } = 100 * 1024 * 1024; // 100 MB
    /// Gets or sets the maximum number of entries allowed in the cache.
    /// Provides an upper bound on cache entries regardless of their individual sizes.
    /// Default: 10,000 entries.
    public int MaxEntries { get; set; } = 10_000;
    /// Gets or sets the default time-to-live (TTL) for cache entries.
    /// Entries will be automatically evicted after this duration unless refreshed.
    /// Default: 1 hour.
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(1);
    /// Gets or sets the cache eviction policy used when the cache reaches capacity limits.
    /// Determines which entries are removed first when space is needed.
    /// Default: LeastRecentlyUsed (LRU).
    public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.LeastRecentlyUsed;
    /// Gets or sets whether automatic cache key generation is enabled.
    /// When true, the system will generate cache keys based on input parameters and execution context.
    /// Default: true.
    public bool AutoKeyGeneration { get; set; } = true;
    /// Gets or sets whether cache policy adaptation is enabled.
    /// When true, the cache will adapt its behavior based on access patterns and performance metrics.
    public bool PolicyAdaptation { get; set; } = true;
    /// Gets or sets the performance improvement threshold for caching decisions.
    /// Only operations that show at least this percentage improvement will be cached.
    /// Range: 0.0 to 1.0, where 0.1 represents 10% improvement.
    /// Default: 0.1 (10%).
    public double PerformanceThreshold { get; set; } = 0.1;
    /// Gets or sets the minimum execution time for an operation to be considered for caching.
    /// Very fast operations may not benefit from caching overhead.
    /// Default: 10 milliseconds.
    public TimeSpan MinExecutionTimeForCaching { get; set; } = TimeSpan.FromMilliseconds(10);
    /// Gets or sets whether cache warming is enabled.
    /// When true, the system will proactively populate frequently accessed cache entries.
    /// Default: false.
    public bool EnableCacheWarming { get; set; } = false;
    /// Gets or sets whether cache compression is enabled for stored entries.
    /// Can reduce memory usage at the cost of CPU overhead for compression/decompression.
    public bool EnableCompression { get; set; } = false;
    /// Gets or sets whether cache statistics collection is enabled.
    /// Provides detailed metrics about cache performance and hit rates.
    public bool EnableStatistics { get; set; } = true;
    /// Gets or sets the interval for cache maintenance operations.
    /// Determines how frequently expired entries are cleaned up and statistics are updated.
    /// Default: 5 minutes.
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromMinutes(5);
    /// Gets or sets custom cache key generators for specific data types.
    /// Allows fine-tuned control over how cache keys are generated for different scenarios.
    public Dictionary<Type, Func<object, string>> CustomKeyGenerators { get; set; } = [];
    /// Gets or sets cache partitioning configuration for multi-tenant scenarios.
    /// Allows isolating cache entries by tenant, user, or other partitioning criteria.
    public CachePartitioningOptions? PartitioningOptions { get; set; }
    /// Gets or sets the concurrency level for cache access.
    /// Higher values can improve performance in highly concurrent scenarios but use more memory.
    /// Default: Environment.ProcessorCount.
    public int ConcurrencyLevel { get; set; } = Environment.ProcessorCount;
    /// Gets or sets whether cache invalidation cascading is enabled.
    /// When true, related cache entries will be invalidated together to maintain consistency.
    public bool EnableCascadingInvalidation { get; set; } = false;
    /// Gets or sets tags that can be used to group and invalidate cache entries.
    /// Useful for invalidating related entries based on business logic.
    public HashSet<string> DefaultTags { get; set; } = [];
    /// Validates the configuration options and throws an exception if invalid.
    /// <exception cref="ArgumentOutOfRangeException">Thrown when configuration values are outside acceptable ranges.</exception>
    public void Validate()
    {
        if (MaxCacheSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxCacheSize), "Cache size must be positive.");
        if (MaxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxEntries), "Max entries must be positive.");
        if (DefaultTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(DefaultTtl), "TTL must be positive.");
        if (PerformanceThreshold < 0.0 || PerformanceThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(PerformanceThreshold), "Performance threshold must be between 0.0 and 1.0.");
        if (MinExecutionTimeForCaching < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MinExecutionTimeForCaching), "Minimum execution time cannot be negative.");
        if (MaintenanceInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MaintenanceInterval), "Maintenance interval must be positive.");
        if (ConcurrencyLevel <= 0)
            throw new ArgumentOutOfRangeException(nameof(ConcurrencyLevel), "Concurrency level must be positive.");
    /// Creates a copy of the current options with specified overrides.
    /// <param name="overrides">Action to apply overrides to the copied options.</param>
    /// <returns>A new instance with applied overrides.</returns>
    public AdaptiveCacheOptions Clone(Action<AdaptiveCacheOptions>? overrides = null)
        var clone = new AdaptiveCacheOptions
            MaxCacheSize = MaxCacheSize,
            MaxEntries = MaxEntries,
            DefaultTtl = DefaultTtl,
            EvictionPolicy = EvictionPolicy,
            AutoKeyGeneration = AutoKeyGeneration,
            PolicyAdaptation = PolicyAdaptation,
            PerformanceThreshold = PerformanceThreshold,
            MinExecutionTimeForCaching = MinExecutionTimeForCaching,
            EnableCacheWarming = EnableCacheWarming,
            EnableCompression = EnableCompression,
            EnableStatistics = EnableStatistics,
            MaintenanceInterval = MaintenanceInterval,
            CustomKeyGenerators = new Dictionary<Type, Func<object, string>>(CustomKeyGenerators),
            PartitioningOptions = PartitioningOptions,
            ConcurrencyLevel = ConcurrencyLevel,
            EnableCascadingInvalidation = EnableCascadingInvalidation,
            DefaultTags = new HashSet<string>(DefaultTags)
        };
        overrides?.Invoke(clone);
        return clone;
/// Cache eviction policies for determining which entries to remove when the cache reaches capacity.
/// Each policy optimizes for different access patterns and use cases.
public enum CacheEvictionPolicy
    {
    /// Least Recently Used (LRU) - Removes entries that haven't been accessed recently.
    /// Best for: General-purpose caching with temporal locality.
    LeastRecentlyUsed,
    /// Least Frequently Used (LFU) - Removes entries with the lowest access frequency.
    /// Best for: Workloads with stable access patterns over time.
    LeastFrequentlyUsed,
    /// First In, First Out (FIFO) - Removes the oldest entries regardless of access patterns.
    /// Best for: Simple cache management with predictable eviction behavior.
    FirstInFirstOut,
    /// Random - Removes random entries from the cache.
    /// Best for: Scenarios where other policies show poor performance due to specific access patterns.
    Random,
    /// Time-based - Removes entries based on their age and TTL settings.
    /// Best for: Caches where data freshness is more important than access patterns.
    TimeBased,
    /// Size-based - Prioritizes removal of larger entries to free maximum space.
    /// Best for: Memory-constrained environments where space efficiency is critical.
    SizeBased,
    /// Adaptive - Dynamically chooses the best eviction strategy based on observed patterns.
    /// Best for: Workloads with changing access patterns over time.
    Adaptive
/// Configuration options for cache partitioning in multi-tenant or multi-user scenarios.
/// Provides isolation and resource management capabilities for different cache consumers.
public class CachePartitioningOptions
    {
    /// Gets or sets whether partitioning is enabled.
    public bool EnablePartitioning { get; set; } = false;
    /// Gets or sets the partitioning strategy to use.
    /// Default: ByTag.
    public CachePartitioningStrategy Strategy { get; set; } = CachePartitioningStrategy.ByTag;
    /// Gets or sets the maximum number of partitions allowed.
    /// Default: 1000.
    public int MaxPartitions { get; set; } = 1000;
    /// Gets or sets the default partition size limit as a percentage of total cache size.
    /// Range: 0.0 to 1.0, where 0.1 represents 10% of total cache.
    public double DefaultPartitionSizeLimit { get; set; } = 0.1;
    /// Gets or sets custom partition size limits for specific partitions.
    /// Key is the partition identifier, value is the size limit as a percentage of total cache.
    public Dictionary<string, double> PartitionSizeLimits { get; set; } = [];
    /// Gets or sets whether cross-partition sharing is allowed when a partition is under-utilized.
    public bool AllowCrossPartitionSharing { get; set; } = true;
/// Strategies for cache partitioning.
public enum CachePartitioningStrategy
    {
    /// Partition by cache entry tags.
    ByTag,
    /// Partition by key prefix patterns.
    ByKeyPrefix,
    /// Partition by data type.
    ByDataType,
    /// Custom partitioning logic provided by the application.
    Custom
}
}
