// <copyright file="SessionMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Metrics for a profiling session.
/// Performance data collected during a single profiling session.
/// </summary>
public class SessionMetrics
{
    /// <summary>
    /// Gets the elapsed time.
    /// Duration of the profiling session.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets the collected metrics.
    /// Performance measurements from the session.
    /// </summary>
    public Dictionary<string, double> Metrics { get; init; } = [];

    /// <summary>
    /// Gets the session tags.
    /// Metadata tags associated with the session.
    /// </summary>
    public Dictionary<string, string> Tags { get; init; } = [];
}
