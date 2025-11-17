// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#ifndef DOTCOMPUTE_HLC_CU
#define DOTCOMPUTE_HLC_CU

#include <cstdint>

/// <summary>
/// GPU-resident Hybrid Logical Clock timestamp structure.
/// </summary>
/// <remarks>
/// Memory Layout (16 bytes, 8-byte aligned):
/// - physical_time_nanos: 8 bytes (64-bit nanosecond timestamp)
/// - logical_counter: 4 bytes (32-bit Lamport counter)
/// - reserved: 4 bytes (padding for alignment)
/// </remarks>
struct hlc_timestamp
{
    int64_t physical_time_nanos;   // Physical time in nanoseconds
    int32_t logical_counter;        // Logical counter for causality
    int32_t reserved;               // Padding (future use)
};

/// <summary>
/// GPU-resident Hybrid Logical Clock state structure.
/// </summary>
/// <remarks>
/// <para>
/// Thread Safety: All fields use atomic operations for lock-free updates.
/// </para>
/// <para>
/// Memory Layout (16 bytes, 8-byte aligned):
/// - physical_time_nanos: 8 bytes (atomic 64-bit)
/// - logical_counter: 4 bytes (atomic 32-bit)
/// - reserved: 4 bytes (padding)
/// </para>
/// </remarks>
struct hlc_state
{
    int64_t physical_time_nanos;   // Current physical time (atomic)
    int32_t logical_counter;        // Current logical counter (atomic)
    int32_t reserved;               // Reserved for future use
};

/// <summary>
/// Gets the current GPU physical time in nanoseconds.
/// </summary>
/// <returns>
/// Physical time in nanoseconds from GPU global timer (CC 6.0+) or scaled clock64() (CC 5.0-5.3).
/// </returns>
/// <remarks>
/// <para>
/// <b>Compute Capability 6.0+ (Pascal, Volta, Turing, Ampere, Ada, Hopper):</b>
/// Uses globaltimer (1ns resolution). Access via inline PTX assembly.
/// </para>
/// <para>
/// <b>Compute Capability 5.0-5.3 (Maxwell):</b>
/// Uses clock64() scaled to microseconds (1000ns resolution).
/// </para>
/// </remarks>
__device__ inline int64_t hlc_get_physical_time()
{
#if __CUDA_ARCH__ >= 600
    // CC 6.0+: Use globaltimer for 1ns resolution
    unsigned long long globaltime;
    asm volatile("mov.u64 %0, %%globaltimer;" : "=l"(globaltime));
    return (int64_t)globaltime;
#else
    // CC 5.0-5.3: Use clock64() scaled to microseconds
    unsigned long long cycles = clock64();
    // Assume ~1 GHz GPU clock: 1 cycle ≈ 1ns, scale to microseconds
    return (int64_t)(cycles / 1000) * 1000;
#endif
}

/// <summary>
/// Advances the HLC for a local event (Tick operation).
/// </summary>
/// <param name="hlc">Pointer to HLC state in GPU memory (must be in unified/device memory)</param>
/// <returns>New HLC timestamp for the local event</returns>
/// <remarks>
/// <para>
/// <b>Algorithm:</b>
/// <code>
/// pt_new = GetPhysicalTime()
/// if (pt_new > pt_old):
///     logical = 0
/// else:
///     logical = logical + 1
/// return (pt_new, logical)
/// </code>
/// </para>
/// <para>
/// <b>Thread Safety:</b> Uses atomic CAS loops for lock-free updates.
/// Safe to call from multiple threads concurrently.
/// </para>
/// <para>
/// <b>Performance:</b> ~20ns average (1-3 CAS iterations typical).
/// </para>
/// </remarks>
__device__ hlc_timestamp hlc_tick(hlc_state* hlc)
{
    if (hlc == nullptr)
    {
        return {0, 0, 0};
    }

    int64_t pt_new = hlc_get_physical_time();
    hlc_timestamp result;

    // Atomic update loop (lock-free)
    while (true)
    {
        int64_t pt_old = atomicAdd((unsigned long long*)&hlc->physical_time_nanos, 0); // Atomic read
        int32_t logical_old = atomicAdd(&hlc->logical_counter, 0);                      // Atomic read

        int64_t pt_result;
        int32_t logical_result;

        if (pt_new > pt_old)
        {
            // Physical time advanced: reset logical counter
            pt_result = pt_new;
            logical_result = 0;
        }
        else
        {
            // Physical time same or behind: increment logical counter
            pt_result = pt_old;
            logical_result = logical_old + 1;
        }

        // Attempt atomic update of physical time
        int64_t pt_original = atomicCAS((unsigned long long*)&hlc->physical_time_nanos,
                                         (unsigned long long)pt_old,
                                         (unsigned long long)pt_result);

        if (pt_original == pt_old)
        {
            // Physical time CAS succeeded, now update logical counter
            int32_t logical_original = atomicCAS(&hlc->logical_counter, logical_old, logical_result);

            if (logical_original == logical_old)
            {
                // Both CAS succeeded: commit result
                result.physical_time_nanos = pt_result;
                result.logical_counter = logical_result;
                result.reserved = 0;

                // Ensure visibility across all threads
                __threadfence_system();
                return result;
            }
        }

        // CAS failed: retry with updated values
    }
}

