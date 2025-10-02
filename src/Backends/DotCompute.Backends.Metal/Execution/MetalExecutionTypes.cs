// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.Serialization;

namespace DotCompute.Backends.Metal.Execution;

// Comprehensive collection of supporting types and enums for the Metal execution system,
// following CUDA patterns adapted for Metal's architecture and constraints.

#region Core Execution Types

/// <summary>
/// Represents the overall health status of the Metal execution environment
/// </summary>
public enum MetalExecutionHealth
{
    /// <summary>
    /// All systems operating normally
    /// </summary>
    Healthy,

    /// <summary>
    /// Minor issues that don't affect core functionality
    /// </summary>
    Warning,

    /// <summary>
    /// Significant issues affecting performance
    /// </summary>
    Degraded,

    /// <summary>
    /// Critical failures preventing normal operation
    /// </summary>
    Critical,


    /// <summary>
    /// System is completely unavailable
    /// </summary>
    Offline
}

/// <summary>
/// Defines the execution strategy for Metal operations
/// </summary>
public enum MetalExecutionStrategy
{
    /// <summary>
    /// Optimize for maximum throughput
    /// </summary>
    Throughput,

    /// <summary>
    /// Optimize for minimum latency
    /// </summary>
    Latency,

    /// <summary>
    /// Balance between throughput and latency
    /// </summary>
    Balanced,

    /// <summary>
    /// Optimize for power efficiency (Apple Silicon)
    /// </summary>
    PowerEfficient,


    /// <summary>
    /// Custom strategy based on runtime conditions
    /// </summary>
    Adaptive
}

/// <summary>
/// Defines the synchronization model for Metal operations
/// </summary>
public enum MetalSynchronizationMode
{
    /// <summary>
    /// Synchronous execution with blocking waits
    /// </summary>
    Synchronous,

    /// <summary>
    /// Asynchronous execution with callbacks
    /// </summary>
    Asynchronous,

    /// <summary>
    /// Event-driven synchronization
    /// </summary>
    EventDriven,


    /// <summary>
    /// Pipeline-based execution
    /// </summary>
    Pipelined
}

#endregion

#region Memory and Resource Types

/// <summary>
/// Defines memory allocation strategies for Metal resources
/// </summary>
public enum MetalMemoryStrategy
{
    /// <summary>
    /// Default Metal memory allocation
    /// </summary>
    Default,

    /// <summary>
    /// Optimized for Apple Silicon unified memory
    /// </summary>
    UnifiedMemory,

    /// <summary>
    /// Optimized for discrete GPU memory
    /// </summary>
    DiscreteGpu,

    /// <summary>
    /// Use shared memory when possible
    /// </summary>
    SharedMemory,

    /// <summary>
    /// Private GPU memory only
    /// </summary>
    PrivateMemory,

    /// <summary>
    /// Managed memory with automatic synchronization
    /// </summary>
    ManagedMemory,

    /// <summary>
    /// Balanced memory allocation strategy
    /// </summary>
    Balanced,


    /// <summary>
    /// Aggressive memory allocation strategy
    /// </summary>
    Aggressive
}

/// <summary>
/// Resource utilization patterns
/// </summary>
public enum MetalResourcePattern
{
    /// <summary>
    /// Single-use resources
    /// </summary>
    SingleUse,

    /// <summary>
    /// Resources reused across multiple operations
    /// </summary>
    Reusable,

    /// <summary>
    /// Long-lived resources
    /// </summary>
    Persistent,

    /// <summary>
    /// Temporary resources with short lifetime
    /// </summary>
    Temporary,


    /// <summary>
    /// Streaming resources for data flow
    /// </summary>
    Streaming
}

#endregion

#region Performance and Profiling Types

/// <summary>
/// Performance profiling levels
/// </summary>
public enum MetalProfilingLevel
{
    /// <summary>
    /// No profiling
    /// </summary>
    None,

    /// <summary>
    /// Basic timing information
    /// </summary>
    Basic,

    /// <summary>
    /// Detailed operation metrics
    /// </summary>
    Detailed,

    /// <summary>
    /// Comprehensive profiling with all metrics
    /// </summary>
    Comprehensive,


