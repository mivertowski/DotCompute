/**
 * CUDA Memory Fence Kernel
 *
 * Provides device-side memory fence operations for causal memory ordering.
 *
 * Copyright (c) 2025 Michael Ivertowski
 * Licensed under the MIT License.
 */

#include <cuda_runtime.h>
#include <cstdint>

// ============================================================================
// Memory Fence Primitives
// ============================================================================

/**
 * Thread-block scope fence.
 * Ensures all memory operations by this thread are visible to all threads
 * in the same thread block before returning.
 *
 * Performance: ~10ns latency
 * Scope: Threads in same block only
 * CUDA: __threadfence_block()
 */
__device__ __forceinline__ void fence_threadblock()
{
    __threadfence_block();
}

/**
 * Device scope fence.
 * Ensures all memory operations by this thread are visible to all threads
 * on the same GPU device before returning.
 *
 * Performance: ~100ns latency
 * Scope: All threads on same GPU
 * CUDA: __threadfence()
 */
__device__ __forceinline__ void fence_device()
{
    __threadfence();
}

/**
 * System scope fence.
 * Ensures all memory operations by this thread are visible to all processors
 * in the system (CPU, all GPUs) before returning.
 *
 * Performance: ~200ns latency
 * Scope: CPU + all GPUs
 * CUDA: __threadfence_system()
 * Requires: Unified Virtual Addressing (UVA)
 */
__device__ __forceinline__ void fence_system()
{
    __threadfence_system();
}

// ============================================================================
// Causal Memory Ordering Primitives
// ============================================================================

/**
 * Causal write with release semantics (thread-block scope).
 * All prior memory operations complete before this write is visible.
 *
 * Pattern: [prior ops] -> fence -> write
 * Use: Producer threads publishing data to consumers in same block
 */
__device__ void causal_write_i64_block(volatile int64_t* addr, int64_t value)
{
    __threadfence_block();  // Release fence
    *addr = value;          // Atomic-like store (volatile ensures ordering)
}

/**
 * Causal read with acquire semantics (thread-block scope).
 * All subsequent memory operations observe values after this read.
 *
 * Pattern: read -> fence -> [subsequent ops]
 * Use: Consumer threads acquiring data from producers in same block
 */
__device__ int64_t causal_read_i64_block(volatile const int64_t* addr)
{
    int64_t value = *addr;  // Atomic-like load
    __threadfence_block();  // Acquire fence
    return value;
}

/**
 * Causal write with release semantics (device scope).
 * All prior memory operations complete before this write is visible to all threads on device.
 *
 * Pattern: [prior ops] -> fence -> write
 * Use: Producer threads publishing data to consumers across blocks
 */
__device__ void causal_write_i64_device(volatile int64_t* addr, int64_t value)
{
    __threadfence();  // Device-wide release fence
    *addr = value;
}

/**
 * Causal read with acquire semantics (device scope).
 * All subsequent memory operations observe values after this read from any thread on device.
 *
 * Pattern: read -> fence -> [subsequent ops]
 * Use: Consumer threads acquiring data from producers across blocks
 */
__device__ int64_t causal_read_i64_device(volatile const int64_t* addr)
{
    int64_t value = *addr;
    __threadfence();  // Device-wide acquire fence
    return value;
}

/**
 * Causal write with release semantics (system scope).
 * All prior memory operations complete before this write is visible to CPU and all GPUs.
 *
 * Pattern: [prior ops] -> fence -> write
 * Use: GPU-CPU communication, multi-GPU synchronization, Orleans.GpuBridge message passing
 *
 * Requires: Unified Virtual Addressing (UVA), CC 2.0+
 */
__device__ void causal_write_i64_system(volatile int64_t* addr, int64_t value)
{
    __threadfence_system();  // System-wide release fence
    *addr = value;
}

/**
 * Causal read with acquire semantics (system scope).
 * All subsequent memory operations observe values after this read from any processor.
 *
 * Pattern: read -> fence -> [subsequent ops]
 * Use: GPU reading data written by CPU or other GPUs
 */
__device__ int64_t causal_read_i64_system(volatile const int64_t* addr)
{
    int64_t value = *addr;
    __threadfence_system();  // System-wide acquire fence
    return value;
}

// ============================================================================
// Atomic Causal Operations (CC 2.0+)
// ============================================================================

