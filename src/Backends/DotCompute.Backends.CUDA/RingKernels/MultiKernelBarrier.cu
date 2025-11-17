// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#ifndef DOTCOMPUTE_MULTI_KERNEL_BARRIER_CU
#define DOTCOMPUTE_MULTI_KERNEL_BARRIER_CU

#include <cstdint>

/// <summary>
/// Multi-kernel barrier for synchronizing persistent Ring Kernels across GPU.
/// </summary>
/// <remarks>
/// Memory Layout (16 bytes, 4-byte aligned):
/// - participant_count: 4 bytes
/// - arrived_count: 4 bytes (atomic)
/// - generation: 4 bytes (atomic)
/// - flags: 4 bytes (atomic)
/// </remarks>
struct multi_kernel_barrier
{
    /// <summary>
    /// Number of kernels that must arrive at barrier (1-65535).
    /// </summary>
    int32_t participant_count;

    /// <summary>
    /// Atomic counter for arrived kernels (0 to participant_count).
    /// </summary>
    int32_t arrived_count;

    /// <summary>
    /// Barrier generation number (incremented after each barrier completion).
    /// </summary>
    int32_t generation;

    /// <summary>
    /// Barrier state flags (active, timeout, failed).
    /// </summary>
    int32_t flags;
};

/// <summary>
/// Barrier flag: Active and in use.
/// </summary>
#define BARRIER_FLAG_ACTIVE 0x0001

/// <summary>
/// Barrier flag: Operation timed out.
/// </summary>
#define BARRIER_FLAG_TIMEOUT 0x0002

/// <summary>
/// Barrier flag: Operation failed (participant crashed).
/// </summary>
#define BARRIER_FLAG_FAILED 0x0004

/// <summary>
/// Waits at a multi-kernel barrier until all participants arrive.
/// </summary>
/// <param name="barrier">Pointer to multi-kernel barrier in GPU memory</param>
/// <param name="timeout_cycles">Timeout in clock cycles (0 = no timeout)</param>
/// <returns>True if barrier completed successfully, false if timed out or failed</returns>
/// <remarks>
/// <para>
/// <b>Barrier Protocol:</b>
/// 1. Record current generation number
/// 2. Atomically increment arrival count
/// 3. If last to arrive: reset counter, increment generation, return
/// 4. Otherwise: spin-wait for generation change with optional timeout
/// </para>
/// <para>
/// <b>Performance Characteristics:</b>
/// - Typical latency: 10-100μs for 2-8 kernels
/// - Scales linearly with participant count
/// - Timeout detection: ~1μs granularity using clock64()
/// - Power-efficient: uses __nanosleep(100) to reduce GPU utilization
/// </para>
/// <para>
/// <b>Thread Safety:</b>
/// - Safe to call from any thread in participating kernels
/// - All threads in a block should arrive together (use __syncthreads() first)
/// - Uses __threadfence_system() for cross-kernel visibility
/// </para>
/// <para>
/// <b>Timeout Behavior:</b>
/// - If timeout_cycles > 0, wait up to specified cycles
/// - On timeout: sets BARRIER_FLAG_TIMEOUT and returns false
/// - Non-timed-out kernels may continue waiting or check flag
/// </para>
/// </remarks>
__device__ bool wait_at_multi_kernel_barrier(
    multi_kernel_barrier* barrier,
    int64_t timeout_cycles = 0)
{
    // Validate barrier pointer
    if (barrier == nullptr)
    {
        return false;
    }

    // Validate participant count
    if (barrier->participant_count <= 0)
    {
        return false;
    }

    // Record arrival generation (prevents ABA problem)
    int32_t my_generation = barrier->generation;

    // Atomically increment arrival count
    int32_t arrived = atomicAdd(&barrier->arrived_count, 1) + 1;

    // Last kernel to arrive resets for next barrier
    if (arrived == barrier->participant_count)
    {
        // Reset arrival counter for next barrier
        atomicExch(&barrier->arrived_count, 0);

        // Increment generation to release waiting kernels
        atomicAdd(&barrier->generation, 1);

        // Ensure visibility to all kernels (system-wide fence)
        __threadfence_system();

        return true; // Barrier completed
    }

    // Check if barrier already failed or timed out
    if ((barrier->flags & (BARRIER_FLAG_TIMEOUT | BARRIER_FLAG_FAILED)) != 0)
    {
        return false;
    }

    // Spin-wait for barrier completion (generation change)
    int64_t start_cycle = (timeout_cycles > 0) ? clock64() : 0;

    while (barrier->generation == my_generation)
    {
        // Check timeout if enabled
        if (timeout_cycles > 0)
        {
            int64_t elapsed = clock64() - start_cycle;
            if (elapsed > timeout_cycles)
            {
                // Timeout - mark barrier as failed
                atomicOr(&barrier->flags, BARRIER_FLAG_TIMEOUT);
                return false;
            }
        }

        // Check if barrier failed
        if ((barrier->flags & (BARRIER_FLAG_TIMEOUT | BARRIER_FLAG_FAILED)) != 0)
        {
            return false;
        }

        // Sleep 100ns to reduce power consumption during spin-wait
        // (available on Compute Capability 7.0+, no-op on older GPUs)
        __nanosleep(100);
    }

    return true; // Barrier completed successfully
}

