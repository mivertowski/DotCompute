// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Execution;

/// <summary>
/// Flags that define stream behavior and properties for kernel execution.
/// These flags can be combined using bitwise OR operations.
/// </summary>
[Flags]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "StreamFlags is an appropriate name for a flags enumeration")]
public enum StreamFlags
{
    /// <summary>
    /// Default stream behavior with no special flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Stream does not synchronize with the default stream.
    /// Enables concurrent execution with default stream operations.
    /// </summary>
    NonBlocking = 1 << 0,

    /// <summary>
    /// Stream has high priority for scheduling.
    /// Operations in this stream will be scheduled before normal priority streams.
    /// </summary>
    HighPriority = 1 << 1,

    /// <summary>
    /// Stream has low priority for scheduling.
    /// Operations in this stream will be scheduled after normal priority streams.
    /// </summary>
    LowPriority = 1 << 2,

    /// <summary>
    /// Stream should wait for all preceding operations to complete.
    /// Ensures strict ordering with previous operations.
    /// </summary>
    Synchronized = 1 << 3,

    /// <summary>
    /// Stream operations are persistent and should not be deallocated eagerly.
    /// </summary>
    Persistent = 1 << 4,

    /// <summary>
    /// Stream should automatically synchronize on disposal.
    /// Ensures all operations complete before stream is destroyed.
    /// </summary>
    AutoSync = 1 << 5,

    /// <summary>
    /// Stream supports asynchronous memory operations.
    /// Enables overlapping of memory transfers and kernel execution.
    /// </summary>
    AsyncMemory = 1 << 6,

    /// <summary>
    /// Stream is optimized for low latency operations.
    /// Reduces overhead at the cost of throughput.
    /// </summary>
    LowLatency = 1 << 7,

    /// <summary>
    /// Stream is optimized for high throughput operations.
    /// May increase latency to maximize throughput.
    /// </summary>
    HighThroughput = 1 << 8,

    /// <summary>
    /// Stream supports GPU direct operations (peer-to-peer).
    /// Enables direct memory access between GPUs without host involvement.
    /// </summary>
    GpuDirect = 1 << 9
}
