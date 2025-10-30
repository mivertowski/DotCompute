// <copyright file="GCMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Garbage collection metrics.
/// Tracks .NET runtime memory management performance.
/// </summary>
public class GCMetrics
{
    /// <summary>
    /// Gets the number of Generation 0 collections.
    /// Count of short-lived object collections.
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    /// Gets the number of Generation 1 collections.
    /// Count of medium-lived object collections.
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    /// Gets the number of Generation 2 collections.
    /// Count of long-lived object collections.
    /// </summary>
    public int Gen2Collections { get; init; }

    /// <summary>
    /// Gets the total pause time due to GC.
    /// Cumulative time spent in garbage collection.
    /// </summary>
    public TimeSpan TotalPauseTime { get; init; }

    /// <summary>
    /// Gets the longest GC pause.
    /// Maximum duration of a single collection.
    /// </summary>
    public TimeSpan LongestPause { get; init; }
}
