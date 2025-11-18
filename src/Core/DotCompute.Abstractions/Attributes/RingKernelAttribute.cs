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
}
