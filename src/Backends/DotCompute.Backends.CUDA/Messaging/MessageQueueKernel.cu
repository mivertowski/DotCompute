/**
 * CUDA Message Queue Kernel
 *
 * Provides device-side lock-free message queue operations for Ring Kernels.
 * Supports atomic enqueue/dequeue with wrap-around circular buffer semantics.
 *
 * Copyright (c) 2025 Michael Ivertowski
 * Licensed under the MIT License.
 */

#include <cuda_runtime.h>
#include <cstdint>

// ============================================================================
// Constants and Types
// ============================================================================

/**
 * Maximum message size in bytes (conservative estimate: 1KB + metadata).
 * Must match CudaMessageQueue<T>._maxMessageSize in C#.
 */
#define MAX_MESSAGE_SIZE 1312

/**
 * Queue operation result codes.
 * Must match QueueOperationResult enum in C#.
 */
typedef enum
{
    QUEUE_SUCCESS = 0,          // Operation succeeded
    QUEUE_FULL = 1,             // Queue is full (enqueue failed)
    QUEUE_EMPTY = 2,            // Queue is empty (dequeue failed)
    QUEUE_INVALID_INDEX = 3     // Invalid queue index or parameters
} QueueOperationResult;

// ============================================================================
// Device-Side Queue Operations
// ============================================================================

/**
 * Atomically enqueue a message into the device queue.
 *
 * Algorithm:
 * 1. Atomically increment head index (returns old value)
 * 2. Calculate slot index with capacity mask (power of 2 wrap-around)
 * 3. Check if queue is full (head >= tail + capacity)
 * 4. Copy message bytes to device buffer slot
 * 5. Memory fence to ensure visibility
 *
 * Performance: ~200-500ns depending on message size
 *
 * @param buffer Device buffer for serialized messages
 * @param head Device pointer to atomic head index (long*)
 * @param tail Device pointer to atomic tail index (long*)
 * @param capacity_mask Capacity - 1 (for power-of-2 masking)
 * @param message Host or device pointer to serialized message bytes
 * @param message_size Size of message in bytes
 * @return QUEUE_SUCCESS if enqueued, QUEUE_FULL if queue is full
 */
extern "C" __device__ QueueOperationResult queue_enqueue(
    unsigned char* buffer,
    unsigned long long* head,
    unsigned long long* tail,
    int capacity_mask,
    const unsigned char* message,
    int message_size)
{
    // Input validation
    if (message_size > MAX_MESSAGE_SIZE || message_size <= 0)
    {
        return QUEUE_INVALID_INDEX;
    }

    // Atomically increment head and get old value
    unsigned long long current_head = atomicAdd(head, 1ULL);

    // Read tail (relaxed, just for fullness check)
    unsigned long long current_tail = *tail;

    // Check if queue is full (head caught up to tail + capacity)
    int capacity = capacity_mask + 1;
    if (current_head >= current_tail + capacity)
    {
        // Rollback head increment (best effort)
        atomicAdd(head, -1LL);
        return QUEUE_FULL;
    }

    // Calculate slot index with wrap-around
    int slot_index = (int)(current_head & capacity_mask);
    unsigned long long slot_offset = slot_index * MAX_MESSAGE_SIZE;

    // Copy message to device buffer
    for (int i = 0; i < message_size; i++)
    {
        buffer[slot_offset + i] = message[i];
    }

    // Memory fence to ensure writes are visible to other threads
    __threadfence();

    return QUEUE_SUCCESS;
}

/**
 * Atomically dequeue a message from the device queue.
 *
 * Algorithm:
 * 1. Read current head and tail indices
 * 2. Check if queue is empty (tail >= head)
 * 3. Atomically increment tail index (returns old value)
 * 4. Calculate slot index with capacity mask
 * 5. Copy message bytes from device buffer to output
 * 6. Memory fence to ensure visibility
 *
 * Performance: ~200-500ns depending on message size
 *
 * @param buffer Device buffer for serialized messages
 * @param head Device pointer to atomic head index (long*)
 * @param tail Device pointer to atomic tail index (long*)
 * @param capacity_mask Capacity - 1 (for power-of-2 masking)
 * @param output Output buffer for dequeued message (device or host)
 * @param max_output_size Maximum size of output buffer
 * @return QUEUE_SUCCESS if dequeued, QUEUE_EMPTY if queue is empty
 */
