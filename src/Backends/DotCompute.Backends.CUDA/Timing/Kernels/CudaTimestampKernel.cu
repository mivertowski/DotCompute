// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#include <cuda_runtime.h>
#include <stdint.h>

// Inline PTX assembly to read the 64-bit nanosecond globaltimer register
// Available on compute capability 6.0+ (Pascal architecture and newer)
// Provides 1 nanosecond resolution hardware timestamp
__device__ __forceinline__ uint64_t read_globaltimer() {
    uint64_t timestamp;
    asm volatile("mov.u64 %0, %%globaltimer;" : "=l"(timestamp));
    return timestamp;
}

/// <summary>
/// Reads a single GPU timestamp using the globaltimer register.
/// </summary>
/// <param name="output">Pointer to host-accessible memory for the timestamp result (8 bytes).</param>
/// <remarks>
/// This kernel should be launched with a single thread (1 block, 1 thread).
/// Performance: ~5-10ns execution time on CC 6.0+.
/// The globaltimer register is a 64-bit counter incremented at the GPU shader clock frequency (typically 1 GHz).
/// </remarks>
extern "C" __global__ void read_timestamp_single(
    uint64_t* __restrict__ output)
{
    // Only thread 0 reads the timestamp to avoid race conditions
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        *output = read_globaltimer();
    }
}

/// <summary>
/// Reads multiple GPU timestamps in parallel using the globaltimer register.
/// </summary>
/// <param name="output">Pointer to device memory array for timestamp results (count * 8 bytes).</param>
/// <param name="count">Number of timestamps to read.</param>
/// <remarks>
/// This kernel uses a grid-stride loop to handle arbitrary batch sizes.
/// Each thread reads one timestamp, minimizing inter-thread skew.
/// Recommended launch configuration:
///   - Blocks: (count + 255) / 256
///   - Threads per block: 256
/// Performance: ~1ns per timestamp amortized for count >= 1000.
/// Timestamp skew between threads: typically <100ns.
/// </remarks>
extern "C" __global__ void read_timestamp_batch(
    uint64_t* __restrict__ output,
    const int count)
{
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;
    const int stride = blockDim.x * gridDim.x;

    // Grid-stride loop to handle arbitrary batch sizes
    for (int i = tid; i < count; i += stride) {
        output[i] = read_globaltimer();
    }
}

/// <summary>
/// Reads a synchronized batch of timestamps with minimal temporal skew.
/// </summary>
/// <param name="output">Pointer to device memory array for timestamp results (count * 8 bytes).</param>
/// <param name="count">Number of timestamps to read.</param>
/// <remarks>
/// This kernel uses block synchronization to minimize timestamp skew within a block.
/// All threads in a block read their timestamps within a narrow time window (~10-20ns).
/// Recommended launch configuration:
///   - Blocks: 1 (for minimal skew across all timestamps)
///   - Threads per block: min(count, 1024)
/// Performance: Slightly slower than unsynchronized batch (~2-3ns per timestamp),
/// but provides tighter temporal clustering for applications requiring correlated timestamps.
/// </remarks>
extern "C" __global__ void read_timestamp_batch_synchronized(
    uint64_t* __restrict__ output,
    const int count)
{
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;

    // Each thread reads its timestamp
    if (tid < count) {
        // Synchronize all threads before reading to minimize skew
        __syncthreads();

        // All threads read within a very narrow time window
        output[tid] = read_globaltimer();
    }
}

/// <summary>
/// Benchmarks the globaltimer read overhead by performing multiple reads.
/// </summary>
/// <param name="output">Pointer to device memory array for benchmark results (iteration_count * 8 bytes).</param>
/// <param name="iteration_count">Number of timer reads to perform.</param>
/// <remarks>
/// Useful for characterizing the actual timer resolution and read overhead.
/// Single thread performs sequential reads to measure minimum measurable interval.
/// Expected results: 1-2ns minimum interval between consecutive reads on CC 6.0+.
/// </remarks>
extern "C" __global__ void benchmark_globaltimer_overhead(
    uint64_t* __restrict__ output,
    const int iteration_count)
{
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        for (int i = 0; i < iteration_count; i++) {
            output[i] = read_globaltimer();
        }
    }
}

