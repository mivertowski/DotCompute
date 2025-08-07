// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Kernels;

namespace DotCompute.Core.Execution;

/// <summary>
/// Base class for execution plans.
/// </summary>
public abstract class ExecutionPlan<T> where T : unmanaged
{
    /// <summary>Gets or sets the kernel name.</summary>
    public required string KernelName { get; set; }
    
    /// <summary>Gets or sets the target devices.</summary>
    public required IAccelerator[] Devices { get; set; }
    
    /// <summary>Gets or sets the execution strategy type.</summary>
    public required ExecutionStrategyType StrategyType { get; set; }
    
    /// <summary>Gets or sets the estimated execution time in milliseconds.</summary>
    public double EstimatedExecutionTimeMs { get; set; }
    
    /// <summary>Gets or sets the plan creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Execution plan for data parallel execution.
/// </summary>
public class DataParallelExecutionPlan<T> : ExecutionPlan<T> where T : unmanaged
{
    /// <summary>Gets or sets the input buffers.</summary>
    public required AbstractionsMemory.IBuffer<T>[] InputBuffers { get; set; }
    
    /// <summary>Gets or sets the output buffers.</summary>
    public required AbstractionsMemory.IBuffer<T>[] OutputBuffers { get; set; }
    
    /// <summary>Gets or sets the device-specific tasks.</summary>
    public required DataParallelDeviceTask<T>[] DeviceTasks { get; set; }

    public DataParallelExecutionPlan()
    {
        StrategyType = ExecutionStrategyType.DataParallel;
    }
}

/// <summary>
/// Task for a single device in data parallel execution.
/// </summary>
public class DataParallelDeviceTask<T> where T : unmanaged
{
    /// <summary>Gets or sets the target device.</summary>
    public required IAccelerator Device { get; set; }
    
    /// <summary>Gets or sets the compiled kernel for this device.</summary>
    public required ManagedCompiledKernel CompiledKernel { get; set; }
    
    /// <summary>Gets or sets the device-local input buffers.</summary>
    public required AbstractionsMemory.IBuffer<T>[] InputBuffers { get; set; }
    
    /// <summary>Gets or sets the device-local output buffers.</summary>
    public required AbstractionsMemory.IBuffer<T>[] OutputBuffers { get; set; }
    
    /// <summary>Gets or sets the start index in the global data.</summary>
    public required int StartIndex { get; set; }
    
    /// <summary>Gets or sets the number of elements to process.</summary>
    public required int ElementCount { get; set; }
    
    /// <summary>Gets or sets dependencies on other device tasks.</summary>
    public List<int> Dependencies { get; set; } = new();
}

/// <summary>
/// Execution plan for model parallel execution.
/// </summary>
public class ModelParallelExecutionPlan<T> : ExecutionPlan<T> where T : unmanaged
{
    /// <summary>Gets or sets the model layers.</summary>
    public required ModelLayer<T>[] ModelLayers { get; set; }
    
    /// <summary>Gets or sets the layer assignments to devices.</summary>
    public required Dictionary<int, IAccelerator> LayerAssignments { get; set; }
    
    /// <summary>Gets or sets the communication schedule between layers.</summary>
    public required CommunicationSchedule<T> CommunicationSchedule { get; set; }

    public ModelParallelExecutionPlan()
    {
        StrategyType = ExecutionStrategyType.ModelParallel;
    }
}

/// <summary>
/// Represents a layer in a model parallel execution.
/// </summary>
public class ModelLayer<T> where T : unmanaged
{
    /// <summary>Gets or sets the layer identifier.</summary>
    public required int LayerId { get; set; }
    
    /// <summary>Gets or sets the layer name.</summary>
    public required string Name { get; set; }
    
    /// <summary>Gets or sets the kernel for this layer.</summary>
    public required ManagedCompiledKernel Kernel { get; set; }
    
    /// <summary>Gets or sets the input tensor descriptions.</summary>
    public required TensorDescription<T>[] InputTensors { get; set; }
    
