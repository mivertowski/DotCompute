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
        _context = context ?? throw new ArgumentNullException(
            nameof(context),
            "TopicRegistryBuilder requires a CudaContext — the topic registry is allocated in device memory and needs a live context. Obtain it from CudaAccelerator.Context.");
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
            throw new ArgumentException(
                $"Subscribe: topic name must be a non-empty string (received '{topicName ?? "<null>"}'). Topic names are hashed to 32-bit IDs — a distinct name per topic is required.",
                nameof(topicName));
        }

        var topicId = HashTopicName(topicName);

        // Check for duplicate subscriptions (same topic + kernel)
        if (_subscriptions.Any(s => s.TopicId == topicId && s.KernelId == kernelId))
        {
            throw new ArgumentException(
                $"Kernel 0x{kernelId:X4} is already subscribed to topic '{topicName}' (topicId=0x{topicId:X8}). Each (topicId, kernelId) pair can appear at most once in the registry — remove the existing subscription first or subscribe a different kernel.",
                nameof(kernelId));
        }

        var subscriptionIndex = _subscriptions.Count;

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
            throw new ArgumentException(
                $"SubscribeMultiple: kernelIds.Length ({kernelIds.Length}) must equal queueIndices.Length ({queueIndices.Length}). Each subscribing kernel needs a matching queue index — the arrays index the same subscription records.",
                nameof(queueIndices));
        }

        var count = 0;
        for (var i = 0; i < kernelIds.Length; i++)
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
            throw new InvalidOperationException(
                "TopicRegistryBuilder has no subscriptions — cannot build an empty topic registry. Call Subscribe(topicName, kernelId, queueIndex) for at least one topic/kernel pair before Build().");
        }

        if (_subscriptions.Count > 65535)
        {
            throw new InvalidOperationException(
                $"TopicRegistryBuilder has {_subscriptions.Count} subscriptions but the encoded subscription index is a uint16 (max 65535). Reduce the number of (topic, kernel) subscriptions, or shard topics across multiple registries.");
        }

        // Sort subscriptions by topic ID for efficient scanning
        _subscriptions.Sort((a, b) => a.TopicId.CompareTo(b.TopicId));

        // Count unique topics
        var uniqueTopicCount = _subscriptions.Select(s => s.TopicId).Distinct().Count();

        // Calculate optimal hash table capacity
        var capacity = TopicRegistry.CalculateCapacity(uniqueTopicCount);

        // Build subscription array
        TopicSubscription[] subscriptions = BuildSubscriptionArray();

        // Allocate GPU memory for subscriptions array
        var subscriptionsPtr = AllocateAndCopySubscriptions(subscriptions);

        // Build and allocate hash table (topic ID → first subscription index)
        var hashTablePtr = BuildAndAllocateHashTable(capacity);

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
            throw new InvalidOperationException(
                $"Built topic registry failed post-validation — the device-side hash table may have a duplicate slot or inconsistent Subscription count ({_subscriptions.Count}). This indicates a bug in TopicRegistryBuilder or memory corruption during upload; inspect device memory via cuda-gdb.");
        }

        return registry;
    }

    /// <summary>
    /// Builds the subscription array from registered subscriptions.
    /// </summary>
    private TopicSubscription[] BuildSubscriptionArray()
    {
        var subscriptions = new TopicSubscription[_subscriptions.Count];

        for (var i = 0; i < _subscriptions.Count; i++)
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
        var sizeBytes = (nuint)(subscriptions.Length * 12); // 12 bytes per TopicSubscription
        var devicePtr = CudaRuntime.cudaMalloc(sizeBytes);

        // Pin host memory for efficient copy
        GCHandle handle = GCHandle.Alloc(subscriptions, GCHandleType.Pinned);
        try
        {
            var hostPtr = handle.AddrOfPinnedObject();
            CudaError result = CudaRuntime.cudaMemcpy(devicePtr, hostPtr, sizeBytes, CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMemcpy H2D for {subscriptions.Length} subscription record(s) failed: {result} ({(int)result}). Device memory was allocated but upload failed — likely GPU fault or context loss. Inspect nvidia-smi and check for kernel crashes.");
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

        for (var i = 0; i < _subscriptions.Count; i++)
        {
            var topicId = _subscriptions[i].TopicId;

            if (!topicFirstIndex.ContainsKey(topicId))
            {
                topicFirstIndex[topicId] = i; // First occurrence
            }
        }

        // Populate hash table with linear probing
        foreach (var kvp in topicFirstIndex)
        {
            var topicId = kvp.Key;
            var firstIndex = kvp.Value;

            var entry = ((ulong)topicId << 32) | (uint)firstIndex;

            // Linear probing to find empty slot
            var hash = (int)(topicId % (uint)capacity);
            var probe = 0;

            while (probe < capacity)
            {
                var index = (hash + probe) % capacity;

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
                throw new InvalidOperationException(
                    $"Topic hash table is full while inserting topic 0x{topicId:X8} (capacity={capacity}). The linear-probe loop exhausted all slots — reduce the number of unique topics, or increase the hash-table size constant in the registry layout.");
            }
        }

        // Allocate GPU memory for hash table
        var sizeBytes = (nuint)(hashTable.Length * sizeof(ulong));
        var devicePtr = CudaRuntime.cudaMalloc(sizeBytes);

        GCHandle handle = GCHandle.Alloc(hashTable, GCHandleType.Pinned);
        try
        {
            var hostPtr = handle.AddrOfPinnedObject();
            CudaError result = CudaRuntime.cudaMemcpy(devicePtr, hostPtr, sizeBytes, CudaMemcpyKind.HostToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"cudaMemcpy H2D for topic hash table failed: {result} ({(int)result}). Device memory was allocated but upload failed — likely GPU fault or context loss. Inspect nvidia-smi and check for kernel crashes.");
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

        var hash = FnvOffsetBasis;

        foreach (var c in topicName)
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
