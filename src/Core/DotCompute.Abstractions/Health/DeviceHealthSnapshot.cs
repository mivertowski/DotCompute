// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Health;

/// <summary>
/// Represents a point-in-time snapshot of device health and performance metrics.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a comprehensive view of compute device health, including:
/// - Sensor readings (temperature, power, utilization, etc.)
/// - Health status and scoring
/// - Error tracking and diagnostics
/// - Availability information
/// </para>
///
/// <para>
/// <b>Performance Characteristics:</b>
/// - Collection time: 1-10ms depending on backend and sensor count
/// - Memory footprint: ~1-2KB per snapshot
/// - Recommended collection interval: 5-10 seconds for monitoring
/// - Use cached snapshots for high-frequency queries
/// </para>
///
/// <para>
/// <b>Orleans Integration:</b>
/// This class is designed for Orleans.GpuBridge.Core integration, providing
/// health metrics for grain placement decisions, fault tolerance, and observability.
/// </para>
///
/// <para>
/// <b>Usage Example:</b>
/// <code>
/// var snapshot = await accelerator.GetHealthSnapshotAsync();
///
/// if (snapshot.HealthScore &lt; 0.7)
/// {
///     logger.LogWarning("Device health degraded: {score:P0}", snapshot.HealthScore);
///
///     // Check specific issues
///     var tempReading = snapshot.GetSensorReading(SensorType.Temperature);
///     if (tempReading?.IsAvailable == true &amp;&amp; tempReading.Value &gt; 85.0)
///     {
///         logger.LogWarning("High temperature detected: {temp}°C", tempReading.Value);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class DeviceHealthSnapshot
{
    /// <summary>
    /// Gets the unique identifier of the device.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Gets the human-readable name of the device.
    /// </summary>
    /// <example>
    /// "NVIDIA GeForce RTX 4090", "AMD Radeon RX 7900 XTX", "Apple M3 Max"
    /// </example>
    public required string DeviceName { get; init; }

    /// <summary>
    /// Gets the backend type for this device.
    /// </summary>
    /// <example>
    /// "CUDA", "OpenCL", "Metal", "CPU"
    /// </example>
    public required string BackendType { get; init; }

    /// <summary>
    /// Gets the timestamp when this snapshot was captured (UTC).
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the overall health score (0.0 to 1.0).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The health score is a composite metric that considers:
    /// - Temperature (weight: 0.3)
    /// - Error rate (weight: 0.3)
    /// - Utilization (weight: 0.2)
    /// - Throttling status (weight: 0.1)
    /// - Memory pressure (weight: 0.1)
    /// </para>
    ///
    /// <para>
    /// <b>Score Interpretation:</b>
    /// - 1.0: Perfect health
    /// - 0.9-1.0: Excellent (green)
    /// - 0.7-0.9: Good (yellow)
    /// - 0.5-0.7: Degraded (orange)
    /// - 0.0-0.5: Critical (red)
    /// </para>
    ///
    /// <para>
    /// <b>Orleans Usage:</b>
    /// Use health scores for grain placement decisions. Prefer devices with
    /// scores > 0.8 for new grain activations.
    /// </para>
    /// </remarks>
    public required double HealthScore { get; init; }

    /// <summary>
    /// Gets the current health status of the device.
    /// </summary>
    public required DeviceHealthStatus Status { get; init; }

    /// <summary>
    /// Gets whether the device is currently available for computation.
    /// </summary>
    /// <remarks>
    /// <c>false</c> indicates the device is offline, in error state, or otherwise
    /// unavailable. Check <see cref="StatusMessage"/> for details.
    /// </remarks>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Gets the collection of sensor readings captured in this snapshot.
    /// </summary>
    /// <remarks>
    /// May be empty if sensor data collection failed or is not supported.
    /// Use <see cref="GetSensorReading(SensorType)"/> for safe access.
    /// </remarks>
    public required IReadOnlyList<SensorReading> SensorReadings { get; init; }

    /// <summary>
    /// Gets the number of errors detected since device initialization or last reset.
    /// </summary>
    /// <remarks>
    /// Includes:
    /// - ECC (Error Correction Code) errors
    /// - Kernel execution failures
    /// - Device reset events
    /// - Communication timeouts
    ///
    /// High error counts may indicate hardware issues or instability.
    /// </remarks>
    public long ErrorCount { get; init; }

    /// <summary>
    /// Gets the most recent error message, if any.
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Gets the timestamp of the last error occurrence (UTC).
    /// </summary>
    public DateTimeOffset? LastErrorTimestamp { get; init; }

    /// <summary>
    /// Gets the number of consecutive failed operations.
    /// </summary>
    /// <remarks>
    /// Used for circuit breaker patterns and automatic device failover.
    /// Resets to 0 after a successful operation.
    /// </remarks>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Gets whether the device is currently throttling due to thermal or power limits.
    /// </summary>
    public bool IsThrottling { get; init; }

    /// <summary>
    /// Gets an optional status message providing additional health context.
    /// </summary>
    /// <example>
    /// "Operating normally", "High temperature warning", "Power limit exceeded",
    /// "Device offline - driver reset required"
    /// </example>
    public string? StatusMessage { get; init; }

    /// <summary>
    /// Gets additional custom metrics specific to the backend or device.
    /// </summary>
    /// <remarks>
    /// Used for vendor-specific metrics not covered by standard sensors.
    /// Examples:
    /// - "nvlink_throughput_gbps": NVLink bandwidth (NVIDIA)
    /// - "infinity_fabric_utilization": IF utilization (AMD)
    /// - "unified_memory_pressure": Memory pressure level (Apple Silicon)
    /// </remarks>
    public IReadOnlyDictionary<string, double>? CustomMetrics { get; init; }

    /// <summary>
    /// Retrieves a specific sensor reading from this snapshot.
    /// </summary>
    /// <param name="sensorType">The type of sensor to retrieve.</param>
    /// <returns>The sensor reading, or <c>null</c> if not available.</returns>
    public SensorReading? GetSensorReading(SensorType sensorType)
    {
        return SensorReadings.FirstOrDefault(r => r.SensorType == sensorType);
    }

    /// <summary>
    /// Checks if a specific sensor type is available and has a valid reading.
    /// </summary>
    /// <param name="sensorType">The type of sensor to check.</param>
    /// <returns>True if the sensor is available with a valid reading.</returns>
    public bool IsSensorAvailable(SensorType sensorType)
    {
        var reading = GetSensorReading(sensorType);
        return reading?.IsAvailable == true;
    }

    /// <summary>
    /// Gets the value of a specific sensor, or null if unavailable.
    /// </summary>
    /// <param name="sensorType">The type of sensor.</param>
    /// <returns>The sensor value, or null if unavailable.</returns>
    public double? GetSensorValue(SensorType sensorType)
    {
        var reading = GetSensorReading(sensorType);
        return reading?.IsAvailable == true ? reading.Value : null;
    }

    /// <summary>
    /// Creates a health snapshot indicating the device is unavailable.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="deviceName">The device name.</param>
    /// <param name="backendType">The backend type.</param>
    /// <param name="reason">The reason for unavailability.</param>
    /// <returns>An unavailable device health snapshot.</returns>
    public static DeviceHealthSnapshot CreateUnavailable(
        string deviceId,
        string deviceName,
        string backendType,
        string reason)
    {
        return new DeviceHealthSnapshot
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            BackendType = backendType,
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 0.0,
            Status = DeviceHealthStatus.Offline,
            IsAvailable = false,
            SensorReadings = Array.Empty<SensorReading>(),
            ErrorCount = 0,
            ConsecutiveFailures = 0,
            IsThrottling = false,
            StatusMessage = reason
        };
    }

    /// <summary>
    /// Returns a summary string of this health snapshot.
    /// </summary>
    public override string ToString()
    {
        var status = IsAvailable ? $"{Status} (Score: {HealthScore:P0})" : "Unavailable";
        var temp = GetSensorValue(SensorType.Temperature);
        var tempStr = temp.HasValue ? $", Temp: {temp:F1}°C" : "";
        var errors = ErrorCount > 0 ? $", Errors: {ErrorCount}" : "";

        return $"{DeviceName} ({BackendType}): {status}{tempStr}{errors}";
    }
}

/// <summary>
/// Defines the health status levels for compute devices.
/// </summary>
public enum DeviceHealthStatus
{
    /// <summary>
    /// Health status is unknown or could not be determined.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Device is operating normally with no issues detected.
    /// Health score typically > 0.9.
    /// </summary>
    Healthy = 1,

    /// <summary>
    /// Device is operational but showing signs of stress or degradation.
    /// Health score typically 0.7-0.9.
    /// Examples: Elevated temperature, increased errors, minor throttling.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Device is in critical condition with significant health concerns.
    /// Health score typically 0.5-0.7.
    /// Examples: High temperature, frequent throttling, elevated error rate.
    /// </summary>
    Critical = 3,

    /// <summary>
    /// Device is offline or unresponsive.
    /// Health score = 0.0.
    /// Requires intervention (driver reset, hardware check, etc.).
    /// </summary>
    Offline = 4,

    /// <summary>
    /// Device is in an error state and requires recovery action.
    /// Health score typically less than 0.5.
    /// Examples: Driver crash, hardware fault, communication failure.
    /// </summary>
    Error = 5
}
