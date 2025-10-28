// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.System;

namespace DotCompute.Core.Telemetry.Samples;

/// <summary>
/// Represents a point-in-time snapshot of profiling data collected during monitoring.
/// Contains performance metrics and system state captured at a specific timestamp.
/// </summary>
public sealed class ProfileSample
{
    /// <summary>
    /// Gets or sets the timestamp when this sample was collected.
    /// Indicates the exact moment this performance snapshot was taken.
    /// </summary>
    /// <value>The sample timestamp as a DateTimeOffset.</value>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the number of active profiling sessions at the time of sampling.
    /// Indicates the current profiling workload.
    /// </summary>
    /// <value>The count of active profiles as an integer.</value>
    public int ActiveProfileCount { get; set; }

    /// <summary>
    /// Gets or sets the system performance snapshot collected with this sample.
    /// Contains detailed system-level performance metrics at the sampling moment.
    /// </summary>
    /// <value>The system snapshot containing performance data.</value>
    public SystemSnapshot SystemSnapshot { get; set; } = new();
}
