// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Builds and manages GPU-resident topic registries for pub/sub messaging.
/// </summary>
/// <remarks>
/// <para>
/// The topic registry builder performs the following operations:
/// 1. Manages topic subscriptions (kernel + topic associations)
/// 2. Calculates optimal hash table capacity for topic lookup
/// 3. Allocates GPU memory for registry structures
/// 4. Populates subscription array sorted by topic ID
/// 5. Returns initialized <see cref="TopicRegistry"/> for GPU usage
/// </para>
/// <para>
/// <b>Memory Management:</b>
/// - Subscriptions array: subscription_count * 12 bytes (TopicSubscription)
/// - Hash table: capacity * 8 bytes (64-bit entries)
/// </para>
/// <para>
/// <b>Thread Safety:</b> Not thread-safe. Builder should be used sequentially during initialization.
/// </para>
/// </remarks>
public sealed class TopicRegistryBuilder : IDisposable
{
    private readonly CudaContext _context;
    private readonly List<SubscriptionRecord> _subscriptions = [];
    private bool _disposed;

    /// <summary>
    /// Initializes a new topic registry builder for the specified CUDA context.
    /// </summary>
    /// <param name="context">CUDA context for GPU memory allocation.</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
    public TopicRegistryBuilder(CudaContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Subscribes a kernel to a topic.
    /// </summary>
    /// <param name="topicName">Topic name (will be hashed to 32-bit ID).</param>
    /// <param name="kernelId">Subscriber kernel ID (16-bit hash).</param>
    /// <param name="queueIndex">Subscriber's queue index.</param>
    /// <param name="flags">Subscription flags (default: none).</param>
    /// <exception cref="ArgumentException">Thrown if topic name is null/empty.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if builder has been disposed.</exception>
    /// <returns>Assigned subscription index.</returns>
    public int Subscribe(string topicName, ushort kernelId, ushort queueIndex, ushort flags = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(topicName))
        {
            throw new ArgumentException("Topic name cannot be null or empty.", nameof(topicName));
        }

        uint topicId = HashTopicName(topicName);

        // Check for duplicate subscriptions (same topic + kernel)
        if (_subscriptions.Any(s => s.TopicId == topicId && s.KernelId == kernelId))
        {
            throw new ArgumentException($"Kernel 0x{kernelId:X4} is already subscribed to topic '{topicName}' (ID: 0x{topicId:X8}).", nameof(kernelId));
        }

        int subscriptionIndex = _subscriptions.Count;

        _subscriptions.Add(new SubscriptionRecord
        {
            TopicName = topicName,
            TopicId = topicId,
            KernelId = kernelId,
            QueueIndex = queueIndex,
            Flags = flags
        });

        return subscriptionIndex;
    }

    /// <summary>
    /// Subscribes multiple kernels to a topic (broadcast subscription).
    /// </summary>
    /// <param name="topicName">Topic name.</param>
    /// <param name="kernelIds">Array of subscriber kernel IDs.</param>
    /// <param name="queueIndices">Array of subscriber queue indices (must match kernelIds length).</param>
    /// <param name="flags">Subscription flags (applied to all subscriptions).</param>
    /// <exception cref="ArgumentException">Thrown if arrays have different lengths.</exception>
    /// <returns>Number of subscriptions added.</returns>
    public int SubscribeMultiple(string topicName, ushort[] kernelIds, ushort[] queueIndices, ushort flags = 0)
    {
        if (kernelIds.Length != queueIndices.Length)
        {
            throw new ArgumentException("kernelIds and queueIndices must have the same length.");
        }

        int count = 0;
        for (int i = 0; i < kernelIds.Length; i++)
        {
            try
            {
                _ = Subscribe(topicName, kernelIds[i], queueIndices[i], flags);
                count++;
            }
            catch (ArgumentException)
            {
                // Skip duplicate subscriptions
            }
        }

        return count;
    }

