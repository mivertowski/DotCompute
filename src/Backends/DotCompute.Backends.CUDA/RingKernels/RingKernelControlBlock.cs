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
/// Memory layout (64 bytes, cache-line aligned):
/// - Flags: active (offset 0), terminate (offset 4), terminated (offset 8), errors (offset 12)
/// - Counters: messagesProcessed (offset 16)
/// - Timestamp: lastActivityTicks (offset 24)
/// - Queue pointers: 4 longs (offsets 32-63)
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
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
    /// Device pointer to input message queue head.
    /// </summary>
    public long InputQueueHeadPtr;

    /// <summary>
    /// Device pointer to input message queue tail.
    /// </summary>
    public long InputQueueTailPtr;

    /// <summary>
    /// Device pointer to output message queue head.
    /// </summary>
    public long OutputQueueHeadPtr;

    /// <summary>
    /// Device pointer to output message queue tail.
    /// </summary>
    public long OutputQueueTailPtr;

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
            OutputQueueHeadPtr = 0,
            OutputQueueTailPtr = 0
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
               OutputQueueHeadPtr == other.OutputQueueHeadPtr &&
               OutputQueueTailPtr == other.OutputQueueTailPtr;
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
        hash.Add(OutputQueueHeadPtr);
        hash.Add(OutputQueueTailPtr);
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
