// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#ifndef DOTCOMPUTE_KERNEL_HEALTH_CU
#define DOTCOMPUTE_KERNEL_HEALTH_CU

#include <cstdint>

/// <summary>
/// Kernel health state enumeration.
/// </summary>
enum kernel_state : int32_t
{
    KERNEL_STATE_HEALTHY = 0,
    KERNEL_STATE_DEGRADED = 1,
    KERNEL_STATE_FAILED = 2,
    KERNEL_STATE_RECOVERING = 3,
    KERNEL_STATE_STOPPED = 4
};

/// <summary>
/// Health monitoring data for a Ring Kernel (GPU-resident).
/// </summary>
/// <remarks>
/// Memory Layout (32 bytes, 8-byte aligned):
/// - last_heartbeat_ticks: 8 bytes (atomic)
/// - failed_heartbeats: 4 bytes
/// - error_count: 4 bytes (atomic)
/// - state: 4 bytes (atomic)
/// - last_checkpoint_id: 8 bytes
/// - reserved: 4 bytes
/// </remarks>
struct kernel_health_status
{
    int64_t last_heartbeat_ticks;   // Last heartbeat timestamp (100-nanosecond ticks)
    int32_t failed_heartbeats;      // Consecutive failed heartbeats
    int32_t error_count;            // Total errors encountered
    int32_t state;                  // Current kernel state
    int64_t last_checkpoint_id;     // Last checkpoint identifier
    int32_t reserved;               // Reserved for future use
};

/// <summary>
/// Updates the heartbeat timestamp with current GPU clock time.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <remarks>
/// <para>
/// <b>Operation:</b> Atomically updates last_heartbeat_ticks with current time.
/// <b>Frequency:</b> Should be called every ~100ms by kernel.
/// <b>Thread Safety:</b> Uses atomic exchange for thread-safe updates.
/// </para>
/// <para>
/// <b>Timestamp Source:</b>
/// - Uses clock64() for GPU timestamp
/// - Note: GPU clock64() returns cycles, not .NET ticks
/// - Host should calibrate GPU cycles to .NET ticks
/// </para>
/// </remarks>
__device__ void update_heartbeat(kernel_health_status* health)
{
    if (health == nullptr)
    {
        return;
    }

    // Get current GPU timestamp (cycles)
    int64_t current_cycles = (int64_t)clock64();

    // Atomically update heartbeat timestamp
    atomicExch((unsigned long long*)&health->last_heartbeat_ticks, (unsigned long long)current_cycles);

    // Ensure visibility across all kernels
    __threadfence_system();
}

/// <summary>
/// Records an error in the kernel health status.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <remarks>
/// <para>
/// <b>Operation:</b> Atomically increments error count.
/// <b>State Transition:</b> If error count exceeds threshold (10), marks kernel as degraded.
/// </para>
/// </remarks>
__device__ void record_error(kernel_health_status* health)
{
    if (health == nullptr)
    {
        return;
    }

    // Atomically increment error count
    int32_t new_error_count = atomicAdd(&health->error_count, 1) + 1;

    // Check error threshold
    const int32_t ERROR_THRESHOLD = 10;
    if (new_error_count > ERROR_THRESHOLD)
    {
        // Mark kernel as degraded
        atomicCAS(&health->state, KERNEL_STATE_HEALTHY, KERNEL_STATE_DEGRADED);
    }

    __threadfence_system();
}

/// <summary>
/// Marks kernel as failed with specified reason.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <remarks>
/// <para>
/// <b>Operation:</b> Atomically updates state to FAILED.
/// <b>Usage:</b> Called when kernel detects unrecoverable error.
/// </para>
/// </remarks>
__device__ void mark_kernel_failed(kernel_health_status* health)
{
    if (health == nullptr)
    {
        return;
    }

    // Atomically set state to failed
    atomicExch(&health->state, KERNEL_STATE_FAILED);

    __threadfence_system();
}

