// <copyright file="IDeviceMetricsCollector.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Runtime.Services.Performance.Metrics;

namespace DotCompute.Runtime.Services.Interfaces;

/// <summary>
/// Service for collecting device-specific metrics.
/// Monitors and tracks performance metrics for individual compute devices.
/// </summary>
public interface IDeviceMetricsCollector
{
    /// <summary>
    /// Starts collecting metrics for a device.
    /// Initializes metric collection for the specified accelerator.
    /// </summary>
    /// <param name="acceleratorId">The unique identifier of the accelerator.</param>
    /// <returns>A task representing the start operation.</returns>
    public Task StartCollectionAsync(string acceleratorId);

    /// <summary>
    /// Stops collecting metrics for a device.
    /// Terminates metric collection for the specified accelerator.
    /// </summary>
    /// <param name="acceleratorId">The unique identifier of the accelerator.</param>
    /// <returns>A task representing the stop operation.</returns>
    public Task StopCollectionAsync(string acceleratorId);

    /// <summary>
    /// Gets current device metrics.
    /// Retrieves the latest performance metrics for the specified device.
    /// </summary>
    /// <param name="acceleratorId">The unique identifier of the accelerator.</param>
    /// <returns>Current device metrics.</returns>
    public Task<DeviceMetrics> GetCurrentMetricsAsync(string acceleratorId);

    /// <summary>
    /// Gets historical device metrics.
    /// Retrieves device performance data for a specific time range.
    /// </summary>
    /// <param name="acceleratorId">The unique identifier of the accelerator.</param>
    /// <param name="startTime">The start time of the historical period.</param>
    /// <param name="endTime">The end time of the historical period.</param>
    /// <returns>Collection of historical device metrics.</returns>
    public Task<IEnumerable<DeviceMetrics>> GetHistoricalMetricsAsync(
        string acceleratorId,

        DateTime startTime,

        DateTime endTime);

    /// <summary>
    /// Gets device utilization statistics.
    /// Provides detailed utilization analysis for the specified device.
    /// </summary>
    /// <param name="acceleratorId">The unique identifier of the accelerator.</param>
    /// <returns>Device utilization statistics.</returns>
    public Task<DeviceUtilizationStats> GetUtilizationStatsAsync(string acceleratorId);
}
