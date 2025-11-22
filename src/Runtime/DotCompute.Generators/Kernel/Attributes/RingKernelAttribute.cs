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

    /// <summary>
    /// Gets or sets whether this ring kernel uses GPU thread barriers for synchronization.
    /// Default is false.
    /// </summary>
    /// <value>
    /// <c>true</c> if the kernel requires explicit thread synchronization barriers;
    /// <c>false</c> if threads operate independently within message processing.
    /// </value>
    /// <remarks>
    /// <para>
    /// Ring kernels often need barriers for coordinating message processing across threads
    /// within a thread block, especially for shared memory communication patterns and
    /// reduction operations on message batches.
    /// </para>
    /// <para>
    /// <strong>Common Ring Kernel Barrier Patterns:</strong>
    /// <list type="bullet">
    /// <item><description>Message batch processing: Sync after reading messages into shared memory</description></item>
    /// <item><description>Reduction operations: Combine results from multiple threads before sending</description></item>
    /// <item><description>Collective communication: Coordinate multi-thread message sends</description></item>
    /// <item><description>State updates: Ensure all threads see consistent state after updates</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong>
    /// In persistent ring kernels, barrier overhead is amortized across the kernel's lifetime.
    /// However, barriers can impact throughput in high-message-rate scenarios (~10-20ns per sync).
    /// </para>
    /// </remarks>
    public bool UseBarriers { get; set; }

    /// <summary>
    /// Gets or sets the synchronization scope for barriers used in this ring kernel.
    /// Default is ThreadBlock.
    /// </summary>
    /// <value>
    /// The barrier scope determining which threads synchronize together.
    /// </value>
    /// <remarks>
    /// <para>
    /// For ring kernels, ThreadBlock scope is most common since ring kernels typically
    /// coordinate message processing within each thread block independently. Warp-level
    /// barriers can be useful for fine-grained message queue operations.
    /// </para>
    /// <para>
    /// <strong>Grid Barriers Not Recommended:</strong>
    /// Grid-wide barriers are typically not useful in ring kernels since:
    /// <list type="bullet">
    /// <item><description>Ring kernels run persistently or event-driven (not batch)</description></item>
    /// <item><description>Message passing provides natural coordination across blocks</description></item>
    /// <item><description>Not supported on Metal backend</description></item>
    /// </list>
    /// If global coordination is needed, use message passing between ring kernels instead.
    /// </para>
    /// </remarks>
    public BarrierScope BarrierScope { get; set; } = BarrierScope.ThreadBlock;

    /// <summary>
    /// Gets or sets the expected number of threads participating in barrier synchronization.
    /// Default is 0 (automatic based on block size).
    /// </summary>
    /// <value>
    /// The barrier capacity, typically equal to the number of threads per block.
    /// Set to 0 for automatic calculation based on BlockDimensions.
    /// </value>
    /// <remarks>
    /// For ring kernels, barrier capacity should match the thread block size since
    /// all threads in a block typically participate in synchronization for message
    /// processing coordination.
    /// </remarks>
    public int BarrierCapacity { get; set; }

    /// <summary>
    /// Gets or sets the memory consistency model for this ring kernel's memory operations.
    /// Default is ReleaseAcquire (recommended for message passing).
    /// </summary>
    /// <value>
    /// The memory consistency model controlling memory operation ordering and visibility.
    /// </value>
    /// <remarks>
    /// <para>
    /// Ring kernels benefit from ReleaseAcquire consistency due to their message-passing
    /// nature. This ensures that:
    /// <list type="bullet">
    /// <item><description>Message writes are visible before queue updates (release)</description></item>
    /// <item><description>Message reads see all prior writes from sender (acquire)</description></item>
    /// <item><description>Causality is preserved across message boundaries</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance vs. Correctness:</strong>
    /// <list type="table">
    /// <item><term>Relaxed</term><description>Fastest but requires manual fencing (expert use only)</description></item>
    /// <item><term>ReleaseAcquire</term><description>85% speed, safe for message passing (recommended)</description></item>
    /// <item><term>Sequential</term><description>60% speed, only if debugging race conditions</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Unlike regular kernels that default to Relaxed, ring kernels default to ReleaseAcquire
    /// because message-passing correctness is critical and the overhead is acceptable given
    /// the persistent/event-driven execution model.
    /// </para>
    /// </remarks>
    public MemoryConsistencyModel MemoryConsistency { get; set; } = MemoryConsistencyModel.ReleaseAcquire;

    /// <summary>
    /// Gets or sets whether to enable causal memory ordering (release-acquire semantics).
    /// Default is true for ring kernels (unlike regular kernels).
    /// </summary>
    /// <value>
    /// <c>true</c> to enable release-acquire memory ordering; <c>false</c> for relaxed ordering.
    /// </value>
    /// <remarks>
    /// <para>
    /// Ring kernels enable causal ordering by default because message-passing patterns
    /// require proper causality to avoid data races. This ensures:
    /// <list type="bullet">
    /// <item><description>Messages enqueued by sender are visible to receiver</description></item>
    /// <item><description>State updates are visible across message boundaries</description></item>
    /// <item><description>No reordering of message operations across barriers</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>When to Disable:</strong> Only disable if you're implementing custom
    /// synchronization with manual fences and have profiled to confirm it's necessary.
    /// The 15% overhead is typically acceptable for ring kernel workloads.
    /// </para>
    /// </remarks>
    public bool EnableCausalOrdering { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable GPU hardware timestamp tracking for temporal consistency.
    /// Default is false.
    /// </summary>
    /// <value>
    /// <c>true</c> to capture GPU timestamps for temporal actor systems and causal ordering;
    /// <c>false</c> to disable timestamp tracking.
    /// </value>
    /// <remarks>
    /// <para>
    /// When enabled, the kernel captures GPU hardware timestamps (via <c>clock64()</c>) for:
    /// <list type="bullet">
    /// <item><description>Hybrid Logical Clock (HLC) implementation</description></item>
    /// <item><description>Vector clock synchronization across actors</description></item>
    /// <item><description>Temporal pattern detection in message streams</description></item>
    /// <item><description>Causal consistency validation</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Timestamp Resolution:</strong> On CUDA CC 6.0+ GPUs, timestamps have ~1ns resolution.
    /// Older GPUs may have coarser granularity. Metal and OpenCL backends support timestamps via
    /// platform-specific APIs.
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong> Timestamp capture adds ~2-5ns overhead per message.
    /// For temporal actor systems, this overhead is typically acceptable and enables powerful
    /// causal ordering guarantees.
    /// </para>
    /// </remarks>
    public bool EnableTimestamps { get; set; }

    /// <summary>
    /// Gets or sets a unified message queue size that overrides both InputQueueSize and OutputQueueSize.
    /// Default is 0 (use InputQueueSize/OutputQueueSize separately).
    /// </summary>
    /// <value>
    /// The unified queue size for both input and output queues, or 0 to use separate sizes.
    /// Must be a power of 2 if non-zero.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property provides a convenient way to set both input and output queue sizes
    /// to the same value for symmetric message passing patterns. When set to a non-zero value,
    /// it overrides both <see cref="InputQueueSize"/> and <see cref="OutputQueueSize"/>.
    /// </para>
    /// <para>
    /// <strong>Usage Examples:</strong>
    /// <list type="bullet">
    /// <item><description>4096 - High-volume actor with symmetric send/receive</description></item>
    /// <item><description>8192 - Very high throughput processing</description></item>
    /// <item><description>0 - Use InputQueueSize/OutputQueueSize for asymmetric patterns</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Memory Impact:</strong> Total queue memory = MessageQueueSize × (MaxInputMessageSizeBytes + MaxOutputMessageSizeBytes)
    /// </para>
    /// </remarks>
    public int MessageQueueSize { get; set; }

    /// <summary>
    /// Gets or sets how the ring kernel processes messages from its input queue.
    /// Default is Continuous (single message per iteration for minimum latency).
    /// </summary>
    /// <value>
    /// The processing mode: Continuous, Batch, or Adaptive.
    /// </value>
    /// <remarks>
    /// <para>
    /// Processing mode affects the trade-off between latency and throughput:
    /// </para>
    /// <para>
    /// <strong>Continuous Mode:</strong> Process one message per iteration.
    /// <list type="bullet">
    /// <item><description>Lowest latency (~100-500ns per message)</description></item>
    /// <item><description>Best for latency-critical actor request-response</description></item>
    /// <item><description>Lower peak throughput due to dispatch overhead</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Batch Mode:</strong> Process multiple messages per iteration.
    /// <list type="bullet">
    /// <item><description>Highest throughput (amortizes dispatch overhead)</description></item>
    /// <item><description>Best for high-volume data processing pipelines</description></item>
    /// <item><description>Higher latency for individual messages</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Adaptive Mode:</strong> Switch between Continuous and Batch based on queue depth.
    /// <list type="bullet">
    /// <item><description>Low latency when queue is shallow</description></item>
    /// <item><description>High throughput when queue is deep</description></item>
    /// <item><description>Recommended for variable workloads</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public RingProcessingMode ProcessingMode { get; set; } = RingProcessingMode.Continuous;

    /// <summary>
    /// Gets or sets the maximum number of messages processed per dispatch loop iteration.
    /// Default is 0 (unlimited - process all available messages).
    /// </summary>
    /// <value>
    /// The maximum messages per iteration, or 0 for unlimited processing.
    /// </value>
    /// <remarks>
    /// <para>
    /// Limiting messages per iteration ensures fairness when multiple ring kernels share
    /// GPU resources. Without a limit, high-volume actors can starve lower-volume actors
    /// by monopolizing execution time.
    /// </para>
    /// <para>
    /// <strong>Fairness Patterns:</strong>
    /// <list type="bullet">
    /// <item><description>16 - Bounded execution time, good for mixed workloads</description></item>
    /// <item><description>32 - Balance between fairness and efficiency</description></item>
    /// <item><description>0 - No limit, process entire queue (can cause starvation)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong> Setting a limit adds a counter check per iteration
    /// (~1-2 cycles overhead). The fairness benefit typically outweighs this cost in multi-actor systems.
    /// </para>
    /// <para>
    /// <strong>Interaction with ProcessingMode:</strong>
    /// <list type="bullet">
    /// <item><description>Continuous: Iteration limit applies to single-message iterations</description></item>
    /// <item><description>Batch: Iteration limit × batch size = total messages processed</description></item>
    /// <item><description>Adaptive: Limit applies to both continuous and batch phases</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int MaxMessagesPerIteration { get; set; }

    // ============================================================================
    // Unified Kernel System Properties (Inline Handler Support)
    // ============================================================================

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
