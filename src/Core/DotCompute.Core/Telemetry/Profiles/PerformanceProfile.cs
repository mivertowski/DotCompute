// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.Enums;
using DotCompute.Core.Telemetry.Analysis;

namespace DotCompute.Core.Telemetry.Profiles;

/// <summary>
/// Represents a completed performance profile containing comprehensive execution data.
/// Contains all collected performance data and analysis results for a profiling session.
/// </summary>
public sealed class PerformanceProfile
{
    /// <summary>
    /// Gets or sets the correlation identifier for this performance profile.
    /// Links all collected data to the original profiling session.
    /// </summary>
    /// <value>The correlation identifier as a string.</value>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the profiling session started.
    /// Marks the beginning of data collection for this profile.
    /// </summary>
    /// <value>The start time as a DateTimeOffset.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the profiling session ended.
    /// Null if the profiling session is still active.
    /// </summary>
    /// <value>The end time as a DateTimeOffset or null if still active.</value>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Gets or sets the total duration of the profiling session.
    /// Calculated as the difference between end time and start time.
    /// </summary>
    /// <value>The total duration as a TimeSpan.</value>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the status of the profiling session.
    /// Indicates whether the session completed successfully or encountered issues.
    /// </summary>
    /// <value>The profile status from the ProfileStatus enumeration.</value>
    public ProfileStatus Status { get; set; }

    /// <summary>
    /// Gets or sets an optional message describing the profile status.
    /// Typically used to provide error details when status is not completed.
    /// </summary>
    /// <value>The status message as a string or null if not provided.</value>
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the total number of kernel executions captured in this profile.
    /// Provides a summary count of kernel performance data collected.
    /// </summary>
    /// <value>The total kernel execution count.</value>
    public int TotalKernelExecutions { get; set; }

    /// <summary>
    /// Gets or sets the total number of memory operations captured in this profile.
    /// Provides a summary count of memory performance data collected.
    /// </summary>
    /// <value>The total memory operation count.</value>
    public int TotalMemoryOperations { get; set; }

    /// <summary>
    /// Gets or sets the number of devices that were involved in this profiling session.
    /// Indicates the scope of distributed computing captured in the profile.
    /// </summary>
    /// <value>The number of devices involved.</value>
    public int DevicesInvolved { get; set; }

    /// <summary>
    /// Gets or sets the analysis results for this performance profile.
    /// Contains insights, bottlenecks, and recommendations derived from the collected data.
    /// </summary>
    /// <value>The profile analysis results or null if analysis hasn't been performed.</value>
    public ProfileAnalysis? Analysis { get; set; }

    /// <summary>
    /// Gets or sets the detailed kernel execution profiles collected during this session.
    /// Each profile contains comprehensive performance data for a single kernel execution.
    /// </summary>
    /// <value>A list of kernel execution profiles.</value>
    public IList<KernelExecutionProfile> KernelExecutions { get; } = [];

    /// <summary>
    /// Gets or sets the detailed memory operation profiles collected during this session.
    /// Each profile contains comprehensive performance data for a memory operation.
    /// </summary>
    /// <value>A list of memory operation profiles.</value>
    public IList<MemoryOperationProfile> MemoryOperations { get; } = [];

    /// <summary>
    /// Gets or sets the device-specific metrics collected during this session.
    /// Maps device IDs to their corresponding performance metrics summary.
    /// </summary>
    /// <value>A dictionary mapping device IDs to device profile metrics.</value>
    public Dictionary<string, DeviceProfileMetrics> DeviceMetrics { get; } = [];
}