// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Execution.Types;
using ManagedCompiledKernel = DotCompute.Core.Execution.ManagedCompiledKernel;

namespace DotCompute.Core.Execution.Plans
{
    /// <summary>
    /// Execution plan for model parallel execution across multiple devices.
    /// In model parallelism, different parts of a large model are distributed across devices,
    /// with each device responsible for computing specific layers or components of the model.
    /// </summary>
    /// <typeparam name="T">The data type for the execution plan. Must be an unmanaged type.</typeparam>
    public class ModelParallelExecutionPlan<T> : ExecutionPlan<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the model layers that make up the complete model.
        /// Each layer represents a computational component that will be executed on an assigned device.
        /// </summary>
        public required ModelLayer<T>[] ModelLayers { get; set; }

        /// <summary>
        /// Gets or sets the layer assignments to devices.
        /// Maps each layer ID to the specific device that will execute that layer.
        /// </summary>
        public required Dictionary<int, IAccelerator> LayerAssignments { get; set; }

        /// <summary>
        /// Gets or sets the communication schedule between layers.
        /// Defines how data flows between layers executing on different devices,
        /// including synchronization points and data transfer operations.
        /// </summary>
        public required CommunicationSchedule<T> CommunicationSchedule { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModelParallelExecutionPlan{T}"/> class.
        /// Sets the strategy type to ModelParallel by default.
        /// </summary>
        public ModelParallelExecutionPlan()
        {
            StrategyType = ExecutionStrategyType.ModelParallel;
        }
    }

    /// <summary>
    /// Represents a layer in a model parallel execution.
    /// Contains all information needed to execute one layer of a distributed model.
    /// </summary>
    /// <typeparam name="T">The data type for the model layer. Must be an unmanaged type.</typeparam>
    public class ModelLayer<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the layer identifier.
        /// Unique identifier for this layer within the model.
        /// </summary>
        public required int LayerId { get; set; }

        /// <summary>
        /// Gets or sets the layer name.
        /// Human-readable name for this layer (e.g., "conv1", "attention", "fc_output").
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the kernel for this layer.
        /// The compiled computational kernel that implements this layer's operations.
        /// </summary>
        public required ManagedCompiledKernel Kernel { get; set; }

        /// <summary>
        /// Gets or sets the input tensor descriptions.
        /// Describes the input tensors that this layer expects to receive.
        /// </summary>
        public required TensorDescription<T>[] InputTensors { get; set; }

        /// <summary>
        /// Gets or sets the output tensor descriptions.
        /// Describes the output tensors that this layer will produce.
        /// </summary>
        public required TensorDescription<T>[] OutputTensors { get; set; }

        /// <summary>
        /// Gets or sets the memory requirements for this layer in bytes.
        /// Total memory needed to execute this layer, including parameters and activations.
        /// </summary>
        public long MemoryRequirementBytes { get; set; }

        /// <summary>
        /// Gets or sets the estimated compute requirements in FLOPS.
        /// Floating-point operations required to execute this layer.
        /// </summary>
        public long ComputeRequirementFLOPS { get; set; }

