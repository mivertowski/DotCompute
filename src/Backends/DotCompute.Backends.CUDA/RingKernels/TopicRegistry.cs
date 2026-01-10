// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// GPU-resident topic subscription entry for pub/sub messaging.
/// </summary>
/// <remarks>
/// <para>
/// Topic subscriptions enable decoupled communication between kernels.
/// Kernels subscribe to topics at initialization, and messages published to
/// those topics are automatically broadcast to all subscribers.
/// </para>
/// <para>
/// <b>Memory Layout (12 bytes, 4-byte aligned):</b>
/// - TopicId: 4 bytes (hash of topic name)
/// - KernelId: 4 bytes (subscriber kernel identifier)
/// - QueueIndex: 2 bytes (subscriber's queue index)
/// - Flags: 2 bytes (wildcard, priority, etc.)
/// </para>
/// <para>
/// <b>Subscription Flags:</b>
/// - Bit 0: Wildcard subscription (matches "physics.*")
/// - Bit 1: High priority (delivered first)
/// - Bits 2-15: Reserved for future use
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 12)]
public struct TopicSubscription : IEquatable<TopicSubscription>
{
    /// <summary>
    /// Topic ID (hash of topic name using FNV-1a algorithm).
    /// </summary>
    /// <remarks>
    /// Topic names are hashed to 32-bit IDs for efficient comparison.
    /// Example: "physics.particles" → 0x7A3B9C12
    /// </remarks>
    public uint TopicId;

    /// <summary>
    /// Subscriber kernel ID (16-bit hash of kernel name).
    /// </summary>
    /// <remarks>
    /// Identifies the kernel that subscribed to this topic.
    /// Stored as uint32 for alignment (upper 16 bits unused).
    /// </remarks>
    public uint KernelId;

    /// <summary>
    /// Queue index for this subscriber (0-65535).
    /// </summary>
    /// <remarks>
    /// Index into the routing table's output queues array.
    /// Used to route messages to the subscriber's input queue.
    /// </remarks>
    public ushort QueueIndex;

    /// <summary>
    /// Subscription flags (wildcard, priority, etc.).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Bit 0: Wildcard subscription</item>
    /// <item>Bit 1: High priority delivery</item>
    /// <item>Bits 2-15: Reserved</item>
    /// </list>
    /// </remarks>
    public ushort Flags;

    /// <summary>
    /// Flag: Subscription matches wildcard patterns (e.g., "physics.*").
    /// </summary>
    public const ushort FlagWildcard = 0x0001;

    /// <summary>
    /// Flag: High-priority subscription (delivered before normal subscriptions).
    /// </summary>
    public const ushort FlagHighPriority = 0x0002;

    /// <summary>
    /// Creates a topic subscription entry.
    /// </summary>
    /// <param name="topicId">Topic ID (hash of topic name).</param>
    /// <param name="kernelId">Subscriber kernel ID.</param>
    /// <param name="queueIndex">Subscriber's queue index.</param>
    /// <param name="flags">Subscription flags (default: none).</param>
    /// <returns>Initialized topic subscription.</returns>
    public static TopicSubscription Create(uint topicId, uint kernelId, ushort queueIndex, ushort flags = 0)
    {
        return new TopicSubscription
        {
            TopicId = topicId,
            KernelId = kernelId,
            QueueIndex = queueIndex,
            Flags = flags
        };
    }

    /// <inheritdoc/>
    public readonly bool Equals(TopicSubscription other)
    {
        return TopicId == other.TopicId &&
               KernelId == other.KernelId &&
               QueueIndex == other.QueueIndex &&
               Flags == other.Flags;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is TopicSubscription other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(TopicId, KernelId, QueueIndex, Flags);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TopicSubscription left, TopicSubscription right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TopicSubscription left, TopicSubscription right)
    {
        return !(left == right);
    }
}

/// <summary>
/// GPU-resident topic registry for pub/sub messaging.
/// </summary>
/// <remarks>
/// <para>
/// The topic registry manages subscriptions and enables efficient message broadcasting.
/// When a message is published to a topic, it is automatically delivered to all
/// subscribers of that topic.
/// </para>
/// <para>
/// <b>Memory Layout (24 bytes, 8-byte aligned):</b>
/// - SubscriptionCount: 4 bytes
/// - Padding: 4 bytes (alignment)
/// - SubscriptionsPtr: 8 bytes
/// - TopicHashTablePtr: 8 bytes
/// - HashTableCapacity: 4 bytes
/// - Padding: 4 bytes (alignment)
/// </para>
/// <para>
/// <b>Performance Characteristics:</b>
/// - Topic lookup: O(1) with hash table
/// - Subscription scan: O(n) where n = subscription count
/// - Broadcast latency: &lt;10μs for 100 subscribers
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 24)]
public struct TopicRegistry : IEquatable<TopicRegistry>
{
    /// <summary>
    /// Number of active subscriptions.
    /// </summary>
    /// <remarks>
    /// Valid range: 0-65535
    /// Total subscriptions across all topics.
    /// </remarks>
    public int SubscriptionCount;

