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
public readonly struct TimestampedValue<T>
{
    /// <summary>
    /// Initializes a new instance of the TimestampedValue struct.
    /// </summary>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="value">The value.</param>
    public TimestampedValue(DateTimeOffset timestamp, T value)
    {
        Timestamp = timestamp;
        Value = value;
    }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    public T Value { get; }
}