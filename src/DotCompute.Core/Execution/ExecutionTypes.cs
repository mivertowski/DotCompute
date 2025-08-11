// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Kernels;
using DotCompute.Core.Memory;

namespace DotCompute.Core.Execution;

/// <summary>
/// Defines the types of parallel execution strategies available.
/// </summary>
public enum ExecutionStrategyType
{
    /// <summary>Single device execution.</summary>
    Single,
    
    /// <summary>Data parallelism across multiple devices.</summary>
    DataParallel,
    
    /// <summary>Model parallelism for large models.</summary>
    ModelParallel,
    
    /// <summary>Pipeline parallelism with streaming.</summary>
    PipelineParallel,
    
    /// <summary>Dynamic work stealing for load balancing.</summary>
    WorkStealing,
    
    /// <summary>Heterogeneous CPU+GPU execution.</summary>
    Heterogeneous
}

/// <summary>
/// Configuration options for data parallelism execution.
/// </summary>
public class DataParallelismOptions
{
    /// <summary>Gets or sets the target device IDs to use for execution.</summary>
    public string[]? TargetDevices { get; set; }
    
    /// <summary>Gets or sets the maximum number of devices to use.</summary>
    public int? MaxDevices { get; set; }
    
    /// <summary>Gets or sets whether to enable peer-to-peer GPU transfers.</summary>
    public bool EnablePeerToPeer { get; set; } = true;
    
    /// <summary>Gets or sets the synchronization strategy.</summary>
    public SynchronizationStrategy SyncStrategy { get; set; } = SynchronizationStrategy.EventBased;
    
    /// <summary>Gets or sets whether to overlap computation with communication.</summary>
    public bool OverlapComputeAndCommunication { get; set; } = true;
    
    /// <summary>Gets or sets the load balancing strategy.</summary>
    public LoadBalancingStrategy LoadBalancing { get; set; } = LoadBalancingStrategy.Adaptive;
}

/// <summary>
/// Configuration options for model parallelism execution.
/// </summary>
public class ModelParallelismOptions
{
    /// <summary>Gets or sets the layer assignment strategy.</summary>
    public LayerAssignmentStrategy LayerAssignment { get; set; } = LayerAssignmentStrategy.Automatic;
    
    /// <summary>Gets or sets the communication backend for inter-layer transfers.</summary>
    public CommunicationBackend CommunicationBackend { get; set; } = CommunicationBackend.NCCL;
    
    /// <summary>Gets or sets whether to enable gradient checkpointing.</summary>
    public bool EnableGradientCheckpointing { get; set; }
    
    /// <summary>Gets or sets the memory optimization level.</summary>
    public MemoryOptimizationLevel MemoryOptimization { get; set; } = MemoryOptimizationLevel.Balanced;

    /// <summary>Converts to data parallel options for device selection.</summary>
    public DataParallelismOptions ToDataParallelOptions() => new()
    {
        MaxDevices = null, // Use all available devices
        EnablePeerToPeer = true,
        SyncStrategy = SynchronizationStrategy.EventBased,
        OverlapComputeAndCommunication = true,
        LoadBalancing = LoadBalancingStrategy.Manual // Layer assignment is manual
    };
}

/// <summary>
/// Configuration options for pipeline parallelism execution.
/// </summary>
public class PipelineParallelismOptions
{
    /// <summary>Gets or sets the number of pipeline stages.</summary>
    public int StageCount { get; set; } = 4;
    
    /// <summary>Gets or sets the microbatch size for pipelining.</summary>
    public int MicrobatchSize { get; set; } = 1;
    
    /// <summary>Gets or sets the buffer depth for each stage.</summary>
    public int BufferDepth { get; set; } = 2;
    
    /// <summary>Gets or sets the scheduling strategy.</summary>
    public PipelineSchedulingStrategy SchedulingStrategy { get; set; } = PipelineSchedulingStrategy.FillDrain;

    /// <summary>Converts to data parallel options for device selection.</summary>
    public DataParallelismOptions ToDataParallelOptions() => new()
    {
        MaxDevices = StageCount,
        EnablePeerToPeer = true,
        SyncStrategy = SynchronizationStrategy.EventBased,
        OverlapComputeAndCommunication = true,
        LoadBalancing = LoadBalancingStrategy.Manual // Pipeline stages are manually assigned
    };
}

