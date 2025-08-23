// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.System;

/// <summary>
/// Represents a sampling point during continuous profiling operations.
/// Captures both the profiling state and system performance at a specific moment.
/// </summary>
public sealed class ProfileSample
{
    /// <summary>
    /// Gets or sets the timestamp when this sample was captured.
    /// Provides the exact moment when this profiling sample was recorded.
    /// </summary>
    /// <value>The timestamp as a DateTimeOffset.</value>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the number of active profiling sessions at the time of sampling.
    /// Indicates the concurrent profiling workload on the system.
    /// </summary>
    /// <value>The active profile count as an integer.</value>
    public int ActiveProfileCount { get; set; }

    /// <summary>
    /// Gets or sets the comprehensive system performance snapshot captured with this sample.
    /// Provides detailed system metrics at the time of sampling.
    /// </summary>
    /// <value>The system performance snapshot.</value>
    public SystemPerformanceSnapshot SystemSnapshot { get; set; } = new();
}