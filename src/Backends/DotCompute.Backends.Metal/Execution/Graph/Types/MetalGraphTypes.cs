// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Graph.Types;

/// <summary>
/// Defines the types of operations that can be represented as nodes in a Metal compute graph.
/// </summary>
public enum MetalNodeType
{
    /// <summary>A compute kernel execution node.</summary>
    Kernel,
    
    /// <summary>A memory copy operation node.</summary>
    MemoryCopy,
    
    /// <summary>A memory set operation node.</summary>
    MemorySet,
    
    /// <summary>A synchronization barrier node.</summary>
    Barrier,
    
    /// <summary>A host callback function node.</summary>
    HostCallback,
    
    /// <summary>An event recording node.</summary>
    EventRecord,
    
    /// <summary>An event wait node.</summary>
    EventWait
}

/// <summary>
/// Represents the type of Metal command encoder required for a node.
/// </summary>
public enum MetalCommandEncoderType
{
    /// <summary>Compute command encoder for kernel dispatch.</summary>
    Compute,
    
    /// <summary>Blit command encoder for memory operations.</summary>
    Blit,
    
    /// <summary>Render command encoder for rendering operations.</summary>
    Render
}

/// <summary>
/// Represents Metal threadgroup and thread dimensions.
/// </summary>
public struct MTLSize
{
    /// <summary>Width dimension.</summary>
    public uint width;
    
    /// <summary>Height dimension.</summary>
    public uint height;
    
    /// <summary>Depth dimension.</summary>
    public uint depth;

    /// <summary>
    /// Initializes a new instance of the <see cref="MTLSize"/> struct.
    /// </summary>
    /// <param name="width">The width dimension.</param>
    /// <param name="height">The height dimension.</param>
    /// <param name="depth">The depth dimension.</param>
    public MTLSize(uint width, uint height, uint depth)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;
    }

    /// <summary>
    /// Creates a 1D MTLSize.
    /// </summary>
    /// <param name="width">The width dimension.</param>
    /// <returns>A new MTLSize instance.</returns>
    public static MTLSize Make(uint width) => new(width, 1, 1);

    /// <summary>
    /// Creates a 2D MTLSize.
    /// </summary>
    /// <param name="width">The width dimension.</param>
    /// <param name="height">The height dimension.</param>
    /// <returns>A new MTLSize instance.</returns>
    public static MTLSize Make(uint width, uint height) => new(width, height, 1);

    /// <summary>
    /// Creates a 3D MTLSize.
    /// </summary>
    /// <param name="width">The width dimension.</param>
    /// <param name="height">The height dimension.</param>
    /// <param name="depth">The depth dimension.</param>
    /// <returns>A new MTLSize instance.</returns>
    public static MTLSize Make(uint width, uint height, uint depth) => new(width, height, depth);

    /// <summary>
    /// Gets the total number of elements.
    /// </summary>
    public readonly uint TotalElements => width * height * depth;

    /// <summary>
    /// Returns a string representation of this MTLSize.
    /// </summary>
    /// <returns>A string representation.</returns>
    public readonly override string ToString() => $"({width}, {height}, {depth})";
}

/// <summary>
/// Contains analysis results for Metal graph optimization opportunities.
/// </summary>
public class MetalGraphAnalysis
{
    /// <summary>Gets or sets the total number of nodes in the graph.</summary>
    public int NodeCount { get; set; }

    /// <summary>Gets or sets the estimated memory footprint in bytes.</summary>
    public long EstimatedMemoryFootprint { get; set; }

    /// <summary>Gets or sets the critical path length through the graph.</summary>
    public int CriticalPathLength { get; set; }

    /// <summary>Gets or sets the number of parallelism opportunities.</summary>
    public int ParallelismOpportunities { get; set; }

    /// <summary>Gets or sets the number of kernel fusion opportunities.</summary>
    public int FusionOpportunities { get; set; }

    /// <summary>Gets or sets the number of memory coalescing opportunities.</summary>
    public int MemoryCoalescingOpportunities { get; set; }

    /// <summary>Gets or sets the number of command buffer batching opportunities.</summary>
    public int CommandBufferBatchingOpportunities { get; set; }

    /// <summary>Gets or sets the estimated execution time in milliseconds.</summary>
    public double EstimatedExecutionTimeMs { get; set; }

    /// <summary>Gets or sets the optimization score (higher is better).</summary>
    public double OptimizationScore { get; set; }

    /// <summary>Gets a value indicating whether the graph has optimization opportunities.</summary>
    public bool HasOptimizationOpportunities =>
        FusionOpportunities > 0 ||
        MemoryCoalescingOpportunities > 0 ||
        CommandBufferBatchingOpportunities > 0 ||
        ParallelismOpportunities > 0;
}

