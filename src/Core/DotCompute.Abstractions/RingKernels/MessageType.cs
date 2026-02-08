// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.RingKernels;

/// <summary>
/// Specifies the type of message being passed between ring kernels.
/// </summary>
/// <remarks>
/// Message types enable kernels to distinguish between data messages,
/// control messages, and lifecycle management messages.
/// </remarks>
public enum MessageType
{
    /// <summary>
    /// Data message containing application payload.
    /// This is the default type for regular inter-kernel communication.
    /// </summary>
    Data = 0,

    /// <summary>
    /// Control message for coordination and synchronization.
    /// Used for non-data communication like barriers, acknowledgments, etc.
    /// </summary>
    Control = 1,

    /// <summary>
    /// Termination message signaling kernel shutdown.
    /// When received, the kernel should complete current work and terminate.
    /// </summary>
    Terminate = 2,

    /// <summary>
    /// Activation message to wake up event-driven kernels.
    /// Causes an idle event-driven kernel to become active.
    /// </summary>
    Activate = 3,

    /// <summary>
    /// Deactivation message to pause event-driven kernels.
    /// Causes an active event-driven kernel to become idle.
    /// </summary>
    Deactivate = 4
}