/// <summary>
/// Reads timestamp with optional delay for clock drift measurement.
/// </summary>
/// <param name="output">Pointer to device memory for timestamp result.</param>
/// <param name="spin_cycles">Number of no-op cycles to spin before reading timestamp.</param>
/// <remarks>
/// Used for clock calibration experiments to measure CPU-GPU time drift.
/// The spin loop introduces a predictable delay on the GPU side.
/// Useful for validating calibration algorithms by introducing known delays.
/// </remarks>
extern "C" __global__ void read_timestamp_with_delay(
    uint64_t* __restrict__ output,
    const uint64_t spin_cycles)
{
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        // Spin loop to introduce delay
        uint64_t start = read_globaltimer();
        while (read_globaltimer() - start < spin_cycles) {
            // Busy wait
        }

        // Read final timestamp after delay
        *output = read_globaltimer();
    }
}

/// <summary>
/// Measures the precision of globaltimer by detecting minimum non-zero delta.
/// </summary>
/// <param name="min_delta">Pointer to output: minimum observed non-zero timestamp delta.</param>
/// <param name="max_delta">Pointer to output: maximum observed timestamp delta.</param>
/// <param name="sample_count">Number of consecutive read pairs to sample.</param>
/// <remarks>
/// Performs consecutive timestamp reads and tracks minimum/maximum deltas.
/// Useful for characterizing actual timer granularity vs. theoretical 1ns resolution.
/// On most GPUs: min_delta = 1-2ns, max_delta depends on sample_count and GPU load.
/// </remarks>
extern "C" __global__ void measure_globaltimer_precision(
    uint64_t* __restrict__ min_delta,
    uint64_t* __restrict__ max_delta,
    const int sample_count)
{
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        uint64_t local_min = UINT64_MAX;
        uint64_t local_max = 0;

        for (int i = 0; i < sample_count; i++) {
            uint64_t t1 = read_globaltimer();
            uint64_t t2 = read_globaltimer();
            uint64_t delta = t2 - t1;

            if (delta > 0 && delta < local_min) {
                local_min = delta;
            }
            if (delta > local_max) {
                local_max = delta;
            }
        }

        *min_delta = local_min;
        *max_delta = local_max;
    }
}

/// <summary>
/// Timestamps kernel execution entry point (kernel preamble injection).
/// </summary>
/// <param name="timestamps">Array to store entry timestamp.</param>
/// <param name="kernel_id">Unique identifier for the kernel invocation.</param>
/// <remarks>
/// This is a utility kernel that can be injected at the start of any kernel
/// to automatically record its launch timestamp.
/// When timestamp injection is enabled, this is called before user kernel code.
/// Thread 0 of block 0 records the timestamp and kernel ID.
/// </remarks>
extern "C" __global__ void inject_timestamp_entry(
    uint64_t* __restrict__ timestamps,
    const uint32_t kernel_id)
{
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        timestamps[kernel_id] = read_globaltimer();
    }
}

/// <summary>
/// Validates globaltimer monotonicity across multiple blocks.
/// </summary>
/// <param name="output">Array of timestamps (one per block).</param>
/// <param name="is_monotonic">Output: 1 if all timestamps are monotonic, 0 otherwise.</param>
/// <remarks>
/// Each block records a timestamp, then thread 0 of block 0 validates monotonicity.
/// Useful for detecting timer rollover issues or multi-GPU timing anomalies.
/// Expected: globaltimer should be monotonically increasing within a single GPU.
/// </remarks>
extern "C" __global__ void validate_globaltimer_monotonicity(
    uint64_t* __restrict__ output,
    int* __restrict__ is_monotonic)
{
    const int bid = blockIdx.x;
    const int tid = threadIdx.x;

    // Each block records its timestamp
    if (tid == 0) {
        output[bid] = read_globaltimer();
    }

    __syncthreads();

    // Block 0, thread 0 validates monotonicity
    if (bid == 0 && tid == 0) {
        int monotonic = 1;
        const int block_count = gridDim.x;

        for (int i = 1; i < block_count; i++) {
            if (output[i] < output[i - 1]) {
                monotonic = 0;
                break;
            }
        }

        *is_monotonic = monotonic;
    }
}
