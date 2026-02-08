// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators;

/// <summary>
/// Specifies the direction of message flow for ring kernel messages.
/// </summary>
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
