// <copyright file="DeviceMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Device-specific performance metrics.
/// Real-time performance data for a compute device.
/// </summary>
public class DeviceMetrics
{
    /// <summary>
    /// Gets the accelerator identifier.
    /// Unique ID of the monitored device.
    /// </summary>
    public required string AcceleratorId { get; init; }

    /// <summary>
    /// Gets the metric timestamp.
    /// When these metrics were collected.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the usage percentage.
    /// Device compute utilization (0-100).
    /// </summary>
    public double UsagePercent { get; init; }

    /// <summary>
    /// Gets memory usage in bytes.
    /// Current device memory consumption.
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets temperature in Celsius.
    /// Current device operating temperature.
    /// </summary>
    public double TemperatureCelsius { get; init; }

    /// <summary>
    /// Gets power usage in watts.
    /// Current device power consumption.
    /// </summary>
    public double PowerUsageWatts { get; init; }

    /// <summary>
    /// Gets custom device metrics.
    /// Device-specific performance indicators.
    /// </summary>
    public Dictionary<string, double> CustomMetrics { get; init; } = [];
}