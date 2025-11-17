// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Temporal;

/// <summary>
/// Represents a Hybrid Logical Clock (HLC) timestamp for distributed causality tracking.
/// </summary>
/// <remarks>
/// <para>
/// HLC combines physical time (wall-clock) with logical time (Lamport counter) to provide:
/// <list type="bullet">
/// <item><description><strong>Causality Tracking</strong>: Preserves happened-before relationships</description></item>
/// <item><description><strong>Total Ordering</strong>: Provides consistent ordering across distributed kernels</description></item>
/// <item><description><strong>Physical Proximity</strong>: Timestamps close in physical time</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>HLC Structure:</strong>
/// <code>
/// timestamp = (physical_time, logical_counter)
/// physical_time: 64-bit nanoseconds since epoch
/// logical_counter: 32-bit Lamport counter
/// </code>
/// </para>
/// <para>
/// <strong>Comparison Rules:</strong>
/// <code>
/// (pt1, l1) &lt; (pt2, l2) if:
///   - pt1 &lt; pt2, OR
///   - pt1 == pt2 AND l1 &lt; l2
/// </code>
/// </para>
/// <para>
/// <strong>Example Usage:</strong>
/// <code>
/// // Create HLC for this kernel
/// var hlc = accelerator.CreateHybridLogicalClock();
///
/// // Local event: advance clock
/// var timestamp1 = hlc.Tick();
///
/// // Receive message with remote timestamp
/// var remoteTimestamp = receiveMessage();
/// var timestamp2 = hlc.Update(remoteTimestamp);
///
/// // Compare timestamps for causality
/// if (timestamp1.HappenedBefore(timestamp2))
/// {
///     // timestamp1 causally precedes timestamp2
/// }
/// </code>
/// </para>
/// </remarks>
public readonly struct HlcTimestamp : IEquatable<HlcTimestamp>, IComparable<HlcTimestamp>
{
    /// <summary>
    /// Gets the physical time component in nanoseconds since epoch.
    /// </summary>
    /// <value>
    /// Physical wall-clock time captured from the GPU or CPU timing provider.
    /// Resolution: 1ns on CUDA CC 6.0+ (globaltimer), 1μs on older GPUs (events).
    /// </value>
    public long PhysicalTimeNanos { get; init; }

    /// <summary>
    /// Gets the logical counter component for causality tracking.
    /// </summary>
    /// <value>
    /// Lamport logical clock counter, incremented when:
    /// <list type="bullet">
    /// <item><description>Local event occurs (Tick)</description></item>
    /// <item><description>Remote timestamp received with same physical time (Update)</description></item>
    /// </list>
    /// </value>
    public int LogicalCounter { get; init; }

    /// <summary>
    /// Determines whether this timestamp happened before another timestamp.
    /// </summary>
    /// <param name="other">The timestamp to compare against.</param>
    /// <returns>
    /// True if this timestamp causally precedes <paramref name="other"/>, false otherwise.
    /// </returns>
    /// <remarks>
    /// Implements the happened-before relation (→):
    /// <code>
    /// this → other if:
    ///   - this.PhysicalTimeNanos &lt; other.PhysicalTimeNanos, OR
    ///   - this.PhysicalTimeNanos == other.PhysicalTimeNanos AND this.LogicalCounter &lt; other.LogicalCounter
    /// </code>
    /// </remarks>
    public bool HappenedBefore(HlcTimestamp other)
    {
        if (PhysicalTimeNanos < other.PhysicalTimeNanos)
        {
            return true;
        }

        if (PhysicalTimeNanos == other.PhysicalTimeNanos)
        {
            return LogicalCounter < other.LogicalCounter;
        }

        return false;
    }

    /// <summary>
    /// Determines whether this timestamp is concurrent with another timestamp.
    /// </summary>
    /// <param name="other">The timestamp to compare against.</param>
    /// <returns>
    /// True if neither timestamp happened before the other (concurrent events), false otherwise.
    /// </returns>
    /// <remarks>
    /// Concurrent timestamps indicate independent events in different causal chains.
    /// Used for conflict detection in distributed systems.
    /// </remarks>
    public bool IsConcurrentWith(HlcTimestamp other)
    {
        return !HappenedBefore(other) && !other.HappenedBefore(this);
    }

    /// <inheritdoc/>
    public int CompareTo(HlcTimestamp other)
    {
        int ptComparison = PhysicalTimeNanos.CompareTo(other.PhysicalTimeNanos);
        if (ptComparison != 0)
        {
            return ptComparison;
        }

        return LogicalCounter.CompareTo(other.LogicalCounter);
    }

    /// <inheritdoc/>
    public bool Equals(HlcTimestamp other)
    {
        return PhysicalTimeNanos == other.PhysicalTimeNanos
            && LogicalCounter == other.LogicalCounter;
    }

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj)
    {
        return obj is HlcTimestamp other && Equals(other);
    }

    /// <inheritdoc/>
    public readonly override int GetHashCode()
    {
        return HashCode.Combine(PhysicalTimeNanos, LogicalCounter);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(HlcTimestamp left, HlcTimestamp right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(HlcTimestamp left, HlcTimestamp right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Less-than operator for happened-before comparison.
    /// </summary>
    public static bool operator <(HlcTimestamp left, HlcTimestamp right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Greater-than operator for happened-after comparison.
    /// </summary>
    public static bool operator >(HlcTimestamp left, HlcTimestamp right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Less-than-or-equal operator.
    /// </summary>
    public static bool operator <=(HlcTimestamp left, HlcTimestamp right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Greater-than-or-equal operator.
    /// </summary>
    public static bool operator >=(HlcTimestamp left, HlcTimestamp right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc/>
    public readonly override string ToString()
    {
        return $"HLC({PhysicalTimeNanos}ns, L{LogicalCounter})";
    }
}

/// <summary>
/// Provides Hybrid Logical Clock (HLC) functionality for distributed causality tracking across GPU kernels.
/// </summary>
/// <remarks>
/// <para>
/// HLC is essential for GPU-native actor systems and distributed kernel coordination because:
/// <list type="bullet">
/// <item><description><strong>Causal Message Ordering</strong>: Ensures messages are processed in happened-before order</description></item>
/// <item><description><strong>Conflict Detection</strong>: Identifies concurrent operations for conflict resolution</description></item>
/// <item><description><strong>Replay and Recovery</strong>: Provides deterministic ordering for checkpoint replay</description></item>
/// <item><description><strong>Distributed Debugging</strong>: Tracks causality across multiple kernels and GPUs</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong>
/// All HLC operations are thread-safe using atomic operations. Multiple GPU threads can safely
/// call Tick() and Update() concurrently without data races.
/// </para>
/// <para>
/// <strong>Performance:</strong>
/// <list type="bullet">
/// <item><description>Tick(): ~20ns (1 atomic increment + 1 timestamp read)</description></item>
/// <item><description>Update(): ~50ns (1 atomic max + 1 atomic increment + 1 timestamp read)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Integration with Timing API:</strong>
/// HLC uses the existing GPU Timing API (ITimingProvider) for high-precision physical timestamps.
/// This ensures nanosecond accuracy on supported hardware (CUDA CC 6.0+).
/// </para>
/// </remarks>
public interface IHybridLogicalClock
{
    /// <summary>
    /// Advances the clock for a local event and returns the new timestamp.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>
    /// A task representing the async operation, returning a new HLC timestamp.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Operation:</strong>
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
    /// <strong>Usage:</strong> Call Tick() before processing a local message or event.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Safe to call from multiple threads concurrently.
    /// </para>
    /// </remarks>
    public Task<HlcTimestamp> TickAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the clock based on a received remote timestamp and returns the new local timestamp.
    /// </summary>
    /// <param name="remoteTimestamp">The timestamp received from another kernel or actor.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>
    /// A task representing the async operation, returning a new HLC timestamp that preserves causality.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Operation:</strong>
    /// <code>
    /// pt_new = GetPhysicalTime()
    /// pt_max = max(pt_old, remote.PhysicalTime)
    ///
    /// if (pt_new > pt_max):
    ///     logical = 0
    /// elif (pt_max == pt_old):
    ///     logical = max(logical_old, remote.Logical) + 1
    /// else:
    ///     logical = remote.Logical + 1
    ///
    /// return (max(pt_new, pt_max), logical)
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Usage:</strong> Call UpdateAsync() after receiving a message from another kernel.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Safe to call from multiple threads concurrently.
    /// </para>
    /// </remarks>
    public Task<HlcTimestamp> UpdateAsync(HlcTimestamp remoteTimestamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current HLC timestamp without advancing the clock.
    /// </summary>
    /// <returns>
    /// The current HLC timestamp.
    /// </returns>
    /// <remarks>
    /// Used for reading the clock state without modifying it (e.g., for diagnostics).
    /// </remarks>
    public HlcTimestamp GetCurrent();

    /// <summary>
    /// Resets the clock to a specific timestamp (for testing or recovery).
    /// </summary>
    /// <param name="timestamp">The timestamp to reset to.</param>
    /// <remarks>
    /// <strong>Caution:</strong> Resetting the clock can violate causality if not done carefully.
    /// Only use for testing, checkpoint recovery, or kernel restart scenarios.
    /// </remarks>
    public void Reset(HlcTimestamp timestamp);
}