/// <summary>
/// Resets a multi-kernel barrier to initial state.
/// </summary>
/// <param name="barrier">Pointer to multi-kernel barrier in GPU memory</param>
/// <remarks>
/// <b>Usage:</b> Call from host or designated kernel thread to reset barrier after timeout/failure.
/// <b>Warning:</b> Not safe to call while kernels are waiting at barrier.
/// </remarks>
__device__ void reset_multi_kernel_barrier(multi_kernel_barrier* barrier)
{
    if (barrier == nullptr)
    {
        return;
    }

    atomicExch(&barrier->arrived_count, 0);
    atomicAnd(&barrier->flags, ~(BARRIER_FLAG_TIMEOUT | BARRIER_FLAG_FAILED));
    __threadfence_system();
}

/// <summary>
/// Checks if a multi-kernel barrier has timed out.
/// </summary>
/// <param name="barrier">Pointer to multi-kernel barrier in GPU memory</param>
/// <returns>True if barrier timed out, false otherwise</returns>
__device__ bool is_barrier_timed_out(const multi_kernel_barrier* barrier)
{
    return (barrier->flags & BARRIER_FLAG_TIMEOUT) != 0;
}

/// <summary>
/// Checks if a multi-kernel barrier has failed.
/// </summary>
/// <param name="barrier">Pointer to multi-kernel barrier in GPU memory</param>
/// <returns>True if barrier failed, false otherwise</returns>
__device__ bool is_barrier_failed(const multi_kernel_barrier* barrier)
{
    return (barrier->flags & BARRIER_FLAG_FAILED) != 0;
}

/// <summary>
/// Marks a multi-kernel barrier as failed (e.g., participant crashed).
/// </summary>
/// <param name="barrier">Pointer to multi-kernel barrier in GPU memory</param>
/// <remarks>
/// Sets BARRIER_FLAG_FAILED to release waiting kernels.
/// Use when a kernel detects that a participant has crashed or disconnected.
/// </remarks>
__device__ void mark_barrier_failed(multi_kernel_barrier* barrier)
{
    if (barrier == nullptr)
    {
        return;
    }

    atomicOr(&barrier->flags, BARRIER_FLAG_FAILED);
    __threadfence_system();
}

/// <summary>
/// Gets the current generation number of a multi-kernel barrier.
/// </summary>
/// <param name="barrier">Pointer to multi-kernel barrier in GPU memory</param>
/// <returns>Current barrier generation number</returns>
/// <remarks>
/// Useful for detecting barrier progress without waiting.
/// Generation increments by 1 after each successful barrier completion.
/// </remarks>
__device__ int32_t get_barrier_generation(const multi_kernel_barrier* barrier)
{
    return barrier->generation;
}

/// <summary>
/// Gets the current arrival count of a multi-kernel barrier.
/// </summary>
/// <param name="barrier">Pointer to multi-kernel barrier in GPU memory</param>
/// <returns>Number of kernels currently arrived (0 to participant_count)</returns>
/// <remarks>
/// Useful for monitoring barrier progress.
/// Count resets to 0 when last participant arrives.
/// </remarks>
__device__ int32_t get_barrier_arrived_count(const multi_kernel_barrier* barrier)
{
    return barrier->arrived_count;
}

#endif // DOTCOMPUTE_MULTI_KERNEL_BARRIER_CU
