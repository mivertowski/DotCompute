// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Abstractions.Pipelines;

/// <summary>
/// Provides real-time metrics for pipeline execution
/// </summary>
public interface IPipelineMetrics
{
    /// <summary>
    /// Total execution time for the pipeline
    /// </summary>
    TimeSpan TotalExecutionTime { get; }
    
    /// <summary>
    /// Number of completed stages
    /// </summary>
    int CompletedStages { get; }
    
    /// <summary>
    /// Current throughput in operations per second
    /// </summary>
    double Throughput { get; }
    
    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    long MemoryUsage { get; }
}

/// <summary>
/// Configuration for pipeline execution
/// </summary>
public interface IPipelineConfiguration
{
    /// <summary>
    /// Maximum execution time before timeout
    /// </summary>
    TimeSpan Timeout { get; set; }
    
    /// <summary>
    /// Enable parallel execution of independent stages
    /// </summary>
    bool EnableParallelExecution { get; set; }
    
    /// <summary>
    /// Memory optimization level
    /// </summary>
    MemoryOptimizationLevel OptimizationLevel { get; set; }
}

/// <summary>
/// Represents a kernel execution stage in the pipeline
/// </summary>
/// <typeparam name="TInput">Input data type</typeparam>
/// <typeparam name="TOutput">Output data type</typeparam>
public interface IKernelStage<TInput, TOutput> : IKernelStage
{
    /// <summary>
    /// Execute the stage with typed input and output
    /// </summary>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for kernel stages
/// </summary>
public interface IKernelStage
{
    /// <summary>
    /// Stage identifier
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Stage execution state
    /// </summary>
    StageState State { get; }
    
    /// <summary>
    /// Execute the stage with generic parameters
    /// </summary>
    Task<object> ExecuteAsync(object input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Partitioning strategy for parallel execution
/// </summary>
/// <typeparam name="T">Data type</typeparam>
public interface IPartitionStrategy<T>
{
    /// <summary>
    /// Partition data for parallel processing
    /// </summary>
    IEnumerable<IEnumerable<T>> Partition(IEnumerable<T> data, int partitionCount);
}

/// <summary>
/// Data analysis interface for pipelines
/// </summary>
/// <typeparam name="T">Data type to analyze</typeparam>
public interface IDataAnalyzer<T>
{
    /// <summary>
    /// Analyze data and provide insights
    /// </summary>
    Task<IAnalysisResult> AnalyzeAsync(IEnumerable<T> data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Analysis result interface
/// </summary>
public interface IAnalysisResult
{
    /// <summary>
    /// Analysis timestamp
    /// </summary>
    DateTime Timestamp { get; }
    
    /// <summary>
    /// Analysis metrics
    /// </summary>
    IReadOnlyDictionary<string, object> Metrics { get; }
    
    /// <summary>
    /// Analysis recommendations
    /// </summary>
    IReadOnlyList<string> Recommendations { get; }
}

/// <summary>
/// Adaptive caching options for pipeline data
/// </summary>
public class AdaptiveCacheOptions
{
    /// <summary>
    /// Enable adaptive caching based on access patterns
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Maximum cache size in bytes
    /// </summary>
    public long MaxCacheSize { get; set; } = 100 * 1024 * 1024; // 100MB default
    
    /// <summary>
    /// Cache eviction policy
    /// </summary>
    public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.LRU;
}

/// <summary>
/// Data validation interface for pipeline inputs
/// </summary>
public interface IDataValidator
{
    /// <summary>
    /// Validate data before pipeline execution
    /// </summary>
    Task<ValidationResult> ValidateAsync(object data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data validation result
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// Validation errors if any
    /// </summary>
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Validation warnings
    /// </summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Memory optimization levels
/// </summary>
public enum MemoryOptimizationLevel
{
    /// <summary>
    /// No optimization, prioritize speed
    /// </summary>
    None,
    
    /// <summary>
    /// Basic memory management
    /// </summary>
    Basic,
    
    /// <summary>
    /// Aggressive memory optimization
    /// </summary>
    Aggressive
}

/// <summary>
/// Stage execution states
/// </summary>
public enum StageState
{
    /// <summary>
    /// Stage not yet started
    /// </summary>
    Pending,
    
    /// <summary>
    /// Stage currently executing
    /// </summary>
    Running,
    
    /// <summary>
    /// Stage completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Stage failed with error
    /// </summary>
    Failed,
    
    /// <summary>
    /// Stage was cancelled
    /// </summary>
    Cancelled
}

/// <summary>
/// Cache eviction policies
/// </summary>
public enum CacheEvictionPolicy
{
    /// <summary>
    /// Least Recently Used
    /// </summary>
    LRU,
    
    /// <summary>
    /// First In First Out
    /// </summary>
    FIFO,
    
    /// <summary>
    /// Least Frequently Used
    /// </summary>
    LFU
}