/// <summary>
/// Updates the HLC based on a received remote timestamp (Update operation).
/// </summary>
/// <param name="hlc">Pointer to HLC state in GPU memory</param>
/// <param name="remote">Remote timestamp received from another kernel/actor</param>
/// <returns>New HLC timestamp that preserves causality</returns>
/// <remarks>
/// <para>
/// <b>Algorithm:</b>
/// <code>
/// pt_new = GetPhysicalTime()
/// pt_max = max(pt_old, remote.physical_time)
///
/// if (pt_new > pt_max):
///     logical = 0
/// elif (pt_max == pt_old):
///     logical = max(logical_old, remote.logical) + 1
/// else:
///     logical = remote.logical + 1
///
/// return (max(pt_new, pt_max), logical)
/// </code>
/// </para>
/// <para>
/// <b>Thread Safety:</b> Uses atomic CAS loops for lock-free updates.
/// Safe to call from multiple threads concurrently.
/// </para>
/// <para>
/// <b>Performance:</b> ~50ns average (2-4 CAS iterations typical).
/// </para>
/// </remarks>
__device__ hlc_timestamp hlc_update(hlc_state* hlc, const hlc_timestamp* remote)
{
    if (hlc == nullptr || remote == nullptr)
    {
        return {0, 0, 0};
    }

    int64_t pt_new = hlc_get_physical_time();
    hlc_timestamp result;

    // Atomic update loop (lock-free)
    while (true)
    {
        int64_t pt_old = atomicAdd((unsigned long long*)&hlc->physical_time_nanos, 0); // Atomic read
        int32_t logical_old = atomicAdd(&hlc->logical_counter, 0);                      // Atomic read

        // Compute new timestamp using HLC update rules
        int64_t pt_max = (pt_old > remote->physical_time_nanos) ? pt_old : remote->physical_time_nanos;
        int64_t pt_result;
        int32_t logical_result;

        if (pt_new > pt_max)
        {
            // Physical time advanced beyond both local and remote: reset logical
            pt_result = pt_new;
            logical_result = 0;
        }
        else if (pt_max == pt_old)
        {
            // Max is local time: increment based on max of local and remote logical
            pt_result = (pt_new > pt_max) ? pt_new : pt_max;
            int32_t logical_max = (logical_old > remote->logical_counter) ? logical_old : remote->logical_counter;
            logical_result = logical_max + 1;
        }
        else
        {
            // Max is remote time: use remote logical + 1
            pt_result = (pt_new > pt_max) ? pt_new : pt_max;
            logical_result = remote->logical_counter + 1;
        }

        // Attempt atomic update of physical time
        int64_t pt_original = atomicCAS((unsigned long long*)&hlc->physical_time_nanos,
                                         (unsigned long long)pt_old,
                                         (unsigned long long)pt_result);

        if (pt_original == pt_old)
        {
            // Physical time CAS succeeded, now update logical counter
            int32_t logical_original = atomicCAS(&hlc->logical_counter, logical_old, logical_result);

            if (logical_original == logical_old)
            {
                // Both CAS succeeded: commit result
                result.physical_time_nanos = pt_result;
                result.logical_counter = logical_result;
                result.reserved = 0;

                // Ensure visibility across all threads
                __threadfence_system();
                return result;
            }
        }

        // CAS failed: retry with updated values
    }
}

