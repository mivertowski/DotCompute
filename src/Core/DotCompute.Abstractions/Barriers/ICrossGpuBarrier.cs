// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Temporal;

namespace DotCompute.Abstractions.Barriers;

/// <summary>
/// Cross-GPU barrier for synchronizing multiple devices in distributed Ring Kernel systems.
/// Enables sub-10μs multi-device coordination with HLC-based temporal ordering.
/// </summary>
/// <remarks>
/// <para>
/// Cross-GPU barriers provide three synchronization modes:
/// </para>
/// <list type="bullet">
/// <item><b>P2P Memory Mode:</b> GPU-GPU direct signaling via peer-to-peer memory writes (fastest)</item>
/// <item><b>CUDA Event Mode:</b> Event-based synchronization using cudaEventWaitExternal</item>
/// <item><b>CPU Fallback Mode:</b> Host-mediated synchronization for non-P2P capable systems</item>
/// </list>
///
/// <para><b>Performance Targets:</b></para>
/// <list type="bullet">
/// <item>P2P Mode: &lt;2μs (direct GPU memory writes)</item>
/// <item>Event Mode: &lt;5μs (CUDA event synchronization)</item>
/// <item>CPU Mode: &lt;50μs (host-mediated roundtrip)</item>
/// </list>
///
/// <para><b>Integration with HLC:</b></para>
/// <para>
/// Each barrier arrival is timestamped with HLC to enable:
/// </para>
/// <list type="bullet">
/// <item>Causal analysis of synchronization patterns</item>
/// <item>Distributed debugging of barrier deadlocks</item>
/// <item>Timeout detection with happened-before relationships</item>
/// </list>
/// </remarks>
public interface ICrossGpuBarrier : IDisposable
{
    /// <summary>
    /// Gets the unique identifier for this barrier.
    /// </summary>
    public string BarrierId { get; }

    /// <summary>
    /// Gets the number of participating GPUs in this barrier.
    /// </summary>
    public int ParticipantCount { get; }

    /// <summary>
    /// Gets the synchronization mode used by this barrier.
    /// </summary>
    public CrossGpuBarrierMode Mode { get; }