/// <summary>
/// Updates checkpoint ID after creating state snapshot.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <param name="checkpoint_id">Checkpoint identifier</param>
/// <remarks>
/// <para>
/// <b>Operation:</b> Atomically updates last checkpoint ID.
/// <b>Usage:</b> Called after kernel creates checkpoint snapshot.
/// </para>
/// </remarks>
__device__ void update_checkpoint(kernel_health_status* health, int64_t checkpoint_id)
{
    if (health == nullptr)
    {
        return;
    }

    // Atomically update checkpoint ID
    atomicExch((unsigned long long*)&health->last_checkpoint_id, (unsigned long long)checkpoint_id);

    __threadfence_system();
}

/// <summary>
/// Resets error count and marks kernel as healthy (recovery complete).
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <remarks>
/// <para>
/// <b>Operation:</b> Resets error count and sets state to HEALTHY.
/// <b>Usage:</b> Called after successful kernel recovery.
/// </para>
/// </remarks>
__device__ void reset_health_status(kernel_health_status* health)
{
    if (health == nullptr)
    {
        return;
    }

    // Reset error count
    atomicExch(&health->error_count, 0);

    // Reset failed heartbeats
    atomicExch(&health->failed_heartbeats, 0);

    // Mark as healthy
    atomicExch(&health->state, KERNEL_STATE_HEALTHY);

    // Update heartbeat
    int64_t current_cycles = (int64_t)clock64();
    atomicExch((unsigned long long*)&health->last_heartbeat_ticks, (unsigned long long)current_cycles);

    __threadfence_system();
}

/// <summary>
/// Checks if kernel is in healthy state.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <returns>True if state is HEALTHY, false otherwise</returns>
__device__ bool is_kernel_healthy(const kernel_health_status* health)
{
    if (health == nullptr)
    {
        return false;
    }

    return health->state == KERNEL_STATE_HEALTHY;
}

/// <summary>
/// Checks if kernel is degraded.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <returns>True if state is DEGRADED, false otherwise</returns>
__device__ bool is_kernel_degraded(const kernel_health_status* health)
{
    if (health == nullptr)
    {
        return false;
    }

    return health->state == KERNEL_STATE_DEGRADED;
}

/// <summary>
/// Checks if kernel has failed.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <returns>True if state is FAILED, false otherwise</returns>
__device__ bool is_kernel_failed(const kernel_health_status* health)
{
    if (health == nullptr)
    {
        return true;  // Null health status = failed
    }

    return health->state == KERNEL_STATE_FAILED;
}

/// <summary>
/// Gets current error count.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <returns>Current error count</returns>
__device__ int32_t get_error_count(const kernel_health_status* health)
{
    if (health == nullptr)
    {
        return -1;
    }

    return health->error_count;
}

/// <summary>
/// Gets last heartbeat timestamp.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <returns>Last heartbeat timestamp (GPU cycles)</returns>
__device__ int64_t get_last_heartbeat(const kernel_health_status* health)
{
    if (health == nullptr)
    {
        return -1;
    }

    return health->last_heartbeat_ticks;
}

/// <summary>
/// Gets last checkpoint ID.
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <returns>Last checkpoint ID (0 if no checkpoint exists)</returns>
__device__ int64_t get_last_checkpoint_id(const kernel_health_status* health)
{
    if (health == nullptr)
    {
        return 0;
    }

    return health->last_checkpoint_id;
}

/// <summary>
/// Marks kernel as recovering (host-initiated recovery).
/// </summary>
/// <param name="health">Pointer to kernel health status in GPU memory</param>
/// <remarks>
/// <para>
/// <b>Operation:</b> Sets state to RECOVERING.
/// <b>Usage:</b> Called by host before starting recovery process.
/// </para>
/// </remarks>
__device__ void mark_kernel_recovering(kernel_health_status* health)
{
    if (health == nullptr)
    {
        return;
    }

    atomicExch(&health->state, KERNEL_STATE_RECOVERING);
    __threadfence_system();
}

#endif // DOTCOMPUTE_KERNEL_HEALTH_CU