    /// <summary>
    /// Device pointer to array of TopicSubscription entries.
    /// </summary>
    /// <remarks>
    /// Array size: SubscriptionCount elements (12 bytes each).
    /// Subscriptions are sorted by topic ID for efficient scanning.
    /// </remarks>
    public long SubscriptionsPtr;

    /// <summary>
    /// Device pointer to topic hash table (topic ID → first subscription index).
    /// </summary>
    /// <remarks>
    /// Array size: HashTableCapacity entries (8 bytes each).
    /// Entry format: (topic_id &lt;&lt; 32) | first_subscription_index
    /// Empty entries: 0x0000000000000000
    /// Collision resolution: Linear probing
    /// </remarks>
    public long TopicHashTablePtr;

    /// <summary>
    /// Hash table capacity (must be power of 2).
    /// </summary>
    /// <remarks>
    /// Recommended: 2x unique topic count for &lt;50% load factor.
    /// Valid range: 16-65536 (must be power of 2).
    /// </remarks>
    public int HashTableCapacity;

    /// <summary>
    /// Creates an uninitialized topic registry (all pointers null, counts zero).
    /// </summary>
    /// <returns>Empty topic registry suitable for GPU allocation.</returns>
    public static TopicRegistry CreateEmpty()
    {
        return new TopicRegistry
        {
            SubscriptionCount = 0,
            SubscriptionsPtr = 0,
            TopicHashTablePtr = 0,
            HashTableCapacity = 0
        };
    }

    /// <summary>
    /// Validates the topic registry structure for correctness.
    /// </summary>
    /// <returns>True if valid, false if any invariant is violated.</returns>
    /// <remarks>
    /// Checks:
    /// - SubscriptionCount in valid range [0, 65535]
    /// - HashTableCapacity is power of 2
    /// - All device pointers are non-zero (if SubscriptionCount > 0)
    /// </remarks>
    public readonly bool Validate()
    {
        // Check subscription count
        if (SubscriptionCount < 0 || SubscriptionCount > 65535)
        {
            return false;
        }

        // Empty registry is valid
        if (SubscriptionCount == 0)
        {
            return true;
        }

        // Check hash table capacity is power of 2
        if (HashTableCapacity <= 0 || (HashTableCapacity & (HashTableCapacity - 1)) != 0)
        {
            return false;
        }

        // Check all pointers are non-zero
        if (SubscriptionsPtr == 0 || TopicHashTablePtr == 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Calculates the recommended hash table capacity for a given number of unique topics.
    /// </summary>
    /// <param name="uniqueTopicCount">Number of unique topics with subscriptions.</param>
    /// <returns>Next power of 2 that is at least 2x the unique topic count.</returns>
    /// <remarks>
    /// Targets ~50% load factor for optimal performance.
    /// Minimum capacity: 16 (even for 1-8 topics).
    /// Maximum capacity: 65536.
    /// </remarks>
    public static int CalculateCapacity(int uniqueTopicCount)
    {
        if (uniqueTopicCount <= 0)
        {
            return 16; // Minimum capacity
        }

        // Target 2x unique topics for ~50% load factor
        var targetCapacity = uniqueTopicCount * 2;

        // Round up to next power of 2
        var capacity = 16; // Start with minimum
        while (capacity < targetCapacity && capacity < 65536)
        {
            capacity *= 2;
        }

        return Math.Min(capacity, 65536); // Cap at 65536
    }

    /// <inheritdoc/>
    public readonly bool Equals(TopicRegistry other)
    {
        return SubscriptionCount == other.SubscriptionCount &&
               SubscriptionsPtr == other.SubscriptionsPtr &&
               TopicHashTablePtr == other.TopicHashTablePtr &&
               HashTableCapacity == other.HashTableCapacity;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is TopicRegistry other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(SubscriptionCount, SubscriptionsPtr, TopicHashTablePtr, HashTableCapacity);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TopicRegistry left, TopicRegistry right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TopicRegistry left, TopicRegistry right)
    {
        return !(left == right);
    }
}
