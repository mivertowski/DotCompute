// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Core.Telemetry.Options;
using DotCompute.Core.Telemetry.System;

namespace DotCompute.Core.Telemetry.Profiles;

/// <summary>
/// Represents an active profiling session collecting performance data in real-time.
/// Contains all the data being gathered during an ongoing profiling operation.
/// </summary>
public sealed class ActiveProfile
{
    /// <summary>
    /// Gets or sets the correlation identifier for this profiling session.
    /// Used to link all collected data to this specific profiling session.
    /// </summary>
    /// <value>The correlation identifier as a string.</value>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the profiling session started.
    /// Used for calculating the total duration of the profiling session.
    /// </summary>
    /// <value>The start time as a DateTimeOffset.</value>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the options that control the behavior of this profiling session.
    /// Determines what data is collected and how the session operates.
    /// </summary>
    /// <value>The profile options for this session.</value>
    public ProfileOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the collection of kernel execution profiles captured during this session.
    /// Each profile represents the performance data from a single kernel execution.
    /// </summary>
    /// <value>A thread-safe collection of kernel execution profiles.</value>
    public ConcurrentBag<KernelExecutionProfile> KernelExecutions { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of memory operation profiles captured during this session.
    /// Each profile represents the performance data from a memory operation.
    /// </summary>
    /// <value>A thread-safe collection of memory operation profiles.</value>
    public ConcurrentBag<MemoryOperationProfile> MemoryOperations { get; set; } = [];

    /// <summary>
    /// Gets or sets the device-specific metrics collected during this session.
    /// Maps device IDs to their corresponding performance metrics.
    /// </summary>
    /// <value>A thread-safe dictionary mapping device IDs to profile metrics.</value>
    public ConcurrentDictionary<string, DeviceProfileMetrics> DeviceMetrics { get; } = new();

    /// <summary>
    /// Gets or sets the collection of system performance snapshots captured during this session.
    /// Each snapshot represents system state at a specific point in time.
    /// </summary>
    /// <value>A thread-safe queue of system performance snapshots.</value>
    public ConcurrentQueue<SystemSnapshot> SystemSnapshots { get; set; } = new();
}