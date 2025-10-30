// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Options;

/// <summary>
/// Configuration options for the performance profiler.
/// Controls profiling behavior, resource limits, and data collection settings.
/// </summary>
public sealed class PerformanceProfilerOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent profiling sessions.
    /// Limits the number of profiles that can run simultaneously to prevent resource exhaustion.
    /// Default value is 10.
    /// </summary>
    /// <value>The maximum number of concurrent profiles.</value>
    public int MaxConcurrentProfiles { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether continuous profiling is enabled.
    /// When enabled, profiling continues automatically across multiple operations.
    /// Default value is true.
    /// </summary>
    /// <value>true if continuous profiling is enabled; otherwise, false.</value>
    public bool EnableContinuousProfiling { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling interval in milliseconds for continuous profiling.
    /// Determines how frequently performance snapshots are captured.
    /// Default value is 100 milliseconds.
    /// </summary>
    /// <value>The sampling interval in milliseconds.</value>
    public int SamplingIntervalMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether orphaned records are allowed.
    /// When true, profile records without a parent session are retained.
    /// Default value is false.
    /// </summary>
    /// <value>true if orphaned records are allowed; otherwise, false.</value>
    public bool AllowOrphanedRecords { get; set; }
}
