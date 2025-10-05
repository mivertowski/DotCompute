// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Enums;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Configuration settings for pipeline optimization operations.
/// Controls optimization behavior, constraints, and target metrics.
/// </summary>
public sealed class PipelineOptimizationSettings
{
    /// <summary>
    /// Gets or sets the optimization level to apply.
    /// </summary>
    public Types.OptimizationLevel Level { get; set; } = Types.OptimizationLevel.Default;

    /// <summary>
    /// Gets or sets the specific optimization types to apply.
    /// </summary>
    public OptimizationType OptimizationTypes { get; set; } = OptimizationType.Conservative;

    /// <summary>
    /// Gets or sets the maximum time allowed for optimization.
    /// </summary>
    public TimeSpan MaxOptimizationTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to validate optimizations for correctness.
    /// </summary>
    public bool ValidateOptimizations { get; set; } = true;

    /// <summary>
    /// Gets or sets the target backends to optimize for.
    /// </summary>
    public IList<string> TargetBackends { get; init; } = [];

    /// <summary>
    /// Gets or sets memory constraints for optimization.
    /// </summary>
    public MemoryConstraints? MemoryConstraints { get; set; }

    /// <summary>
    /// Gets or sets performance targets for optimization.
    /// </summary>
    public PerformanceTargets? PerformanceTargets { get; set; }

    /// <summary>
    /// Gets or sets whether to preserve debugging information after optimization.
    /// </summary>
    public bool PreserveDebugInfo { get; set; }

    /// <summary>
    /// Gets or sets whether to enable aggressive optimizations that may affect numerical precision.
    /// </summary>
    public bool AllowPrecisionLoss { get; set; }