    /// <summary>
    /// Arrives at the barrier from the specified GPU and waits for all participants.
    /// </summary>
    /// <param name="gpuId">GPU device ID (0-based index).</param>
    /// <param name="arrivalTimestamp">HLC timestamp of arrival for causality tracking.</param>
    /// <param name="timeout">Maximum wait time before timing out.</param>
    /// <param name="cancellationToken">Cancellation token for aborting wait.</param>
    /// <returns>
    /// Barrier result containing:
    /// - Success/timeout/failure status
    /// - Release timestamp (max HLC of all arrivals)
    /// - Arrival timestamps from all participants
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="gpuId"/> is invalid.</exception>
    /// <exception cref="BarrierTimeoutException">Thrown if barrier times out.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if barrier has been disposed.</exception>
    public Task<CrossGpuBarrierResult> ArriveAndWaitAsync(
        int gpuId,
        HlcTimestamp arrivalTimestamp,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Arrives at the barrier without waiting (split-phase barrier).
    /// </summary>
    /// <param name="gpuId">GPU device ID.</param>
    /// <param name="arrivalTimestamp">HLC timestamp of arrival.</param>
    /// <returns>Barrier phase token for later wait operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="gpuId"/> is invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if barrier has been disposed.</exception>
    public Task<CrossGpuBarrierPhase> ArriveAsync(int gpuId, HlcTimestamp arrivalTimestamp);

    /// <summary>
    /// Waits for barrier completion using a phase token from prior arrival.
    /// </summary>
    /// <param name="phase">Phase token from <see cref="ArriveAsync"/>.</param>
    /// <param name="timeout">Maximum wait time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Barrier result with release timestamp and participant arrivals.</returns>
    /// <exception cref="BarrierTimeoutException">Thrown if barrier times out.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if barrier has been disposed.</exception>
    public Task<CrossGpuBarrierResult> WaitAsync(
        CrossGpuBarrierPhase phase,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current barrier status without blocking.
    /// </summary>
    /// <returns>Status including arrival count and HLC timestamps.</returns>
    public CrossGpuBarrierStatus GetStatus();

    /// <summary>
    /// Resets the barrier for reuse (increments generation counter).
    /// </summary>
    /// <remarks>
    /// <para>
    /// All participants must have completed the current barrier phase before resetting.
    /// Reset increments the generation counter to prevent ABA problems in wait loops.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if barrier is still in use.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if barrier has been disposed.</exception>
    public Task ResetAsync();
}

/// <summary>
/// Synchronization mode for cross-GPU barriers.
/// </summary>
public enum CrossGpuBarrierMode
{
    /// <summary>
    /// Automatic mode selection based on device capabilities.
    /// Prefers P2P &gt; Event &gt; CPU fallback.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Peer-to-peer memory writes for direct GPU-GPU signaling.
    /// Requires P2P access enabled between all participating devices.
    /// Fastest mode: &lt;2μs typical latency.
    /// </summary>
    P2PMemory = 1,

    /// <summary>
    /// CUDA event-based synchronization using cudaEventWaitExternal.
    /// Compatible with all CUDA-capable devices.
    /// Fast mode: &lt;5μs typical latency.
    /// </summary>
    CudaEvent = 2,

    /// <summary>
    /// Host-mediated synchronization via CPU roundtrip.
    /// Fallback for systems without P2P or event support.
    /// Slower mode: &lt;50μs typical latency.
    /// </summary>
    CpuFallback = 3
}

/// <summary>
/// Result of barrier synchronization operation.
/// </summary>
public readonly struct CrossGpuBarrierResult : IEquatable<CrossGpuBarrierResult>
{
    /// <summary>
    /// Indicates whether the barrier completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Release timestamp (max HLC of all participant arrivals).
    /// Provides total ordering for post-barrier operations.
    /// </summary>
    public HlcTimestamp ReleaseTimestamp { get; init; }

    /// <summary>
    /// HLC timestamps of each participant's arrival.
    /// Indexed by GPU ID (0-based).
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Array provides optimal performance for GPU memory transfers")]
    public required HlcTimestamp[] ArrivalTimestamps { get; init; }

    /// <summary>
    /// Total time spent waiting at the barrier.
    /// </summary>
    public TimeSpan WaitTime { get; init; }

    /// <summary>
    /// Error message if barrier failed (null if successful).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is CrossGpuBarrierResult other && Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(CrossGpuBarrierResult other)
    {
        return Success == other.Success &&
            ReleaseTimestamp.Equals(other.ReleaseTimestamp) &&
            WaitTime == other.WaitTime &&
            ErrorMessage == other.ErrorMessage &&
            ArrivalTimestamps.SequenceEqual(other.ArrivalTimestamps);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(Success, ReleaseTimestamp, WaitTime, ErrorMessage);

    /// <summary>
    /// Determines whether two <see cref="CrossGpuBarrierResult"/> instances are equal.
    /// </summary>
    public static bool operator ==(CrossGpuBarrierResult left, CrossGpuBarrierResult right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="CrossGpuBarrierResult"/> instances are not equal.
    /// </summary>
    public static bool operator !=(CrossGpuBarrierResult left, CrossGpuBarrierResult right) => !left.Equals(right);
}

/// <summary>
/// Opaque token representing a barrier phase for split-phase synchronization.
/// </summary>
public readonly struct CrossGpuBarrierPhase : IEquatable<CrossGpuBarrierPhase>
{
    /// <summary>
    /// Generation counter for ABA problem prevention.
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    /// GPU ID of the participant that issued this phase token.
    /// </summary>
    public int GpuId { get; init; }

    /// <summary>
    /// HLC timestamp when this phase was created.
    /// </summary>
    public HlcTimestamp PhaseTimestamp { get; init; }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is CrossGpuBarrierPhase other && Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(CrossGpuBarrierPhase other)
    {
        return Generation == other.Generation &&
            GpuId == other.GpuId &&
            PhaseTimestamp.Equals(other.PhaseTimestamp);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(Generation, GpuId, PhaseTimestamp);

    /// <summary>
    /// Determines whether two <see cref="CrossGpuBarrierPhase"/> instances are equal.
    /// </summary>
    public static bool operator ==(CrossGpuBarrierPhase left, CrossGpuBarrierPhase right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="CrossGpuBarrierPhase"/> instances are not equal.
    /// </summary>
    public static bool operator !=(CrossGpuBarrierPhase left, CrossGpuBarrierPhase right) => !left.Equals(right);
}

/// <summary>
/// Current status of a cross-GPU barrier.
/// </summary>
public readonly struct CrossGpuBarrierStatus : IEquatable<CrossGpuBarrierStatus>
{
    /// <summary>
    /// Current generation counter.
    /// </summary>
    public int Generation { get; init; }

    /// <summary>
    /// Number of participants that have arrived so far.
    /// </summary>
    public int ArrivedCount { get; init; }

    /// <summary>
    /// Total number of participants required.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Indicates whether all participants have arrived.
    /// </summary>
    public readonly bool IsComplete => ArrivedCount == TotalCount;

    /// <summary>
    /// HLC timestamps of participants that have arrived.
    /// Sparse array indexed by GPU ID.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Array provides optimal performance for GPU memory transfers")]
    public required HlcTimestamp?[] ArrivalTimestamps { get; init; }

    /// <summary>
    /// Synchronization mode being used.
    /// </summary>
    public CrossGpuBarrierMode Mode { get; init; }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is CrossGpuBarrierStatus other && Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(CrossGpuBarrierStatus other)
    {
        return Generation == other.Generation &&
            ArrivedCount == other.ArrivedCount &&
            TotalCount == other.TotalCount &&
            Mode == other.Mode &&
            ArrivalTimestamps.SequenceEqual(other.ArrivalTimestamps);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode() => HashCode.Combine(Generation, ArrivedCount, TotalCount, Mode);

    /// <summary>
    /// Determines whether two <see cref="CrossGpuBarrierStatus"/> instances are equal.
    /// </summary>
    public static bool operator ==(CrossGpuBarrierStatus left, CrossGpuBarrierStatus right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="CrossGpuBarrierStatus"/> instances are not equal.
    /// </summary>
    public static bool operator !=(CrossGpuBarrierStatus left, CrossGpuBarrierStatus right) => !left.Equals(right);
}

/// <summary>
/// Exception thrown when a barrier operation times out.
/// </summary>
public sealed class BarrierTimeoutException : Exception
{
    /// <summary>
    /// GPU ID that timed out.
    /// </summary>
    public int GpuId { get; }

    /// <summary>
    /// Generation counter at time of timeout.
    /// </summary>
    public int Generation { get; }

    /// <summary>
    /// Number of participants that arrived before timeout.
    /// </summary>
    public int ArrivedCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BarrierTimeoutException"/> class.
    /// </summary>
    public BarrierTimeoutException()
        : base("Barrier operation timed out")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BarrierTimeoutException"/> class with a message.
    /// </summary>
    /// <param name="message">Exception message.</param>
    public BarrierTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BarrierTimeoutException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">Exception message.</param>
    /// <param name="innerException">Inner exception.</param>
    public BarrierTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BarrierTimeoutException"/> class.
    /// </summary>
    /// <param name="gpuId">GPU ID that timed out.</param>
    /// <param name="generation">Generation counter.</param>
    /// <param name="arrivedCount">Number of arrivals.</param>
    /// <param name="totalCount">Total participants.</param>
    public BarrierTimeoutException(int gpuId, int generation, int arrivedCount, int totalCount)
        : base($"Barrier timeout on GPU {gpuId} (generation {generation}): {arrivedCount}/{totalCount} arrived")
    {
        GpuId = gpuId;
        Generation = generation;
        ArrivedCount = arrivedCount;
    }
}
