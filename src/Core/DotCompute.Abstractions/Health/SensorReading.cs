// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Health;

/// <summary>
/// Represents a single sensor reading from a compute device.
/// </summary>
/// <remarks>
/// <para>
/// This immutable record captures a point-in-time sensor measurement with metadata
/// about availability, units, and quality indicators. Sensor readings are typically
/// collected at 1-10 second intervals to balance monitoring overhead with responsiveness.
/// </para>
///
/// <para>
/// <b>Performance Characteristics:</b>
/// - Reading collection: 1-5ms per device (NVML/CUPTI)
/// - Memory footprint: ~100 bytes per reading
/// - Typical collection interval: 5-10 seconds
/// </para>
///
/// <para>
/// <b>Usage Example:</b>
/// <code>
/// var readings = await accelerator.GetSensorReadingsAsync();
/// var tempReading = readings.FirstOrDefault(r => r.SensorType == SensorType.Temperature);
///
/// if (tempReading?.IsAvailable == true)
/// {
///     Console.WriteLine($"GPU Temperature: {tempReading.Value}°C");
///
///     if (tempReading.Value > 85.0)
///     {
///         logger.LogWarning("GPU temperature critical: {temp}°C", tempReading.Value);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed record SensorReading
{
    /// <summary>
    /// Gets the type of sensor this reading represents.
    /// </summary>
    public required SensorType SensorType { get; init; }

    /// <summary>
    /// Gets the sensor value. Interpretation depends on <see cref="SensorType"/>.
    /// </summary>
    /// <remarks>
    /// <b>Common Units by Sensor Type:</b>
    /// - Temperature: Degrees Celsius
    /// - PowerDraw: Watts
    /// - ComputeUtilization/MemoryUtilization/FanSpeed: Percentage (0-100)
    /// - GraphicsClock/MemoryClock: MHz
    /// - Memory*Bytes: Bytes
    /// - PcieThroughput*: Bytes per second
    /// - ThrottlingStatus/PowerThrottlingStatus: Enumerated severity (0-3+)
    /// - ErrorCount: Count
    ///
    /// Always check <see cref="Unit"/> for authoritative unit information.
    /// </remarks>
    public required double Value { get; init; }

    /// <summary>
    /// Gets the unit of measurement for this sensor reading.
    /// </summary>
    /// <example>
    /// "Celsius", "Watts", "Percent", "MHz", "Bytes", "Bytes/sec", "Count"
    /// </example>
    public required string Unit { get; init; }

    /// <summary>
    /// Gets whether this sensor is available on the current device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>false</c>, <see cref="Value"/> should be ignored. This typically occurs when:
    /// - The hardware doesn't support this sensor type
    /// - Driver capabilities are insufficient
    /// - Permissions are lacking (e.g., reading power on Linux without root)
    /// - The sensor failed to initialize
    /// </para>
    ///
    /// <para>
    /// <b>Backend Availability:</b>
    /// - CUDA: All standard sensors typically available (requires NVML)
    /// - Metal: Temperature, memory, utilization available; power limited
    /// - OpenCL: Varies widely by vendor and platform
    /// - CPU: Temperature and utilization usually available
    /// </para>
    /// </remarks>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Gets the timestamp when this reading was captured (UTC).
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets an optional human-readable name for this sensor.
    /// Useful for custom sensors or vendor-specific metrics.
    /// </summary>
    /// <example>
    /// "GPU Core Temperature", "NVLink Throughput", "Infinity Fabric Bandwidth"
    /// </example>
    public string? Name { get; init; }

    /// <summary>
    /// Gets optional minimum expected value for this sensor.
    /// Used for validation and normalization.
    /// </summary>
    /// <remarks>
    /// For percentage-based sensors (utilization, fan speed), typically 0.
    /// For temperature sensors, typically ambient temperature (~20-30°C).
    /// </remarks>
    public double? MinValue { get; init; }

    /// <summary>
    /// Gets optional maximum expected value for this sensor.
    /// Used for validation and alerting thresholds.
    /// </summary>
    /// <remarks>
    /// <b>Common Max Values:</b>
    /// - Temperature: 95-105°C (thermal shutdown threshold)
    /// - PowerDraw: TDP (Thermal Design Power) of the device
    /// - Utilization/FanSpeed: 100 (percent)
    /// - Clocks: Maximum boost clock frequency
    /// </remarks>
    public double? MaxValue { get; init; }

    /// <summary>
    /// Gets the quality indicator for this reading (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Quality factors that may reduce this value:
    /// - Age of reading (stale data)
    /// - Sensor accuracy/precision
    /// - Thermal/electrical noise
    /// - Interpolated vs. directly measured
    /// </para>
    ///
    /// <para>
    /// <b>Quality Guidelines:</b>
    /// - 1.0: Fresh, directly measured, high precision
    /// - 0.9-1.0: Good quality, acceptable for most uses
    /// - 0.5-0.9: Moderate quality, may be stale or estimated
    /// - 0.0-0.5: Poor quality, use with caution
    /// </para>
    /// </remarks>
    public double Quality { get; init; } = 1.0;

    /// <summary>
    /// Creates a sensor reading indicating the sensor is unavailable.
    /// </summary>
    /// <param name="sensorType">The type of sensor.</param>
    /// <param name="name">Optional human-readable name.</param>
    /// <returns>An unavailable sensor reading with default values.</returns>
    public static SensorReading Unavailable(SensorType sensorType, string? name = null)
    {
        return new SensorReading
        {
            SensorType = sensorType,
            Value = 0.0,
            Unit = GetDefaultUnit(sensorType),
            IsAvailable = false,
            Timestamp = DateTimeOffset.UtcNow,
            Name = name,
            Quality = 0.0
        };
    }

    /// <summary>
    /// Creates an available sensor reading with standard unit and timestamp.
    /// </summary>
    /// <param name="sensorType">The type of sensor.</param>
    /// <param name="value">The measured value.</param>
    /// <param name="minValue">Optional minimum expected value.</param>
    /// <param name="maxValue">Optional maximum expected value.</param>
    /// <param name="name">Optional human-readable name.</param>
    /// <returns>An available sensor reading.</returns>
    public static SensorReading Create(
        SensorType sensorType,
        double value,
        double? minValue = null,
        double? maxValue = null,
        string? name = null)
    {
        return new SensorReading
        {
            SensorType = sensorType,
            Value = value,
            Unit = GetDefaultUnit(sensorType),
            IsAvailable = true,
            Timestamp = DateTimeOffset.UtcNow,
            Name = name,
            MinValue = minValue,
            MaxValue = maxValue,
            Quality = 1.0
        };
    }

    /// <summary>
    /// Gets the standard unit for a given sensor type.
    /// </summary>
    private static string GetDefaultUnit(SensorType sensorType)
    {
        return sensorType switch
        {
            SensorType.Temperature => "Celsius",
            SensorType.PowerDraw => "Watts",
            SensorType.ComputeUtilization or
            SensorType.MemoryUtilization or
            SensorType.FanSpeed => "Percent",
            SensorType.GraphicsClock or
            SensorType.MemoryClock => "MHz",
            SensorType.MemoryUsedBytes or
            SensorType.MemoryTotalBytes or
            SensorType.MemoryFreeBytes => "Bytes",
            SensorType.PcieLinkGeneration => "Generation",
            SensorType.PcieLinkWidth => "Lanes",
            SensorType.PcieThroughputTx or
            SensorType.PcieThroughputRx => "Bytes/sec",
            SensorType.ThrottlingStatus or
            SensorType.PowerThrottlingStatus => "Level",
            SensorType.ErrorCount => "Count",
            SensorType.Custom => "Unknown",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Returns a string representation of this sensor reading.
    /// </summary>
    public override string ToString()
    {
        if (!IsAvailable)
        {
            return $"{SensorType}: Unavailable";
        }

        var name = !string.IsNullOrEmpty(Name) ? $" ({Name})" : "";
        return $"{SensorType}{name}: {Value:F2} {Unit} [Quality: {Quality:P0}]";
    }
}
