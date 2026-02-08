// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.RingKernels;

/// <summary>
/// Execution mode for ring kernels.
/// </summary>
public enum RingKernelMode
{
    /// <summary>
    /// Persistent kernel that stays active until explicitly terminated.
    /// </summary>
    /// <remarks>
    /// The kernel runs in an infinite loop, waiting for messages and processing them
    /// as they arrive. This mode minimizes kernel launch overhead but consumes GPU
    /// resources continuously.
    /// </remarks>
    Persistent,

    /// <summary>
    /// Event-driven kernel that is launched on-demand.
    /// </summary>
    /// <remarks>
    /// The kernel is launched when messages are available and terminates after
    /// processing the current batch. This mode conserves GPU resources but incurs
    /// kernel launch overhead for each batch.
    /// </remarks>
    EventDriven
}

/// <summary>
/// Strategy for message passing between kernels.
/// </summary>
public enum MessagePassingStrategy
{
    /// <summary>
    /// Lock-free ring buffers in shared memory.
    /// </summary>
    /// <remarks>
    /// Uses atomic operations to implement lock-free queues in GPU shared memory.
    /// Fastest for intra-block communication but limited by shared memory size.
    /// </remarks>
    SharedMemory,

    /// <summary>
    /// Lock-free ring buffers in global memory with atomics.
    /// </summary>
    /// <remarks>
    /// Uses CUDA atomics for lock-free queues in device global memory.
    /// Slower than shared memory but scales to larger queue sizes and supports
    /// inter-block communication.
    /// </remarks>
    AtomicQueue,

    /// <summary>
    /// Peer-to-peer GPU memory transfers.
    /// </summary>
    /// <remarks>
    /// Direct GPU-to-GPU memory copies via P2P. Requires compatible GPUs and
    /// P2P support enabled. Optimal for multi-GPU setups.
    /// </remarks>
    P2P,

    /// <summary>
    /// NVIDIA Collective Communications Library (NCCL).
    /// </summary>
    /// <remarks>
    /// High-performance multi-GPU communication using NCCL. Supports various
    /// collective operations (broadcast, reduce, etc.) and is optimized for
    /// distributed training workloads.
    /// </remarks>
    NCCL
}

/// <summary>
/// Application domain for ring kernel optimization.
/// </summary>
public enum RingKernelDomain
{
    /// <summary>
    /// General-purpose computation.
    /// </summary>
    /// <remarks>
    /// No domain-specific optimizations applied. Suitable for most workloads
    /// that don't fit specialized domains.
    /// </remarks>
    General,

    /// <summary>
    /// Graph analytics and network processing.
    /// </summary>
    /// <remarks>
    /// Optimized for irregular memory access patterns and load imbalance common
    /// in graph algorithms. May include grid synchronization for bulk-synchronous
    /// parallel (BSP) computation patterns.
    /// </remarks>
    GraphAnalytics,

    /// <summary>
    /// Spatial simulations (physics, fluids, particles).
    /// </summary>
    /// <remarks>
    /// Optimized for regular memory access patterns and local communication.
    /// May use spatial decomposition and halo exchange patterns.
    /// </remarks>
    SpatialSimulation,

    /// <summary>
    /// Actor model and agent-based systems.
    /// </summary>
    /// <remarks>
    /// Optimized for message-heavy workloads with many small computations.
    /// Emphasizes low-latency message passing and dynamic workload distribution.
    /// </remarks>
    ActorModel
}

/// <summary>
/// Defines the target backends for kernel compilation.
/// Multiple backends can be specified using bitwise OR flags.
/// </summary>
[Flags]
public enum KernelBackends
{
    /// <summary>
    /// No backends specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// CPU backend with SIMD vectorization.
    /// </summary>
    CPU = 1 << 0,

    /// <summary>
    /// NVIDIA CUDA backend for NVIDIA GPUs.
    /// </summary>
    CUDA = 1 << 1,

    /// <summary>
    /// OpenCL backend for cross-platform GPU/accelerator support.
    /// </summary>
    OpenCL = 1 << 2,

    /// <summary>
    /// Apple Metal backend for macOS/iOS GPUs.
    /// </summary>
    Metal = 1 << 3,

    /// <summary>
    /// AMD ROCm backend for AMD GPUs (future support).
    /// </summary>
    ROCm = 1 << 4,

    /// <summary>
    /// All available backends.
    /// </summary>
    All = CPU | CUDA | OpenCL | Metal | ROCm
}

