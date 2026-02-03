// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Generators;

/// <summary>
/// Marks a method as a persistent ring kernel with message passing capabilities.
/// </summary>
/// <remarks>
/// Ring kernels are GPU-resident kernels that run continuously (persistent mode)
/// or activate on demand (event-driven mode). They support inter-kernel communication
/// through message queues, enabling GPU-native actor systems and complex algorithms.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RingKernelAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the unique identifier for this ring kernel.
    /// </summary>
    public string KernelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of concurrent work items in the ring buffer.
    /// Default is 1024.
    /// </summary>
    public int Capacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the input queue size for incoming messages.
    /// Default is 256.
    /// </summary>
    public int InputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the output queue size for outgoing messages.
    /// Default is 256.
    /// </summary>
    public int OutputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the target compute backends for this ring kernel.
    /// Default is CUDA | OpenCL | Metal (all GPU backends).
    /// </summary>
    public KernelBackends Backends { get; set; } =
        KernelBackends.CUDA | KernelBackends.OpenCL | KernelBackends.Metal;

    /// <summary>
    /// Gets or sets the execution mode (Persistent or EventDriven).
    /// Default is Persistent.
    /// </summary>
    public RingKernelMode Mode { get; set; } = RingKernelMode.Persistent;

    /// <summary>
    /// Gets or sets the message passing strategy for inter-kernel communication.
    /// Default is SharedMemory.
    /// </summary>
    public MessagePassingStrategy MessagingStrategy { get; set; } =
        MessagePassingStrategy.SharedMemory;

    /// <summary>
    /// Gets or sets the application domain for domain-specific optimizations.
    /// Default is General.
    /// </summary>
    public RingKernelDomain Domain { get; set; } = RingKernelDomain.General;

    /// <summary>
    /// Gets or sets the grid dimensions for GPU execution.
    /// </summary>
    public int[]? GridDimensions { get; set; }

    /// <summary>
    /// Gets or sets the block dimensions for GPU execution.
    /// </summary>
    public int[]? BlockDimensions { get; set; }

    /// <summary>
    /// Gets or sets whether this ring kernel should use local (shared) memory.
    /// Default is false.
    /// </summary>
    public bool UseSharedMemory { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes per thread block.
    /// Default is 0 (no shared memory).
    /// </summary>
    public int SharedMemorySize { get; set; }

    /// <summary>
    /// Gets or sets whether this ring kernel uses GPU thread barriers for synchronization.
    /// Default is false.
    /// </summary>
    public bool UseBarriers { get; set; }

    /// <summary>
    /// Gets or sets the synchronization scope for barriers used in this ring kernel.
    /// Default is ThreadBlock.
    /// </summary>
    public BarrierScope BarrierScope { get; set; } = BarrierScope.ThreadBlock;

    /// <summary>
    /// Gets or sets the expected number of threads participating in barrier synchronization.
    /// Default is 0 (automatic based on block size).
    /// </summary>
    public int BarrierCapacity { get; set; }

    /// <summary>
    /// Gets or sets the memory consistency model for this ring kernel's memory operations.
    /// Default is ReleaseAcquire (recommended for message passing).
    /// </summary>
    public MemoryConsistencyModel MemoryConsistency { get; set; } = MemoryConsistencyModel.ReleaseAcquire;

    /// <summary>
    /// Gets or sets whether to enable causal memory ordering (release-acquire semantics).
    /// Default is true for ring kernels.
    /// </summary>
    public bool EnableCausalOrdering { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable GPU hardware timestamp tracking.
    /// Default is false.
    /// </summary>
    public bool EnableTimestamps { get; set; }

    /// <summary>
    /// Gets or sets a unified message queue size that overrides both InputQueueSize and OutputQueueSize.
    /// Default is 0 (use separate sizes).
    /// </summary>
    public int MessageQueueSize { get; set; }

    /// <summary>
    /// Gets or sets how the ring kernel processes messages from its input queue.
    /// Default is Continuous.
    /// </summary>
    public RingProcessingMode ProcessingMode { get; set; } = RingProcessingMode.Continuous;

    /// <summary>
    /// Gets or sets the maximum number of messages processed per dispatch loop iteration.
    /// Default is 0 (unlimited).
    /// </summary>
    public int MaxMessagesPerIteration { get; set; }

    /// <summary>
    /// Gets or sets the input message type for this kernel.
    /// When null, auto-detected from the method's first parameter.
    /// </summary>
    public Type? InputMessageType { get; set; }

    /// <summary>
    /// Gets or sets the output message type for this kernel.
    /// When null, auto-detected from the method's return type.
    /// </summary>
    public Type? OutputMessageType { get; set; }

    /// <summary>
    /// Gets or sets the kernel IDs that this kernel subscribes to (receives K2K messages from).
    /// </summary>
    public string[] SubscribesToKernels { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the kernel IDs that this kernel publishes to (sends K2K messages to).
    /// </summary>
    public string[] PublishesToKernels { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the topics this kernel subscribes to for pub/sub messaging.
    /// </summary>
    public string[] SubscribesToTopics { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the topics this kernel can publish to.
    /// </summary>
    public string[] PublishesToTopics { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets named barriers that this kernel participates in for cross-kernel synchronization.
    /// </summary>
    public string[] NamedBarriers { get; set; } = Array.Empty<string>();
}