    /// <summary>Gets or sets the output tensor descriptions.</summary>
    public required TensorDescription<T>[] OutputTensors { get; set; }
    
    /// <summary>Gets or sets the memory requirements for this layer.</summary>
    public long MemoryRequirementBytes { get; set; }
    
    /// <summary>Gets or sets the estimated compute requirements in FLOPS.</summary>
    public long ComputeRequirementFLOPS { get; set; }
    
    /// <summary>Gets or sets the dependencies on other layers.</summary>
    public List<int> Dependencies { get; set; } = new();
}

/// <summary>
/// Describes a tensor for model parallel execution.
/// </summary>
public class TensorDescription<T> where T : unmanaged
{
    /// <summary>Gets or sets the tensor name.</summary>
    public required string Name { get; set; }
    
    /// <summary>Gets or sets the tensor dimensions.</summary>
    public required int[] Dimensions { get; set; }
    
    /// <summary>Gets or sets the tensor data type.</summary>
    public required Type DataType { get; set; }
    
    /// <summary>Gets or sets whether this tensor is shared across devices.</summary>
    public bool IsShared { get; set; }
    
    /// <summary>Gets or sets the buffer containing the tensor data.</summary>
    public AbstractionsMemory.IBuffer<T>? Buffer { get; set; }
    
    /// <summary>Gets the total number of elements in the tensor.</summary>
    public int ElementCount => Dimensions.Aggregate(1, (a, b) => a * b);
    
    /// <summary>Gets the size in bytes of the tensor.</summary>
    public long SizeInBytes => ElementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();
}

/// <summary>
/// Communication schedule for model parallel execution.
/// </summary>
public class CommunicationSchedule<T> where T : unmanaged
{
    /// <summary>Gets or sets the communication operations.</summary>
    public required List<CommunicationOperation<T>> Operations { get; set; }
    
    /// <summary>Gets or sets the synchronization points.</summary>
    public required List<SynchronizationPoint> SynchronizationPoints { get; set; }
}

/// <summary>
/// Represents a communication operation between devices.
/// </summary>
public class CommunicationOperation<T> where T : unmanaged
{
    /// <summary>Gets or sets the operation identifier.</summary>
    public required int OperationId { get; set; }
    
    /// <summary>Gets or sets the source device.</summary>
    public required IAccelerator SourceDevice { get; set; }
    
    /// <summary>Gets or sets the destination device.</summary>
    public required IAccelerator DestinationDevice { get; set; }
    
    /// <summary>Gets or sets the tensor to transfer.</summary>
    public required TensorDescription<T> Tensor { get; set; }
    
    /// <summary>Gets or sets the operation type.</summary>
    public required CommunicationOperationType Type { get; set; }
    
    /// <summary>Gets or sets when this operation should be executed.</summary>
    public required int ExecutionOrder { get; set; }
}

/// <summary>
/// Types of communication operations.
/// </summary>
public enum CommunicationOperationType
{
    /// <summary>Point-to-point transfer between two devices.</summary>
    PointToPoint,
    
    /// <summary>Broadcast from one device to multiple devices.</summary>
    Broadcast,
    
    /// <summary>Gather data from multiple devices to one device.</summary>
    Gather,
    
    /// <summary>Scatter data from one device to multiple devices.</summary>
    Scatter,
    
    /// <summary>All-reduce operation across all devices.</summary>
    AllReduce,
    
    /// <summary>All-gather operation across all devices.</summary>
    AllGather
}

/// <summary>
/// Represents a synchronization point in execution.
/// </summary>
public class SynchronizationPoint
{
    /// <summary>Gets or sets the synchronization point identifier.</summary>
    public required int Id { get; set; }
    
    /// <summary>Gets or sets the devices that must synchronize at this point.</summary>
    public required IAccelerator[] Devices { get; set; }
    
    /// <summary>Gets or sets the synchronization type.</summary>
    public required SynchronizationType Type { get; set; }
    