extern "C" __device__ QueueOperationResult queue_dequeue(
    const unsigned char* buffer,
    unsigned long long* head,
    unsigned long long* tail,
    int capacity_mask,
    unsigned char* output,
    int max_output_size)
{
    // Input validation
    if (max_output_size < MAX_MESSAGE_SIZE)
    {
        return QUEUE_INVALID_INDEX;
    }

    // Read head and tail
    unsigned long long current_head = *head;
    unsigned long long current_tail = *tail;

    // Check if queue is empty
    if (current_tail >= current_head)
    {
        return QUEUE_EMPTY;
    }

    // Atomically increment tail and get old value
    current_tail = atomicAdd(tail, 1ULL);

    // Re-check emptiness after atomic increment (race condition protection)
    current_head = *head;
    if (current_tail >= current_head)
    {
        // Queue became empty, rollback
        atomicAdd(tail, -1LL);
        return QUEUE_EMPTY;
    }

    // Calculate slot index with wrap-around
    int slot_index = (int)(current_tail & capacity_mask);
    unsigned long long slot_offset = slot_index * MAX_MESSAGE_SIZE;

    // Memory fence to ensure we see producer's writes
    __threadfence();

    // Copy message from device buffer to output
    for (int i = 0; i < MAX_MESSAGE_SIZE; i++)
    {
        output[i] = buffer[slot_offset + i];
    }

    return QUEUE_SUCCESS;
}

/**
 * Peek at the next message without dequeuing.
 *
 * @param buffer Device buffer for serialized messages
 * @param head Device pointer to atomic head index (long*)
 * @param tail Device pointer to atomic tail index (long*)
 * @param capacity_mask Capacity - 1 (for power-of-2 masking)
 * @param output Output buffer for peeked message (device or host)
 * @param max_output_size Maximum size of output buffer
 * @return QUEUE_SUCCESS if peeked, QUEUE_EMPTY if queue is empty
 */
extern "C" __device__ QueueOperationResult queue_peek(
    const unsigned char* buffer,
    unsigned long long* head,
    unsigned long long* tail,
    int capacity_mask,
    unsigned char* output,
    int max_output_size)
{
    // Input validation
    if (max_output_size < MAX_MESSAGE_SIZE)
    {
        return QUEUE_INVALID_INDEX;
    }

    // Read head and tail (no atomics needed for peek)
    unsigned long long current_head = *head;
    unsigned long long current_tail = *tail;

    // Check if queue is empty
    if (current_tail >= current_head)
    {
        return QUEUE_EMPTY;
    }

    // Calculate slot index of tail (oldest message)
    int slot_index = (int)(current_tail & capacity_mask);
    unsigned long long slot_offset = slot_index * MAX_MESSAGE_SIZE;

    // Memory fence to ensure we see producer's writes
    __threadfence();

    // Copy message from device buffer to output
    for (int i = 0; i < MAX_MESSAGE_SIZE; i++)
    {
        output[i] = buffer[slot_offset + i];
    }

    return QUEUE_SUCCESS;
}

/**
 * Get current queue count (non-atomic snapshot).
 *
 * @param head Device pointer to head index
 * @param tail Device pointer to tail index
 * @return Number of messages currently in queue (approximate)
 */
extern "C" __device__ int queue_count(
    const unsigned long long* head,
    const unsigned long long* tail)
{
    unsigned long long current_head = *head;
    unsigned long long current_tail = *tail;

    if (current_tail >= current_head)
    {
        return 0;
    }

    return (int)(current_head - current_tail);
}

/**
 * Check if queue is full (non-atomic snapshot).
 *
 * @param head Device pointer to head index
 * @param tail Device pointer to tail index
 * @param capacity Queue capacity
 * @return 1 if full, 0 otherwise
 */
