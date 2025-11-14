// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Metal Shading Language Kernel for Lock-Free Message Queue Operations
// Implements atomic circular buffer with serialized message support
// MAX_MESSAGE_SIZE = 1312 bytes (matches CUDA/OpenCL implementation)

#include <metal_stdlib>
#include <metal_atomic>
using namespace metal;

// Queue operation result codes
enum QueueOperationResult : int {
    QUEUE_SUCCESS = 0,
    QUEUE_FULL = 1,
    QUEUE_EMPTY = 2,
    QUEUE_INVALID_SIZE = 3,
    QUEUE_DUPLICATE = 4
};

// Constants
constant int MAX_MESSAGE_SIZE = 1312;

/**
 * Enqueue a serialized message into the circular buffer
 *
 * @param buffer Device buffer for serialized messages (capacity * MAX_MESSAGE_SIZE bytes)
 * @param head Atomic head index (next write position)
 * @param tail Atomic tail index (next read position)
 * @param capacity_mask Capacity - 1 (for fast modulo via bitwise AND)
 * @param message Serialized message data
 * @param message_size Size of serialized message in bytes
 * @param result Output result code
 */
kernel void queue_enqueue(
    device uchar* buffer [[buffer(0)]],
    device atomic_ulong* head [[buffer(1)]],
    device atomic_ulong* tail [[buffer(2)]],
    constant int& capacity_mask [[buffer(3)]],
    device const uchar* message [[buffer(4)]],
    constant int& message_size [[buffer(5)]],
    device int* result [[buffer(6)]])
{
    // Validate message size
    if (message_size <= 0 || message_size > MAX_MESSAGE_SIZE) {
        *result = QUEUE_INVALID_SIZE;
        return;
    }

    // Atomically increment head index to reserve a slot
    ulong current_head = atomic_fetch_add_explicit(head, 1UL, memory_order_relaxed);

    // Calculate actual capacity from mask
    int capacity = capacity_mask + 1;

    // Read current tail to check if queue is full
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);

    // Check if queue is full (head has wrapped around and caught up to tail)
    if (current_head >= current_tail + (ulong)capacity) {
        // Queue is full, rollback the head increment
        atomic_fetch_sub_explicit(head, 1UL, memory_order_relaxed);
        *result = QUEUE_FULL;
        return;
    }

    // Calculate slot index with wrap-around using capacity mask
    int slot_index = (int)(current_head & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;

    // Copy message to device buffer at calculated slot
    for (int i = 0; i < message_size; i++) {
        buffer[buffer_offset + i] = message[i];
    }

    // Memory fence to ensure message is visible before other threads can read
    threadgroup_barrier(mem_flags::mem_device);

    *result = QUEUE_SUCCESS;
}

/**
 * Dequeue a serialized message from the circular buffer
 *
 * @param buffer Device buffer for serialized messages
 * @param head Atomic head index (next write position)
 * @param tail Atomic tail index (next read position)
 * @param capacity_mask Capacity - 1 (for fast modulo)
 * @param message Output buffer for dequeued message
 * @param max_message_size Maximum size of output buffer
 * @param actual_size Output parameter for actual message size
 * @param result Output result code
 */
kernel void queue_dequeue(
    device uchar* buffer [[buffer(0)]],
    device atomic_ulong* head [[buffer(1)]],
    device atomic_ulong* tail [[buffer(2)]],
    constant int& capacity_mask [[buffer(3)]],
    device uchar* message [[buffer(4)]],
    constant int& max_message_size [[buffer(5)]],
    device int* actual_size [[buffer(6)]],
    device int* result [[buffer(7)]])
{
    // Validate output buffer size
    if (max_message_size < MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        *result = QUEUE_INVALID_SIZE;
        return;
    }

    // Read current head and tail indices
    ulong current_head = atomic_load_explicit(head, memory_order_acquire);
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);

    // Check if queue is empty
    if (current_tail >= current_head) {
        *actual_size = 0;
        *result = QUEUE_EMPTY;
        return;
    }

    // Atomically increment tail to reserve the message for dequeue
    ulong my_tail = atomic_fetch_add_explicit(tail, 1UL, memory_order_relaxed);

    // Re-check if we actually got a valid slot (race condition check)
    current_head = atomic_load_explicit(head, memory_order_acquire);
    if (my_tail >= current_head) {
        *actual_size = 0;
        *result = QUEUE_EMPTY;
        return;
    }

    // Calculate slot index with wrap-around
    int slot_index = (int)(my_tail & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;

    // Memory fence to ensure message is fully written before we read
    threadgroup_barrier(mem_flags::mem_device);

    // Read message size from first 4 bytes (int32)
    int msg_size = *reinterpret_cast<device int*>(buffer + buffer_offset);

    // Validate message size
    if (msg_size <= 0 || msg_size > MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        *result = QUEUE_INVALID_SIZE;
        return;
    }

    // Copy message from device buffer to output
    for (int i = 0; i < msg_size; i++) {
        message[i] = buffer[buffer_offset + i];
    }

    *actual_size = msg_size;
    *result = QUEUE_SUCCESS;
}

