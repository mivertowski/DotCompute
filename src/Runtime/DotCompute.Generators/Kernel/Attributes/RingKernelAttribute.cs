// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Generators.Kernel.Enums;

namespace DotCompute.Generators.Kernel.Attributes;

/// <summary>
/// Marks a method as a persistent ring kernel with message passing capabilities.
/// </summary>
/// <remarks>
/// Ring kernels are GPU-resident kernels that run continuously (persistent mode)
/// or activate on demand (event-driven mode). They support inter-kernel communication
/// through message queues, enabling GPU-native actor systems and complex algorithms
/// like graph analytics and spatial simulations.
///
/// Unlike standard kernels that execute once and return, ring kernels maintain state
/// across invocations and can communicate with other ring kernels through lock-free
/// message queues.
/// </remarks>
/// <example>
/// <code>
/// [RingKernel(
///     KernelId = "pagerank-vertex",
///     Domain = RingKernelDomain.GraphAnalytics,
///     Mode = RingKernelMode.Persistent,
///     Capacity = 10000)]
/// public static void PageRankVertex(
///     MessageQueue&lt;VertexMessage&gt; incoming,
///     MessageQueue&lt;VertexMessage&gt; outgoing,
///     Span&lt;float&gt; pageRank)
/// {
///     int vertexId = Kernel.ThreadId.X;
///
///     // Process incoming rank contributions
///     while (incoming.TryDequeue(out var msg))
///     {
///         if (msg.TargetVertex == vertexId)
///             pageRank[vertexId] += msg.Rank;
///     }
///
///     // Send updated rank to neighbors
///     outgoing.Enqueue(new VertexMessage
///     {
///         TargetVertex = neighborId,
///         Rank = pageRank[vertexId]
///     });
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RingKernelAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the unique identifier for this ring kernel.
    /// </summary>
    /// <value>
    /// A unique string identifier used for kernel lookup and coordination.
    /// Must be unique within the application.
    /// </value>
    /// <remarks>
    /// The kernel ID is used by the runtime to route messages between ring kernels
    /// and manage kernel lifecycle. Choose descriptive IDs like "pagerank-vertex"
    /// or "wave-propagation-2d" rather than generic names.
    /// </remarks>
    public string KernelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of concurrent work items in the ring buffer.
    /// Default is 1024.
    /// </summary>
    /// <value>
    /// The capacity of the kernel's internal work buffer. Must be a power of 2
    /// for optimal performance.
    /// </value>
    /// <remarks>
    /// This defines how many concurrent work items the kernel can handle.
    /// Higher capacity allows more buffering but consumes more GPU memory.
    /// Typical values: 256 (low latency), 1024 (balanced), 4096 (high throughput).
    ///
    /// Capacity should be sized based on:
    /// - Expected message arrival rate
    /// - Processing time per message
    /// - Available GPU memory
    /// </remarks>
    public int Capacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the input queue size for incoming messages.
    /// Default is 256.
    /// </summary>
    /// <value>
    /// The size of the message queue for receiving messages from other kernels.
    /// Must be a power of 2.
    /// </value>
    /// <remarks>
    /// Input queues buffer incoming messages before the kernel processes them.
    /// Size should accommodate burst traffic patterns. If the queue fills,
    /// senders will block or drop messages based on the messaging strategy.
    /// </remarks>
    public int InputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the output queue size for outgoing messages.
    /// Default is 256.
    /// </summary>
    /// <value>
    /// The size of the message queue for sending messages to other kernels.
    /// Must be a power of 2.
    /// </value>
    /// <remarks>
    /// Output queues buffer outgoing messages before they're consumed by receivers.
    /// Larger queues provide better throughput but increase memory usage.
    /// Consider the downstream kernel's input queue size when configuring.
    /// </remarks>
    public int OutputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the target compute backends for this ring kernel.
    /// Default is CUDA | OpenCL | Metal (all GPU backends).
    /// </summary>
    /// <value>
    /// A combination of <see cref="KernelBackends"/> flags indicating
    /// which execution backends this ring kernel supports.
    /// </value>
    /// <remarks>
    /// Ring kernels require persistent kernel support, which is best
    /// supported on GPU backends. CPU backend provides a simulation
    /// using thread pools for testing purposes.
    ///
    /// Not all messaging strategies are available on all backends:
    /// - SharedMemory: All backends
    /// - AtomicQueue: All backends
    /// - P2P: CUDA only
    /// - NCCL: CUDA only
    /// </remarks>
    public KernelBackends Backends { get; set; } =
        KernelBackends.CUDA | KernelBackends.OpenCL | KernelBackends.Metal;

    /// <summary>
    /// Gets or sets the execution mode (Persistent or EventDriven).
    /// Default is Persistent.
    /// </summary>
    /// <value>
    /// The execution mode determining when the kernel is active.
    /// </value>
    /// <remarks>
    /// Persistent mode keeps the kernel running continuously, ideal for
    /// streaming workloads and real-time simulations. Event-driven mode
    /// activates the kernel only when messages arrive, saving power for
    /// sporadic workloads.
    /// </remarks>
    public RingKernelMode Mode { get; set; } = RingKernelMode.Persistent;

    /// <summary>
    /// Gets or sets the message passing strategy for inter-kernel communication.
    /// Default is SharedMemory.
    /// </summary>
    /// <value>
    /// The strategy used for message queue implementation and synchronization.
    /// </value>
    /// <remarks>
    /// Strategy selection depends on:
    /// - Hardware capabilities (P2P requires NVLink, NCCL requires CUDA)
    /// - Contention levels (AtomicQueue better under high contention)
    /// - Performance requirements (P2P best for multi-GPU, SharedMemory for single GPU)
    ///
    /// Start with SharedMemory (default) and switch to specialized strategies
    /// only if profiling shows benefits.
    /// </remarks>
    public MessagePassingStrategy MessagingStrategy { get; set; } =
        MessagePassingStrategy.SharedMemory;

    /// <summary>
    /// Gets or sets the application domain for domain-specific optimizations.
    /// Default is General (no domain-specific optimizations).
    /// </summary>
    /// <value>
    /// The application domain hint for enabling specialized optimizations.
    /// </value>
    /// <remarks>
    /// Domain-specific optimizations can significantly improve performance:
    /// - GraphAnalytics: Sparse data structures, irregular workloads
    /// - SpatialSimulation: Stencil patterns, halo exchange
    /// - ActorModel: Mailbox queues, supervision hierarchies
    ///
    /// Setting the correct domain enables the compiler to apply proven
    /// optimization patterns for that class of algorithms.
    /// </remarks>
    public RingKernelDomain Domain { get; set; } = RingKernelDomain.General;

    /// <summary>
    /// Gets or sets the grid dimensions for GPU execution.
    /// </summary>
    /// <value>
    /// An array specifying the number of thread blocks in each dimension.
    /// Can be 1D, 2D, or 3D. Null indicates automatic grid size calculation.
    /// </value>
    /// <remarks>
    /// For ring kernels, the grid typically remains fixed for the kernel's
    /// lifetime. Each thread block can represent a logical unit of work
    /// (e.g., a vertex in graph analytics, a spatial region in simulations).
    /// </remarks>
    public int[]? GridDimensions { get; set; }

    /// <summary>
    /// Gets or sets the block dimensions for GPU execution.
    /// </summary>
    /// <value>
    /// An array specifying the number of threads per block in each dimension.
    /// Can be 1D, 2D, or 3D. Null indicates automatic block size selection.
    /// </value>
    /// <remarks>
    /// Block size affects message processing parallelism and memory access
    /// patterns. For message-driven kernels, typical values are 64-256 threads
    /// per block to balance occupancy and message processing efficiency.
    /// </remarks>
    public int[]? BlockDimensions { get; set; }

    /// <summary>
    /// Gets or sets whether this ring kernel should use local (shared) memory.
    /// Default is false.
    /// </summary>
    /// <value>
    /// True to allocate shared memory for thread-block-local scratch space.
    /// </value>
    /// <remarks>
    /// Shared memory is useful for reduction operations, staging data,
    /// or coordinating between threads in a block. For graph analytics
    /// and spatial simulations, shared memory can significantly improve
    /// performance by reducing global memory traffic.
    /// </remarks>
    public bool UseSharedMemory { get; set; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes per thread block.
    /// Default is 0 (no shared memory).
    /// </summary>
    /// <value>
    /// The amount of shared memory to allocate, in bytes. Must not exceed
    /// device limits (typically 48-96 KB per block).
    /// </value>
    /// <remarks>
    /// Only relevant when UseSharedMemory is true. The compiler will
    /// automatically calculate required size if set to 0 and shared
    /// memory is needed.
    /// </remarks>
    public int SharedMemorySize { get; set; }
}
