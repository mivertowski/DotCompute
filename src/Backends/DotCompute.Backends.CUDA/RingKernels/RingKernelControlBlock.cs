// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// GPU-resident control block for managing persistent ring kernel state.
/// </summary>
/// <remarks>
/// This structure is allocated in device memory and shared between host and kernel.
/// All fields use 32-bit atomics for lock-free coordination.
///
/// Memory layout (128 bytes, dual cache-line aligned):
/// - Flags: active (offset 0), terminate (offset 4), terminated (offset 8), errors (offset 12)
/// - Counters: messagesProcessed (offset 16)
/// - Timestamp: lastActivityTicks (offset 24)
/// - Input Queue: head ptr (offset 32), tail ptr (offset 40), buffer ptr (offset 48), capacity (offset 56), messageSize (offset 60)
/// - Output Queue: head ptr (offset 64), tail ptr (offset 72), buffer ptr (offset 80), capacity (offset 88), messageSize (offset 92)
/// - Reserved: padding to 128 bytes (offsets 96-127)
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 128)]
public struct RingKernelControlBlock : IEquatable<RingKernelControlBlock>
{
    /// <summary>
    /// Atomic flag: 1 = kernel is active and processing, 0 = inactive/paused.
    /// </summary>
    public int IsActive;

    /// <summary>
    /// Atomic flag: 1 = kernel should terminate gracefully, 0 = continue running.
    /// </summary>
    public int ShouldTerminate;

    /// <summary>
    /// Atomic flag: 1 = kernel has finished termination, 0 = still running.
    /// Value meanings: 0 = running, 1 = terminated permanently, 2 = terminated but relaunchable (EventDriven mode)
    /// </summary>
    public int HasTerminated;

    /// <summary>
    /// Atomic counter: total errors encountered during processing.
    /// </summary>
    public int ErrorsEncountered;

    /// <summary>
    /// Atomic counter: total messages processed by this kernel.
    /// </summary>
    public long MessagesProcessed;

    /// <summary>
    /// Atomic timestamp: last time kernel processed a message (ticks).
    /// </summary>
    public long LastActivityTicks;

    /// <summary>
    /// Device pointer to input message queue head atomic counter.
    /// </summary>
    public long InputQueueHeadPtr;

    /// <summary>
    /// Device pointer to input message queue tail atomic counter.
    /// </summary>
    public long InputQueueTailPtr;

    /// <summary>
    /// Device pointer to input message queue data buffer.
    /// </summary>
    public long InputQueueBufferPtr;

    /// <summary>
    /// Capacity of input message queue (number of slots, power of 2).
    /// </summary>
    public int InputQueueCapacity;

    /// <summary>
    /// Size of each message in the input queue (bytes).
    /// </summary>
    public int InputQueueMessageSize;

    /// <summary>
    /// Device pointer to output message queue head atomic counter.
    /// </summary>
    public long OutputQueueHeadPtr;

    /// <summary>
    /// Device pointer to output message queue tail atomic counter.
    /// </summary>
    public long OutputQueueTailPtr;

    /// <summary>
    /// Device pointer to output message queue data buffer.
    /// </summary>
    public long OutputQueueBufferPtr;

    /// <summary>
    /// Capacity of output message queue (number of slots, power of 2).
    /// </summary>
    public int OutputQueueCapacity;

    /// <summary>
    /// Size of each message in the output queue (bytes).
    /// </summary>
    public int OutputQueueMessageSize;

    /// <summary>
    /// Reserved padding for future use and alignment.
    /// </summary>
    private readonly long _reserved1;

    /// <summary>
    /// Reserved padding for future use and alignment.
    /// </summary>
    private readonly long _reserved2;

    /// <summary>
    /// Reserved padding for future use and alignment.
    /// </summary>
    private readonly long _reserved3;

    /// <summary>
    /// Reserved padding for future use and alignment.
    /// </summary>
    private readonly long _reserved4;