/**
 * Peek at the next message without removing it from the queue
 *
 * @param buffer Device buffer for serialized messages
 * @param head Atomic head index (next write position)
 * @param tail Atomic tail index (next read position)
 * @param capacity_mask Capacity - 1 (for fast modulo)
 * @param message Output buffer for peeked message
 * @param max_message_size Maximum size of output buffer
 * @param actual_size Output parameter for actual message size
 * @param result Output result code
 */
kernel void queue_peek(
    device uchar* buffer [[buffer(0)]],
    device atomic_ulong* head [[buffer(1)]],
    device atomic_ulong* tail [[buffer(2)]],
    constant int& capacity_mask [[buffer(3)]],
    device uchar* message [[buffer(4)]],
    constant int& max_message_size [[buffer(5)]],
    device int* actual_size [[buffer(6)]],
    device int* result [[buffer(7)]])
{
    // Validate output buffer size
    if (max_message_size < MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        *result = QUEUE_INVALID_SIZE;
        return;
    }

    // Read current head and tail indices (no atomic increment)
    ulong current_head = atomic_load_explicit(head, memory_order_acquire);
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);

    // Check if queue is empty
    if (current_tail >= current_head) {
        *actual_size = 0;
        *result = QUEUE_EMPTY;
        return;
    }

    // Calculate slot index for the tail (next to be dequeued)
    int slot_index = (int)(current_tail & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;

    // Memory fence to ensure message is fully written
    threadgroup_barrier(mem_flags::mem_device);

    // Read message size from first 4 bytes
    int msg_size = *reinterpret_cast<device int*>(buffer + buffer_offset);

    // Validate message size
    if (msg_size <= 0 || msg_size > MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        *result = QUEUE_INVALID_SIZE;
        return;
    }

    // Copy message from device buffer to output
    for (int i = 0; i < msg_size; i++) {
        message[i] = buffer[buffer_offset + i];
    }

    *actual_size = msg_size;
    *result = QUEUE_SUCCESS;
}

/**
 * Get the current number of messages in the queue
 *
 * @param head Atomic head index (next write position)
 * @param tail Atomic tail index (next read position)
 * @param count Output parameter for message count
 */
kernel void queue_count(
    device atomic_ulong* head [[buffer(0)]],
    device atomic_ulong* tail [[buffer(1)]],
    device int* count [[buffer(2)]])
{
    // Read current head and tail indices
    ulong current_head = atomic_load_explicit(head, memory_order_acquire);
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);

    // Calculate count (head - tail)
    if (current_head >= current_tail) {
        *count = (int)(current_head - current_tail);
    } else {
        *count = 0; // Should never happen in valid queue
    }
}

/**
 * Check if the queue is empty
 *
 * @param head Atomic head index
 * @param tail Atomic tail index
 * @param is_empty Output parameter (1 if empty, 0 otherwise)
 */
kernel void queue_is_empty(
    device atomic_ulong* head [[buffer(0)]],
    device atomic_ulong* tail [[buffer(1)]],
    device int* is_empty [[buffer(2)]])
{
    ulong current_head = atomic_load_explicit(head, memory_order_acquire);
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);

    *is_empty = (current_tail >= current_head) ? 1 : 0;
}

/**
 * Check if the queue is full
 *
 * @param head Atomic head index
 * @param tail Atomic tail index
 * @param capacity_mask Capacity - 1
 * @param is_full Output parameter (1 if full, 0 otherwise)
 */
kernel void queue_is_full(
    device atomic_ulong* head [[buffer(0)]],
    device atomic_ulong* tail [[buffer(1)]],
    constant int& capacity_mask [[buffer(2)]],
    device int* is_full [[buffer(3)]])
{
    int capacity = capacity_mask + 1;

    ulong current_head = atomic_load_explicit(head, memory_order_acquire);
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);

    *is_full = (current_head >= current_tail + (ulong)capacity) ? 1 : 0;
}

/**
 * Clear the queue by resetting head and tail to zero
 * WARNING: Not thread-safe, should only be called when no other operations are in progress
 *
 * @param head Atomic head index
 * @param tail Atomic tail index
 */