    /// <summary>
    /// Debug-level profiling with maximum detail
    /// </summary>
    Debug
}

/// <summary>
/// Metrics collection categories
/// </summary>
[Flags]
public enum MetalMetricsCategory
{
    /// <summary>
    /// No metrics collection
    /// </summary>
    None = 0,


    /// <summary>
    /// Timing metrics
    /// </summary>
    Timing = 1 << 0,


    /// <summary>
    /// Memory usage metrics
    /// </summary>
    Memory = 1 << 1,


    /// <summary>
    /// GPU utilization metrics
    /// </summary>
    GpuUtilization = 1 << 2,


    /// <summary>
    /// Command buffer metrics
    /// </summary>
    CommandBuffer = 1 << 3,


    /// <summary>
    /// Pipeline state metrics
    /// </summary>
    Pipeline = 1 << 4,


    /// <summary>
    /// Error and recovery metrics
    /// </summary>
    Error = 1 << 5,


    /// <summary>
    /// All metrics categories
    /// </summary>
    All = Timing | Memory | GpuUtilization | CommandBuffer | Pipeline | Error
}

#endregion

#region Architecture-Specific Types

/// <summary>
/// GPU architecture types for optimization
/// </summary>
public enum MetalGpuArchitecture
{
    /// <summary>
    /// Unknown or unsupported architecture
    /// </summary>
    Unknown,

    /// <summary>
    /// Apple Silicon M1 family
    /// </summary>
    AppleM1,

    /// <summary>
    /// Apple Silicon M2 family
    /// </summary>
    AppleM2,

    /// <summary>
    /// Apple Silicon M3 family
    /// </summary>
    AppleM3,

    /// <summary>
    /// Intel Integrated Graphics
    /// </summary>
    IntelIntegrated,

    /// <summary>
    /// AMD Discrete GPU
    /// </summary>
    AmdDiscrete,

    /// <summary>
    /// Intel Discrete GPU
    /// </summary>
    IntelDiscrete,


    /// <summary>
    /// Other discrete GPU
    /// </summary>
    OtherDiscrete
}

/// <summary>
/// Platform-specific optimizations
/// </summary>
public enum MetalPlatformOptimization
{
    /// <summary>
    /// Generic Metal optimizations
    /// </summary>
    Generic,

    /// <summary>
    /// macOS-specific optimizations
    /// </summary>
    MacOS,

    /// <summary>
    /// iOS-specific optimizations
    /// </summary>
    iOS,

    /// <summary>
    /// iPadOS-specific optimizations
    /// </summary>
    iPadOS,

    /// <summary>
    /// tvOS-specific optimizations
    /// </summary>
    tvOS,


    /// <summary>
    /// watchOS-specific optimizations
    /// </summary>
    watchOS
}

#endregion

#region Execution Configuration Types

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

/// <summary>
/// Configuration for Metal command stream optimization
/// </summary>
public sealed class MetalStreamConfiguration
{
    /// <summary>
    /// Stream priority
    /// </summary>
    public MetalStreamPriority Priority { get; set; } = MetalStreamPriority.Normal;


    /// <summary>
    /// Stream flags
    /// </summary>
    public MetalStreamFlags Flags { get; set; } = MetalStreamFlags.Concurrent;


    /// <summary>
    /// Maximum operations per stream
    /// </summary>
    public int MaxOperationsPerStream { get; set; } = 64;


    /// <summary>
    /// Stream timeout for synchronization
    /// </summary>
    public TimeSpan SynchronizationTimeout { get; set; } = TimeSpan.FromSeconds(10);


    /// <summary>
    /// Enable stream dependency tracking
    /// </summary>
    public bool EnableDependencyTracking { get; set; } = true;


    /// <summary>
    /// Enable stream performance monitoring
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = true;
}

#endregion

#region Diagnostic and Monitoring Types

/// <summary>
/// Comprehensive diagnostic information for Metal execution
/// </summary>
public sealed class MetalDiagnosticInfo
{
    /// <summary>
    /// Current execution health status
    /// </summary>
    public MetalExecutionHealth Health { get; set; }