/// <summary>
/// Configuration options for work stealing execution.
/// </summary>
public class WorkStealingOptions
{
    /// <summary>Gets or sets the initial work chunk size.</summary>
    public int InitialChunkSize { get; set; } = 1024;
    
    /// <summary>Gets or sets the minimum work chunk size.</summary>
    public int MinChunkSize { get; set; } = 64;
    
    /// <summary>Gets or sets the stealing strategy.</summary>
    public StealingStrategy StealingStrategy { get; set; } = StealingStrategy.RandomVictim;
    
    /// <summary>Gets or sets the work queue depth per device.</summary>
    public int WorkQueueDepth { get; set; } = 16;

    /// <summary>Converts to data parallel options for device selection.</summary>
    public DataParallelismOptions ToDataParallelOptions() => new()
    {
        MaxDevices = null, // Use all available devices
        EnablePeerToPeer = false, // Work stealing manages its own transfers
        SyncStrategy = SynchronizationStrategy.LockFree,
        OverlapComputeAndCommunication = true,
        LoadBalancing = LoadBalancingStrategy.Dynamic
    };
}

/// <summary>
/// Synchronization strategies for parallel execution.
/// </summary>
public enum SynchronizationStrategy
{
    /// <summary>Use CUDA events or OpenCL events for synchronization.</summary>
    EventBased,
    
    /// <summary>Use barrier synchronization.</summary>
    Barrier,
    
    /// <summary>Use lock-free atomic operations.</summary>
    LockFree,
    
    /// <summary>Use host-based synchronization.</summary>
    HostBased
}

/// <summary>
/// Load balancing strategies.
/// </summary>
public enum LoadBalancingStrategy
{
    /// <summary>Equal distribution across devices.</summary>
    RoundRobin,
    
    /// <summary>Distribution based on device capabilities.</summary>
    Weighted,
    
    /// <summary>Adaptive distribution based on performance feedback.</summary>
    Adaptive,
    
    /// <summary>Dynamic load balancing with work stealing.</summary>
    Dynamic,
    
    /// <summary>Manual assignment by user.</summary>
    Manual
}

/// <summary>
/// Layer assignment strategies for model parallelism.
/// </summary>
public enum LayerAssignmentStrategy
{
    /// <summary>Automatic assignment based on memory requirements.</summary>
    Automatic,
    
    /// <summary>Sequential assignment of layers to devices.</summary>
    Sequential,
    
    /// <summary>Interleaved assignment for better load balancing.</summary>
    Interleaved,
    
    /// <summary>Custom assignment specified by user.</summary>
    Custom
}

/// <summary>
/// Communication backends for multi-GPU communication.
/// </summary>
public enum CommunicationBackend
{
    /// <summary>NVIDIA Collective Communications Library.</summary>
    NCCL,
    
    /// <summary>Message Passing Interface.</summary>
    MPI,
    
    /// <summary>Direct peer-to-peer GPU transfers.</summary>
    P2P,
    
    /// <summary>Host-based communication.</summary>
    Host
}

/// <summary>
/// Memory optimization levels.
/// </summary>
public enum MemoryOptimizationLevel
{
    /// <summary>No memory optimizations.</summary>
    None,
    
    /// <summary>Basic optimizations (buffer reuse).</summary>
    Basic,
    
    /// <summary>Balanced optimizations.</summary>
    Balanced,
    
    /// <summary>Aggressive memory optimizations.</summary>
    Aggressive
}

/// <summary>
/// Pipeline scheduling strategies.
/// </summary>
public enum PipelineSchedulingStrategy
{
    /// <summary>Simple fill-drain scheduling.</summary>
    FillDrain,
    
    /// <summary>1F1B (One Forward One Backward) scheduling.</summary>
    OneForwardOneBackward,
    
    /// <summary>Interleaved scheduling for better pipeline utilization.</summary>
    Interleaved
}

/// <summary>
/// Work stealing strategies.
/// </summary>
public enum StealingStrategy
{
    /// <summary>Steal from a random victim.</summary>
    RandomVictim,
    
