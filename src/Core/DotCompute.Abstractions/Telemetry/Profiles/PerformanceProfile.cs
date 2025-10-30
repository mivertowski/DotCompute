// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Telemetry.Profiles;

/// <summary>
/// Contains comprehensive performance profile data for analysis.
/// </summary>
public sealed class PerformanceProfile
{
    /// <summary>
    /// Gets the profile identifier.
    /// </summary>
    public required string ProfileId { get; init; }

    /// <summary>
    /// Gets the correlation identifier.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Gets the profiling start time.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Gets the profiling end time.
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Gets the total profiling duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Gets the CPU utilization data.
    /// </summary>
    public required CpuProfileData CpuProfile { get; init; }

    /// <summary>
    /// Gets the memory utilization data.
    /// </summary>
    public required MemoryProfileData MemoryProfile { get; init; }

    /// <summary>
    /// Gets the GPU utilization data, if available.
    /// </summary>
    public GpuProfileData? GpuProfile { get; init; }

    /// <summary>
    /// Gets the performance metrics.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Metrics { get; init; }
}

/// <summary>
/// CPU profiling data.
/// </summary>
public sealed class CpuProfileData
{
    /// <summary>
    /// Gets the average CPU utilization percentage.
    /// </summary>
    public required double AverageUtilization { get; init; }

    /// <summary>
    /// Gets the peak CPU utilization percentage.
    /// </summary>
    public required double PeakUtilization { get; init; }

    /// <summary>
    /// Gets the CPU utilization samples over time.
    /// </summary>
    public required IReadOnlyList<TimestampedValue<double>> UtilizationSamples { get; init; }
}

/// <summary>
/// Memory profiling data.
/// </summary>
public sealed class MemoryProfileData
{
    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    public required long PeakMemoryUsage { get; init; }

    /// <summary>
    /// Gets the average memory usage in bytes.
    /// </summary>
    public required long AverageMemoryUsage { get; init; }

    /// <summary>
    /// Gets the memory usage samples over time.
    /// </summary>
    public required IReadOnlyList<TimestampedValue<long>> MemoryUsageSamples { get; init; }

    /// <summary>
    /// Gets the allocation pattern data.
    /// </summary>
    public required AllocationPatternData AllocationPatterns { get; init; }
}

/// <summary>
/// GPU profiling data.
/// </summary>
public sealed class GpuProfileData
{
    /// <summary>
    /// Gets the average GPU utilization percentage.
    /// </summary>
    public required double AverageUtilization { get; init; }

    /// <summary>
    /// Gets the peak GPU utilization percentage.
    /// </summary>
    public required double PeakUtilization { get; init; }

    /// <summary>
    /// Gets the GPU memory usage in bytes.
    /// </summary>
    public required long MemoryUsage { get; init; }

    /// <summary>
    /// Gets the GPU utilization samples over time.
    /// </summary>
    public required IReadOnlyList<TimestampedValue<double>> UtilizationSamples { get; init; }
}

/// <summary>
/// Allocation pattern analysis data.
/// </summary>
public sealed class AllocationPatternData
{
    /// <summary>
    /// Gets the total number of allocations.
    /// </summary>
    public required int TotalAllocations { get; init; }

    /// <summary>
    /// Gets the total bytes allocated.
    /// </summary>
    public required long TotalBytesAllocated { get; init; }

    /// <summary>
    /// Gets the allocation frequency (allocations per second).
    /// </summary>
    public required double AllocationFrequency { get; init; }
}

/// <summary>
/// Represents a timestamped value for time-series data.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <remarks>
/// Initializes a new instance of the TimestampedValue struct.
/// </remarks>
/// <param name="timestamp">The timestamp.</param>
/// <param name="value">The value.</param>
public readonly struct TimestampedValue<T>(DateTimeOffset timestamp, T value) : IEquatable<TimestampedValue<T>>
{

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// Gets the value.
    /// </summary>
    public T Value { get; } = value;

    /// <summary>
    /// Determines whether the current instance is equal to another TimestampedValue instance.
    /// </summary>
    /// <param name="other">The TimestampedValue instance to compare with this instance.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public bool Equals(TimestampedValue<T> other)
        => Timestamp == other.Timestamp && EqualityComparer<T>.Default.Equals(Value, other.Value);

    /// <summary>
    /// Determines whether the current instance is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>true if obj is a TimestampedValue and is equal to this instance; otherwise, false.</returns>
    public override bool Equals(object? obj)
        => obj is TimestampedValue<T> other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A hash code for the current instance.</returns>
    public override int GetHashCode()
        => HashCode.Combine(Timestamp, Value);

    /// <summary>
    /// Determines whether two TimestampedValue instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(TimestampedValue<T> left, TimestampedValue<T> right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two TimestampedValue instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(TimestampedValue<T> left, TimestampedValue<T> right)
        => !left.Equals(right);
}
