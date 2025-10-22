// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Execution.Graph.Types;

namespace DotCompute.Backends.Metal.Execution.Graph.Configuration;

/// <summary>
/// Configuration settings for Metal compute graph construction and execution.
/// </summary>
public class MetalGraphConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic kernel fusion optimization.
    /// </summary>
    public bool EnableKernelFusion { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable memory access pattern optimization.
    /// </summary>
    public bool EnableMemoryOptimization { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable parallel execution of independent nodes.
    /// </summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable command buffer batching.
    /// </summary>
    public bool EnableCommandBufferBatching { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable Apple Silicon specific optimizations.
    /// </summary>
    public bool EnableAppleSiliconOptimizations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate the graph structure before execution.
    /// </summary>
    public bool EnableGraphValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to collect detailed performance metrics.
    /// </summary>
    public bool EnablePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of nodes that can be fused together.
    /// </summary>
    public int MaxKernelFusionDepth { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum number of operations to batch in a single command buffer.
    /// </summary>
    public int MaxCommandBufferSize { get; set; } = 64;

    /// <summary>
    /// Gets or sets the maximum number of concurrent operations during graph execution.
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the memory allocation strategy for graph operations.
    /// </summary>
    public MetalMemoryStrategy MemoryStrategy { get; set; } = MetalMemoryStrategy.Balanced;

    /// <summary>
    /// Gets or sets the preferred threadgroup size for compute kernels.
    /// </summary>
    public uint PreferredThreadgroupSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum threadgroup size allowed for compute kernels.
    /// </summary>
    public uint MaxThreadgroupSize { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the timeout for individual node execution in milliseconds.
    /// </summary>
    public int NodeExecutionTimeoutMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Gets or sets the timeout for overall graph execution in milliseconds.
    /// </summary>
    public int GraphExecutionTimeoutMs { get; set; } = 300000; // 5 minutes

    /// <summary>
    /// Gets or sets the priority level for graph execution.
    /// </summary>
    public MetalExecutionPriority ExecutionPriority { get; set; } = MetalExecutionPriority.Normal;

    /// <summary>
    /// Gets or sets custom optimization parameters.
    /// </summary>
    public MetalOptimizationParameters OptimizationParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets resource limits for graph execution.
    /// </summary>
    public MetalResourceLimits ResourceLimits { get; set; } = new();

    /// <summary>
    /// Gets or sets debugging options for graph execution.
    /// </summary>
    public MetalGraphDebuggingOptions DebuggingOptions { get; set; } = new();

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    /// <returns>A list of validation errors, or an empty list if valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (MaxKernelFusionDepth < 1)
        {
            errors.Add("MaxKernelFusionDepth must be at least 1");
        }

        if (MaxCommandBufferSize < 1)
        {
            errors.Add("MaxCommandBufferSize must be at least 1");
        }

        if (MaxConcurrentOperations < 1)
        {
            errors.Add("MaxConcurrentOperations must be at least 1");
        }

        if (PreferredThreadgroupSize == 0)
        {
            errors.Add("PreferredThreadgroupSize must be greater than 0");
        }

        if (MaxThreadgroupSize == 0)
        {
            errors.Add("MaxThreadgroupSize must be greater than 0");
        }

        if (PreferredThreadgroupSize > MaxThreadgroupSize)
        {
            errors.Add("PreferredThreadgroupSize cannot be greater than MaxThreadgroupSize");
        }

        if (NodeExecutionTimeoutMs <= 0)
        {
            errors.Add("NodeExecutionTimeoutMs must be positive");
        }

        if (GraphExecutionTimeoutMs <= 0)
        {
            errors.Add("GraphExecutionTimeoutMs must be positive");
        }

        if (NodeExecutionTimeoutMs >= GraphExecutionTimeoutMs)
        {
            errors.Add("NodeExecutionTimeoutMs should be less than GraphExecutionTimeoutMs");
        }

        // Validate optimization parameters
        var optimizationErrors = OptimizationParameters.Validate();
        errors.AddRange(optimizationErrors.Select(error => $"OptimizationParameters: {error}"));

        // Validate resource limits
        var resourceErrors = ResourceLimits.Validate();
        errors.AddRange(resourceErrors.Select(error => $"ResourceLimits: {error}"));

        return errors;
    }

    /// <summary>
    /// Creates a configuration optimized for Apple Silicon devices.
    /// </summary>
    /// <returns>A new configuration instance optimized for Apple Silicon.</returns>
    public static MetalGraphConfiguration CreateAppleSiliconOptimized()
    {
        return new MetalGraphConfiguration
        {
            EnableAppleSiliconOptimizations = true,
            MemoryStrategy = MetalMemoryStrategy.UnifiedMemory,
            MaxConcurrentOperations = Environment.ProcessorCount * 2, // Take advantage of unified memory
            PreferredThreadgroupSize = 512, // Optimal for Apple Silicon
            MaxThreadgroupSize = 1024,
            OptimizationParameters = new MetalOptimizationParameters
            {
                EnableKernelFusion = true,
                EnableMemoryCoalescing = true,
                EnableCommandBufferBatching = true,
                EnableAppleSiliconOptimizations = true,
                MemoryStrategy = Types.MetalMemoryStrategy.UnifiedMemory
            }
        };
    }

    /// <summary>
    /// Creates a configuration optimized for discrete GPUs.
    /// </summary>
    /// <returns>A new configuration instance optimized for discrete GPUs.</returns>
    public static MetalGraphConfiguration CreateDiscreteGpuOptimized()
    {
        return new MetalGraphConfiguration
        {
            EnableAppleSiliconOptimizations = false,
            MemoryStrategy = MetalMemoryStrategy.Aggressive,
            MaxConcurrentOperations = Environment.ProcessorCount,
            PreferredThreadgroupSize = 256, // Conservative for discrete GPUs
            MaxThreadgroupSize = 1024,
            OptimizationParameters = new MetalOptimizationParameters
            {
                EnableKernelFusion = true,
                EnableMemoryCoalescing = true,
                EnableCommandBufferBatching = true,
                EnableAppleSiliconOptimizations = false,
                MemoryStrategy = Types.MetalMemoryStrategy.Aggressive
            }
        };
    }

    /// <summary>
    /// Creates a configuration optimized for debugging and development.
    /// </summary>
    /// <returns>A new configuration instance optimized for debugging.</returns>
    public static MetalGraphConfiguration CreateDebugOptimized()
    {
        return new MetalGraphConfiguration
        {
            EnableGraphValidation = true,
            EnablePerformanceMetrics = true,
            MaxConcurrentOperations = 1, // Single-threaded for easier debugging
            NodeExecutionTimeoutMs = 60000, // Longer timeouts for debugging
            GraphExecutionTimeoutMs = 600000,
            DebuggingOptions = new MetalGraphDebuggingOptions
            {
                EnableDetailedLogging = true,
                EnableNodeTimingMeasurement = true,
                EnableMemoryTracker = true,
                EnableValidationLayer = true,
                LogExecutionOrder = true
            }
        };
    }
}

/// <summary>
/// Defines execution priority levels for Metal compute graphs.
/// </summary>
public enum MetalExecutionPriority
{
    /// <summary>Low priority execution.</summary>
    Low = 0,


    /// <summary>Normal priority execution.</summary>
    Normal = 1,


    /// <summary>High priority execution.</summary>
    High = 2,


    /// <summary>Real-time priority execution.</summary>
    RealTime = 3
}

/// <summary>
/// Defines resource limits for Metal compute graph execution.
/// </summary>
public class MetalResourceLimits
{
    /// <summary>Gets or sets the maximum GPU memory usage in bytes.</summary>
    public long MaxGpuMemoryBytes { get; set; } = 8L * 1024 * 1024 * 1024; // 8GB

    /// <summary>Gets or sets the maximum number of active command buffers.</summary>
    public int MaxCommandBuffers { get; set; } = 64;

    /// <summary>Gets or sets the maximum number of active compute command encoders.</summary>
    public int MaxComputeEncoders { get; set; } = 16;

    /// <summary>Gets or sets the maximum number of active blit command encoders.</summary>
    public int MaxBlitEncoders { get; set; } = 8;

    /// <summary>Gets or sets the maximum threadgroup memory usage per kernel.</summary>
    public uint MaxThreadgroupMemoryBytes { get; set; } = 32 * 1024; // 32KB

    /// <summary>Gets or sets the maximum number of kernel arguments.</summary>
    public int MaxKernelArguments { get; set; } = 31; // Metal limit

    /// <summary>
    /// Validates the resource limits.
    /// </summary>
    /// <returns>A list of validation errors.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (MaxGpuMemoryBytes <= 0)
        {
            errors.Add("MaxGpuMemoryBytes must be positive");
        }


        if (MaxCommandBuffers <= 0)
        {
            errors.Add("MaxCommandBuffers must be positive");
        }


        if (MaxComputeEncoders <= 0)
        {
            errors.Add("MaxComputeEncoders must be positive");
        }


        if (MaxBlitEncoders <= 0)
        {
            errors.Add("MaxBlitEncoders must be positive");
        }


        if (MaxThreadgroupMemoryBytes == 0)
        {
            errors.Add("MaxThreadgroupMemoryBytes must be greater than 0");
        }


        if (MaxKernelArguments <= 0)
        {
            errors.Add("MaxKernelArguments must be positive");
        }


        if (MaxKernelArguments > 31)
        {

            errors.Add("MaxKernelArguments cannot exceed Metal limit of 31");
        }


        return errors;
    }
}

/// <summary>
/// Debugging and diagnostic options for Metal compute graphs.
/// </summary>
public class MetalGraphDebuggingOptions
{
    /// <summary>Gets or sets whether to enable detailed execution logging.</summary>
    public bool EnableDetailedLogging { get; set; }

    /// <summary>Gets or sets whether to measure timing for individual nodes.</summary>
    public bool EnableNodeTimingMeasurement { get; set; }

    /// <summary>Gets or sets whether to track memory allocations and usage.</summary>
    public bool EnableMemoryTracker { get; set; }

    /// <summary>Gets or sets whether to enable Metal validation layers.</summary>
    public bool EnableValidationLayer { get; set; }

    /// <summary>Gets or sets whether to log the execution order of nodes.</summary>
    public bool LogExecutionOrder { get; set; }

    /// <summary>Gets or sets whether to save command buffers for inspection.</summary>
    public bool SaveCommandBuffers { get; set; }

    /// <summary>Gets or sets whether to capture GPU timeline events.</summary>
    public bool CaptureGpuTimeline { get; set; }

    /// <summary>Gets or sets the path to save debugging artifacts.</summary>
    public string? DebugArtifactsPath { get; set; }
}