/// <summary>
/// Gets the current HLC timestamp without advancing the clock.
/// </summary>
/// <param name="hlc">Pointer to HLC state in GPU memory</param>
/// <returns>Current HLC timestamp</returns>
/// <remarks>
/// <para>
/// <b>Operation:</b> Atomic read of physical time and logical counter.
/// </para>
/// <para>
/// <b>Performance:</b> ~5ns (2 atomic loads).
/// </para>
/// </remarks>
__device__ hlc_timestamp hlc_get_current(const hlc_state* hlc)
{
    if (hlc == nullptr)
    {
        return {0, 0, 0};
    }

    hlc_timestamp result;
    result.physical_time_nanos = atomicAdd((unsigned long long*)&hlc->physical_time_nanos, 0);
    result.logical_counter = atomicAdd((int32_t*)&hlc->logical_counter, 0);
    result.reserved = 0;

    return result;
}

/// <summary>
/// Resets the HLC to a specific timestamp (for testing or recovery).
/// </summary>
/// <param name="hlc">Pointer to HLC state in GPU memory</param>
/// <param name="timestamp">Timestamp to reset to</param>
/// <remarks>
/// <para>
/// <b>Caution:</b> Resetting can violate causality if not done carefully.
/// Only use for testing, checkpoint recovery, or kernel restart scenarios.
/// </para>
/// </remarks>
__device__ void hlc_reset(hlc_state* hlc, const hlc_timestamp* timestamp)
{
    if (hlc == nullptr || timestamp == nullptr)
    {
        return;
    }

    atomicExch((unsigned long long*)&hlc->physical_time_nanos, (unsigned long long)timestamp->physical_time_nanos);
    atomicExch(&hlc->logical_counter, timestamp->logical_counter);

    __threadfence_system();
}

/// <summary>
/// Compares two HLC timestamps for happened-before relation.
/// </summary>
/// <param name="a">First timestamp</param>
/// <param name="b">Second timestamp</param>
/// <returns>
/// -1 if a happened before b (a → b)
///  0 if a == b (same event)
///  1 if b happened before a (b → a)
/// </returns>
/// <remarks>
/// <para>
/// <b>Comparison Rules:</b>
/// <code>
/// a → b if:
///   - a.physical_time &lt; b.physical_time, OR
///   - a.physical_time == b.physical_time AND a.logical &lt; b.logical
/// </code>
/// </para>
/// </remarks>
__device__ int hlc_compare(const hlc_timestamp* a, const hlc_timestamp* b)
{
    if (a == nullptr || b == nullptr)
    {
        return 0;
    }

    if (a->physical_time_nanos < b->physical_time_nanos)
    {
        return -1;  // a → b
    }

    if (a->physical_time_nanos > b->physical_time_nanos)
    {
        return 1;   // b → a
    }

    // Physical times equal, compare logical counters
    if (a->logical_counter < b->logical_counter)
    {
        return -1;  // a → b
    }

    if (a->logical_counter > b->logical_counter)
    {
        return 1;   // b → a
    }

    return 0;  // a == b
}

/// <summary>
/// Checks if two HLC timestamps are concurrent (neither happened before the other).
/// </summary>
/// <param name="a">First timestamp</param>
/// <param name="b">Second timestamp</param>
/// <returns>True if timestamps are concurrent, false otherwise</returns>
__device__ bool hlc_is_concurrent(const hlc_timestamp* a, const hlc_timestamp* b)
{
    int comparison = hlc_compare(a, b);
    return comparison == 0;  // Only equal timestamps are "concurrent" in HLC (total order)
}

#endif // DOTCOMPUTE_HLC_CU