/**
 * Atomic causal exchange with release-acquire semantics (device scope).
 * Provides both release (all prior ops visible) and acquire (all subsequent ops observe).
 *
 * Use: Synchronization variables, locks, flags
 * Performance: ~50ns (atomic + 2 fences)
 */
__device__ int64_t atomic_causal_exchange_device(int64_t* addr, int64_t value)
{
    __threadfence();  // Release: make prior ops visible
    int64_t old = atomicExch((unsigned long long*)addr, (unsigned long long)value);
    __threadfence();  // Acquire: ensure subsequent ops observe
    return old;
}

/**
 * Atomic causal compare-and-swap with release-acquire semantics (device scope).
 * Only swaps if current value equals `expected`. Returns old value.
 *
 * Use: Lock-free data structures, conditional updates
 * Performance: ~50ns (atomic + 2 fences)
 */
__device__ int64_t atomic_causal_cas_device(int64_t* addr, int64_t expected, int64_t desired)
{
    __threadfence();  // Release
    int64_t old = atomicCAS((unsigned long long*)addr,
                            (unsigned long long)expected,
                            (unsigned long long)desired);
    __threadfence();  // Acquire
    return old;
}

/**
 * Atomic causal add with release-acquire semantics (device scope).
 * Atomically adds `value` to `*addr` and returns old value.
 *
 * Use: Counters, accumulators
 * Performance: ~50ns (atomic + 2 fences)
 */
__device__ int64_t atomic_causal_add_device(int64_t* addr, int64_t value)
{
    __threadfence();  // Release
    int64_t old = atomicAdd((unsigned long long*)addr, (unsigned long long)value);
    __threadfence();  // Acquire
    return old;
}

// ============================================================================
// Test Kernels for Validation
// ============================================================================

/**
 * Test kernel: Producer-consumer pattern with causal ordering.
 * Producer threads write data, consumer threads read. Validates causality.
 *
 * Pattern:
 * - Producer: writes[tid] = value; causal_write(&flags[tid], READY);
 * - Consumer: while (causal_read(&flags[producer]) != READY) {}; read writes[producer];
 */
extern "C" __global__ void test_producer_consumer_causal(
    volatile int64_t* data,     // Shared data buffer
    volatile int64_t* flags,    // Ready flags (0=not ready, 1=ready)
    int64_t* results,           // Output results
    int producer_count,         // Number of producer threads
    int consumer_count)         // Number of consumer threads
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int total_threads = producer_count + consumer_count;

    if (tid >= total_threads) return;

    // First half are producers, second half are consumers
    if (tid < producer_count)
    {
        // Producer: write data with causal ordering
        int64_t value = tid * 1000;  // Unique value
        data[tid] = value;
        causal_write_i64_device(&flags[tid], 1);  // Signal ready with release
    }
    else
    {
        // Consumer: read data with causal ordering
        int producer_id = tid - producer_count;  // Which producer to consume from

        // Spin-wait until producer signals ready (with acquire)
        while (causal_read_i64_device(&flags[producer_id]) != 1) { }

        // Read data (causality ensures we see producer's write)
        results[tid - producer_count] = data[producer_id];
    }
}

/**
 * Test kernel: Memory fence benchmarking.
 * Measures overhead of different fence types.
 */
extern "C" __global__ void benchmark_memory_fences(
    int64_t* output,        // Output: elapsed cycles
    FenceType fence_type,   // Which fence to benchmark
    int iteration_count)    // Number of iterations for amortization
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    if (tid != 0) return;  // Single thread only

    // Warm-up
    for (int i = 0; i < 10; i++)
    {
        __threadfence();
    }

    // Benchmark
    uint64_t start, end;
    asm volatile("mov.u64 %0, %%globaltimer;" : "=l"(start));

    for (int i = 0; i < iteration_count; i++)
    {
        switch (fence_type)
        {
            case 0:  // ThreadBlock
                __threadfence_block();
                break;
            case 1:  // Device
                __threadfence();
                break;
            case 2:  // System
                __threadfence_system();
                break;
        }
    }

    asm volatile("mov.u64 %0, %%globaltimer;" : "=l"(end));

    output[0] = (int64_t)(end - start) / iteration_count;
}

/**
 * Fence type enum (must match C# FenceType)
 */
typedef enum
{
    FENCE_THREADBLOCK = 0,
    FENCE_DEVICE = 1,
    FENCE_SYSTEM = 2
} FenceType;
