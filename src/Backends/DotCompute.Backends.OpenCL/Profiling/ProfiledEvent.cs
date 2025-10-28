// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Profiling;

/// <summary>
/// Represents a single profiled event with detailed timing information.
/// </summary>
/// <remarks>
/// This class captures comprehensive timing data for OpenCL operations:
/// - Queued time: When the command was added to the queue
/// - Start time: When execution actually began on the device
/// - End time: When execution completed
/// - Duration: Calculated end - start time
///
/// All times are in nanoseconds since device timer initialization.
/// </remarks>
public sealed class ProfiledEvent
{
    /// <summary>
    /// Gets or initializes the type of operation that was profiled.
    /// </summary>
    public required ProfiledOperation Operation { get; init; }

    /// <summary>
    /// Gets or initializes the descriptive name of the operation.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the timestamp when the command was queued (nanoseconds).
    /// </summary>
    public required long QueuedTimeNanoseconds { get; init; }

    /// <summary>
    /// Gets or initializes the timestamp when execution started (nanoseconds).
    /// </summary>
    public required long StartTimeNanoseconds { get; init; }

    /// <summary>
    /// Gets or initializes the timestamp when execution completed (nanoseconds).
    /// </summary>
    public required long EndTimeNanoseconds { get; init; }

    /// <summary>
    /// Gets the execution duration in nanoseconds (end - start).
    /// </summary>
    public long DurationNanoseconds => EndTimeNanoseconds - StartTimeNanoseconds;

    /// <summary>
    /// Gets the execution duration in milliseconds for easier interpretation.
    /// </summary>
    public double DurationMilliseconds => DurationNanoseconds / 1_000_000.0;

    /// <summary>
    /// Gets the queue-to-start latency in nanoseconds.
    /// This measures the time spent waiting in the queue before execution.
    /// </summary>
    public long QueueToStartNanoseconds => StartTimeNanoseconds - QueuedTimeNanoseconds;

    /// <summary>
    /// Gets the queue-to-start latency in milliseconds.
    /// </summary>
    public double QueueToStartMilliseconds => QueueToStartNanoseconds / 1_000_000.0;

    /// <summary>
    /// Gets or initializes optional metadata associated with this event.
    /// Common metadata includes: SizeBytes, BandwidthGBPerSecond, WorkGroupSize, GlobalWorkSize.
    /// </summary>
    public required Dictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Returns a string representation of this profiled event.
    /// </summary>
    public override string ToString()
    {
        return $"{Operation} - {Name}: {DurationMilliseconds:F3}ms " +
               $"(queue latency: {QueueToStartMilliseconds:F3}ms)";
    }
}
