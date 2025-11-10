// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#include <cuda_runtime.h>
#include <cooperative_groups.h>

namespace cg = cooperative_groups;

// ============================================================================
// Thread-Block Barrier Kernel
// ============================================================================

/// <summary>
/// Demonstrates thread-block barrier synchronization using __syncthreads().
/// All threads within the block wait until every thread reaches the barrier.
/// </summary>
/// <param name="input">Input data array.</param>
/// <param name="output">Output data array.</param>
/// <param name="size">Array size.</param>
/// <remarks>
/// This is the most common barrier pattern, available on all CUDA devices (CC 1.0+).
/// Hardware latency: ~10ns typical.
/// </remarks>
__global__ void ThreadBlockBarrierKernel(const float* input, float* output, int size)
{
    __shared__ float sharedData[1024];

    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;

    // Phase 1: Load data into shared memory
    sharedData[threadIdx.x] = input[idx];

    // Barrier: Ensure all threads have completed loading
    __syncthreads();

    // Phase 2: Process data (all reads are now safe)
    float value = sharedData[threadIdx.x];
    if (threadIdx.x > 0)
    {
        value += sharedData[threadIdx.x - 1]; // Read neighbor (safe after barrier)
    }

    // Barrier: Ensure all threads have completed processing
    __syncthreads();

    // Phase 3: Write results
    output[idx] = value;
}

// ============================================================================
// Grid-Wide Barrier Kernel (Cooperative Groups)
// ============================================================================

/// <summary>
/// Demonstrates grid-wide barrier synchronization across all thread blocks.
/// Requires cooperative kernel launch via cudaLaunchCooperativeKernel.
/// </summary>
/// <param name="data">Data array to process.</param>
/// <param name="size">Array size.</param>
/// <param name="iterations">Number of iterative phases.</param>
/// <remarks>
/// Grid barriers enable global synchronization for multi-phase algorithms.
/// Requires Compute Capability 6.0+ (Pascal or newer).
/// Latency: ~1-10μs depending on grid size.
/// </remarks>
__global__ void GridBarrierKernel(float* data, int size, int iterations)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;

    // Get the grid group (all threads in the kernel)
    cg::grid_group grid = cg::this_grid();

    for (int iter = 0; iter < iterations; iter++)
    {
        // Phase 1: Each thread processes its element
        float value = data[idx];
        value = value * 1.1f + 0.5f;
        data[idx] = value;

        // Grid-wide barrier: Wait for all threads across all blocks
        grid.sync();

        // Phase 2: All threads can now safely read any element
        // (all writes from phase 1 are complete and visible)
        if (idx > 0 && idx < size - 1)
        {
            float left = data[idx - 1];
            float right = data[idx + 1];
            data[idx] = (left + value + right) / 3.0f;
        }

        // Another grid-wide barrier before next iteration
        grid.sync();
    }
}

// ============================================================================
// Named Barrier Kernel (Multiple Barriers Per Block)
// ============================================================================

/// <summary>
/// Demonstrates named barriers for complex multi-phase algorithms.
/// Up to 16 distinct barriers can be used per thread block.
/// </summary>
/// <param name="input">Input data array.</param>
/// <param name="output">Output data array.</param>
/// <param name="size">Array size.</param>
/// <remarks>
/// Named barriers enable different thread subsets to synchronize independently.
/// Requires Compute Capability 11.0+ or device with named barrier support.
/// </remarks>
__global__ void NamedBarrierKernel(const float* input, float* output, int size)
{
#if __CUDA_ARCH__ >= 1100  // CC 11.0+
    __shared__ float sharedData[1024];
    __shared__ float tempData[1024];

    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;

    // Phase 1: Load with barrier ID 0
    sharedData[threadIdx.x] = input[idx];
    __barrier_sync(0); // Named barrier 0

    // Phase 2: First half of threads process
    if (threadIdx.x < blockDim.x / 2)
    {
        tempData[threadIdx.x] = sharedData[threadIdx.x] * 2.0f;
        __barrier_sync(1); // Named barrier 1 (first half only)
    }

    // Phase 3: Second half of threads process
    if (threadIdx.x >= blockDim.x / 2)
    {
        tempData[threadIdx.x] = sharedData[threadIdx.x] * 3.0f;
        __barrier_sync(2); // Named barrier 2 (second half only)
    }

    // Phase 4: All threads sync again
    __barrier_sync(0); // Named barrier 0 (all threads)

    // Phase 5: Write results
    output[idx] = tempData[threadIdx.x];
#else
    // Fallback for older devices
    __shared__ float sharedData[1024];

    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;

    sharedData[threadIdx.x] = input[idx];
    __syncthreads();

    output[idx] = sharedData[threadIdx.x];
#endif
}