    /// <summary>
    /// Gets or sets the parallel degree for optimization operations.
    /// </summary>
    public int OptimizationParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets custom optimization parameters.
    /// </summary>
    public IDictionary<string, object> CustomParameters { get; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the strategy for handling optimization failures.
    /// </summary>
    public OptimizationFailureStrategy FailureStrategy { get; set; } = OptimizationFailureStrategy.FallbackToOriginal;

    /// <summary>
    /// Gets or sets profiling configuration for optimization analysis.
    /// </summary>
    public OptimizationProfilingConfig? ProfilingConfig { get; set; }

    /// <summary>
    /// Gets or sets cache settings for optimization results.
    /// </summary>
    public OptimizationCacheSettings? CacheSettings { get; set; }

    /// <summary>
    /// Gets or sets the minimum performance improvement threshold to apply optimizations.
    /// Value is a percentage (e.g., 0.05 for 5% improvement).
    /// </summary>
    public double MinimumImprovementThreshold { get; set; } = 0.05;

    /// <summary>
    /// Gets or sets whether to enable kernel fusion optimization.
    /// </summary>
    public bool EnableKernelFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable stage reordering optimization.
    /// </summary>
    public bool EnableStageReordering { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable memory optimization strategies.
    /// </summary>
    public bool EnableMemoryOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable parallel merging of operations.
    /// </summary>
    public bool EnableParallelMerging { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable fusion optimization (alias for EnableKernelFusion).
    /// </summary>
    public bool EnableFusion
    {
        get => EnableKernelFusion;
        set => EnableKernelFusion = value;
    }

    /// <summary>
    /// Creates a copy of the current settings.
    /// </summary>
    /// <returns>A new instance with the same configuration</returns>
    public PipelineOptimizationSettings Clone()
    {
        var clone = new PipelineOptimizationSettings
        {
            Level = Level,
            OptimizationTypes = OptimizationTypes,
            MaxOptimizationTime = MaxOptimizationTime,
            ValidateOptimizations = ValidateOptimizations,
            MemoryConstraints = MemoryConstraints?.Clone(),
            PerformanceTargets = PerformanceTargets?.Clone(),
            PreserveDebugInfo = PreserveDebugInfo,
            AllowPrecisionLoss = AllowPrecisionLoss,
            OptimizationParallelism = OptimizationParallelism,
            FailureStrategy = FailureStrategy,
            ProfilingConfig = ProfilingConfig?.Clone(),
            CacheSettings = CacheSettings?.Clone(),
            MinimumImprovementThreshold = MinimumImprovementThreshold,
            EnableKernelFusion = EnableKernelFusion,
            EnableStageReordering = EnableStageReordering,
            EnableMemoryOptimization = EnableMemoryOptimization,
            EnableParallelMerging = EnableParallelMerging,
            EnableFusion = EnableFusion
        };

        // Copy read-only collections
        foreach (var backend in TargetBackends)
        {
            clone.TargetBackends.Add(backend);
        }

        foreach (var kvp in CustomParameters)
        {
            clone.CustomParameters[kvp.Key] = kvp.Value;
        }

        return clone;
    }
}

/// <summary>
/// Defines optimization levels with increasing aggressiveness.
/// </summary>
public enum OptimizationLevel
{
    /// <summary>
    /// No optimizations applied.
    /// </summary>
    None,

    /// <summary>
    /// Basic safe optimizations with minimal risk.
    /// </summary>
    Basic,

    /// <summary>
    /// Balanced optimizations with good safety-performance trade-off.
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive optimizations that may require careful validation.
    /// </summary>
    Aggressive,

    /// <summary>
    /// Maximum optimizations with potential correctness risks.
    /// </summary>
    Maximum
}

/// <summary>
/// Strategy for handling optimization failures.
/// </summary>
public enum OptimizationFailureStrategy
{
    /// <summary>
    /// Fallback to the original unoptimized pipeline.
    /// </summary>
    FallbackToOriginal,

    /// <summary>
    /// Retry with less aggressive optimization settings.
    /// </summary>
    RetryWithLowerLevel,

    /// <summary>
    /// Throw an exception and fail the operation.
    /// </summary>
    ThrowException,

    /// <summary>
    /// Use the best partial optimization achieved so far.
    /// </summary>
    UseBestPartial
}


/// <summary>
/// Performance targets for optimization operations.
/// </summary>
public sealed class PerformanceTargets
{
    /// <summary>
    /// Gets or sets the target execution time improvement factor.
    /// </summary>
    public double TargetSpeedupFactor { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the target throughput in operations per second.
    /// </summary>
    public double TargetThroughput { get; set; } = double.MaxValue;

    /// <summary>
    /// Gets or sets the target latency in milliseconds.
    /// </summary>
    public double TargetLatencyMs { get; set; } = double.MaxValue;

    /// <summary>
    /// Gets or sets the target energy efficiency improvement.
    /// </summary>
    public double TargetEnergyEfficiency { get; set; } = 1.0;

    /// <summary>
    /// Creates a copy of the current targets.
    /// </summary>
    /// <returns>A new instance with the same configuration</returns>
    public PerformanceTargets Clone()
    {
        return new PerformanceTargets
        {
            TargetSpeedupFactor = TargetSpeedupFactor,
            TargetThroughput = TargetThroughput,
            TargetLatencyMs = TargetLatencyMs,
            TargetEnergyEfficiency = TargetEnergyEfficiency
        };
    }
}

/// <summary>
/// Configuration for profiling during optimization.
/// </summary>
public sealed class OptimizationProfilingConfig
{
    /// <summary>
    /// Gets or sets whether to enable detailed profiling.
    /// </summary>
    public bool EnableProfiling { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling interval for profiling.
    /// </summary>
    public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets whether to collect memory usage statistics.
    /// </summary>
    public bool CollectMemoryStats { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to collect CPU usage statistics.
    /// </summary>
    public bool CollectCpuStats { get; set; } = true;

    /// <summary>
    /// Creates a copy of the current configuration.
    /// </summary>
    /// <returns>A new instance with the same configuration</returns>
    public OptimizationProfilingConfig Clone()
    {
        return new OptimizationProfilingConfig
        {
            EnableProfiling = EnableProfiling,
            SamplingInterval = SamplingInterval,
            CollectMemoryStats = CollectMemoryStats,
            CollectCpuStats = CollectCpuStats
        };
    }
}

/// <summary>
/// Cache settings for optimization results.
/// </summary>
public sealed class OptimizationCacheSettings
{
    /// <summary>
    /// Gets or sets whether to enable caching of optimization results.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache expiration time.
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the maximum cache size in bytes.
    /// </summary>
    public long MaxCacheSize { get; set; } = 1024 * 1024 * 1024; // 1 GB

    /// <summary>
    /// Gets or sets the cache key generator for optimization results.
    /// </summary>
    public string CacheKeyPrefix { get; set; } = "opt_";

    /// <summary>
    /// Creates a copy of the current settings.
    /// </summary>
    /// <returns>A new instance with the same configuration</returns>
    public OptimizationCacheSettings Clone()
    {
        return new OptimizationCacheSettings
        {
            EnableCaching = EnableCaching,
            CacheExpiration = CacheExpiration,
            MaxCacheSize = MaxCacheSize,
            CacheKeyPrefix = CacheKeyPrefix
        };
    }
}
