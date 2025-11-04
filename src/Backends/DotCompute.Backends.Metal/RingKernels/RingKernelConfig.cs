// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.RingKernels;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// Configuration for a Metal ring kernel compilation.
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
    /// Gets or sets the ring buffer capacity.
    /// </summary>
    public int Capacity { get; init; } = 1024;

    /// <summary>
    /// Gets or sets the input queue size.
    /// </summary>
    public int InputQueueSize { get; init; } = 256;

    /// <summary>
    /// Gets or sets the output queue size.
    /// </summary>
    public int OutputQueueSize { get; init; } = 256;

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
#pragma warning disable CA1819 // Properties should not return arrays - Required for configuration serialization
    public int[]? GridDimensions { get; init; }
#pragma warning restore CA1819

    /// <summary>
    /// Gets or sets the block dimensions.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays - Required for configuration serialization
    public int[]? BlockDimensions { get; init; }
#pragma warning restore CA1819

    /// <summary>
    /// Gets or sets whether to use threadgroup memory (Metal shared memory).
    /// </summary>
    public bool UseThreadgroupMemory { get; init; }

    /// <summary>
    /// Gets or sets the threadgroup memory size in bytes.
    /// </summary>
    public int ThreadgroupMemorySize { get; init; }
}