kernel void queue_clear(
    device atomic_ulong* head [[buffer(0)]],
    device atomic_ulong* tail [[buffer(1)]])
{
    atomic_store_explicit(head, 0UL, memory_order_release);
    atomic_store_explicit(tail, 0UL, memory_order_release);

    // Ensure visibility
    threadgroup_barrier(mem_flags::mem_device);
}

/**
 * Batch enqueue operation for multiple messages
 * Each work item enqueues one message
 *
 * @param buffer Device buffer for serialized messages
 * @param head Atomic head index
 * @param tail Atomic tail index
 * @param capacity_mask Capacity - 1
 * @param messages Array of serialized messages (each MAX_MESSAGE_SIZE bytes)
 * @param message_sizes Array of message sizes
 * @param results Array of operation results (one per work item)
 * @param gid Global thread ID
 */
kernel void queue_batch_enqueue(
    device uchar* buffer [[buffer(0)]],
    device atomic_ulong* head [[buffer(1)]],
    device atomic_ulong* tail [[buffer(2)]],
    constant int& capacity_mask [[buffer(3)]],
    device const uchar* messages [[buffer(4)]],
    device const int* message_sizes [[buffer(5)]],
    device int* results [[buffer(6)]],
    uint gid [[thread_position_in_grid]])
{
    int message_size = message_sizes[gid];

    // Validate message size
    if (message_size <= 0 || message_size > MAX_MESSAGE_SIZE) {
        results[gid] = QUEUE_INVALID_SIZE;
        return;
    }

    // Atomically increment head
    ulong current_head = atomic_fetch_add_explicit(head, 1UL, memory_order_relaxed);

    // Calculate capacity
    int capacity = capacity_mask + 1;

    // Check if full
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);
    if (current_head >= current_tail + (ulong)capacity) {
        atomic_fetch_sub_explicit(head, 1UL, memory_order_relaxed);
        results[gid] = QUEUE_FULL;
        return;
    }

    // Calculate slot
    int slot_index = (int)(current_head & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;
    ulong message_offset = (ulong)gid * MAX_MESSAGE_SIZE;

    // Copy message
    for (int i = 0; i < message_size; i++) {
        buffer[buffer_offset + i] = messages[message_offset + i];
    }

    // Fence
    threadgroup_barrier(mem_flags::mem_device);

    results[gid] = QUEUE_SUCCESS;
}

/**
 * Batch dequeue operation for multiple messages
 * Each work item dequeues one message
 *
 * @param buffer Device buffer for serialized messages
 * @param head Atomic head index
 * @param tail Atomic tail index
 * @param capacity_mask Capacity - 1
 * @param messages Output array for dequeued messages (each MAX_MESSAGE_SIZE bytes)
 * @param message_sizes Output array for message sizes
 * @param results Array of operation results
 * @param gid Global thread ID
 */
kernel void queue_batch_dequeue(
    device uchar* buffer [[buffer(0)]],
    device atomic_ulong* head [[buffer(1)]],
    device atomic_ulong* tail [[buffer(2)]],
    constant int& capacity_mask [[buffer(3)]],
    device uchar* messages [[buffer(4)]],
    device int* message_sizes [[buffer(5)]],
    device int* results [[buffer(6)]],
    uint gid [[thread_position_in_grid]])
{
    // Read indices
    ulong current_head = atomic_load_explicit(head, memory_order_acquire);
    ulong current_tail = atomic_load_explicit(tail, memory_order_acquire);

    // Check if empty
    if (current_tail >= current_head) {
        message_sizes[gid] = 0;
        results[gid] = QUEUE_EMPTY;
        return;
    }

    // Atomically increment tail
    ulong my_tail = atomic_fetch_add_explicit(tail, 1UL, memory_order_relaxed);

    // Re-check
    current_head = atomic_load_explicit(head, memory_order_acquire);
    if (my_tail >= current_head) {
        message_sizes[gid] = 0;
        results[gid] = QUEUE_EMPTY;
        return;
    }

    // Calculate slot
    int slot_index = (int)(my_tail & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;
    ulong message_offset = (ulong)gid * MAX_MESSAGE_SIZE;

    // Fence
    threadgroup_barrier(mem_flags::mem_device);

    // Read size
    int msg_size = *reinterpret_cast<device int*>(buffer + buffer_offset);

    // Validate
    if (msg_size <= 0 || msg_size > MAX_MESSAGE_SIZE) {
        message_sizes[gid] = 0;
        results[gid] = QUEUE_INVALID_SIZE;
        return;
    }

    // Copy message
    for (int i = 0; i < msg_size; i++) {
        messages[message_offset + i] = buffer[buffer_offset + i];
    }

    message_sizes[gid] = msg_size;
    results[gid] = QUEUE_SUCCESS;
}