    /// <summary>
    /// Creates a new control block with default (inactive) state.
    /// </summary>
    public static RingKernelControlBlock CreateInactive()
    {
        return new RingKernelControlBlock
        {
            IsActive = 0,
            ShouldTerminate = 0,
            HasTerminated = 0,
            ErrorsEncountered = 0,
            MessagesProcessed = 0,
            LastActivityTicks = DateTime.UtcNow.Ticks,
            InputQueueHeadPtr = 0,
            InputQueueTailPtr = 0,
            InputQueueBufferPtr = 0,
            InputQueueCapacity = 0,
            InputQueueMessageSize = 0,
            OutputQueueHeadPtr = 0,
            OutputQueueTailPtr = 0,
            OutputQueueBufferPtr = 0,
            OutputQueueCapacity = 0,
            OutputQueueMessageSize = 0
        };
    }

    /// <summary>
    /// Creates a new control block with active state.
    /// </summary>
    /// <remarks>
    /// This is used for WSL2 where cross-CPU/GPU memory visibility is unreliable.
    /// By starting the kernel with is_active=1 already set, we avoid the need for
    /// mid-execution activation signaling which doesn't work with system-scope atomics
    /// in virtualized GPU environments.
    /// </remarks>
    public static RingKernelControlBlock CreateActive()
    {
        return new RingKernelControlBlock
        {
            IsActive = 1,
            ShouldTerminate = 0,
            HasTerminated = 0,
            ErrorsEncountered = 0,
            MessagesProcessed = 0,
            LastActivityTicks = DateTime.UtcNow.Ticks,
            InputQueueHeadPtr = 0,
            InputQueueTailPtr = 0,
            InputQueueBufferPtr = 0,
            InputQueueCapacity = 0,
            InputQueueMessageSize = 0,
            OutputQueueHeadPtr = 0,
            OutputQueueTailPtr = 0,
            OutputQueueBufferPtr = 0,
            OutputQueueCapacity = 0,
            OutputQueueMessageSize = 0
        };
    }

    /// <inheritdoc/>
    public readonly bool Equals(RingKernelControlBlock other)
    {
        return IsActive == other.IsActive &&
               ShouldTerminate == other.ShouldTerminate &&
               HasTerminated == other.HasTerminated &&
               MessagesProcessed == other.MessagesProcessed &&
               ErrorsEncountered == other.ErrorsEncountered &&
               LastActivityTicks == other.LastActivityTicks &&
               InputQueueHeadPtr == other.InputQueueHeadPtr &&
               InputQueueTailPtr == other.InputQueueTailPtr &&
               InputQueueBufferPtr == other.InputQueueBufferPtr &&
               InputQueueCapacity == other.InputQueueCapacity &&
               InputQueueMessageSize == other.InputQueueMessageSize &&
               OutputQueueHeadPtr == other.OutputQueueHeadPtr &&
               OutputQueueTailPtr == other.OutputQueueTailPtr &&
               OutputQueueBufferPtr == other.OutputQueueBufferPtr &&
               OutputQueueCapacity == other.OutputQueueCapacity &&
               OutputQueueMessageSize == other.OutputQueueMessageSize;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is RingKernelControlBlock other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsActive);
        hash.Add(ShouldTerminate);
        hash.Add(HasTerminated);
        hash.Add(MessagesProcessed);
        hash.Add(ErrorsEncountered);
        hash.Add(LastActivityTicks);
        hash.Add(InputQueueHeadPtr);
        hash.Add(InputQueueTailPtr);
        hash.Add(InputQueueBufferPtr);
        hash.Add(InputQueueCapacity);
        hash.Add(InputQueueMessageSize);
        hash.Add(OutputQueueHeadPtr);
        hash.Add(OutputQueueTailPtr);
        hash.Add(OutputQueueBufferPtr);
        hash.Add(OutputQueueCapacity);
        hash.Add(OutputQueueMessageSize);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(RingKernelControlBlock left, RingKernelControlBlock right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(RingKernelControlBlock left, RingKernelControlBlock right)
    {
        return !(left == right);
    }
}
