// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Profiles;

/// <summary>
/// Contains aggregated performance metrics for a specific device during a profiling session.
/// Provides high-level insights into device utilization and resource consumption.
/// </summary>
public sealed class DeviceProfileMetrics
{
    /// <summary>
    /// Gets or sets the utilization percentage of the device.
    /// Indicates how much of the device's compute capacity was used during profiling.
    /// </summary>
    /// <value>The utilization percentage as a decimal (0.0 to 1.0).</value>
    public double UtilizationPercentage { get; set; }

    /// <summary>
    /// Gets or sets the operating temperature of the device in Celsius.
    /// Provides insight into thermal characteristics during operation.
    /// </summary>
    /// <value>The temperature in degrees Celsius.</value>
    public double TemperatureCelsius { get; set; }

    /// <summary>
    /// Gets or sets the memory usage of the device in bytes.
    /// Indicates how much device memory was consumed during profiling.
    /// </summary>
    /// <value>The memory usage in bytes.</value>
    public long MemoryUsageBytes { get; set; }

    /// <summary>
    /// Gets or sets the power consumption of the device in watts.
    /// Provides insight into energy efficiency during operation.
    /// </summary>
    /// <value>The power consumption in watts.</value>
    public double PowerConsumptionWatts { get; set; }
}
