// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

/// <summary>
/// Specifies the message passing strategy for inter-kernel communication.
/// </summary>
/// <remarks>
/// Different strategies offer trade-offs between performance, complexity,
/// and hardware requirements.
/// </remarks>
public enum MessagePassingStrategy
{
    /// <summary>
    /// Lock-free ring buffers in shared GPU memory using atomic operations.
    /// This is the default strategy with good performance and wide compatibility.
    /// </summary>
    /// <remarks>
    /// Uses atomic compare-and-swap operations for concurrent queue access.
    /// Supports all GPU backends (CUDA, OpenCL, Metal).
    /// Best for moderate message rates (1-10M messages/sec).
    /// </remarks>
    SharedMemory = 0,

    /// <summary>
    /// Atomic queue with exponential backoff for high-contention scenarios.
    /// Optimized for many producers/consumers accessing the same queue.
    /// </summary>
    /// <remarks>
    /// Implements exponential backoff to reduce contention under heavy load.
    /// Slightly lower peak throughput but more stable under contention.
    /// Recommended when 10+ kernels share a single message queue.
    /// </remarks>
    AtomicQueue = 1,

    /// <summary>
    /// Peer-to-peer GPU memory transfers (CUDA only).
    /// Enables direct memory access between GPUs without CPU involvement.
    /// </summary>
    /// <remarks>
    /// Requires CUDA Unified Virtual Addressing (UVA) and peer access enabled.
    /// Highest performance for multi-GPU systems with NVLink.
    /// Only available on CUDA backend with compatible hardware.
    /// </remarks>
    P2P = 2,

    /// <summary>
    /// NVIDIA Collective Communications Library (NCCL) for collective operations.
    /// Optimized for all-reduce, broadcast, and other collective patterns.
    /// </summary>
    /// <remarks>
    /// Designed for distributed training and multi-GPU synchronization.
    /// Automatically selects optimal communication topology.
    /// CUDA-only, requires NCCL library installation.
    /// Best for 4+ GPU systems with regular synchronization needs.
    /// </remarks>
    NCCL = 3
}