    /// <summary>Steal from the richest victim.</summary>
    RichestVictim,
    
    /// <summary>Steal from the nearest victim (NUMA-aware).</summary>
    NearestVictim,
    
    /// <summary>Hierarchical stealing strategy.</summary>
    Hierarchical
}

/// <summary>
/// Result of parallel execution.
/// </summary>
public class ParallelExecutionResult
{
    /// <summary>Gets or sets whether the execution was successful.</summary>
    public required bool Success { get; set; }
    
    /// <summary>Gets or sets the total execution time in milliseconds.</summary>
    public required double TotalExecutionTimeMs { get; set; }
    
    /// <summary>Gets or sets the results from each device.</summary>
    public required DeviceExecutionResult[] DeviceResults { get; set; }
    
    /// <summary>Gets or sets the execution strategy used.</summary>
    public required ExecutionStrategyType Strategy { get; set; }
    
    /// <summary>Gets or sets the overall throughput in GFLOPS.</summary>
    public double ThroughputGFLOPS { get; set; }
    
    /// <summary>Gets or sets the overall memory bandwidth in GB/s.</summary>
    public double MemoryBandwidthGBps { get; set; }
    
    /// <summary>Gets or sets the parallel efficiency percentage.</summary>
    public double EfficiencyPercentage { get; set; }
    
    /// <summary>Gets or sets any error message.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of execution on a single device.
/// </summary>
public class DeviceExecutionResult
{
    /// <summary>Gets or sets the device identifier.</summary>
    public required string DeviceId { get; set; }
    
    /// <summary>Gets or sets whether the execution was successful.</summary>
    public required bool Success { get; set; }
    
    /// <summary>Gets or sets the execution time in milliseconds.</summary>
    public double ExecutionTimeMs { get; set; }
    
    /// <summary>Gets or sets the number of elements processed.</summary>
    public int ElementsProcessed { get; set; }
    
    /// <summary>Gets or sets the memory bandwidth achieved in GB/s.</summary>
    public double MemoryBandwidthGBps { get; set; }
    
    /// <summary>Gets or sets the throughput achieved in GFLOPS.</summary>
    public double ThroughputGFLOPS { get; set; }
    
    /// <summary>Gets or sets any error message.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Performance metrics for parallel execution.
/// </summary>
public class ParallelExecutionMetrics
{
    /// <summary>Gets or sets the total number of executions.</summary>
    public int TotalExecutions { get; set; }
    
    /// <summary>Gets or sets the average execution time in milliseconds.</summary>
    public double AverageExecutionTimeMs { get; set; }
    
    /// <summary>Gets or sets the average parallel efficiency percentage.</summary>
    public double AverageEfficiencyPercentage { get; set; }
    
    /// <summary>Gets or sets the total GFLOPS-hours processed.</summary>
    public double TotalGFLOPSHours { get; set; }
    
    /// <summary>Gets or sets metrics by execution strategy.</summary>
    public Dictionary<ExecutionStrategyType, StrategyMetrics> MetricsByStrategy { get; set; } = new();
    
    /// <summary>Gets or sets metrics by device.</summary>
    public Dictionary<string, DeviceMetrics> MetricsByDevice { get; set; } = new();
}

/// <summary>
/// Metrics for a specific execution strategy.
/// </summary>
public class StrategyMetrics
{
    /// <summary>Gets or sets the number of times this strategy was used.</summary>
    public int UsageCount { get; set; }
    
    /// <summary>Gets or sets the average execution time for this strategy.</summary>
    public double AverageExecutionTimeMs { get; set; }
    
    /// <summary>Gets or sets the average efficiency for this strategy.</summary>
    public double AverageEfficiencyPercentage { get; set; }
    
    /// <summary>Gets or sets the success rate for this strategy.</summary>
    public double SuccessRatePercentage { get; set; }
}

/// <summary>
/// Metrics for a specific device.
/// </summary>
public class DeviceMetrics
{
    /// <summary>Gets or sets the device identifier.</summary>
    public required string DeviceId { get; set; }
    
    /// <summary>Gets or sets the total number of executions on this device.</summary>
    public int TotalExecutions { get; set; }
    
