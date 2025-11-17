// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Kernel health state enumeration.
/// </summary>
/// <remarks>
/// Represents the current health status of a Ring Kernel for monitoring and recovery.
/// </remarks>
public enum KernelState : int
{
    /// <summary>
    /// Kernel is healthy and operating normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Kernel is degraded (experiencing errors but still functional).
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// Kernel has failed and requires recovery.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// Kernel is in recovery mode.
    /// </summary>
    Recovering = 3,

    /// <summary>
    /// Kernel has been stopped intentionally.
    /// </summary>
    Stopped = 4
}

/// <summary>
/// Health monitoring data for a Ring Kernel (GPU-resident).
/// </summary>
/// <remarks>
/// <para>
/// Enables automatic failure detection and recovery for persistent Ring Kernels.
/// Kernels periodically update heartbeat timestamps, and host monitors for stale data.
/// </para>
/// <para>
/// <b>Memory Layout (32 bytes, 8-byte aligned):</b>
/// - LastHeartbeatTicks: 8 bytes (atomic timestamp)
/// - FailedHeartbeats: 4 bytes (consecutive failures)
/// - ErrorCount: 4 bytes (total errors)
/// - State: 4 bytes (kernel health state)
/// - LastCheckpointId: 8 bytes (checkpoint identifier)
/// - Reserved: 4 bytes (padding/future use)
/// </para>
/// <para>
/// <b>Failure Detection:</b>
/// 1. Heartbeat Monitoring: Each kernel updates timestamp periodically (~100ms)
/// 2. Timeout Detection: Host checks for stale timestamps (>5 seconds)
/// 3. Error Threshold: Host monitors error count (>10 errors triggers failure)
/// </para>
/// <para>
/// <b>Recovery Strategies:</b>
/// - Checkpoint/Restore: Periodic state snapshots for recovery
/// - Message Replay: Re-send messages from last checkpoint
/// - Kernel Restart: Relaunch failed kernel with restored state
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
public struct KernelHealthStatus : IEquatable<KernelHealthStatus>
{
    /// <summary>
    /// Last heartbeat timestamp in ticks (100-nanosecond intervals since epoch).
    /// </summary>
    /// <remarks>
    /// Updated atomically by kernel every ~100ms.
    /// Host compares with current time to detect stale kernels.
    /// Uses DateTime.UtcNow.Ticks for consistency with host.
    /// </remarks>
    public long LastHeartbeatTicks;

    /// <summary>
    /// Number of consecutive failed heartbeats.
    /// </summary>
    /// <remarks>
    /// Incremented by host when heartbeat is stale.
    /// Reset to 0 when valid heartbeat received.
    /// Threshold: 3 failed heartbeats triggers degraded state.
    /// </remarks>
    public int FailedHeartbeats;

    /// <summary>
    /// Total number of errors encountered by kernel.
    /// </summary>
    /// <remarks>
    /// Incremented atomically by kernel on errors.
    /// Threshold: >10 errors triggers kernel failure.
    /// Includes message processing errors, memory errors, etc.
    /// </remarks>
    public int ErrorCount;

    /// <summary>
    /// Current kernel health state.
    /// </summary>
    /// <remarks>
    /// Modified by both kernel (self-diagnosis) and host (monitoring).
    /// State transitions: Healthy → Degraded → Failed → Recovering → Healthy
    /// </remarks>
    public int State;

    /// <summary>
    /// Last checkpoint identifier (for recovery).
    /// </summary>
    /// <remarks>
    /// Updated when kernel creates checkpoint snapshot.
    /// Used to identify which state to restore during recovery.
    /// 0 = no checkpoint exists.
    /// </remarks>
    public long LastCheckpointId;

    /// <summary>
    /// Reserved for future extensions.
    /// </summary>
    private int _reserved;

    /// <summary>
    /// Creates an uninitialized health status (all fields zero).
    /// </summary>
    /// <returns>Empty health status suitable for GPU allocation.</returns>
    public static KernelHealthStatus CreateEmpty()
    {
        return new KernelHealthStatus
        {
            LastHeartbeatTicks = 0,
            FailedHeartbeats = 0,
            ErrorCount = 0,
            State = (int)KernelState.Healthy,
            LastCheckpointId = 0,
            _reserved = 0
        };
    }

