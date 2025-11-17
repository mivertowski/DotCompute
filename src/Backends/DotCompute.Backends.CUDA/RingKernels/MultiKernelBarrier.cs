// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Multi-kernel barrier for synchronizing persistent Ring Kernels across GPU.
/// </summary>
/// <remarks>
/// <para>
/// Enables coordination of multiple Ring Kernels through generation-based barrier protocol.
/// Kernels wait at barrier until all participants arrive, then proceed to next generation.
/// </para>
/// <para>
/// <b>Memory Layout (16 bytes, 4-byte aligned):</b>
/// - ParticipantCount: 4 bytes (number of kernels)
/// - ArrivedCount: 4 bytes (atomic counter)
/// - Generation: 4 bytes (barrier generation number)
/// - Flags: 4 bytes (barrier state flags)
/// </para>
/// <para>
/// <b>Protocol Phases:</b>
/// 1. Arrival: Each kernel atomically increments arrived counter
/// 2. Wait: Kernels spin until generation changes (all arrived)
/// 3. Departure: Last kernel resets counter and increments generation
/// </para>
/// <para>
/// <b>Barrier Scopes:</b>
/// - Thread-Block: `__syncthreads()` for all threads in block (~10ns)
/// - Grid-Wide: Cooperative groups for all blocks in kernel (~1-10μs)
/// - Multi-Kernel: This struct for all participating kernels (~10-100μs)
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
public struct MultiKernelBarrier : IEquatable<MultiKernelBarrier>
{
    /// <summary>
    /// Number of kernels that must arrive at barrier.
    /// </summary>
    /// <remarks>
    /// Valid range: 1-65535 kernels.
    /// Must be set before barrier use and remain constant during barrier lifetime.
    /// </remarks>
    public int ParticipantCount;

    /// <summary>
    /// Atomic counter for arrived kernels (0 to ParticipantCount).
    /// </summary>
    /// <remarks>
    /// Atomically incremented by each arriving kernel.
    /// Reset to 0 by last arriving kernel.
    /// Modified using `atomicAdd()` in CUDA device code.
    /// </remarks>
    public int ArrivedCount;

    /// <summary>
    /// Barrier generation number (incremented after each barrier completion).
    /// </summary>
    /// <remarks>
    /// Kernels wait for generation change to detect barrier completion.
    /// Prevents ABA problem in wait loops.
    /// Wraps around at int.MaxValue (2.1 billion barriers).
    /// Modified using `atomicAdd()` in CUDA device code.
    /// </remarks>
    public int Generation;

    /// <summary>
    /// Barrier state flags.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Bit 0: Active flag (barrier is in use)</item>
    /// <item>Bit 1: Timeout flag (barrier timed out)</item>
    /// <item>Bit 2: Failed flag (barrier failed)</item>
    /// <item>Bits 3-31: Reserved for future use</item>
    /// </list>
    /// Modified using `atomicOr()` in CUDA device code.
    /// </remarks>
    public int Flags;

    /// <summary>
    /// Flag: Barrier is active and in use.
    /// </summary>
    public const int FlagActive = 0x0001;

    /// <summary>
    /// Flag: Barrier operation timed out.
    /// </summary>
    public const int FlagTimeout = 0x0002;

    /// <summary>
    /// Flag: Barrier operation failed (participant crashed or disconnected).
    /// </summary>
    public const int FlagFailed = 0x0004;

    /// <summary>
    /// Creates an uninitialized multi-kernel barrier (all fields zero).
    /// </summary>
    /// <returns>Empty barrier suitable for GPU allocation.</returns>
    public static MultiKernelBarrier CreateEmpty()
    {
        return new MultiKernelBarrier
        {
            ParticipantCount = 0,
            ArrivedCount = 0,
            Generation = 0,
            Flags = 0
        };
    }

    /// <summary>
    /// Creates a multi-kernel barrier configured for specified participant count.
    /// </summary>
    /// <param name="participantCount">Number of kernels that must arrive at barrier (1-65535).</param>
    /// <returns>Initialized barrier ready for GPU use.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if participantCount is out of valid range.</exception>
    public static MultiKernelBarrier Create(int participantCount)
    {
        if (participantCount <= 0 || participantCount > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(participantCount), participantCount, "Participant count must be between 1 and 65535.");
        }

        return new MultiKernelBarrier
        {
            ParticipantCount = participantCount,
            ArrivedCount = 0,
            Generation = 0,
            Flags = FlagActive
        };
    }

    /// <summary>
    /// Validates the barrier structure for correctness.
    /// </summary>
    /// <returns>True if valid, false if any invariant is violated.</returns>
    /// <remarks>
    /// Checks:
    /// - ParticipantCount in valid range [1, 65535]
    /// - ArrivedCount in valid range [0, ParticipantCount]
    /// - Generation non-negative
    /// </remarks>
    public readonly bool Validate()
    {
        // Check participant count
        if (ParticipantCount <= 0 || ParticipantCount > 65535)
        {
            return false;
        }

        // Check arrived count
        if (ArrivedCount < 0 || ArrivedCount > ParticipantCount)
        {
            return false;
        }

        // Check generation is non-negative
        if (Generation < 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if barrier has timed out.
    /// </summary>
    /// <returns>True if timeout flag is set, false otherwise.</returns>
    public readonly bool IsTimedOut()
    {
        return (Flags & FlagTimeout) != 0;
    }

    /// <summary>
    /// Checks if barrier has failed.
    /// </summary>
    /// <returns>True if failed flag is set, false otherwise.</returns>
    public readonly bool IsFailed()
    {
        return (Flags & FlagFailed) != 0;
    }

    /// <summary>
    /// Checks if barrier is active.
    /// </summary>
    /// <returns>True if active flag is set, false otherwise.</returns>
    public readonly bool IsActive()
    {
        return (Flags & FlagActive) != 0;
    }

    /// <inheritdoc/>
    public readonly bool Equals(MultiKernelBarrier other)
    {
        return ParticipantCount == other.ParticipantCount &&
               ArrivedCount == other.ArrivedCount &&
               Generation == other.Generation &&
               Flags == other.Flags;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is MultiKernelBarrier other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(ParticipantCount, ArrivedCount, Generation, Flags);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(MultiKernelBarrier left, MultiKernelBarrier right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(MultiKernelBarrier left, MultiKernelBarrier right)
    {
        return !(left == right);
    }
}