    /// <summary>
    /// GPU architecture information
    /// </summary>
    public MetalGpuArchitecture Architecture { get; set; }


    /// <summary>
    /// Platform optimization in use
    /// </summary>
    public MetalPlatformOptimization PlatformOptimization { get; set; }


    /// <summary>
    /// Active configuration
    /// </summary>
    public MetalExecutionConfiguration Configuration { get; set; } = new();


    /// <summary>
    /// Current resource usage
    /// </summary>
    public Dictionary<MetalResourceType, long> ResourceUsage { get; } = [];


    /// <summary>
    /// Performance metrics
    /// </summary>
    public Dictionary<string, object> PerformanceMetrics { get; } = [];


    /// <summary>
    /// Recent errors and warnings
    /// </summary>
    public IList<MetalDiagnosticMessage> Messages { get; } = [];


    /// <summary>
    /// System information
    /// </summary>
    public Dictionary<string, string> SystemInfo { get; } = [];


    /// <summary>
    /// Timestamp when diagnostic was generated
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Diagnostic message for Metal execution
/// </summary>
public sealed class MetalDiagnosticMessage
{
    /// <summary>
    /// Message severity level
    /// </summary>
    public enum SeverityLevel
    {
        /// <summary>
        /// Informational message
        /// </summary>
        Info,

        /// <summary>
        /// Warning message
        /// </summary>
        Warning,

        /// <summary>
        /// Error message
        /// </summary>
        Error,

        /// <summary>
        /// Critical error message
        /// </summary>
        Critical
    }


    /// <summary>
    /// Severity of the message
    /// </summary>
    public SeverityLevel Severity { get; set; }


    /// <summary>
    /// Message text
    /// </summary>
    public string Message { get; set; } = string.Empty;


    /// <summary>
    /// Component that generated the message
    /// </summary>
    public string Component { get; set; } = string.Empty;


    /// <summary>
    /// Operation ID associated with the message
    /// </summary>
    public string? OperationId { get; set; }


    /// <summary>
    /// Timestamp when message was generated
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;


    /// <summary>
    /// Additional context data
    /// </summary>
    public Dictionary<string, object> Context { get; } = [];
}

#endregion

#region Specialized Operation Types

/// <summary>
/// Base class for Metal operation descriptors
/// </summary>
public abstract class MetalOperationDescriptor
{
    /// <summary>
    /// Unique identifier for the operation
    /// </summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N")[..8];


    /// <summary>
    /// Human-readable name for the operation
    /// </summary>
    public string Name { get; set; } = string.Empty;


    /// <summary>
    /// Operation priority
    /// </summary>
    public MetalOperationPriority Priority { get; set; } = MetalOperationPriority.Normal;


    /// <summary>
    /// Dependencies on other operations
    /// </summary>
    public IList<string> Dependencies { get; } = [];


    /// <summary>
    /// Expected resource usage
    /// </summary>
    public Dictionary<MetalResourceType, long> ResourceUsage { get; } = [];


    /// <summary>
    /// Estimated execution time
    /// </summary>
    public TimeSpan? EstimatedDuration { get; set; }


    /// <summary>
    /// Custom metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Descriptor for compute operations
/// </summary>
public sealed class MetalComputeOperationDescriptor : MetalOperationDescriptor
{
    /// <summary>
    /// Compute pipeline state to use
    /// </summary>
    public IntPtr PipelineState { get; set; }


    /// <summary>
    /// Thread group size
    /// </summary>
    public (int x, int y, int z) ThreadgroupSize { get; set; }


    /// <summary>
    /// Grid size for dispatch
    /// </summary>
    public (int x, int y, int z) GridSize { get; set; }


    /// <summary>
    /// Input buffers for the compute operation
    /// </summary>
    public IList<IntPtr> InputBuffers { get; } = [];


    /// <summary>
    /// Output buffers for the compute operation
    /// </summary>
    public IList<IntPtr> OutputBuffers { get; } = [];