        /// <summary>
        /// Gets or sets the dependencies on other layers.
        /// Contains the layer IDs that must complete execution before this layer can begin.
        /// </summary>
        public List<int> Dependencies { get; set; } = [];
    }

    /// <summary>
    /// Describes a tensor for model parallel execution.
    /// Contains metadata about tensor shape, type, and storage for distributed model execution.
    /// </summary>
    /// <typeparam name="T">The data type of the tensor elements. Must be an unmanaged type.</typeparam>
    public class TensorDescription<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the tensor name.
        /// Human-readable identifier for this tensor.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the tensor dimensions.
        /// Array representing the shape of the tensor (e.g., [batch_size, height, width, channels]).
        /// </summary>
        public required int[] Dimensions { get; set; }

        /// <summary>
        /// Gets or sets the tensor data type.
        /// The .NET type representing the data elements in this tensor.
        /// </summary>
        public required Type DataType { get; set; }

        /// <summary>
        /// Gets or sets whether this tensor is shared across devices.
        /// If true, this tensor needs to be accessible by multiple devices.
        /// </summary>
        public bool IsShared { get; set; }

        /// <summary>
        /// Gets or sets the buffer containing the tensor data.
        /// The actual memory buffer where tensor data is stored, if allocated.
        /// </summary>
        public AbstractionsMemory.IUnifiedMemoryBuffer<T>? Buffer { get; set; }

        /// <summary>
        /// Gets the total number of elements in the tensor.
        /// Calculated as the product of all dimensions.
        /// </summary>
        public int ElementCount => Dimensions.Aggregate(1, (a, b) => a * b);

        /// <summary>
        /// Gets the size in bytes of the tensor.
        /// Calculated based on element count and the size of type T.
        /// </summary>
        public long SizeInBytes => ElementCount * global::System.Runtime.InteropServices.Marshal.SizeOf<T>();
    }

    /// <summary>
    /// Defines the communication schedule between layers in model parallel execution.
    /// Manages data flow and synchronization between model layers distributed across devices.
    /// </summary>
    /// <typeparam name="T">The data type for communication operations. Must be an unmanaged type.</typeparam>
    public class CommunicationSchedule<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the communication operations.
        /// Ordered list of data transfer operations between devices.
        /// </summary>
        public required List<CommunicationOperation<T>> Operations { get; set; }

        /// <summary>
        /// Gets or sets the synchronization points.
        /// Points in the execution where devices must wait for each other.
        /// </summary>
        public required List<SynchronizationPoint> SynchronizationPoints { get; set; }
    }

    /// <summary>
    /// Represents a communication operation between devices.
    /// Defines a single data transfer operation in the model parallel execution.
    /// </summary>
    /// <typeparam name="T">The data type being transferred. Must be an unmanaged type.</typeparam>
    public class CommunicationOperation<T> where T : unmanaged
    {
        /// <summary>
        /// Gets or sets the operation identifier.
        /// Unique identifier for this communication operation.
        /// </summary>
        public required int OperationId { get; set; }

        /// <summary>
        /// Gets or sets the source device.
        /// The device that will send the data.
        /// </summary>
        public required IAccelerator SourceDevice { get; set; }

        /// <summary>
        /// Gets or sets the destination device.
        /// The device that will receive the data.
        /// </summary>
        public required IAccelerator DestinationDevice { get; set; }

        /// <summary>
        /// Gets or sets the tensor being transferred.
        /// Description of the data that will be sent between devices.
        /// </summary>
        public required TensorDescription<T> Tensor { get; set; }

        /// <summary>
        /// Gets or sets the operation type.
        /// Specifies whether this is a point-to-point transfer, broadcast, reduce, etc.
        /// </summary>
        public required CommunicationOperationType OperationType { get; set; }

        /// <summary>
        /// Gets or sets the estimated transfer time in milliseconds.
        /// Used for scheduling and optimization decisions.
        /// </summary>
        public double EstimatedTransferTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the execution order for this communication operation.
        /// Used to determine the sequence of operations in the communication schedule.
        /// </summary>
        public int ExecutionOrder { get; set; }
    }

    /// <summary>
    /// Represents a synchronization point in model parallel execution.
    /// Defines a point where multiple devices must coordinate before proceeding.
    /// </summary>
    public class SynchronizationPoint
    {
        /// <summary>
        /// Gets or sets the synchronization point identifier.
        /// Unique identifier for this synchronization point.
        /// </summary>
        public required int SyncId { get; set; }

        /// <summary>
        /// Gets or sets the participating devices.
        /// List of devices that must reach this synchronization point.
        /// </summary>
        public required List<IAccelerator> ParticipatingDevices { get; set; }

        /// <summary>
        /// Gets or sets the synchronization type.
        /// Specifies the type of synchronization required (barrier, allreduce, etc.).
        /// </summary>
        public required SynchronizationType SyncType { get; set; }

        /// <summary>
        /// Gets or sets the timeout for this synchronization in milliseconds.
        /// Maximum time to wait for all devices to reach this point.
        /// </summary>
        public double TimeoutMs { get; set; } = 10000; // 10 seconds default
    }
}
