// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#ifndef DOTCOMPUTE_TOPIC_PUBSUB_CU
#define DOTCOMPUTE_TOPIC_PUBSUB_CU

#include <cstdint>

// Forward declarations
struct topic_subscription;
struct topic_registry;
struct kernel_routing_table;

// Import routing functions
extern __device__ bool route_message_to_kernel(
    const kernel_routing_table* routing_table,
    uint32_t target_kernel_id,
    const unsigned char* message_buffer,
    int32_t message_size);

/// <summary>
/// GPU-resident topic subscription entry for pub/sub messaging.
/// </summary>
/// <remarks>
/// Memory Layout (12 bytes, 4-byte aligned):
/// - topic_id: 4 bytes (hash of topic name)
/// - kernel_id: 4 bytes (subscriber kernel identifier)
/// - queue_index: 2 bytes (subscriber's queue index)
/// - flags: 2 bytes (wildcard, priority, etc.)
/// </remarks>
struct topic_subscription
{
    /// <summary>
    /// Topic ID (hash of topic name using FNV-1a algorithm).
    /// </summary>
    uint32_t topic_id;

    /// <summary>
    /// Subscriber kernel ID (16-bit hash of kernel name).
    /// </summary>
    uint32_t kernel_id;

    /// <summary>
    /// Queue index for this subscriber (0-65535).
    /// </summary>
    uint16_t queue_index;

    /// <summary>
    /// Subscription flags (wildcard, priority, etc.).
    /// </summary>
    /// <remarks>
    /// Bit 0: Wildcard subscription
    /// Bit 1: High priority delivery
    /// Bits 2-15: Reserved
    /// </remarks>
    uint16_t flags;
};

/// <summary>
/// GPU-resident topic registry for pub/sub messaging.
/// </summary>
/// <remarks>
/// Memory Layout (24 bytes, 8-byte aligned):
/// - subscription_count: 4 bytes
/// - padding: 4 bytes (alignment)
/// - subscriptions_ptr: 8 bytes
/// - topic_hash_table_ptr: 8 bytes
/// - hash_table_capacity: 4 bytes
/// - padding: 4 bytes (alignment)
/// </remarks>
struct topic_registry
{
    /// <summary>
    /// Number of active subscriptions.
    /// </summary>
    int32_t subscription_count;

    /// <summary>
    /// Device pointer to array of topic_subscription entries.
    /// </summary>
    int64_t subscriptions_ptr;

    /// <summary>
    /// Device pointer to topic hash table (topic ID → first subscription index).
    /// </summary>
    int64_t topic_hash_table_ptr;

    /// <summary>
    /// Hash table capacity (must be power of 2).
    /// </summary>
    int32_t hash_table_capacity;
};

/// <summary>
/// Subscription flag: Wildcard subscription (matches "physics.*").
/// </summary>
#define TOPIC_FLAG_WILDCARD 0x0001

/// <summary>
/// Subscription flag: High-priority delivery (delivered before normal subscriptions).
/// </summary>
#define TOPIC_FLAG_HIGH_PRIORITY 0x0002

/// <summary>
/// Computes a 32-bit hash of a topic name string using FNV-1a algorithm.
/// </summary>
/// <param name="topic_name">Null-terminated topic name string</param>
/// <returns>32-bit hash value</returns>
/// <remarks>
/// Uses FNV-1a hash algorithm for good distribution.
/// Example: "physics.particles" → 0x7A3B9C12
/// </remarks>
__device__ uint32_t hash_topic_name(const char* topic_name)
{
    // FNV-1a hash constants
    const uint32_t FNV_OFFSET_BASIS = 2166136261u;
    const uint32_t FNV_PRIME = 16777619u;

    uint32_t hash = FNV_OFFSET_BASIS;

    // Hash each character
    while (*topic_name != '\0')
    {
        hash ^= static_cast<uint32_t>(*topic_name);
        hash *= FNV_PRIME;
        topic_name++;
    }

    return hash;
}