    /// <summary>Gets or sets the execution order when this sync point should occur.</summary>
    public required int ExecutionOrder { get; set; }
}

/// <summary>
/// Types of synchronization operations.
/// </summary>
public enum SynchronizationType
{
    /// <summary>Barrier synchronization - wait for all devices.</summary>
    Barrier,
    
    /// <summary>Event-based synchronization.</summary>
    Event,
    
    /// <summary>Memory fence synchronization.</summary>
    MemoryFence,
    
    /// <summary>Stream synchronization.</summary>
    Stream
}

/// <summary>
/// Execution plan for pipeline parallel execution.
/// </summary>
public class PipelineExecutionPlan<T> : ExecutionPlan<T> where T : unmanaged
{
    /// <summary>Gets or sets the pipeline stages.</summary>
    public required PipelineStage<T>[] Stages { get; set; }
    
    /// <summary>Gets or sets the microbatch configuration.</summary>
    public required MicrobatchConfiguration MicrobatchConfig { get; set; }
    
    /// <summary>Gets or sets the buffer management strategy.</summary>
    public required PipelineBufferStrategy<T> BufferStrategy { get; set; }

    public PipelineExecutionPlan()
    {
        StrategyType = ExecutionStrategyType.PipelineParallel;
    }
}

/// <summary>
/// Represents a stage in a pipeline.
/// </summary>
public class PipelineStage<T> where T : unmanaged
{
    /// <summary>Gets or sets the stage identifier.</summary>
    public required int StageId { get; set; }
    
    /// <summary>Gets or sets the stage name.</summary>
    public required string Name { get; set; }
    
    /// <summary>Gets or sets the device assigned to this stage.</summary>
    public required IAccelerator Device { get; set; }
    
    /// <summary>Gets or sets the kernel for this stage.</summary>
    public required ManagedCompiledKernel Kernel { get; set; }
    
    /// <summary>Gets or sets the input buffers for this stage.</summary>
    public required AbstractionsMemory.IBuffer<T>[] InputBuffers { get; set; }
    
    /// <summary>Gets or sets the output buffers for this stage.</summary>
    public required AbstractionsMemory.IBuffer<T>[] OutputBuffers { get; set; }
    
    /// <summary>Gets or sets the processing time estimate for this stage.</summary>
    public double EstimatedProcessingTimeMs { get; set; }
}

/// <summary>
/// Configuration for microbatching in pipeline execution.
/// </summary>
public class MicrobatchConfiguration
{
    /// <summary>Gets or sets the microbatch size.</summary>
    public required int Size { get; set; }
    
    /// <summary>Gets or sets the number of microbatches to process.</summary>
    public required int Count { get; set; }
    
    /// <summary>Gets or sets the scheduling strategy for microbatches.</summary>
    public required MicrobatchSchedulingStrategy SchedulingStrategy { get; set; }
}

/// <summary>
/// Microbatch scheduling strategies.
/// </summary>
public enum MicrobatchSchedulingStrategy
{
    /// <summary>Process microbatches sequentially.</summary>
    Sequential,
    
    /// <summary>Interleave forward and backward passes.</summary>
    Interleaved,
    
    /// <summary>Use 1F1B (One Forward One Backward) scheduling.</summary>
    OneForwardOneBackward
}

/// <summary>
/// Buffer management strategy for pipeline execution.
/// </summary>
public class PipelineBufferStrategy<T> where T : unmanaged
{
    /// <summary>Gets or sets the buffer pool for reusing buffers.</summary>
    public required BufferPool<T> BufferPool { get; set; }
    
    /// <summary>Gets or sets the double buffering configuration.</summary>
    public required DoubleBufferingConfig DoubleBuffering { get; set; }
    
    /// <summary>Gets or sets the prefetching strategy.</summary>
    public required PrefetchingStrategy Prefetching { get; set; }
}

/// <summary>
/// Buffer pool for efficient buffer reuse.
/// </summary>
public class BufferPool<T> where T : unmanaged
{
    private readonly Queue<AbstractionsMemory.IBuffer<T>> _availableBuffers = new();
    private readonly object _lock = new();
    