    /// <summary>
    /// Creates a health status initialized with current timestamp.
    /// </summary>
    /// <returns>Health status with current heartbeat timestamp.</returns>
    public static KernelHealthStatus CreateInitialized()
    {
        return new KernelHealthStatus
        {
            LastHeartbeatTicks = DateTime.UtcNow.Ticks,
            FailedHeartbeats = 0,
            ErrorCount = 0,
            State = (int)KernelState.Healthy,
            LastCheckpointId = 0,
            _reserved = 0
        };
    }

    /// <summary>
    /// Checks if heartbeat is stale (older than specified timeout).
    /// </summary>
    /// <param name="timeout">Timeout duration.</param>
    /// <returns>True if heartbeat is stale, false otherwise.</returns>
    public readonly bool IsHeartbeatStale(TimeSpan timeout)
    {
        long currentTicks = DateTime.UtcNow.Ticks;
        long elapsedTicks = currentTicks - LastHeartbeatTicks;
        return elapsedTicks > timeout.Ticks;
    }

    /// <summary>
    /// Gets the time since last heartbeat.
    /// </summary>
    /// <returns>TimeSpan since last heartbeat.</returns>
    public readonly TimeSpan TimeSinceLastHeartbeat()
    {
        long currentTicks = DateTime.UtcNow.Ticks;
        long elapsedTicks = currentTicks - LastHeartbeatTicks;
        return TimeSpan.FromTicks(elapsedTicks);
    }

    /// <summary>
    /// Checks if kernel is in healthy state.
    /// </summary>
    /// <returns>True if state is Healthy, false otherwise.</returns>
    public readonly bool IsHealthy()
    {
        return State == (int)KernelState.Healthy;
    }

    /// <summary>
    /// Checks if kernel is degraded.
    /// </summary>
    /// <returns>True if state is Degraded, false otherwise.</returns>
    public readonly bool IsDegraded()
    {
        return State == (int)KernelState.Degraded;
    }

    /// <summary>
    /// Checks if kernel has failed.
    /// </summary>
    /// <returns>True if state is Failed, false otherwise.</returns>
    public readonly bool IsFailed()
    {
        return State == (int)KernelState.Failed;
    }

    /// <summary>
    /// Checks if kernel is in recovery mode.
    /// </summary>
    /// <returns>True if state is Recovering, false otherwise.</returns>
    public readonly bool IsRecovering()
    {
        return State == (int)KernelState.Recovering;
    }

    /// <summary>
    /// Validates the health status structure for correctness.
    /// </summary>
    /// <returns>True if valid, false if any invariant is violated.</returns>
    /// <remarks>
    /// Checks:
    /// - LastHeartbeatTicks is non-negative
    /// - FailedHeartbeats is non-negative
    /// - ErrorCount is non-negative
    /// - State is valid KernelState value
    /// - LastCheckpointId is non-negative
    /// </remarks>
    public readonly bool Validate()
    {
        if (LastHeartbeatTicks < 0)
        {
            return false;
        }

        if (FailedHeartbeats < 0)
        {
            return false;
        }

        if (ErrorCount < 0)
        {
            return false;
        }

        if (State < (int)KernelState.Healthy || State > (int)KernelState.Stopped)
        {
            return false;
        }

        if (LastCheckpointId < 0)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public readonly bool Equals(KernelHealthStatus other)
    {
        return LastHeartbeatTicks == other.LastHeartbeatTicks &&
               FailedHeartbeats == other.FailedHeartbeats &&
               ErrorCount == other.ErrorCount &&
               State == other.State &&
               LastCheckpointId == other.LastCheckpointId;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is KernelHealthStatus other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(LastHeartbeatTicks, FailedHeartbeats, ErrorCount, State, LastCheckpointId);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(KernelHealthStatus left, KernelHealthStatus right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(KernelHealthStatus left, KernelHealthStatus right)
    {
        return !(left == right);
    }
}
