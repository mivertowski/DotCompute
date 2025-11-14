// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// OpenCL Device Kernel for Lock-Free Message Queue Operations
// Implements atomic circular buffer with serialized message support
// MAX_MESSAGE_SIZE = 1312 bytes (matches CUDA implementation)

#pragma OPENCL EXTENSION cl_khr_int64_base_atomics : enable
#pragma OPENCL EXTENSION cl_khr_int64_extended_atomics : enable

// Queue operation result codes
typedef enum {
    QUEUE_SUCCESS = 0,
    QUEUE_FULL = 1,
    QUEUE_EMPTY = 2,
    QUEUE_INVALID_SIZE = 3,
    QUEUE_DUPLICATE = 4
} QueueOperationResult;

// Constants
#define MAX_MESSAGE_SIZE 1312

/**
 * Atomic add operation for 64-bit unsigned integers
 * OpenCL equivalent of CUDA's atomicAdd
 */
inline ulong atomic_add_ulong(__global ulong* addr, ulong val)
{
    return atomic_fetch_add_explicit((__global atomic_ulong*)addr, val, memory_order_relaxed, memory_scope_device);
}

/**
 * Atomic subtract operation for 64-bit signed integers
 * OpenCL equivalent of CUDA's atomicAdd with negative value
 */
inline long atomic_sub_long(__global long* addr, long val)
{
    return atomic_fetch_sub_explicit((__global atomic_long*)addr, val, memory_order_relaxed, memory_scope_device);
}

/**
 * Atomic load operation for 64-bit unsigned integers
 */
inline ulong atomic_load_ulong(__global ulong* addr)
{
    return atomic_load_explicit((__global atomic_ulong*)addr, memory_order_acquire, memory_scope_device);
}

/**
 * Memory fence for device-wide visibility
 * OpenCL equivalent of CUDA's __threadfence()
 */
inline void device_fence()
{
    atomic_work_item_fence(CLK_GLOBAL_MEM_FENCE, memory_order_seq_cst, memory_scope_device);
}

/**
 * Enqueue a serialized message into the circular buffer
 *
 * @param buffer Device buffer for serialized messages (capacity * MAX_MESSAGE_SIZE bytes)
 * @param head Atomic head index (next write position)
 * @param tail Atomic tail index (next read position)
 * @param capacity_mask Capacity - 1 (for fast modulo via bitwise AND)
 * @param message Serialized message data
 * @param message_size Size of serialized message in bytes
 * @return QueueOperationResult indicating success or failure reason
 */