    /// <summary>Gets or sets the average execution time on this device.</summary>
    public double AverageExecutionTimeMs { get; set; }
    
    /// <summary>Gets or sets the average throughput achieved on this device.</summary>
    public double AverageThroughputGFLOPS { get; set; }
    
    /// <summary>Gets or sets the average memory bandwidth achieved on this device.</summary>
    public double AverageMemoryBandwidthGBps { get; set; }
    
    /// <summary>Gets or sets the utilization percentage of this device.</summary>
    public double UtilizationPercentage { get; set; }
}

/// <summary>
/// Analysis of parallel execution performance.
/// </summary>
public class ParallelExecutionAnalysis
{
    /// <summary>Gets or sets the overall performance rating (1-10).</summary>
    public double OverallRating { get; set; }
    
    /// <summary>Gets or sets the primary bottlenecks identified.</summary>
    public List<BottleneckAnalysis> Bottlenecks { get; set; } = new();
    
    /// <summary>Gets or sets optimization recommendations.</summary>
    public List<string> OptimizationRecommendations { get; set; } = new();
    
    /// <summary>Gets or sets the recommended execution strategy.</summary>
    public ExecutionStrategyType RecommendedStrategy { get; set; }
    
    /// <summary>Gets or sets device utilization analysis.</summary>
    public Dictionary<string, double> DeviceUtilizationAnalysis { get; set; } = new();
}

/// <summary>
/// Recommendation for optimal execution strategy.
/// </summary>
public class ExecutionStrategyRecommendation
{
    /// <summary>Gets or sets the recommended strategy.</summary>
    public required ExecutionStrategyType Strategy { get; set; }
    
    /// <summary>Gets or sets the confidence score (0-1).</summary>
    public required double ConfidenceScore { get; set; }
    
    /// <summary>Gets or sets the reasoning for the recommendation.</summary>
    public required string Reasoning { get; set; }
    
    /// <summary>Gets or sets the expected performance improvement percentage.</summary>
    public double ExpectedImprovementPercentage { get; set; }
    
    /// <summary>Gets or sets recommended options for the strategy.</summary>
    public object? RecommendedOptions { get; set; }
}

/// <summary>
/// Analysis of performance bottlenecks.
/// </summary>
public class BottleneckAnalysis
{
    /// <summary>Gets or sets the bottleneck type.</summary>
    public required BottleneckType Type { get; set; }
    
    /// <summary>Gets or sets the severity of the bottleneck (0-1).</summary>
    public required double Severity { get; set; }
    
    /// <summary>Gets or sets detailed information about the bottleneck.</summary>
    public string? Details { get; set; }
    
    /// <summary>Gets or sets affected devices or components.</summary>
    public string[]? AffectedComponents { get; set; }
    
    /// <summary>Gets or sets suggested mitigations.</summary>
    public string[]? SuggestedMitigations { get; set; }
}

/// <summary>
/// Types of performance bottlenecks.
/// </summary>
public enum BottleneckType
{
    /// <summary>No significant bottleneck identified.</summary>
    None,
    
    /// <summary>Memory bandwidth limited.</summary>
    MemoryBandwidth,
    
    /// <summary>Memory latency limited.</summary>
    MemoryLatency,
    
    /// <summary>Compute capacity limited.</summary>
    Compute,
    
    /// <summary>Synchronization overhead.</summary>
    Synchronization,
    
    /// <summary>Communication between devices.</summary>
    Communication,
    
    /// <summary>Load imbalance between devices.</summary>
    LoadImbalance,
    
    /// <summary>Instruction issue rate limited.</summary>
    InstructionIssue,
    
    /// <summary>Register pressure limiting occupancy.</summary>
    RegisterPressure,
    
    /// <summary>Shared memory bank conflicts.</summary>
    SharedMemoryConflicts,
    
    /// <summary>Branch divergence in warps/wavefronts.</summary>
    BranchDivergence,
    
    /// <summary>Cache miss rate too high.</summary>
    CacheMisses,
    
    /// <summary>Host-device transfer bandwidth.</summary>
    HostDeviceTransfer,
    
    /// <summary>Thermal throttling.</summary>
    ThermalThrottling,
    
    /// <summary>Power limiting.</summary>
    PowerLimit
}