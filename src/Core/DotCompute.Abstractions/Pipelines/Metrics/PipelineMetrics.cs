// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Metrics;

/// <summary>
/// Time series metric data for pipeline monitoring.
/// </summary>
public sealed class TimeSeriesMetric
{
    /// <summary>
    /// Gets the metric name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the metric values over time.
    /// </summary>
    public required IReadOnlyList<TimestampedValue<double>> Values { get; init; }

    /// <summary>
    /// Gets the metric unit.
    /// </summary>
    public required string Unit { get; init; }
}

/// <summary>
/// Represents a timestamped value for metrics.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <remarks>
/// Initializes a new instance of the TimestampedValue struct.
/// </remarks>
/// <param name="timestamp">The timestamp.</param>
/// <param name="value">The value.</param>
public readonly struct TimestampedValue<T>(DateTimeOffset timestamp, T value)
{

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = timestamp;

    /// <summary>
    /// Gets the value.
    /// </summary>
    public T Value { get; } = value;
}