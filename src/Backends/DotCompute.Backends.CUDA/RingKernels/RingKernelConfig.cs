// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.RingKernels;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Configuration for a ring kernel compilation.
/// </summary>
/// <remarks>
/// This is a simplified data structure that contains the essential
/// configuration without requiring a reference to the source generators.
/// The source generator will extract these values from the RingKernelAttribute
/// and pass them to the compiler.
/// </remarks>
public sealed class RingKernelConfig
{
    /// <summary>
    /// Gets or sets the unique kernel identifier.
    /// </summary>
    public required string KernelId { get; init; }

    /// <summary>
    /// Gets or sets the ring buffer capacity (number of messages the queue can hold).
    /// </summary>
    /// <remarks>
    /// This determines how many messages can be buffered in the queue at once.
    /// Default: 1024 messages.
    /// </remarks>
    public int QueueCapacity { get; init; } = 1024;

    /// <summary>
    /// Gets or sets the maximum input message size in bytes.
    /// </summary>
    /// <remarks>
    /// This determines the maximum size of a single input message payload.
    /// Used for buffer allocation on GPU device.
    /// Default: 65792 bytes (64KB + 256-byte header for MemoryPack serialization).
    /// </remarks>
    public int MaxInputMessageSizeBytes { get; init; } = 65792;

    /// <summary>
    /// Gets or sets the maximum output message size in bytes.
    /// </summary>
    /// <remarks>
    /// This determines the maximum size of a single output message payload.
    /// Used for buffer allocation on GPU device.
    /// Default: 65792 bytes (64KB + 256-byte header for MemoryPack serialization).
    /// </remarks>
    public int MaxOutputMessageSizeBytes { get; init; } = 65792;

    /// <summary>
    /// Gets or sets the execution mode.
    /// </summary>
    public RingKernelMode Mode { get; init; } = RingKernelMode.Persistent;

    /// <summary>
    /// Gets or sets the message passing strategy.
    /// </summary>
    public MessagePassingStrategy MessagingStrategy { get; init; } = MessagePassingStrategy.SharedMemory;

    /// <summary>
    /// Gets or sets the application domain.
    /// </summary>
    public RingKernelDomain Domain { get; init; } = RingKernelDomain.General;

    /// <summary>
    /// Gets or sets the grid dimensions.
    /// </summary>
    public int[]? GridDimensions { get; init; }

    /// <summary>
    /// Gets or sets the block dimensions.
    /// </summary>
    public int[]? BlockDimensions { get; init; }

    /// <summary>
    /// Gets or sets whether to use shared memory.
    /// </summary>
    public bool UseSharedMemory { get; init; }

    /// <summary>
    /// Gets or sets the shared memory size in bytes.
    /// </summary>
    public int SharedMemorySize { get; init; }

    /// <summary>
    /// Gets or sets the list of message type names for automatic CUDA serialization includes.
    /// </summary>
    /// <remarks>
    /// Each message type name will be converted to an #include directive for its serialization file.
    /// For example, "VectorAdd" becomes #include "VectorAddSerialization.cu".
    /// If null or empty, no message includes will be generated.
    /// </remarks>
    public IReadOnlyList<string>? MessageTypes { get; init; }
}