/// <summary>
/// Publishes a message to all subscribers of a topic.
/// </summary>
/// <param name="registry">Topic registry with subscriptions</param>
/// <param name="routing_table">Kernel routing table for message delivery</param>
/// <param name="topic_id">Topic ID (hash of topic name)</param>
/// <param name="message_buffer">Serialized message bytes</param>
/// <param name="message_size">Message size in bytes</param>
/// <returns>Number of subscribers that successfully received the message</returns>
/// <remarks>
/// <para>
/// <b>Broadcast Algorithm:</b>
/// 1. Scan subscription array for matching topic ID
/// 2. For each subscriber, route message using kernel routing table
/// 3. Count successful deliveries
/// </para>
/// <para>
/// <b>Performance:</b>
/// - Subscription scan: O(n) where n = total subscriptions
/// - Message routing: O(1) per subscriber (hash table lookup)
/// - Target latency: &lt;10μs for 100 subscribers
/// </para>
/// <para>
/// <b>Priority Handling:</b>
/// High-priority subscriptions (TOPIC_FLAG_HIGH_PRIORITY) are processed first.
/// </para>
/// </remarks>
__device__ int publish_to_topic(
    const topic_registry* registry,
    const kernel_routing_table* routing_table,
    uint32_t topic_id,
    const unsigned char* message_buffer,
    int32_t message_size)
{
    // Validate registry
    if (registry->subscription_count <= 0 || registry->subscriptions_ptr == 0)
    {
        return 0; // No subscriptions
    }

    int messages_sent = 0;
    topic_subscription* subscriptions = reinterpret_cast<topic_subscription*>(registry->subscriptions_ptr);

    // Two-pass delivery: high-priority first, then normal priority
    // Pass 1: High-priority subscriptions
    for (int i = 0; i < registry->subscription_count; i++)
    {
        if (subscriptions[i].topic_id == topic_id &&
            (subscriptions[i].flags & TOPIC_FLAG_HIGH_PRIORITY) != 0)
        {
            uint32_t kernel_id = subscriptions[i].kernel_id;

            // Route message to subscriber
            if (route_message_to_kernel(routing_table, kernel_id, message_buffer, message_size))
            {
                messages_sent++;
            }
        }
    }

    // Pass 2: Normal-priority subscriptions
    for (int i = 0; i < registry->subscription_count; i++)
    {
        if (subscriptions[i].topic_id == topic_id &&
            (subscriptions[i].flags & TOPIC_FLAG_HIGH_PRIORITY) == 0)
        {
            uint32_t kernel_id = subscriptions[i].kernel_id;

            // Route message to subscriber
            if (route_message_to_kernel(routing_table, kernel_id, message_buffer, message_size))
            {
                messages_sent++;
            }
        }
    }

    return messages_sent;
}

/// <summary>
/// Publishes a message to a topic by name (convenience wrapper).
/// </summary>
/// <param name="registry">Topic registry with subscriptions</param>
/// <param name="routing_table">Kernel routing table for message delivery</param>
/// <param name="topic_name">Topic name (null-terminated string)</param>
/// <param name="message_buffer">Serialized message bytes</param>
/// <param name="message_size">Message size in bytes</param>
/// <returns>Number of subscribers that successfully received the message</returns>
__device__ int publish_to_topic_by_name(
    const topic_registry* registry,
    const kernel_routing_table* routing_table,
    const char* topic_name,
    const unsigned char* message_buffer,
    int32_t message_size)
{
    uint32_t topic_id = hash_topic_name(topic_name);
    return publish_to_topic(registry, routing_table, topic_id, message_buffer, message_size);
}

/// <summary>
/// Checks if a kernel is subscribed to a topic.
/// </summary>
/// <param name="registry">Topic registry with subscriptions</param>
/// <param name="topic_id">Topic ID to check</param>
/// <param name="kernel_id">Kernel ID to check</param>
/// <returns>True if kernel is subscribed to topic, false otherwise</returns>
__device__ bool is_subscribed(
    const topic_registry* registry,
    uint32_t topic_id,
    uint32_t kernel_id)
{
    if (registry->subscription_count <= 0 || registry->subscriptions_ptr == 0)
    {
        return false;
    }

    topic_subscription* subscriptions = reinterpret_cast<topic_subscription*>(registry->subscriptions_ptr);

    for (int i = 0; i < registry->subscription_count; i++)
    {
        if (subscriptions[i].topic_id == topic_id &&
            subscriptions[i].kernel_id == kernel_id)
        {
            return true;
        }
    }

    return false;
}

/// <summary>
/// Counts the number of subscribers for a topic.
/// </summary>
/// <param name="registry">Topic registry with subscriptions</param>
/// <param name="topic_id">Topic ID to count</param>
/// <returns>Number of subscribers for the topic</returns>
__device__ int count_subscribers(
    const topic_registry* registry,
    uint32_t topic_id)
{
    if (registry->subscription_count <= 0 || registry->subscriptions_ptr == 0)
    {
        return 0;
    }

    int count = 0;
    topic_subscription* subscriptions = reinterpret_cast<topic_subscription*>(registry->subscriptions_ptr);

    for (int i = 0; i < registry->subscription_count; i++)
    {
        if (subscriptions[i].topic_id == topic_id)
        {
            count++;
        }
    }

    return count;
}

/// <summary>
/// Lists all subscribers for a topic.
/// </summary>
/// <param name="registry">Topic registry with subscriptions</param>
/// <param name="topic_id">Topic ID to query</param>
/// <param name="subscriber_ids">Output array for subscriber kernel IDs</param>
/// <param name="max_subscribers">Maximum number of subscribers to return</param>
/// <returns>Number of subscribers written to output array</returns>
__device__ int list_subscribers(
    const topic_registry* registry,
    uint32_t topic_id,
    uint32_t* subscriber_ids,
    int max_subscribers)
{
    if (registry->subscription_count <= 0 || registry->subscriptions_ptr == 0)
    {
        return 0;
    }

    int count = 0;
    topic_subscription* subscriptions = reinterpret_cast<topic_subscription*>(registry->subscriptions_ptr);

    for (int i = 0; i < registry->subscription_count && count < max_subscribers; i++)
    {
        if (subscriptions[i].topic_id == topic_id)
        {
            subscriber_ids[count] = subscriptions[i].kernel_id;
            count++;
        }
    }

    return count;
}

#endif // DOTCOMPUTE_TOPIC_PUBSUB_CU
