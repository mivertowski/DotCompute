// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Ports;

/// <summary>
/// Port interface for device health monitoring.
/// Part of hexagonal architecture - defines the contract that backend adapters must implement.
/// </summary>
public interface IHealthMonitoringPort
{
    /// <summary>
    /// Gets a health snapshot for the device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current health snapshot.</returns>
    public ValueTask<HealthSnapshot> GetHealthSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current sensor readings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sensor readings.</returns>
    public ValueTask<IReadOnlyList<SensorReading>> GetSensorReadingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether health monitoring is supported.
    /// </summary>
    public bool IsSupported { get; }
}

/// <summary>
/// A snapshot of device health.
/// </summary>
public sealed record HealthSnapshot
{
    /// <summary>Timestamp of the snapshot.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Overall health status.</summary>
    public required HealthStatus Status { get; init; }

    /// <summary>Temperature in Celsius.</summary>
    public double? TemperatureCelsius { get; init; }

    /// <summary>Power usage in watts.</summary>
    public double? PowerWatts { get; init; }

    /// <summary>GPU utilization (0.0-1.0).</summary>
    public double? GpuUtilization { get; init; }

    /// <summary>Memory utilization (0.0-1.0).</summary>
    public double? MemoryUtilization { get; init; }

    /// <summary>Fan speed percentage (0-100).</summary>
    public int? FanSpeedPercent { get; init; }

    /// <summary>Memory used in bytes.</summary>
    public long? MemoryUsedBytes { get; init; }

    /// <summary>Total memory in bytes.</summary>
    public long? MemoryTotalBytes { get; init; }

    /// <summary>Whether the device is throttling.</summary>
    public bool IsThrottling { get; init; }

    /// <summary>Reason for degraded status.</summary>
    public string? StatusReason { get; init; }
}

/// <summary>
/// Device health status.
/// </summary>
public enum HealthStatus
{
    /// <summary>Unknown status.</summary>
    Unknown,

    /// <summary>Device is healthy.</summary>
    Healthy,

    /// <summary>Device is degraded but operational.</summary>
    Degraded,

    /// <summary>Device is unhealthy.</summary>
    Unhealthy
}

/// <summary>
/// A reading from a device sensor.
/// </summary>
public sealed record SensorReading
{
    /// <summary>Sensor type.</summary>
    public required SensorType Type { get; init; }

    /// <summary>Sensor name.</summary>
    public required string Name { get; init; }

    /// <summary>Current value.</summary>
    public required double Value { get; init; }

    /// <summary>Unit of measurement.</summary>
    public required string Unit { get; init; }

    /// <summary>Minimum safe value.</summary>
    public double? MinValue { get; init; }

    /// <summary>Maximum safe value.</summary>
    public double? MaxValue { get; init; }

    /// <summary>Timestamp of reading.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Type of sensor.
/// </summary>
public enum SensorType
{
    /// <summary>Temperature sensor.</summary>
    Temperature,

    /// <summary>Power consumption.</summary>
    Power,

    /// <summary>Clock speed.</summary>
    Clock,

    /// <summary>Fan speed.</summary>
    Fan,

    /// <summary>Voltage.</summary>
    Voltage,

    /// <summary>Utilization.</summary>
    Utilization,

    /// <summary>Memory bandwidth.</summary>
    MemoryBandwidth,

    /// <summary>Other sensor type.</summary>
    Other
}
