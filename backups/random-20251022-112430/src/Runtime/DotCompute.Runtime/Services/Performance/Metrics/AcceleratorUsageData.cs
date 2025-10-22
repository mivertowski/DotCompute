// <copyright file="AcceleratorUsageData.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Metrics;

/// <summary>
/// Accelerator device usage data.
/// Contains performance and resource utilization metrics for a specific accelerator.
/// </summary>
public class AcceleratorUsageData
{
    /// <summary>
    /// Gets the usage percentage.
    /// Compute utilization of the accelerator (0-100).
    /// </summary>
    public double UsagePercent { get; init; }

    /// <summary>
    /// Gets memory usage in bytes.
    /// Current memory consumption on the accelerator device.
    /// </summary>
    public long MemoryUsageBytes { get; init; }

    /// <summary>
    /// Gets temperature in Celsius.
    /// Current operating temperature of the device.
    /// </summary>
    public double TemperatureCelsius { get; init; }

    /// <summary>
    /// Gets power usage in watts.
    /// Current power consumption of the device.
    /// </summary>
    public double PowerUsageWatts { get; init; }
}