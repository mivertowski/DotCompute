// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

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
    SharedMemory = 0,

    /// <summary>
    /// Atomic queue with exponential backoff for high-contention scenarios.
    /// Optimized for many producers/consumers accessing the same queue.
    /// </summary>
    AtomicQueue = 1,

    /// <summary>
    /// Peer-to-peer GPU memory transfers (CUDA only).
    /// Enables direct memory access between GPUs without CPU involvement.
    /// </summary>
    P2P = 2,

    /// <summary>
    /// NVIDIA Collective Communications Library (NCCL) for collective operations.
    /// Optimized for all-reduce, broadcast, and other collective patterns.
    /// </summary>
    NCCL = 3
}