    /// <summary>Gets a buffer from the pool or creates a new one.</summary>
    public async ValueTask<AbstractionsMemory.IBuffer<T>> GetBufferAsync(int elementCount, AbstractionsMemory.IMemoryManager memoryManager, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_availableBuffers.Count > 0)
            {
                var buffer = _availableBuffers.Dequeue();
                var bufferElementCount = (int)(buffer.SizeInBytes / System.Runtime.InteropServices.Marshal.SizeOf<T>());
                if (bufferElementCount >= elementCount)
                {
                    return buffer;
                }
                // Buffer is too small, dispose and create new one
                _ = buffer.DisposeAsync();
            }
        }
        
        // Create new buffer - need to implement this based on actual interface
        var sizeInBytes = elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var memoryBuffer = await memoryManager.AllocateAsync(sizeInBytes, AbstractionsMemory.MemoryOptions.None, cancellationToken);
        // Note: This is simplified - actual implementation would need proper buffer creation
        throw new NotImplementedException("Buffer creation from memory manager needs proper implementation");
    }
    
    /// <summary>Returns a buffer to the pool.</summary>
    public void ReturnBuffer(AbstractionsMemory.IBuffer<T> buffer)
    {
        lock (_lock)
        {
            _availableBuffers.Enqueue(buffer);
        }
    }
}

/// <summary>
/// Double buffering configuration.
/// </summary>
public class DoubleBufferingConfig
{
    /// <summary>Gets or sets whether double buffering is enabled.</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>Gets or sets the buffer swap strategy.</summary>
    public BufferSwapStrategy SwapStrategy { get; set; } = BufferSwapStrategy.Automatic;
}

/// <summary>
/// Buffer swap strategies for double buffering.
/// </summary>
public enum BufferSwapStrategy
{
    /// <summary>Automatic buffer swapping based on completion events.</summary>
    Automatic,
    
    /// <summary>Manual buffer swapping controlled by the application.</summary>
    Manual,
    
    /// <summary>Time-based buffer swapping.</summary>
    TimeBased
}

/// <summary>
/// Prefetching strategy for pipeline execution.
/// </summary>
public class PrefetchingStrategy
{
    /// <summary>Gets or sets whether prefetching is enabled.</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>Gets or sets the number of stages to prefetch ahead.</summary>
    public int PrefetchDepth { get; set; } = 2;
    
    /// <summary>Gets or sets the prefetching policy.</summary>
    public PrefetchingPolicy Policy { get; set; } = PrefetchingPolicy.Aggressive;
}

/// <summary>
/// Prefetching policies.
/// </summary>
public enum PrefetchingPolicy
{
    /// <summary>Conservative prefetching to minimize memory usage.</summary>
    Conservative,
    
    /// <summary>Balanced prefetching for good performance and memory usage.</summary>
    Balanced,
    
    /// <summary>Aggressive prefetching for maximum performance.</summary>
    Aggressive
}

/// <summary>
/// Workload definition for model parallel execution.
/// </summary>
public class ModelParallelWorkload<T> where T : unmanaged
{
    /// <summary>Gets or sets the model layers.</summary>
    public required List<ModelLayer<T>> ModelLayers { get; set; }
    
    /// <summary>Gets or sets the input tensors.</summary>
    public required TensorDescription<T>[] InputTensors { get; set; }
    
    /// <summary>Gets or sets the output tensors.</summary>
    public required TensorDescription<T>[] OutputTensors { get; set; }
    
    /// <summary>Gets or sets the total memory requirement in bytes.</summary>
    public long TotalMemoryRequirementBytes { get; set; }
    
    /// <summary>Gets or sets the total compute requirement in FLOPS.</summary>
    public long TotalComputeRequirementFLOPS { get; set; }
}

/// <summary>
/// Pipeline definition for streaming execution.
/// </summary>
public class PipelineDefinition<T> where T : unmanaged
{
    /// <summary>Gets or sets the pipeline stages.</summary>
    public required List<PipelineStageDefinition> Stages { get; set; }
    
