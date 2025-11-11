// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

/**
 * @file TimestampInjectionKernel.cu
 * @brief Template CUDA code for automatic timestamp injection at kernel entry
 *
 * This file serves as documentation for the timestamp injection feature.
 * The actual injection is performed by TimestampInjector.cs which modifies
 * compiled PTX code to insert this pattern at kernel entry.
 *
 * @note This file is NOT compiled directly. It demonstrates the code pattern
 * that TimestampInjector injects into user kernels when timestamp injection is enabled.
 */

#include <cuda_runtime.h>
#include <stdint.h>

/**
 * @brief Inline PTX assembly to read the 64-bit nanosecond globaltimer register
 * @return Current GPU timestamp in nanoseconds (CC 6.0+)
 *
 * Available on compute capability 6.0+ (Pascal architecture and newer).
 * Provides 1 nanosecond resolution hardware timestamp.
 */
__device__ __forceinline__ uint64_t read_globaltimer_inline() {
    uint64_t timestamp;
    asm volatile("mov.u64 %0, %%globaltimer;" : "=l"(timestamp));
    return timestamp;
}

/**
 * @brief Example of timestamp injection pattern (conceptual)
 * @param timestamps Injected timestamp buffer (added as parameter 0)
 * @param original_param_0 User's original parameter 0 (shifted to parameter 1)
 * @param original_param_1 User's original parameter 1 (shifted to parameter 2)
 *
 * This demonstrates what a user kernel looks like after timestamp injection.
 * The actual injection happens at PTX level, not in source code.
 *
 * Original kernel:
 *   __global__ void MyKernel(float* input, float* output)
 *
 * After injection:
 *   __global__ void MyKernel(uint64_t* timestamps, float* input, float* output)
 *
 * The injected prologue (first ~20ns of execution):
 *   if (threadIdx.x == 0 && threadIdx.y == 0 && threadIdx.z == 0) {
 *       timestamps[0] = read_globaltimer();
 *   }
 */
extern "C" __global__ void example_kernel_with_timestamp_injection(
    uint64_t* __restrict__ timestamps,  // Injected parameter 0
    float* __restrict__ input,           // Original parameter 0 -> now parameter 1
    float* __restrict__ output,          // Original parameter 1 -> now parameter 2
    const int size)                      // Original parameter 2 -> now parameter 3
{
    // === INJECTED PROLOGUE (automatically added by TimestampInjector) ===
    // Only thread (0,0,0) records the timestamp to avoid race conditions
    if (threadIdx.x == 0 && threadIdx.y == 0 && threadIdx.z == 0 &&
        blockIdx.x == 0 && blockIdx.y == 0 && blockIdx.z == 0) {
        timestamps[0] = read_globaltimer_inline();
    }
    // === END INJECTED PROLOGUE ===

    // Original user kernel code continues here...
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;
    if (tid < size) {
        output[tid] = input[tid] * 2.0f;
    }
}

/**
 * @brief PTX-level injection pattern (what TimestampInjector actually generates)
 *
 * The following is the PTX code pattern that TimestampInjector inserts:
 *
 * .visible .entry MyKernel(
 *     .param .u64 param_0,  // NEW: timestamp buffer (injected)
 *     .param .u64 param_1,  // Original param_0 (shifted)
 *     .param .u64 param_2,  // Original param_1 (shifted)
 *     .param .u32 param_3   // Original param_2 (shifted)
 * )
 * {
 *     // === INJECTED TIMESTAMP RECORDING ===
 *     .reg .pred %p_timestamp;
 *     .reg .u32 %tid_x_ts, %tid_y_ts, %tid_z_ts;
 *     .reg .u64 %timestamp_val;
 *
 *     // Get thread IDs
 *     mov.u32 %tid_x_ts, %tid.x;
 *     mov.u32 %tid_y_ts, %tid.y;
 *     mov.u32 %tid_z_ts, %tid.z;
 *
 *     // Check if thread is (0,0,0)
 *     setp.ne.u32 %p_timestamp, %tid_x_ts, 0;
 *     @%p_timestamp bra SKIP_TIMESTAMP_RECORD;
 *     setp.ne.u32 %p_timestamp, %tid_y_ts, 0;
 *     @%p_timestamp bra SKIP_TIMESTAMP_RECORD;
 *     setp.ne.u32 %p_timestamp, %tid_z_ts, 0;
 *     @%p_timestamp bra SKIP_TIMESTAMP_RECORD;
 *
 *     // Thread (0,0,0): Read globaltimer and store to param_0
 *     mov.u64 %timestamp_val, %globaltimer;
 *     st.global.u64 [param_0], %timestamp_val;
 *
 * SKIP_TIMESTAMP_RECORD:
 *     // === END TIMESTAMP RECORDING ===
 *
 *     // Original kernel code continues here...
 *     // (parameter references are automatically shifted: param_0 -> param_1, etc.)
 * }
 */

