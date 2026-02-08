// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Abstractions.RingKernels;

namespace DotCompute.Abstractions.Attributes;

/// <summary>
/// Marks a method as a ring kernel - a persistent GPU kernel with message passing capabilities.
/// Ring kernels stay resident on the GPU and can be activated/deactivated dynamically,
/// enabling GPU-native actor programming models and complex communication patterns.
/// </summary>
/// <remarks>
/// <para>
/// Ring kernels differ from standard kernels in several key ways:
/// </para>
/// <list type="bullet">
/// <item><description>They remain persistent on the GPU after launch</description></item>
/// <item><description>They support message passing between kernels via lock-free queues</description></item>
/// <item><description>They can be activated/deactivated without termination</description></item>
/// <item><description>They enable GPU-native actor systems and complex algorithms</description></item>
/// </list>
/// <para>
/// Ring kernels are ideal for:
/// </para>
/// <list type="bullet">
/// <item><description>Graph analytics (vertex-centric message passing)</description></item>
/// <item><description>Spatial simulations (halo exchange between blocks)</description></item>
/// <item><description>Actor model implementations (mailbox-based communication)</description></item>
/// <item><description>Streaming pipelines (producer-consumer patterns)</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// [RingKernel(
///     KernelId = "graph-worker",
///     Domain = RingKernelDomain.GraphAnalytics,
///     MessagingStrategy = MessagePassingStrategy.SharedMemory,
///     Capacity = 1024)]
/// public static void GraphWorker(
///     MessageQueue&lt;VertexMessage&gt; input,
///     MessageQueue&lt;VertexMessage&gt; output,
///     Span&lt;float&gt; vertexData)
/// {
///     int vertexId = Kernel.ThreadId.X;
///
///     while (input.TryDequeue(out var msg))
///     {
///         // Process message and update vertex data
///         vertexData[vertexId] += msg.Value;
///
///         // Send result to neighbors
///         output.Enqueue(new VertexMessage { TargetVertex = msg.Sender, Value = vertexData[vertexId] });
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RingKernelAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the unique identifier for this ring kernel.
    /// Must be unique within the application to enable runtime management and message routing.
    /// </summary>
    /// <value>The kernel identifier string. Defaults to an empty string if not specified.</value>
    /// <remarks>
    /// The kernel ID is used for:
    /// <list type="bullet">
    /// <item><description>Launching and managing kernel instances at runtime</description></item>
    /// <item><description>Routing messages between kernels</description></item>
    /// <item><description>Collecting metrics and profiling data</description></item>
    /// </list>
    /// </remarks>
    public string KernelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of work items in the ring buffer.
    /// This determines how many concurrent work items can be processed by the kernel.
    /// </summary>
    /// <value>The ring buffer capacity. Defaults to 1024 work items.</value>
    /// <remarks>
    /// Capacity affects:
    /// <list type="bullet">
    /// <item><description>GPU memory usage (larger capacity requires more memory)</description></item>
    /// <item><description>Throughput (larger capacity may improve throughput for bursty workloads)</description></item>
    /// <item><description>Latency (smaller capacity may reduce latency for steady workloads)</description></item>
    /// </list>
    /// Choose capacity based on expected workload characteristics and available GPU memory.
    /// </remarks>
    public int Capacity { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the input queue size for incoming messages.
    /// Controls the buffer size for messages sent to this kernel from other kernels or the host.
    /// </summary>
    /// <value>The input queue size in number of messages. Defaults to 256 messages.</value>
    /// <remarks>
    /// A larger input queue allows more messages to be buffered, reducing the chance of
    /// dropped messages in high-throughput scenarios, but increases memory usage.
    /// </remarks>
    public int InputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the output queue size for outgoing messages.
    /// Controls the buffer size for messages sent from this kernel to other kernels or the host.
    /// </summary>
    /// <value>The output queue size in number of messages. Defaults to 256 messages.</value>
    /// <remarks>
    /// A larger output queue allows more messages to be buffered, reducing backpressure
    /// in producer-consumer scenarios, but increases memory usage.
    /// </remarks>
    public int OutputQueueSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum input message size in bytes.
    /// This configures the buffer size allocated for each individual input message.
    /// </summary>
    /// <value>The maximum size of a single input message. Defaults to 65792 bytes (64KB + 256-byte header).</value>
    /// <remarks>
    /// <para>
    /// This value must be large enough to accommodate the largest serialized message that will
    /// be sent to this kernel. If a message exceeds this size, it will be truncated or rejected.
    /// </para>
    /// <para>
    /// The default value of 65792 bytes (65536 + 256) is designed to handle:
    /// <list type="bullet">
    /// <item><description>256-byte message header (routing, timestamp, metadata)</description></item>
    /// <item><description>64KB payload (MemoryPack serialized data)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Memory Impact:</b> Total input queue memory = InputQueueSize × MaxInputMessageSizeBytes
    /// </para>
    /// <para>
    /// <b>Performance:</b> Larger buffers use more GPU memory but prevent message truncation.
    /// Match this value to your actual message size for optimal memory usage.
    /// </para>
    /// </remarks>
    public int MaxInputMessageSizeBytes { get; set; } = 65792;

    /// <summary>
    /// Gets or sets the maximum output message size in bytes.
    /// This configures the buffer size allocated for each individual output message.
    /// </summary>
    /// <value>The maximum size of a single output message. Defaults to 65792 bytes (64KB + 256-byte header).</value>
    /// <remarks>
    /// <para>
    /// This value must be large enough to accommodate the largest serialized message that will
    /// be sent from this kernel. If a message exceeds this size, it will be truncated or rejected.
    /// </para>
    /// <para>
    /// The default value of 65792 bytes (65536 + 256) is designed to handle:
    /// <list type="bullet">
    /// <item><description>256-byte message header (routing, timestamp, metadata)</description></item>
    /// <item><description>64KB payload (MemoryPack serialized data)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Memory Impact:</b> Total output queue memory = OutputQueueSize × MaxOutputMessageSizeBytes
    /// </para>
    /// <para>
    /// <b>Performance:</b> Larger buffers use more GPU memory but prevent message truncation.
    /// Match this value to your actual message size for optimal memory usage.
    /// </para>
    /// </remarks>
    public int MaxOutputMessageSizeBytes { get; set; } = 65792;

    /// <summary>
    /// Gets or sets the target backends for this ring kernel.
    /// Multiple backends can be specified using bitwise OR flags.
    /// </summary>
    /// <value>
    /// The supported backends as flags. Defaults to CUDA | OpenCL | Metal.
    /// CPU backend can be added for testing and simulation.
    /// </value>
    /// <remarks>
    /// Backend selection affects:
    /// <list type="bullet">
    /// <item><description>Code generation strategies (each backend has different capabilities)</description></item>
    /// <item><description>Performance characteristics (persistent kernels are optimal on GPU)</description></item>
    /// <item><description>Availability (some backends may not support all features)</description></item>
    /// </list>
    /// The runtime will select the best available backend from the specified options.
    /// </remarks>
    public KernelBackends Backends { get; set; } = KernelBackends.CUDA | KernelBackends.OpenCL | KernelBackends.Metal;

    /// <summary>
    /// Gets or sets the execution mode for this ring kernel.
    /// Determines whether the kernel runs continuously or activates on demand.
    /// </summary>
    /// <value>The execution mode. Defaults to <see cref="RingKernelMode.Persistent"/>.</value>
    /// <remarks>
    /// <para><b>Persistent mode:</b></para>
    /// <list type="bullet">
    /// <item><description>Kernel runs continuously in a loop until termination</description></item>
    /// <item><description>Lower latency for message processing</description></item>
    /// <item><description>Higher GPU utilization</description></item>
    /// <item><description>Best for streaming and real-time workloads</description></item>
    /// </list>
    /// <para><b>EventDriven mode:</b></para>
    /// <list type="bullet">
    /// <item><description>Kernel activates only when messages arrive or events occur</description></item>
    /// <item><description>Lower power consumption when idle</description></item>
    /// <item><description>Better GPU sharing with other workloads</description></item>
    /// <item><description>Best for bursty or sporadic workloads</description></item>
    /// </list>
    /// </remarks>
    public RingKernelMode Mode { get; set; } = RingKernelMode.Persistent;

    /// <summary>
    /// Gets or sets the message passing strategy used by this ring kernel.
    /// Different strategies offer different trade-offs in performance, complexity, and hardware support.
    /// </summary>
    /// <value>The messaging strategy. Defaults to <see cref="MessagePassingStrategy.SharedMemory"/>.</value>
    /// <remarks>
    /// <para><b>SharedMemory:</b></para>
    /// <list type="bullet">
    /// <item><description>Lock-free ring buffers in GPU shared memory</description></item>
    /// <item><description>Lowest latency for intra-block communication</description></item>
    /// <item><description>Supported on all GPU backends</description></item>
    /// </list>
    /// <para><b>AtomicQueue:</b></para>
    /// <list type="bullet">
    /// <item><description>Atomic operations with exponential backoff</description></item>
    /// <item><description>Better for highly contended scenarios</description></item>
    /// <item><description>More robust under heavy load</description></item>
    /// </list>
    /// <para><b>P2P:</b></para>
    /// <list type="bullet">
    /// <item><description>Peer-to-peer GPU memory transfers (CUDA only)</description></item>
    /// <item><description>Direct GPU-to-GPU communication without CPU involvement</description></item>
    /// <item><description>Best for multi-GPU systems</description></item>
    /// </list>
    /// <para><b>NCCL:</b></para>
    /// <list type="bullet">
    /// <item><description>NVIDIA Collective Communications Library (CUDA only)</description></item>
    /// <item><description>Optimized for collective operations across multiple GPUs</description></item>
    /// <item><description>Best for distributed training and large-scale simulations</description></item>
    /// </list>
    /// </remarks>
    public MessagePassingStrategy MessagingStrategy { get; set; } = MessagePassingStrategy.SharedMemory;

    /// <summary>
    /// Gets or sets domain-specific optimization hints for this ring kernel.
    /// Helps the compiler apply appropriate optimizations for the algorithm type.
    /// </summary>
    /// <value>The kernel domain. Defaults to <see cref="RingKernelDomain.General"/>.</value>
    /// <remarks>
    /// <para><b>General:</b></para>
    /// <list type="bullet">
    /// <item><description>No specific domain optimizations</description></item>
    /// <item><description>Use for custom algorithms</description></item>
    /// </list>
    /// <para><b>GraphAnalytics:</b></para>
    /// <list type="bullet">
    /// <item><description>Vertex-centric message passing patterns (Pregel model)</description></item>
    /// <item><description>Optimized for sparse data structures</description></item>
    /// <item><description>Best for PageRank, BFS, shortest paths</description></item>
    /// </list>
    /// <para><b>SpatialSimulation:</b></para>
    /// <list type="bullet">
    /// <item><description>Stencil operations on regular grids</description></item>
    /// <item><description>Optimized for halo exchange patterns</description></item>
    /// <item><description>Best for wave propagation, heat transfer, fluid dynamics</description></item>
    /// </list>
    /// <para><b>ActorModel:</b></para>
    /// <list type="bullet">
    /// <item><description>Mailbox-based message passing</description></item>
    /// <item><description>Optimized for message routing and supervision</description></item>
    /// <item><description>Best for concurrent systems, simulations</description></item>
    /// </list>
    /// </remarks>
    public RingKernelDomain Domain { get; set; } = RingKernelDomain.General;

    /// <summary>
    /// Gets or sets whether this ring kernel uses GPU thread barriers for synchronization.
    /// Default is false.
    /// </summary>
    public bool UseBarriers { get; set; }

    /// <summary>
    /// Gets or sets the synchronization scope for barriers used in this ring kernel.
    /// Default is ThreadBlock.
    /// </summary>
    public Barriers.BarrierScope BarrierScope { get; set; } = Barriers.BarrierScope.ThreadBlock;

    /// <summary>
    /// Gets or sets the expected number of threads participating in barrier synchronization.
    /// Default is 0 (automatic based on block size).
    /// </summary>
    public int BarrierCapacity { get; set; }

    /// <summary>
    /// Gets or sets the memory consistency model for this ring kernel's memory operations.
    /// Default is ReleaseAcquire (recommended for message passing).
    /// </summary>
    public Memory.MemoryConsistencyModel MemoryConsistency { get; set; } = Memory.MemoryConsistencyModel.ReleaseAcquire;

    /// <summary>
    /// Gets or sets whether to enable causal memory ordering (release-acquire semantics).
    /// Default is true for ring kernels (unlike regular kernels).
    /// </summary>
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
    /// </summary>
    /// <value>
    /// The type of input messages this kernel processes, or null for auto-detection.
    /// </value>
    /// <remarks>
    /// <para>
    /// When specified, the kernel method should have a parameter of this type.
    /// The code generator will create serialization code for this type.
    /// </para>
    /// <para>
    /// When null, the generator auto-detects the input type from the method's first parameter.
    /// </para>
    /// </remarks>
    public Type? InputMessageType { get; set; }

    /// <summary>
    /// Gets or sets the output message type for this kernel.
    /// </summary>
    /// <value>
    /// The type of output messages this kernel produces, or null for auto-detection.
    /// </value>
    /// <remarks>
    /// <para>
    /// When specified, the kernel method should return this type (or void for fire-and-forget).
    /// The code generator will create serialization code for this type.
    /// </para>
    /// <para>
    /// When null, the generator auto-detects the output type from the method's return type.
    /// </para>
    /// </remarks>
    public Type? OutputMessageType { get; set; }

    /// <summary>
    /// Gets or sets the kernel IDs that this kernel subscribes to (receives messages from).
    /// </summary>
    /// <value>
    /// Array of kernel IDs for kernel-to-kernel message reception, or empty for none.
    /// </value>
    /// <remarks>
    /// <para>
    /// Enables actor-to-actor communication patterns. When specified, this kernel can
    /// receive messages from the listed kernels using <c>RingKernelContext.TryReceiveFromKernel</c>.
    /// </para>
    /// <para>
    /// The runtime allocates K2K message queues for each subscription.
    /// </para>
    /// <example>
    /// <code>
    /// [RingKernel(
    ///     KernelId = "Aggregator",
    ///     SubscribesToKernels = new[] { "Worker1", "Worker2", "Worker3" })]
    /// public static AggregateResponse Process(AggregateRequest req, RingKernelContext ctx)
    /// {
    ///     // Can receive from Worker1, Worker2, Worker3
    ///     while (ctx.TryReceiveFromKernel&lt;WorkerResult&gt;("Worker1", out var result))
    ///     {
    ///         // Process result from Worker1
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public string[] SubscribesToKernels { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the kernel IDs that this kernel publishes to (sends messages to).
    /// </summary>
    /// <value>
    /// Array of kernel IDs for kernel-to-kernel message sending, or empty for none.
    /// </value>
    /// <remarks>
    /// <para>
    /// Enables actor-to-actor communication patterns. When specified, this kernel can
    /// send messages to the listed kernels using <c>RingKernelContext.SendToKernel</c>.
    /// </para>
    /// <para>
    /// The runtime validates that target kernels exist and allocates K2K message queues.
    /// </para>
    /// <example>
    /// <code>
    /// [RingKernel(
    ///     KernelId = "Producer",
    ///     PublishesToKernels = new[] { "Consumer", "Logger" })]
    /// public static ProduceResponse Process(ProduceRequest req, RingKernelContext ctx)
    /// {
    ///     var result = ComputeResult(req);
    ///
    ///     // Send to Consumer kernel (K2K messaging)
    ///     ctx.SendToKernel("Consumer", new ConsumeMessage(result));
    ///
    ///     // Also log to Logger kernel
    ///     ctx.SendToKernel("Logger", new LogMessage($"Produced: {result}"));
    ///
    ///     return new ProduceResponse(result);
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public string[] PublishesToKernels { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the topics this kernel subscribes to for pub/sub messaging.
    /// </summary>
    /// <value>
    /// Array of topic names for pub/sub subscription, or empty for none.
    /// </value>
    /// <remarks>
    /// <para>
    /// Enables publish-subscribe communication patterns. Messages published to a topic
    /// are delivered to all kernels subscribed to that topic.
    /// </para>
    /// <para>
    /// Topics are useful for broadcast scenarios where multiple kernels need to receive
    /// the same message (e.g., configuration updates, heartbeats).
    /// </para>
    /// </remarks>
    public string[] SubscribesToTopics { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the topics this kernel can publish to.
    /// </summary>
    /// <value>
    /// Array of topic names for pub/sub publishing, or empty for none.
    /// </value>
    /// <remarks>
    /// <para>
    /// Enables publish-subscribe communication patterns. Use
    /// <c>RingKernelContext.PublishToTopic</c> to send messages to all subscribers.
    /// </para>
    /// </remarks>
    public string[] PublishesToTopics { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets named barriers that this kernel participates in.
    /// </summary>
    /// <value>
    /// Array of named barrier identifiers this kernel uses, or empty for none.
    /// </value>
    /// <remarks>
    /// <para>
    /// Named barriers enable synchronization across multiple kernels. All kernels
    /// participating in a named barrier must reach the barrier before any can proceed.
    /// </para>
    /// <para>
    /// Use <c>RingKernelContext.NamedBarrier(name)</c> to synchronize at a named barrier.
    /// </para>
    /// </remarks>
    public string[] NamedBarriers { get; set; } = Array.Empty<string>();

    // ============================================================================
    // WSL2 Compatibility Properties (EventDriven Mode)
    // ============================================================================

    /// <summary>
    /// Gets or sets the maximum number of loop iterations for EventDriven mode kernels.
    /// Default is 1000. After this many iterations, the kernel exits and can be relaunched.
    /// </summary>
    /// <value>
    /// The maximum number of dispatch loop iterations before kernel exits.
    /// Only applies when <see cref="Mode"/> is <see cref="RingKernelMode.EventDriven"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// In WSL2, persistent kernels block CUDA API calls from the host, making control block
    /// updates impossible. EventDriven mode with a finite iteration count allows the kernel
    /// to exit periodically, enabling the host to update the control block and relaunch.
    /// </para>
    /// <para>
    /// <strong>Trade-offs:</strong>
    /// <list type="bullet">
    /// <item><description>Lower values: More responsive to control changes, higher launch overhead</description></item>
    /// <item><description>Higher values: Lower launch overhead, less responsive to control changes</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Typical Values:</strong>
    /// <list type="bullet">
    /// <item><description>100-500: High responsiveness (interactive applications)</description></item>
    /// <item><description>1000-5000: Balanced (typical workloads)</description></item>
    /// <item><description>10000+: Maximum throughput (batch processing)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public int EventDrivenMaxIterations { get; set; } = 1000;
}
