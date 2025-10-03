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