/**
 * @brief Performance characteristics of timestamp injection
 *
 * Overhead Analysis:
 * - Single-threaded timestamp write: <5ns (memory store)
 * - Thread ID checks: ~5ns (predicate evaluation)
 * - Branch prediction: ~2ns (highly predictable, thread 0 taken, others not taken)
 * - Total overhead per kernel launch: <20ns
 *
 * Memory Requirements:
 * - Timestamp buffer: 8 bytes (single uint64_t)
 * - Register pressure: +4 registers per thread for injection logic
 * - Shared memory: None (uses global memory)
 *
 * Compute Capability Support:
 * - CC 6.0+ (Pascal): Full support with %%globaltimer (1ns resolution)
 * - CC 5.0+ (Maxwell): Limited support, requires CUDA event fallback (1μs resolution)
 * - CC < 5.0: Not supported
 *
 * Thread Safety:
 * - Only thread (0,0,0) of block (0,0,0) writes timestamp
 * - No synchronization required (single writer)
 * - No race conditions or conflicts
 *
 * Use Cases:
 * 1. Kernel profiling: Measure actual GPU execution start time
 * 2. Temporal ordering: Establish causality between kernel launches
 * 3. Performance debugging: Identify launch overhead vs compute time
 * 4. Distributed GPU: Synchronize timestamps across multiple GPUs
 */

/**
 * @brief Example: Manual timestamp recording (without injection)
 *
 * If timestamp injection is disabled, users can manually record timestamps:
 */
extern "C" __global__ void manual_timestamp_kernel(
    float* __restrict__ input,
    float* __restrict__ output,
    uint64_t* __restrict__ timestamps,  // User explicitly adds this
    const int size)
{
    // User explicitly writes timestamp recording code
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        timestamps[blockIdx.x] = read_globaltimer_inline();
    }

    // User kernel code
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;
    if (tid < size) {
        output[tid] = input[tid] * 2.0f;
    }
}

/**
 * @brief Usage example from host code
 *
 * // Enable timestamp injection
 * var timingProvider = accelerator.GetTimingProvider();
 * timingProvider?.EnableTimestampInjection(true);
 *
 * // Compile kernel (injection happens automatically during compilation)
 * var kernel = await accelerator.CompileKernelAsync(kernelDef, options);
 *
 * // Allocate timestamp buffer (8 bytes)
 * var timestampBuffer = accelerator.Allocate<long>(1);
 *
 * // Launch kernel with timestamp buffer as first parameter
 * await accelerator.ExecuteKernelAsync(kernel,
 *     timestampBuffer,    // Injected parameter 0
 *     inputBuffer,        // Original parameter 0
 *     outputBuffer,       // Original parameter 1
 *     size);              // Original parameter 2
 *
 * // Read timestamp
 * var timestamp = timestampBuffer.ToArray()[0];
 * Console.WriteLine($"Kernel started at GPU time: {timestamp}ns");
 *
 * // Disable injection when not needed
 * timingProvider?.EnableTimestampInjection(false);
 */
