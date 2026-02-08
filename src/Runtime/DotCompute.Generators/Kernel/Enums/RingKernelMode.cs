// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Kernel.Enums;

/// <summary>
/// Specifies the execution mode for a ring kernel.
/// </summary>
/// <remarks>
/// Ring kernels can operate in persistent mode (continuously running) or
/// event-driven mode (activated by messages/events).
/// </remarks>
public enum RingKernelMode
{
    /// <summary>
    /// Kernel runs continuously in a loop until explicitly terminated.
    /// This mode keeps the kernel resident on the GPU, minimizing launch overhead.
    /// </summary>
    /// <remarks>
    /// Persistent kernels are ideal for streaming workloads, real-time simulations,
    /// and scenarios where CPU-GPU synchronization overhead must be minimized.
    /// The kernel continuously polls for work items and processes them immediately.
    /// </remarks>
    Persistent = 0,

    /// <summary>
    /// Kernel activates only when events or messages are available.
    /// The kernel remains idle when no work is present, conserving resources.
    /// </summary>
    /// <remarks>
    /// Event-driven kernels are more power-efficient for sporadic workloads.
    /// They wake up when messages arrive in input queues and return to idle
    /// state when all work is completed.
    /// </remarks>
    EventDriven = 1
}
