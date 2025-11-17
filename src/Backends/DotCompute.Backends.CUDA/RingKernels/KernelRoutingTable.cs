// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// GPU-resident routing table for kernel-to-kernel message passing.
/// </summary>
/// <remarks>
/// <para>
/// This structure enables efficient message routing between ring kernels with
/// minimal latency. Uses a hash table with linear probing for collision resolution.
/// </para>
/// <para>
/// <b>Memory Layout (32 bytes, cache-line sub-aligned):</b>
/// - KernelCount: 4 bytes
/// - KernelControlBlocksPtr: 8 bytes
/// - OutputQueuesPtr: 8 bytes
/// - RoutingHashTablePtr: 8 bytes
/// - HashTableCapacity: 4 bytes
/// </para>
/// <para>
/// <b>Hash Table Entry Format (32-bit):</b>
/// - Bits 31-16: Kernel ID (16-bit hash of kernel name)
/// - Bits 15-0: Queue index (0-65535)
/// </para>
/// <para>
/// <b>Performance Characteristics:</b>
/// - Lookup: O(1) average, O(n) worst case (linear probing)
/// - Target latency: &lt; 1μs per message route
/// - Throughput: 1M+ messages/sec sustained
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
public struct KernelRoutingTable : IEquatable<KernelRoutingTable>
{
    /// <summary>
    /// Number of active kernels registered in the routing table.
    /// </summary>
    /// <remarks>
    /// Valid range: 1-65535
    /// Used for bounds checking and iteration.
    /// </remarks>
    public int KernelCount;

    /// <summary>
    /// Device pointer to array of <see cref="RingKernelControlBlock"/> structures.
    /// </summary>
    /// <remarks>
    /// Array size: <see cref="KernelCount"/> elements.
    /// Used for health monitoring and control flow.
    /// </remarks>
    public long KernelControlBlocksPtr;

    /// <summary>
    /// Device pointer to array of output queue pointers (one per kernel).
    /// </summary>
    /// <remarks>
    /// Array size: <see cref="KernelCount"/> elements.
    /// Each element is a device pointer to an output queue buffer.
    /// Format: unsigned char* queues[KernelCount]
    /// </remarks>
    public long OutputQueuesPtr;

    /// <summary>
    /// Device pointer to routing hash table (kernel ID → queue index mapping).
    /// </summary>
    /// <remarks>
    /// Array size: <see cref="HashTableCapacity"/> entries (32-bit each).
    /// Entry format: (kernel_id &lt;&lt; 16) | queue_index
    /// Empty entries: 0x00000000
    /// Collision resolution: Linear probing
    /// </remarks>
    public long RoutingHashTablePtr;

    /// <summary>
    /// Hash table capacity (must be power of 2 for fast modulo).
    /// </summary>
    /// <remarks>
    /// Recommended: 2x <see cref="KernelCount"/> for &lt;50% load factor.
    /// Valid range: 16-65536 (must be power of 2).
    /// Used for hash computation: hash % capacity.
    /// </remarks>
    public int HashTableCapacity;

    /// <summary>
    /// Creates an uninitialized routing table (all pointers null, counts zero).
    /// </summary>
    /// <returns>Empty routing table suitable for GPU allocation.</returns>
    public static KernelRoutingTable CreateEmpty()
    {
        return new KernelRoutingTable
        {
            KernelCount = 0,
            KernelControlBlocksPtr = 0,
            OutputQueuesPtr = 0,
            RoutingHashTablePtr = 0,
            HashTableCapacity = 0
        };
    }

    /// <summary>
    /// Validates the routing table structure for correctness.
    /// </summary>
    /// <returns>True if valid, false if any invariant is violated.</returns>
    /// <remarks>
    /// Checks:
    /// - KernelCount in valid range [1, 65535]
    /// - HashTableCapacity is power of 2
    /// - HashTableCapacity >= KernelCount
    /// - All device pointers are non-zero (if KernelCount > 0)
    /// </remarks>
    public readonly bool Validate()
    {
        // Check kernel count
        if (KernelCount < 0 || KernelCount > 65535)
        {
            return false;
        }

        // Empty table is valid
        if (KernelCount == 0)
        {
            return true;
        }

        // Check hash table capacity is power of 2
        if (HashTableCapacity <= 0 || (HashTableCapacity & (HashTableCapacity - 1)) != 0)
        {
            return false;
        }

        // Check capacity is sufficient
        if (HashTableCapacity < KernelCount)
        {
            return false;
        }

        // Check all pointers are non-zero
        if (KernelControlBlocksPtr == 0 || OutputQueuesPtr == 0 || RoutingHashTablePtr == 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates the recommended hash table capacity for a given kernel count.
    /// </summary>
    /// <param name="kernelCount">Number of kernels to route between.</param>
    /// <returns>Next power of 2 that is at least 2x the kernel count.</returns>
    /// <remarks>
    /// Targets ~50% load factor for optimal performance.
    /// Minimum capacity: 16 (even for 1-8 kernels).
    /// Maximum capacity: 65536.
    /// </remarks>
    public static int CalculateCapacity(int kernelCount)
    {
        if (kernelCount <= 0)
        {
            return 16; // Minimum capacity
        }

        // Target 2x kernel count for ~50% load factor
        int targetCapacity = kernelCount * 2;

        // Round up to next power of 2
        int capacity = 16; // Start with minimum
        while (capacity < targetCapacity && capacity < 65536)
        {
            capacity *= 2;
        }

        return Math.Min(capacity, 65536); // Cap at 65536
    }

    /// <inheritdoc/>
    public readonly bool Equals(KernelRoutingTable other)
    {
        return KernelCount == other.KernelCount &&
               KernelControlBlocksPtr == other.KernelControlBlocksPtr &&
               OutputQueuesPtr == other.OutputQueuesPtr &&
               RoutingHashTablePtr == other.RoutingHashTablePtr &&
               HashTableCapacity == other.HashTableCapacity;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is KernelRoutingTable other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(
            KernelCount,
            KernelControlBlocksPtr,
            OutputQueuesPtr,
            RoutingHashTablePtr,
            HashTableCapacity);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(KernelRoutingTable left, KernelRoutingTable right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(KernelRoutingTable left, KernelRoutingTable right)
    {
        return !(left == right);
    }
}
