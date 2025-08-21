// <copyright file="TimeRange.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Types;

/// <summary>
/// Represents a time range specification for performance metrics.
/// Defines a period between two points in time.
/// </summary>
/// <param name="Start">The start time of the range.</param>
/// <param name="End">The end time of the range.</param>
public record TimeRange(DateTime Start, DateTime End)
{
    /// <summary>
    /// Gets the duration of this time range.
    /// Calculates the time span between start and end.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Determines if a given time falls within this range.
    /// </summary>
    /// <param name="time">The time to check.</param>
    /// <returns>True if the time is within the range, false otherwise.</returns>
    public bool Contains(DateTime time) => time >= Start && time <= End;

    /// <summary>
    /// Determines if this range overlaps with another range.
    /// </summary>
    /// <param name="other">The other time range.</param>
    /// <returns>True if the ranges overlap, false otherwise.</returns>
    public bool Overlaps(TimeRange other) => Start < other.End && End > other.Start;
}