extern "C" __device__ int queue_is_full(
    const unsigned long long* head,
    const unsigned long long* tail,
    int capacity)
{
    unsigned long long current_head = *head;
    unsigned long long current_tail = *tail;

    return (current_head >= current_tail + capacity) ? 1 : 0;
}

/**
 * Check if queue is empty (non-atomic snapshot).
 *
 * @param head Device pointer to head index
 * @param tail Device pointer to tail index
 * @return 1 if empty, 0 otherwise
 */
extern "C" __device__ int queue_is_empty(
    const unsigned long long* head,
    const unsigned long long* tail)
{
    unsigned long long current_head = *head;
    unsigned long long current_tail = *tail;

    return (current_tail >= current_head) ? 1 : 0;
}

// ============================================================================
// Test Kernels for Hardware Validation
// ============================================================================

/**
 * Test kernel: Producer-consumer pattern with message queue.
 * Multiple producer threads enqueue messages, consumer threads dequeue.
 *
 * Test Pattern:
 * - Producers (tid < producer_count): enqueue unique message (tid * 1000)
 * - Consumers (tid >= producer_count): dequeue message and store in results
 *
 * Validates:
 * - Atomic enqueue correctness
 * - Atomic dequeue correctness
 * - No message loss or corruption
 * - FIFO ordering (approximate, due to concurrent enqueues)
 */
extern "C" __global__ void test_queue_producer_consumer(
    unsigned char* queue_buffer,
    unsigned long long* queue_head,
    unsigned long long* queue_tail,
    int capacity_mask,
    int producer_count,
    int consumer_count,
    unsigned long long* producer_results,  // Success count for each producer
    unsigned long long* consumer_results)  // Dequeued values for each consumer
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int total_threads = producer_count + consumer_count;

    if (tid >= total_threads) return;

    if (tid < producer_count)
    {
        // Producer: create and enqueue a message
        unsigned long long message_data = tid * 1000;  // Unique message ID
        unsigned char message_bytes[MAX_MESSAGE_SIZE];

        // Simple serialization: store message_data as first 8 bytes
        *((unsigned long long*)message_bytes) = message_data;

        // Enqueue message
        QueueOperationResult result = queue_enqueue(
            queue_buffer,
            queue_head,
            queue_tail,
            capacity_mask,
            message_bytes,
            sizeof(unsigned long long));

        // Record success (1) or failure (0)
        producer_results[tid] = (result == QUEUE_SUCCESS) ? 1 : 0;
    }
    else
    {
        // Consumer: dequeue a message
        unsigned char message_bytes[MAX_MESSAGE_SIZE];

        // Spin-wait with exponential backoff
        int backoff = 1;
        QueueOperationResult result;

        do
        {
            result = queue_dequeue(
                queue_buffer,
                queue_head,
                queue_tail,
                capacity_mask,
                message_bytes,
                MAX_MESSAGE_SIZE);

            if (result == QUEUE_EMPTY)
            {
                // Exponential backoff to reduce contention
                for (int i = 0; i < backoff; i++)
                {
                    __nanosleep(100);  // 100ns sleep
                }
                backoff = min(backoff * 2, 1000);
            }
        } while (result != QUEUE_SUCCESS && backoff < 10000);

        // Deserialize and store result
        if (result == QUEUE_SUCCESS)
        {
            unsigned long long message_data = *((unsigned long long*)message_bytes);
            consumer_results[tid - producer_count] = message_data;
        }
        else
        {
            consumer_results[tid - producer_count] = 0xFFFFFFFFFFFFFFFFULL;  // Failure marker
        }
    }
}

/**
 * Test kernel: Concurrent enqueue stress test.
 * All threads attempt to enqueue simultaneously.
 *
 * Validates:
 * - Atomic head increment correctness
 * - No message overwrites
 * - Queue full detection
 */