/// <summary>
/// Represents execution results for a Metal compute graph.
/// </summary>
public class MetalGraphExecutionResult
{
    /// <summary>Gets or sets the unique execution identifier.</summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the executed graph.</summary>
    public string GraphName { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the execution was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the execution start time.</summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>Gets or sets the execution end time.</summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>Gets the total execution duration.</summary>
    public TimeSpan ExecutionDuration => EndTime - StartTime;

    /// <summary>Gets or sets the GPU execution time in milliseconds.</summary>
    public double GpuExecutionTimeMs { get; set; }

    /// <summary>Gets or sets the number of nodes executed.</summary>
    public int NodesExecuted { get; set; }

    /// <summary>Gets or sets the number of command buffers used.</summary>
    public int CommandBuffersUsed { get; set; }

    /// <summary>Gets or sets the total memory transferred in bytes.</summary>
    public long TotalMemoryTransferred { get; set; }

    /// <summary>Gets or sets any error message if execution failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the exception that caused the failure, if any.</summary>
    public Exception? Exception { get; set; }

    /// <summary>Gets or sets performance metrics for the execution.</summary>
    public Dictionary<string, object> PerformanceMetrics { get; set; } = [];
}

/// <summary>
/// Represents a group of operations that can be batched together in a command buffer.
/// </summary>
public class MetalCommandBatch
{
    /// <summary>Gets or sets the unique identifier for this batch.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the nodes included in this batch.</summary>
    public List<string> NodeIds { get; set; } = [];

    /// <summary>Gets or sets the type of command encoder required.</summary>
    public MetalCommandEncoderType EncoderType { get; set; }

    /// <summary>Gets or sets the estimated execution time for this batch.</summary>
    public double EstimatedExecutionTimeMs { get; set; }

    /// <summary>Gets or sets the priority of this batch.</summary>
    public int Priority { get; set; }
}

/// <summary>
/// Represents optimization parameters for Metal graph execution.
/// </summary>
public class MetalOptimizationParameters
{
    /// <summary>Gets or sets a value indicating whether kernel fusion is enabled.</summary>
    public bool EnableKernelFusion { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether memory coalescing is enabled.</summary>
    public bool EnableMemoryCoalescing { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether command buffer batching is enabled.</summary>
    public bool EnableCommandBufferBatching { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether parallel execution is enabled.</summary>
    public bool EnableParallelExecution { get; set; } = true;

    /// <summary>Gets or sets the maximum number of nodes to fuse together.</summary>
    public int MaxFusionDepth { get; set; } = 3;

    /// <summary>Gets or sets the maximum command buffer size in operations.</summary>
    public int MaxCommandBufferSize { get; set; } = 64;

    /// <summary>Gets or sets the memory allocation strategy.</summary>
    public MetalMemoryStrategy MemoryStrategy { get; set; } = MetalMemoryStrategy.Balanced;

    /// <summary>Gets or sets Apple Silicon specific optimizations.</summary>
    public bool EnableAppleSiliconOptimizations { get; set; } = true;

    /// <summary>
    /// Validates the optimization parameters and returns a list of validation errors.
    /// </summary>
    /// <returns>A list of validation errors, empty if parameters are valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();
        
        if (MaxFusionDepth < 1 || MaxFusionDepth > 10)
            errors.Add("MaxFusionDepth must be between 1 and 10");
            
        if (MaxCommandBufferSize < 1 || MaxCommandBufferSize > 1024)
            errors.Add("MaxCommandBufferSize must be between 1 and 1024");
            
        return errors;
    }
}

/// <summary>
/// Defines memory allocation and optimization strategies for Metal operations.
/// </summary>
public enum MetalMemoryStrategy
{
    /// <summary>Minimize memory usage at the cost of some performance.</summary>
    Conservative,
    
    /// <summary>Balance memory usage and performance.</summary>
    Balanced,
    
    /// <summary>Maximize performance, allowing higher memory usage.</summary>
    Aggressive,
    
    /// <summary>Optimize specifically for Apple Silicon unified memory.</summary>
    UnifiedMemory
}

/// <summary>
/// Represents resource requirements for a Metal graph node or operation.
/// </summary>
public class MetalResourceRequirements
{
    /// <summary>Gets or sets the required GPU memory in bytes.</summary>
    public long RequiredMemoryBytes { get; set; }

    /// <summary>Gets or sets the required compute units.</summary>
    public int RequiredComputeUnits { get; set; }

    /// <summary>Gets or sets the required command buffer slots.</summary>
    public int RequiredCommandBufferSlots { get; set; }

    /// <summary>Gets or sets a value indicating whether the operation requires unified memory.</summary>
    public bool RequiresUnifiedMemory { get; set; }

    /// <summary>Gets or sets a value indicating whether the operation can run concurrently.</summary>
    public bool SupportsConcurrentExecution { get; set; } = true;

    /// <summary>Gets or sets the maximum threadgroup size supported.</summary>
    public uint MaxThreadgroupSize { get; set; } = 1024;
}