/// <summary>
/// Stream priority levels for Ring Kernel execution scheduling.
/// </summary>
/// <remarks>
/// <para>
/// Stream priority affects how the GPU schedules work from different streams.
/// Lower numerical priority values indicate higher execution priority.
/// </para>
/// <para><b>Priority Behavior:</b></para>
/// <list type="bullet">
/// <item><description><b>High</b>: GPU scheduler gives preferential access to SM resources</description></item>
/// <item><description><b>Normal</b>: Default scheduling behavior</description></item>
/// <item><description><b>Low</b>: Deprioritized when competing with higher priority streams</description></item>
/// </list>
/// <para>
/// <b>Note</b>: Stream priority is a scheduling hint and does not guarantee execution order.
/// It affects resource allocation when multiple streams compete for GPU resources.
/// </para>
/// </remarks>
public enum RingKernelStreamPriority
{
    /// <summary>
    /// Low priority for background processing tasks.
    /// </summary>
    /// <remarks>
    /// Use for batch processing, analytics, or non-critical operations that can
    /// tolerate higher latency when higher-priority work is executing.
    /// </remarks>
    Low,

    /// <summary>
    /// Normal priority for typical workloads (default).
    /// </summary>
    /// <remarks>
    /// Balanced priority suitable for most general-purpose computations.
    /// Provides fair scheduling with other normal-priority streams.
    /// </remarks>
    Normal,

    /// <summary>
    /// High priority for latency-sensitive operations.
    /// </summary>
    /// <remarks>
    /// Use for actor request processing, real-time data streams, or operations
    /// where low latency is critical. Higher priority streams get preferential
    /// access to GPU SMs when resources are contended.
    /// </remarks>
    High
}

/// <summary>
/// Specifies the direction of message flow for ring kernel messages.
/// </summary>
/// <remarks>
/// Message direction helps the code generator optimize message routing and serialization:
/// <list type="bullet">
/// <item><description><b>Input</b>: Messages from host to kernel (external requests)</description></item>
/// <item><description><b>Output</b>: Messages from kernel to host (responses)</description></item>
/// <item><description><b>KernelToKernel</b>: Messages between kernels (actor-to-actor)</description></item>
/// <item><description><b>Bidirectional</b>: Messages that can flow in any direction</description></item>
/// </list>
/// </remarks>
public enum MessageDirection
{
    /// <summary>
    /// Message flows from host to kernel (input request).
    /// </summary>
    Input = 0,

    /// <summary>
    /// Message flows from kernel to host (output response).
    /// </summary>
    Output = 1,

    /// <summary>
    /// Message flows between kernels (actor-to-actor communication).
    /// </summary>
    KernelToKernel = 2,

    /// <summary>
    /// Message can flow in any direction (flexible routing).
    /// </summary>
    Bidirectional = 3
}

/// <summary>
/// Specifies how a ring kernel processes messages from its input queue.
/// </summary>
/// <remarks>
/// <para>
/// Ring kernels can operate in different processing modes to optimize for either
/// latency or throughput depending on the workload characteristics.
/// </para>
/// <para>
/// <b>Continuous Mode</b> processes one message at a time with minimal latency overhead,
/// ideal for latency-sensitive applications like real-time actor systems.
/// </para>
/// <para>
/// <b>Batch Mode</b> processes multiple messages per iteration to maximize throughput,
/// suitable for high-volume message processing where latency is less critical.
/// </para>
/// <para>
/// <b>Adaptive Mode</b> dynamically switches between continuous and batch processing
/// based on queue depth, providing a balance between latency and throughput.
/// </para>
/// </remarks>
public enum RingProcessingMode
{
    /// <summary>
    /// Process one message per iteration for minimum latency.
    /// </summary>
    /// <remarks>
    /// In Continuous mode, the ring kernel processes a single message per dispatch
    /// loop iteration. This minimizes end-to-end message latency at the cost of
    /// lower peak throughput. Best for latency-critical applications like actor
    /// request-response patterns or real-time event processing.
    /// </remarks>
    Continuous = 0,

    /// <summary>
    /// Process multiple messages per iteration for maximum throughput.
    /// </summary>
    /// <remarks>
    /// In Batch mode, the ring kernel processes multiple messages (up to a configured
    /// batch size) per dispatch loop iteration. This maximizes message throughput by
    /// amortizing kernel dispatch overhead across multiple messages. Best for high-volume
    /// data processing where individual message latency is less important than overall
    /// throughput.
    /// </remarks>
    Batch = 1,

    /// <summary>
    /// Dynamically switch between Continuous and Batch based on queue depth.
    /// </summary>
    /// <remarks>
    /// In Adaptive mode, the ring kernel monitors its input queue depth and switches
    /// processing strategy dynamically:
    /// <list type="bullet">
    /// <item>Low queue depth (&lt; threshold): Process single messages (Continuous mode)</item>
    /// <item>High queue depth (≥ threshold): Process batches (Batch mode)</item>
    /// </list>
    /// This provides the best of both worlds - low latency when queue is shallow,
    /// high throughput when queue is deep. Recommended for most workloads with
    /// variable message arrival rates.
    /// </remarks>
    Adaptive = 2
}
