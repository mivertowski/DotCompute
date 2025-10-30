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
