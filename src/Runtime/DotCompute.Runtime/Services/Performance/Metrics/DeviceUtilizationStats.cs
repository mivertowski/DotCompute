// <copyright file="DeviceUtilizationStats.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Device utilization statistics.
/// Aggregated performance data for device usage analysis.
/// </summary>
public class DeviceUtilizationStats
{
    /// <summary>
    /// Gets the accelerator identifier.
    /// Unique ID of the analyzed device.
    /// </summary>
    public required string AcceleratorId { get; init; }

    /// <summary>
    /// Gets the average usage percentage.
    /// Mean device utilization over the period.
    /// </summary>
    public double AverageUsagePercent { get; init; }

    /// <summary>
    /// Gets the peak usage percentage.
    /// Maximum observed device utilization.
    /// </summary>
    public double PeakUsagePercent { get; init; }

    /// <summary>
    /// Gets the total active time.
    /// Duration the device was actively processing.
    /// </summary>
    public TimeSpan TotalActiveTime { get; init; }

    /// <summary>
    /// Gets the total idle time.
    /// Duration the device was idle.
    /// </summary>
    public TimeSpan TotalIdleTime { get; init; }
}
