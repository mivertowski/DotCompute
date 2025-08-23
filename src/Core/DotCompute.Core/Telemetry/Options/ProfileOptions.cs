// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Options;

/// <summary>
/// Configuration options for individual profiling sessions.
/// Defines what data to collect and profiling behavior for a specific session.
/// </summary>
public sealed class ProfileOptions
{
    /// <summary>
    /// Gets or sets the automatic stop timeout for the profiling session.
    /// If set, the profile will automatically stop after this duration.
    /// </summary>
    /// <value>The auto-stop timeout as a TimeSpan or null for manual control.</value>
    public TimeSpan? AutoStopAfter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether detailed memory profiling is enabled.
    /// When enabled, collects comprehensive memory allocation and usage data.
    /// Default value is true.
    /// </summary>
    /// <value>true if detailed memory profiling is enabled; otherwise, false.</value>
    public bool EnableDetailedMemoryProfiling { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether kernel profiling is enabled.
    /// When enabled, collects performance data from compute kernel executions.
    /// Default value is true.
    /// </summary>
    /// <value>true if kernel profiling is enabled; otherwise, false.</value>
    public bool EnableKernelProfiling { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether system profiling is enabled.
    /// When enabled, collects system-level performance metrics like CPU and memory usage.
    /// Default value is true.
    /// </summary>
    /// <value>true if system profiling is enabled; otherwise, false.</value>
    public bool EnableSystemProfiling { get; set; } = true;
}