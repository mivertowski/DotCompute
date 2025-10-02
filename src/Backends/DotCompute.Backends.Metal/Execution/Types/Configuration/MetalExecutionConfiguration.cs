// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Configuration;

/// <summary>
/// Configuration for Metal execution optimization
/// </summary>
public sealed class MetalExecutionConfiguration
{
    /// <summary>
    /// Execution strategy to use
    /// </summary>
    public MetalExecutionStrategy Strategy { get; set; } = MetalExecutionStrategy.Balanced;

    /// <summary>
    /// Synchronization mode
    /// </summary>
    public MetalSynchronizationMode SynchronizationMode { get; set; } = MetalSynchronizationMode.Asynchronous;

    /// <summary>
    /// Memory allocation strategy
    /// </summary>
    public MetalMemoryStrategy MemoryStrategy { get; set; } = MetalMemoryStrategy.Default;

    /// <summary>
    /// Platform-specific optimizations
    /// </summary>
    public MetalPlatformOptimization PlatformOptimization { get; set; } = MetalPlatformOptimization.Generic;

    /// <summary>
    /// Profiling level
    /// </summary>
    public MetalProfilingLevel ProfilingLevel { get; set; } = MetalProfilingLevel.Basic;

    /// <summary>
    /// Metrics to collect
    /// </summary>
    public MetalMetricsCategory MetricsCategories { get; set; } = MetalMetricsCategory.Timing | MetalMetricsCategory.Memory;

    /// <summary>
    /// Maximum number of concurrent operations
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = 16;

    /// <summary>
    /// Maximum command buffer pool size
    /// </summary>
    public int MaxCommandBufferPoolSize { get; set; } = 32;

    /// <summary>
    /// Maximum event pool size
    /// </summary>
    public int MaxEventPoolSize { get; set; } = 64;

    /// <summary>
    /// Enable automatic resource cleanup
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// Cleanup interval for automatic cleanup
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Enable performance optimizations
    /// </summary>
    public bool EnableOptimizations { get; set; } = true;

    /// <summary>
    /// Enable debug mode with additional validation
    /// </summary>
    public bool EnableDebugMode { get; set; }

}