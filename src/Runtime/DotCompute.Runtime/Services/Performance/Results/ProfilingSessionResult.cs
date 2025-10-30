// <copyright file="ProfilingSessionResult.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Results;

/// <summary>
/// Final results from a profiling session.
/// Complete performance analysis from a profiling operation.
/// </summary>
public class ProfilingSessionResult
{
    /// <summary>
    /// Gets the session identifier.
    /// Unique ID for this profiling session.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the operation name.
    /// Name of the profiled operation.
    /// </summary>
    public required string OperationName { get; init; }

    /// <summary>
    /// Gets the total execution time.
    /// Complete duration of the profiled operation.
    /// </summary>
    public TimeSpan TotalTime { get; init; }

    /// <summary>
    /// Gets the start time.
    /// When the profiling session began.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets the end time.
    /// When the profiling session completed.
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Gets the session metrics.
    /// Detailed performance measurements from the session.
    /// </summary>
    public SessionMetrics Metrics { get; init; } = new();
}
