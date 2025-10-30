// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Core.Telemetry.Spans;

namespace DotCompute.Core.Telemetry.Traces;

/// <summary>
/// Tracks operations performed on a specific compute device within a trace.
/// Provides aggregated information about device usage and active spans.
/// </summary>
public sealed class DeviceOperationTrace
{
    /// <summary>
    /// Gets or sets the identifier of the device being tracked.
    /// Used to correlate operations to specific compute devices.
    /// </summary>
    /// <value>The device identifier as a string.</value>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of operations performed on this device.
    /// Incremented each time a new operation is started on the device.
    /// </summary>
    /// <value>The total operation count as an integer.</value>
    public int OperationCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the first operation on this device.
    /// Used for calculating the total timespan of device usage.
    /// </summary>
    /// <value>The first operation time as a DateTimeOffset.</value>
    public DateTimeOffset FirstOperationTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the most recent operation on this device.
    /// Updated whenever a new operation is started on the device.
    /// </summary>
    /// <value>The last operation time as a DateTimeOffset.</value>
    public DateTimeOffset LastOperationTime { get; set; }

    /// <summary>
    /// Gets the collection of currently active spans on this device.
    /// Spans are added when operations start and removed when they complete.
    /// </summary>
    /// <value>A thread-safe collection of active span contexts.</value>
    public ConcurrentBag<SpanContext> ActiveSpans { get; } = [];
}