    /// <summary>
    /// Constant parameters
    /// </summary>
    public Dictionary<int, object> Constants { get; } = [];
}

/// <summary>
/// Descriptor for memory operations
/// </summary>
public sealed class MetalMemoryOperationDescriptor : MetalOperationDescriptor
{
    /// <summary>
    /// Type of memory operation
    /// </summary>
    public enum OperationType
    {
        /// <summary>
        /// Copy from host to device
        /// </summary>
        HostToDevice,

        /// <summary>
        /// Copy from device to host
        /// </summary>
        DeviceToHost,

        /// <summary>
        /// Copy from device to device
        /// </summary>
        DeviceToDevice,

        /// <summary>
        /// Fill buffer with pattern
        /// </summary>
        Fill,

        /// <summary>
        /// Clear buffer
        /// </summary>
        Clear
    }


    /// <summary>
    /// Type of memory operation
    /// </summary>
    public OperationType Operation { get; set; }


    /// <summary>
    /// Source buffer or data
    /// </summary>
    public IntPtr Source { get; set; }


    /// <summary>
    /// Destination buffer
    /// </summary>
    public IntPtr Destination { get; set; }


    /// <summary>
    /// Number of bytes to copy
    /// </summary>
    public long BytesToCopy { get; set; }


    /// <summary>
    /// Source offset
    /// </summary>
    public long SourceOffset { get; set; }


    /// <summary>
    /// Destination offset
    /// </summary>
    public long DestinationOffset { get; set; }
}

#endregion

#region Exception Types

/// <summary>
/// Exception thrown when Metal execution system encounters configuration errors
/// </summary>
[Serializable]
public class MetalConfigurationException : MetalException
{
    public MetalConfigurationException(string message) : base(MetalError.SystemFailure, message)
    {
    }


    public MetalConfigurationException(string message, Exception innerException)

        : base(MetalError.SystemFailure, message, innerException)
    {
    }


    protected MetalConfigurationException(SerializationInfo info, StreamingContext context)

        : base(MetalError.SystemFailure, info.GetString("Message") ?? "Configuration error")
    {
    }


    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue("ErrorCode", ErrorCode);
    }
}

/// <summary>
/// Exception thrown when Metal resource limits are exceeded
/// </summary>
[Serializable]
public class MetalResourceLimitException : MetalException
{
    public string ResourceType { get; }
    public long RequestedAmount { get; }
    public long AvailableAmount { get; }


    public MetalResourceLimitException(string resourceType, long requested, long available)

        : base(MetalError.InsufficientMemory, $"Resource limit exceeded for {resourceType}: requested {requested}, available {available}")
    {
        ResourceType = resourceType;
        RequestedAmount = requested;
        AvailableAmount = available;
    }


    protected MetalResourceLimitException(SerializationInfo info, StreamingContext context)

        : base(MetalError.InsufficientMemory, info.GetString("Message") ?? "Resource limit exceeded")
    {
        ResourceType = info.GetString(nameof(ResourceType)) ?? string.Empty;
        RequestedAmount = info.GetInt64(nameof(RequestedAmount));
        AvailableAmount = info.GetInt64(nameof(AvailableAmount));
    }


    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(ResourceType), ResourceType);
        info.AddValue(nameof(RequestedAmount), RequestedAmount);
        info.AddValue(nameof(AvailableAmount), AvailableAmount);
    }
}

/// <summary>
/// Exception thrown when Metal operation times out
/// </summary>
[Serializable]
public class MetalTimeoutException : MetalException
{
    public TimeSpan Timeout { get; }
    public string OperationId { get; }


    public MetalTimeoutException(string operationId, TimeSpan timeout)

        : base(MetalError.Timeout, $"Metal operation {operationId} timed out after {timeout}")
    {
        OperationId = operationId;
        Timeout = timeout;
    }


    protected MetalTimeoutException(SerializationInfo info, StreamingContext context)

        : base(MetalError.Timeout, info.GetString("Message") ?? "Operation timed out")
    {
        OperationId = info.GetString(nameof(OperationId)) ?? string.Empty;
        Timeout = (TimeSpan)info.GetValue(nameof(Timeout), typeof(TimeSpan))!;
    }


    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(OperationId), OperationId);
        info.AddValue(nameof(Timeout), Timeout);
    }
}



#endregion