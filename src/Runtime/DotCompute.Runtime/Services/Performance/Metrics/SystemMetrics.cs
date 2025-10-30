// <copyright file="SystemMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// System-wide performance metrics.
/// Captures overall system resource utilization and health indicators.
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Gets the average CPU usage percentage.
    /// Mean CPU utilization over the measurement period.
    /// </summary>
    public double AverageCpuUsage { get; init; }

    /// <summary>
    /// Gets the peak CPU usage percentage.
    /// Maximum CPU utilization observed.
    /// </summary>
    public double PeakCpuUsage { get; init; }

    /// <summary>
    /// Gets the system uptime.
    /// Duration since the system or service started.
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// Gets the number of active threads.
    /// Current thread pool and worker thread count.
    /// </summary>
    public int ActiveThreadCount { get; init; }

    /// <summary>
    /// Gets garbage collection metrics.
    /// .NET runtime memory management statistics.
    /// </summary>
    public GCMetrics GarbageCollection { get; init; } = new();
}
