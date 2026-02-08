// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

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
    Persistent = 0,

    /// <summary>
    /// Kernel activates only when events or messages are available.
    /// The kernel remains idle when no work is present, conserving resources.
    /// </summary>
    EventDriven = 1
}