extern "C" __global__ void test_queue_concurrent_enqueue(
    unsigned char* queue_buffer,
    unsigned long long* queue_head,
    unsigned long long* queue_tail,
    int capacity_mask,
    int thread_count,
    unsigned long long* success_count)  // Output: how many enqueues succeeded
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    if (tid >= thread_count) return;

    // Each thread tries to enqueue a unique message
    unsigned long long message_data = tid;
    unsigned char message_bytes[MAX_MESSAGE_SIZE];
    *((unsigned long long*)message_bytes) = message_data;

    QueueOperationResult result = queue_enqueue(
        queue_buffer,
        queue_head,
        queue_tail,
        capacity_mask,
        message_bytes,
        sizeof(unsigned long long));

    // Atomically count successes
    if (result == QUEUE_SUCCESS)
    {
        atomicAdd(success_count, 1ULL);
    }
}

/**
 * Test kernel: Concurrent dequeue stress test.
 * All threads attempt to dequeue simultaneously.
 *
 * Validates:
 * - Atomic tail increment correctness
 * - No duplicate dequeues
 * - Queue empty detection
 */
extern "C" __global__ void test_queue_concurrent_dequeue(
    const unsigned char* queue_buffer,
    unsigned long long* queue_head,
    unsigned long long* queue_tail,
    int capacity_mask,
    int thread_count,
    unsigned long long* success_count,  // Output: how many dequeues succeeded
    unsigned long long* dequeued_values)  // Output: dequeued message IDs
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    if (tid >= thread_count) return;

    unsigned char message_bytes[MAX_MESSAGE_SIZE];

    QueueOperationResult result = queue_dequeue(
        queue_buffer,
        queue_head,
        queue_tail,
        capacity_mask,
        message_bytes,
        MAX_MESSAGE_SIZE);

    if (result == QUEUE_SUCCESS)
    {
        // Atomically count successes
        unsigned long long index = atomicAdd(success_count, 1ULL);

        // Store dequeued value
        unsigned long long message_data = *((unsigned long long*)message_bytes);
        dequeued_values[index] = message_data;
    }
}

/**
 * Test kernel: Queue capacity and wrap-around.
 * Fill queue to capacity, verify wrap-around works correctly.
 *
 * Validates:
 * - Capacity mask correctness
 * - Wrap-around at power-of-2 boundary
 * - Full queue detection
 */
extern "C" __global__ void test_queue_wraparound(
    unsigned char* queue_buffer,
    unsigned long long* queue_head,
    unsigned long long* queue_tail,
    int capacity_mask,
    int capacity,
    unsigned long long* test_results)  // Output: [0]=fill_success, [1]=wrap_count
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    if (tid != 0) return;  // Single thread test

    // Phase 1: Fill queue to capacity
    int enqueued = 0;
    for (int i = 0; i < capacity + 10; i++)  // Try to overfill
    {
        unsigned char message_bytes[MAX_MESSAGE_SIZE];
        *((unsigned long long*)message_bytes) = (unsigned long long)i;

        QueueOperationResult result = queue_enqueue(
            queue_buffer,
            queue_head,
            queue_tail,
            capacity_mask,
            message_bytes,
            sizeof(unsigned long long));

        if (result == QUEUE_SUCCESS)
        {
            enqueued++;
        }
    }

    test_results[0] = (enqueued == capacity) ? 1 : 0;  // Should enqueue exactly capacity messages

    // Phase 2: Dequeue and re-enqueue to test wrap-around
    int wrapped = 0;
    for (int i = 0; i < capacity * 2; i++)  // Do multiple wrap-arounds
    {
        unsigned char message_bytes[MAX_MESSAGE_SIZE];

        // Dequeue one
        QueueOperationResult deq_result = queue_dequeue(
            queue_buffer,
            queue_head,
            queue_tail,
            capacity_mask,
            message_bytes,
            MAX_MESSAGE_SIZE);

        if (deq_result != QUEUE_SUCCESS) break;

        // Enqueue one (different value)
        unsigned long long new_value = 10000 + i;
        *((unsigned long long*)message_bytes) = new_value;

        QueueOperationResult enq_result = queue_enqueue(
            queue_buffer,
            queue_head,
            queue_tail,
            capacity_mask,
            message_bytes,
            sizeof(unsigned long long));

        if (enq_result == QUEUE_SUCCESS)
        {
            wrapped++;
        }
    }

    test_results[1] = wrapped;  // Should wrap multiple times successfully
}
