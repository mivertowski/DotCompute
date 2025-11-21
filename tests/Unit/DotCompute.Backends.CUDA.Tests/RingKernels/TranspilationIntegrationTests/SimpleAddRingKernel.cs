// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Attributes;
using DotCompute.Abstractions.RingKernels;
using MemoryPack;

namespace DotCompute.Backends.CUDA.Tests.RingKernels.TranspilationIntegrationTests;

/// <summary>
/// Simple ring kernel for testing C# to CUDA transpilation end-to-end.
/// </summary>
public static class SimpleAddRingKernels
{
    /// <summary>
    /// Simple addition ring kernel that adds two integers.
    /// This kernel has a corresponding handler: SimpleAddHandler.cs
    /// </summary>
    [RingKernel(
        KernelId = "SimpleAdd",
        Domain = RingKernelDomain.General,
        MessagingStrategy = MessagePassingStrategy.SharedMemory,
        Capacity = 256,
        MaxInputMessageSizeBytes = 64,
        MaxOutputMessageSizeBytes = 64)]
    public static void SimpleAddKernel()
    {
        // Kernel body - placeholder for code generation
        // The actual message processing is done by SimpleAddHandler
    }
}

/// <summary>
/// Request message for SimpleAdd kernel.
/// Binary format: MessageId(16) + Priority(1) + CorrelationId(17) + A(4) + B(4) = 42 bytes
/// </summary>
[MemoryPackable]
public partial class SimpleAddRequest
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Message priority (0-255).
    /// </summary>
    [MemoryPackOrder(1)]
    public byte Priority { get; set; } = 1;

    /// <summary>
    /// Correlation ID for request-response matching.
    /// </summary>
    [MemoryPackOrder(2)]
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// First operand.
    /// </summary>
    [MemoryPackOrder(3)]
    public int A { get; set; }

    /// <summary>
    /// Second operand.
    /// </summary>
    [MemoryPackOrder(4)]
    public int B { get; set; }
}

/// <summary>
/// Response message for SimpleAdd kernel.
/// Binary format: MessageId(16) + Priority(1) + CorrelationId(17) + Result(4) = 38 bytes
/// </summary>
[MemoryPackable]
public partial class SimpleAddResponse
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    [MemoryPackOrder(0)]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Message priority (0-255).
    /// </summary>
    [MemoryPackOrder(1)]
    public byte Priority { get; set; } = 1;

    /// <summary>
    /// Correlation ID matching the request.
    /// </summary>
    [MemoryPackOrder(2)]
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// Addition result (A + B).
    /// </summary>
    [MemoryPackOrder(3)]
    public int Result { get; set; }
}