__kernel void queue_enqueue(
    __global uchar* buffer,
    __global ulong* head,
    __global ulong* tail,
    int capacity_mask,
    __global const uchar* message,
    int message_size)
{
    // Validate message size
    if (message_size <= 0 || message_size > MAX_MESSAGE_SIZE) {
        return; // QUEUE_INVALID_SIZE
    }

    // Atomically increment head index to reserve a slot
    ulong current_head = atomic_add_ulong(head, 1UL);

    // Calculate actual capacity from mask
    int capacity = capacity_mask + 1;

    // Read current tail to check if queue is full
    ulong current_tail = atomic_load_ulong(tail);

    // Check if queue is full (head has wrapped around and caught up to tail)
    if (current_head >= current_tail + (ulong)capacity) {
        // Queue is full, rollback the head increment
        atomic_sub_long((__global long*)head, 1L);
        return; // QUEUE_FULL
    }

    // Calculate slot index with wrap-around using capacity mask
    int slot_index = (int)(current_head & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;

    // Copy message to device buffer at calculated slot
    for (int i = 0; i < message_size; i++) {
        buffer[buffer_offset + i] = message[i];
    }

    // Memory fence to ensure message is visible before other threads can read
    device_fence();

    // Success (QUEUE_SUCCESS implicit)
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
 * @return QueueOperationResult indicating success or failure reason
 */
__kernel void queue_dequeue(
    __global uchar* buffer,
    __global ulong* head,
    __global ulong* tail,
    int capacity_mask,
    __global uchar* message,
    int max_message_size,
    __global int* actual_size)
{
    // Validate output buffer size
    if (max_message_size < MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        return; // QUEUE_INVALID_SIZE
    }

    // Read current head and tail indices
    ulong current_head = atomic_load_ulong(head);
    ulong current_tail = atomic_load_ulong(tail);

    // Check if queue is empty
    if (current_tail >= current_head) {
        *actual_size = 0;
        return; // QUEUE_EMPTY
    }

    // Atomically increment tail to reserve the message for dequeue
    ulong my_tail = atomic_add_ulong(tail, 1UL);

    // Re-check if we actually got a valid slot (race condition check)
    current_head = atomic_load_ulong(head);
    if (my_tail >= current_head) {
        *actual_size = 0;
        return; // QUEUE_EMPTY (lost race)
    }

    // Calculate slot index with wrap-around
    int slot_index = (int)(my_tail & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;

    // Memory fence to ensure message is fully written before we read
    device_fence();

    // Read message size from first 4 bytes (int32)
    int msg_size = 0;
    for (int i = 0; i < 4; i++) {
        msg_size |= ((int)buffer[buffer_offset + i]) << (i * 8);
    }

    // Validate message size
    if (msg_size <= 0 || msg_size > MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        return; // QUEUE_INVALID_SIZE
    }

    // Copy message from device buffer to output
    for (int i = 0; i < msg_size; i++) {
        message[i] = buffer[buffer_offset + i];
    }

    *actual_size = msg_size;

    // Success (QUEUE_SUCCESS implicit)
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
 * @return QueueOperationResult indicating success or failure reason
 */
__kernel void queue_peek(
    __global uchar* buffer,
    __global ulong* head,
    __global ulong* tail,
    int capacity_mask,
    __global uchar* message,
    int max_message_size,
    __global int* actual_size)
{
    // Validate output buffer size
    if (max_message_size < MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        return; // QUEUE_INVALID_SIZE
    }

    // Read current head and tail indices (no atomic increment)
    ulong current_head = atomic_load_ulong(head);
    ulong current_tail = atomic_load_ulong(tail);

    // Check if queue is empty
    if (current_tail >= current_head) {
        *actual_size = 0;
        return; // QUEUE_EMPTY
    }

    // Calculate slot index for the tail (next to be dequeued)
    int slot_index = (int)(current_tail & (ulong)capacity_mask);
    ulong buffer_offset = (ulong)slot_index * MAX_MESSAGE_SIZE;

    // Memory fence to ensure message is fully written
    device_fence();

    // Read message size from first 4 bytes
    int msg_size = 0;
    for (int i = 0; i < 4; i++) {
        msg_size |= ((int)buffer[buffer_offset + i]) << (i * 8);
    }

    // Validate message size
    if (msg_size <= 0 || msg_size > MAX_MESSAGE_SIZE) {
        *actual_size = 0;
        return; // QUEUE_INVALID_SIZE
    }

    // Copy message from device buffer to output
    for (int i = 0; i < msg_size; i++) {
        message[i] = buffer[buffer_offset + i];
    }

    *actual_size = msg_size;

    // Success (QUEUE_SUCCESS implicit)
}

/**
 * Get the current number of messages in the queue
 *
 * @param head Atomic head index (next write position)
 * @param tail Atomic tail index (next read position)
 * @param count Output parameter for message count
 */
__kernel void queue_count(
    __global ulong* head,
    __global ulong* tail,
    __global int* count)
{
    // Read current head and tail indices
    ulong current_head = atomic_load_ulong(head);
    ulong current_tail = atomic_load_ulong(tail);

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
__kernel void queue_is_empty(
    __global ulong* head,
    __global ulong* tail,
    __global int* is_empty)
{
    ulong current_head = atomic_load_ulong(head);
    ulong current_tail = atomic_load_ulong(tail);

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
__kernel void queue_is_full(
    __global ulong* head,
    __global ulong* tail,
    int capacity_mask,
    __global int* is_full)
{
    int capacity = capacity_mask + 1;

    ulong current_head = atomic_load_ulong(head);
    ulong current_tail = atomic_load_ulong(tail);

    *is_full = (current_head >= current_tail + (ulong)capacity) ? 1 : 0;
}

/**
 * Clear the queue by resetting head and tail to zero
 * WARNING: Not thread-safe, should only be called when no other operations are in progress
 *
 * @param head Atomic head index
 * @param tail Atomic tail index
 */
__kernel void queue_clear(
    __global ulong* head,
    __global ulong* tail)
{
    atomic_store_explicit((__global atomic_ulong*)head, 0UL, memory_order_release, memory_scope_device);
    atomic_store_explicit((__global atomic_ulong*)tail, 0UL, memory_order_release, memory_scope_device);

    // Ensure visibility
    device_fence();
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
 */
__kernel void queue_batch_enqueue(
    __global uchar* buffer,
    __global ulong* head,
    __global ulong* tail,
    int capacity_mask,
    __global const uchar* messages,
    __global const int* message_sizes,
    __global int* results)
{
    int gid = get_global_id(0);
    int message_size = message_sizes[gid];

    // Validate message size
    if (message_size <= 0 || message_size > MAX_MESSAGE_SIZE) {
        results[gid] = QUEUE_INVALID_SIZE;
        return;
    }

    // Atomically increment head
    ulong current_head = atomic_add_ulong(head, 1UL);

    // Calculate capacity
    int capacity = capacity_mask + 1;

    // Check if full
    ulong current_tail = atomic_load_ulong(tail);
    if (current_head >= current_tail + (ulong)capacity) {
        atomic_sub_long((__global long*)head, 1L);
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
    device_fence();

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
 */
__kernel void queue_batch_dequeue(
    __global uchar* buffer,
    __global ulong* head,
    __global ulong* tail,
    int capacity_mask,
    __global uchar* messages,
    __global int* message_sizes,
    __global int* results)
{
    int gid = get_global_id(0);

    // Read indices
    ulong current_head = atomic_load_ulong(head);
    ulong current_tail = atomic_load_ulong(tail);

    // Check if empty
    if (current_tail >= current_head) {
        message_sizes[gid] = 0;
        results[gid] = QUEUE_EMPTY;
        return;
    }

    // Atomically increment tail
    ulong my_tail = atomic_add_ulong(tail, 1UL);

    // Re-check
    current_head = atomic_load_ulong(head);
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
    device_fence();

    // Read size
    int msg_size = 0;
    for (int i = 0; i < 4; i++) {
        msg_size |= ((int)buffer[buffer_offset + i]) << (i * 8);
    }

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