    /// <summary>
    /// Builds the topic registry and allocates GPU memory.
    /// </summary>
    /// <returns>Initialized <see cref="TopicRegistry"/> with GPU-resident data.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no subscriptions registered or validation fails.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if builder has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// <b>GPU Memory Allocation:</b>
    /// - Subscriptions array: subscription_count * 12 bytes
    /// - Hash table: capacity * 8 bytes
    /// </para>
    /// <para>
    /// <b>Subscription Array Construction:</b>
    /// Subscriptions are sorted by topic ID for efficient scanning.
    /// </para>
    /// </remarks>
    public TopicRegistry Build()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_subscriptions.Count == 0)
        {
            throw new InvalidOperationException("No subscriptions registered. Cannot build empty topic registry.");
        }

        if (_subscriptions.Count > 65535)
        {
            throw new InvalidOperationException($"Too many subscriptions ({_subscriptions.Count}). Maximum is 65535.");
        }

        // Sort subscriptions by topic ID for efficient scanning
        _subscriptions.Sort((a, b) => a.TopicId.CompareTo(b.TopicId));

        // Count unique topics
        int uniqueTopicCount = _subscriptions.Select(s => s.TopicId).Distinct().Count();

        // Calculate optimal hash table capacity
        int capacity = TopicRegistry.CalculateCapacity(uniqueTopicCount);

        // Build subscription array
        TopicSubscription[] subscriptions = BuildSubscriptionArray();

        // Allocate GPU memory for subscriptions array
        long subscriptionsPtr = AllocateAndCopySubscriptions(subscriptions);

        // Build and allocate hash table (topic ID → first subscription index)
        long hashTablePtr = BuildAndAllocateHashTable(capacity);

        // Create topic registry structure
        var registry = new TopicRegistry
        {
            SubscriptionCount = _subscriptions.Count,
            SubscriptionsPtr = subscriptionsPtr,
            TopicHashTablePtr = hashTablePtr,
            HashTableCapacity = capacity
        };

        // Validate before returning
        if (!registry.Validate())
        {
            throw new InvalidOperationException("Built topic registry failed validation.");
        }

        return registry;
    }

    /// <summary>
    /// Builds the subscription array from registered subscriptions.
    /// </summary>
    private TopicSubscription[] BuildSubscriptionArray()
    {
        var subscriptions = new TopicSubscription[_subscriptions.Count];

        for (int i = 0; i < _subscriptions.Count; i++)
        {
            var record = _subscriptions[i];
            subscriptions[i] = TopicSubscription.Create(
                record.TopicId,
                record.KernelId,
                record.QueueIndex,
                record.Flags
            );
        }

        return subscriptions;
    }

    /// <summary>
    /// Allocates GPU memory for subscriptions array and copies data from host.
    /// </summary>
    private static long AllocateAndCopySubscriptions(TopicSubscription[] subscriptions)
    {
        nuint sizeBytes = (nuint)(subscriptions.Length * 12); // 12 bytes per TopicSubscription
        nint devicePtr = CudaRuntime.cudaMalloc(sizeBytes);

        // Pin host memory for efficient copy
        GCHandle handle = GCHandle.Alloc(subscriptions, GCHandleType.Pinned);
        try
        {
            nint hostPtr = handle.AddrOfPinnedObject();
            CudaError result = CudaRuntime.cudaMemcpy(devicePtr, hostPtr, sizeBytes, CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to copy subscriptions to device: {result}");
            }
        }
        finally
        {
            handle.Free();
        }

        return (long)devicePtr;
    }

    /// <summary>
    /// Builds the hash table with topic ID → first subscription index mappings.
    /// </summary>
    /// <remarks>
    /// Hash table entry format: (topic_id &lt;&lt; 32) | first_subscription_index
    /// Since subscriptions are sorted by topic ID, we only need to store the first index.
    /// </remarks>
    private long BuildAndAllocateHashTable(int capacity)
    {
        var hashTable = new ulong[capacity];

        // Build topic ID → first subscription index mapping
        var topicFirstIndex = new Dictionary<uint, int>();

        for (int i = 0; i < _subscriptions.Count; i++)
        {
            uint topicId = _subscriptions[i].TopicId;

            if (!topicFirstIndex.ContainsKey(topicId))
            {
                topicFirstIndex[topicId] = i; // First occurrence
            }
        }

        // Populate hash table with linear probing
        foreach (var kvp in topicFirstIndex)
        {
            uint topicId = kvp.Key;
            int firstIndex = kvp.Value;

            ulong entry = ((ulong)topicId << 32) | (uint)firstIndex;

            // Linear probing to find empty slot
            int hash = (int)(topicId % (uint)capacity);
            int probe = 0;

            while (probe < capacity)
            {
                int index = (hash + probe) % capacity;

                if (hashTable[index] == 0)
                {
                    // Empty slot found
                    hashTable[index] = entry;
                    break;
                }

                probe++;
            }

            if (probe >= capacity)
            {
                throw new InvalidOperationException($"Hash table full. Cannot insert topic 0x{topicId:X8}.");
            }
        }

        // Allocate GPU memory for hash table
        nuint sizeBytes = (nuint)(hashTable.Length * sizeof(ulong));
        nint devicePtr = CudaRuntime.cudaMalloc(sizeBytes);

        GCHandle handle = GCHandle.Alloc(hashTable, GCHandleType.Pinned);
        try
        {
            nint hostPtr = handle.AddrOfPinnedObject();
            CudaError result = CudaRuntime.cudaMemcpy(devicePtr, hostPtr, sizeBytes, CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to copy hash table to device: {result}");
            }
        }
        finally
        {
            handle.Free();
        }

        return (long)devicePtr;
    }

    /// <summary>
    /// Computes a 32-bit hash of a topic name using FNV-1a algorithm.
    /// </summary>
    /// <param name="topicName">Topic name to hash.</param>
    /// <returns>32-bit hash value.</returns>
    /// <remarks>
    /// Uses the same FNV-1a algorithm as CUDA device code for consistency.
    /// Example: "physics.particles" → 0x7A3B9C12
    /// </remarks>
    private static uint HashTopicName(string topicName)
    {
        const uint FnvOffsetBasis = 2166136261u;
        const uint FnvPrime = 16777619u;

        uint hash = FnvOffsetBasis;

        foreach (char c in topicName)
        {
            hash ^= c;
            hash *= FnvPrime;
        }

        return hash;
    }

    /// <summary>
    /// Disposes the topic registry builder (does not free GPU memory allocated during Build).
    /// </summary>
    /// <remarks>
    /// GPU memory allocated by <see cref="Build"/> must be freed separately using CudaRuntime.cudaFree.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _subscriptions.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Internal subscription record.
    /// </summary>
    private sealed class SubscriptionRecord
    {
        public required string TopicName { get; init; }
        public required uint TopicId { get; init; }
        public required ushort KernelId { get; init; }
        public required ushort QueueIndex { get; init; }
        public required ushort Flags { get; init; }
    }
}
