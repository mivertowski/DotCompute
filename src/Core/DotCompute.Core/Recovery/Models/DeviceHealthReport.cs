// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents a comprehensive health report for GPU/compute devices.
/// </summary>
public class DeviceHealthReport
{
    /// <summary>
    /// Gets or sets the timestamp when this report was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the overall health score (0.0 to 1.0).
    /// </summary>
    public double OverallHealth { get; set; }

    /// <summary>
    /// Gets or sets the health status of individual devices.
    /// </summary>
    public Dictionary<string, DeviceHealth> DeviceHealth { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of active kernels across all devices.
    /// </summary>
    public int ActiveKernels { get; set; }

    /// <summary>
    /// Gets or sets the total memory usage across all devices.
    /// </summary>
    public long TotalMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the total memory capacity across all devices.
    /// </summary>
    public long TotalMemoryCapacity { get; set; }

    /// <summary>
    /// Gets or sets the average GPU utilization percentage.
    /// </summary>
    public double AverageUtilization { get; set; }

    /// <summary>
    /// Gets or sets the average temperature across all devices.
    /// </summary>
    public double AverageTemperature { get; set; }

    /// <summary>
    /// Gets or sets the number of devices with errors.
    /// </summary>
    public int DevicesWithErrors { get; set; }

    /// <summary>
    /// Gets or sets the number of devices that are offline.
    /// </summary>
    public int OfflineDevices { get; set; }

    /// <summary>
    /// Gets or sets any critical issues detected.
    /// </summary>
    public List<string> CriticalIssues { get; set; } = new();
}

/// <summary>
/// Represents the health status of an individual device.
/// </summary>
public class DeviceHealth
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device status.
    /// </summary>
    public DeviceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the health score (0.0 to 1.0).
    /// </summary>
    public double HealthScore { get; set; }

    /// <summary>
    /// Gets or sets the GPU utilization percentage.
    /// </summary>
    public double Utilization { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the memory capacity in bytes.
    /// </summary>
    public long MemoryCapacity { get; set; }

    /// <summary>
    /// Gets or sets the device temperature in Celsius.
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Gets or sets the power draw in watts.
    /// </summary>
    public double PowerDraw { get; set; }

    /// <summary>
    /// Gets or sets the number of active kernels.
    /// </summary>
    public int ActiveKernels { get; set; }

    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the last error message if any.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivity { get; set; }
}

/// <summary>
/// Represents the status of a device.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// Device is healthy and operational.
    /// </summary>
    Healthy,

    /// <summary>
    /// Device has warnings but is operational.
    /// </summary>
    Warning,

    /// <summary>
    /// Device has critical issues.
    /// </summary>
    Critical,

    /// <summary>
    /// Device is offline or unavailable.
    /// </summary>
    Offline,

    /// <summary>
    /// Device status is unknown.
    /// </summary>
    Unknown
}