// ============================================================================
// Warp-Level Barrier Kernel
// ============================================================================

/// <summary>
/// Demonstrates warp-level barrier synchronization using __syncwarp().
/// Synchronizes all 32 threads within a CUDA warp (SIMD group).
/// </summary>
/// <param name="input">Input data array.</param>
/// <param name="output">Output data array.</param>
/// <param name="size">Array size.</param>
/// <remarks>
/// Warp barriers provide ultra-fast (~1ns) synchronization for 32-thread groups.
/// Requires Compute Capability 7.0+ for explicit warp sync (Volta and newer).
/// Prior architectures had implicit lockstep execution.
/// </remarks>
__global__ void WarpBarrierKernel(const float* input, float* output, int size)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;

    float value = input[idx];

    // Warp-level reduction using shuffle and explicit warp sync
    for (int offset = 16; offset > 0; offset /= 2)
    {
        value += __shfl_down_sync(0xffffffff, value, offset);

        // Explicit warp barrier (CC 7.0+)
        // Ensures all shuffle operations complete before next iteration
        __syncwarp();
    }

    // Lane 0 of each warp writes the reduced value
    if ((threadIdx.x % 32) == 0)
    {
        output[idx / 32] = value;
    }
}

// ============================================================================
// Tile (Partial) Barrier Kernel
// ============================================================================

/// <summary>
/// Demonstrates tile-based barriers for arbitrary thread subsets.
/// More flexible than warp barriers, but slightly slower (~20ns vs ~1ns).
/// </summary>
/// <param name="input">Input data array.</param>
/// <param name="output">Output data array.</param>
/// <param name="size">Array size.</param>
/// <param name="tileSize">Number of threads per tile.</param>
/// <remarks>
/// Tile barriers enable flexible synchronization patterns for irregular workloads.
/// Best performance on Volta (CC 7.0) and newer architectures.
/// </remarks>
__global__ void TileBarrierKernel(const float* input, float* output, int size, int tileSize)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;

    // Create a tile group (subset of threads)
    cg::thread_block block = cg::this_thread_block();
    cg::thread_block_tile<32> tile = cg::tiled_partition<32>(block);

    float value = input[idx];

    // Tile-level reduction
    for (int offset = tile.size() / 2; offset > 0; offset /= 2)
    {
        value += tile.shfl_down(value, offset);

        // Tile barrier: synchronize threads within tile
        tile.sync();
    }

    // First thread of each tile writes result
    if (tile.thread_rank() == 0)
    {
        output[idx / tile.size()] = value;
    }
}

// ============================================================================
// Utility: Test Barrier Correctness
// ============================================================================

/// <summary>
/// Tests barrier correctness by detecting race conditions.
/// Each thread writes to shared memory, syncs, then reads all values.
/// If barrier is broken, race conditions will cause incorrect results.
/// </summary>
/// <param name="output">Output array (1 if correct, 0 if race detected).</param>
/// <param name="size">Array size.</param>
__global__ void TestBarrierCorrectnessKernel(int* output, int size)
{
    __shared__ int sharedCounter[1024];

    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= size) return;

    // Phase 1: Each thread writes its thread ID
    sharedCounter[threadIdx.x] = threadIdx.x;

    // Barrier: CRITICAL - ensures all writes complete
    __syncthreads();

    // Phase 2: Each thread reads all values and verifies
    int isCorrect = 1;
    for (int i = 0; i < blockDim.x; i++)
    {
        if (sharedCounter[i] != i)
        {
            isCorrect = 0; // Race condition detected!
            break;
        }
    }

    output[idx] = isCorrect;
}