    /// <summary>Gets or sets the input specification.</summary>
    public required PipelineInputSpec<T> InputSpec { get; set; }
    
    /// <summary>Gets or sets the output specification.</summary>
    public required PipelineOutputSpec<T> OutputSpec { get; set; }
}

/// <summary>
/// Definition of a pipeline stage.
/// </summary>
public class PipelineStageDefinition
{
    /// <summary>Gets or sets the stage name.</summary>
    public required string Name { get; set; }
    
    /// <summary>Gets or sets the kernel name for this stage.</summary>
    public required string KernelName { get; set; }
    
    /// <summary>Gets or sets the dependencies on other stages.</summary>
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Input specification for pipeline execution.
/// </summary>
public class PipelineInputSpec<T> where T : unmanaged
{
    /// <summary>Gets or sets the input tensor descriptions.</summary>
    public required TensorDescription<T>[] Tensors { get; set; }
    
    /// <summary>Gets or sets the streaming configuration.</summary>
    public StreamingConfig? StreamingConfig { get; set; }
}

/// <summary>
/// Output specification for pipeline execution.
/// </summary>
public class PipelineOutputSpec<T> where T : unmanaged
{
    /// <summary>Gets or sets the output tensor descriptions.</summary>
    public required TensorDescription<T>[] Tensors { get; set; }
    
    /// <summary>Gets or sets the output format.</summary>
    public OutputFormat Format { get; set; } = OutputFormat.Native;
}

/// <summary>
/// Streaming configuration for pipeline input.
/// </summary>
public class StreamingConfig
{
    /// <summary>Gets or sets whether streaming is enabled.</summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>Gets or sets the chunk size for streaming.</summary>
    public int ChunkSize { get; set; } = 1024;
    
    /// <summary>Gets or sets the buffer depth for streaming.</summary>
    public int BufferDepth { get; set; } = 3;
}

/// <summary>
/// Output formats for pipeline execution.
/// </summary>
public enum OutputFormat
{
    /// <summary>Native device format.</summary>
    Native,
    
    /// <summary>Host-accessible format.</summary>
    Host,
    
    /// <summary>Interleaved format for better cache performance.</summary>
    Interleaved,
    
    /// <summary>Compressed format for reduced memory usage.</summary>
    Compressed
}

/// <summary>
/// Workload definition for work stealing execution.
/// </summary>
public class WorkStealingWorkload<T> where T : unmanaged
{
    /// <summary>Gets or sets the work items.</summary>
    public required List<WorkItem<T>> WorkItems { get; set; }
    
    /// <summary>Gets or sets the load balancing hints.</summary>
    public LoadBalancingHints? LoadBalancingHints { get; set; }
}

/// <summary>
/// Represents a unit of work for work stealing execution.
/// </summary>
public class WorkItem<T> where T : unmanaged
{
    /// <summary>Gets or sets the work item identifier.</summary>
    public required int Id { get; set; }
    
    /// <summary>Gets or sets the input data for this work item.</summary>
    public required AbstractionsMemory.IBuffer<T>[] InputBuffers { get; set; }
    
    /// <summary>Gets or sets the output data for this work item.</summary>
    public required AbstractionsMemory.IBuffer<T>[] OutputBuffers { get; set; }
    
    /// <summary>Gets or sets the estimated processing time for this work item.</summary>
    public double EstimatedProcessingTimeMs { get; set; }
    
    /// <summary>Gets or sets the dependencies on other work items.</summary>
    public List<int> Dependencies { get; set; } = new();
}

/// <summary>
/// Load balancing hints for work stealing execution.
/// </summary>
public class LoadBalancingHints
{
    /// <summary>Gets or sets the preferred devices for specific work items.</summary>
    public Dictionary<int, string> PreferredDevices { get; set; } = new();
    
    /// <summary>Gets or sets the work item priorities.</summary>
    public Dictionary<int, int> Priorities { get; set; } = new();
    
    /// <summary>Gets or sets the affinity groups for work items.</summary>
    public Dictionary<int, int> AffinityGroups { get; set; } = new();
}