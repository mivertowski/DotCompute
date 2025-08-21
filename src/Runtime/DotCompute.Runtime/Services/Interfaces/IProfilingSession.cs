// <copyright file="IProfilingSession.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Runtime.Services.Performance.Results;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Profiling session for tracking operation performance.
/// Represents an active profiling context that collects metrics for a specific operation.
/// </summary>
public interface IProfilingSession : IDisposable
{
    /// <summary>
    /// Gets the unique session identifier.
    /// Used to correlate metrics and events within this profiling session.
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Gets the name of the operation being profiled.
    /// Identifies what operation or workflow this session is tracking.
    /// </summary>
    string OperationName { get; }

    /// <summary>
    /// Gets the start time of the profiling session.
    /// Marks when the profiling began for duration calculations.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Records a custom metric for this session.
    /// Allows tracking of application-specific performance indicators.
    /// </summary>
    /// <param name="name">The metric name.</param>
    /// <param name="value">The metric value.</param>
    void RecordMetric(string name, double value);

    /// <summary>
    /// Adds a tag to this session.
    /// Tags provide additional context and metadata for the profiling session.
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    void AddTag(string key, string value);

    /// <summary>
    /// Gets the current session metrics.
    /// Returns intermediate metrics collected so far in the session.
    /// </summary>
    /// <returns>Current session metrics.</returns>
    SessionMetrics GetMetrics();

    /// <summary>
    /// Ends the profiling session.
    /// Finalizes metric collection and returns the complete session results.
    /// </summary>
    /// <returns>Final profiling session results.</returns>
    ProfilingSessionResult End();
}