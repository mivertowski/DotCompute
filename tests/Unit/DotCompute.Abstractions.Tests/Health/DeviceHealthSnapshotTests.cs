// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Health;
using FluentAssertions;
using Xunit;

namespace DotCompute.Abstractions.Tests.Health;

/// <summary>
/// Unit tests for DeviceHealthSnapshot class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Health")]
public sealed class DeviceHealthSnapshotTests
{
    [Fact]
    public void CreateSuccess_WithValidData_ReturnsSnapshot()
    {
        // Arrange
        var deviceId = "cuda-0";
        var deviceName = "NVIDIA RTX 2000 Ada";
        var backendType = "CUDA";
        var timestamp = DateTimeOffset.UtcNow;
        var healthScore = 0.95;
        var status = DeviceHealthStatus.Healthy;
        var sensorReadings = new List<SensorReading>
        {
            SensorReading.Create(SensorType.Temperature, 65.0, minValue: 20.0, maxValue: 95.0),
            SensorReading.Create(SensorType.MemoryUsedBytes, 2048.0 * 1024 * 1024)
        };

        // Act
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            BackendType = backendType,
            Timestamp = timestamp,
            HealthScore = healthScore,
            Status = status,
            IsAvailable = true,
            SensorReadings = sensorReadings
        };

        // Assert
        snapshot.DeviceId.Should().Be(deviceId);
        snapshot.DeviceName.Should().Be(deviceName);
        snapshot.BackendType.Should().Be(backendType);
        snapshot.Timestamp.Should().Be(timestamp);
        snapshot.HealthScore.Should().Be(healthScore);
        snapshot.Status.Should().Be(status);
        snapshot.IsAvailable.Should().BeTrue();
        snapshot.SensorReadings.Should().HaveCount(2);
    }

    [Fact]
    public void CreateUnavailable_WithReason_ReturnsUnavailableSnapshot()
    {
        // Act
        var snapshot = DeviceHealthSnapshot.CreateUnavailable(
            deviceId: "cuda-0",
            deviceName: "Test Device",
            backendType: "CUDA",
            reason: "Device not initialized"
        );

        // Assert
        snapshot.IsAvailable.Should().BeFalse();
        snapshot.HealthScore.Should().Be(0.0);
        snapshot.Status.Should().Be(DeviceHealthStatus.Offline);
        snapshot.StatusMessage.Should().Contain("Device not initialized");
        snapshot.SensorReadings.Should().BeEmpty();
    }

    [Fact]
    public void GetSensorReading_ExistingSensor_ReturnsSensorReading()
    {
        // Arrange
        var tempReading = SensorReading.Create(SensorType.Temperature, 65.0);
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "test",
            DeviceName = "Test",
            BackendType = "Test",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 1.0,
            Status = DeviceHealthStatus.Healthy,
            IsAvailable = true,
            SensorReadings = new[] { tempReading }
        };

        // Act
        var result = snapshot.GetSensorReading(SensorType.Temperature);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be(65.0);
        result.SensorType.Should().Be(SensorType.Temperature);
    }

    [Fact]
    public void GetSensorReading_NonExistentSensor_ReturnsNull()
    {
        // Arrange
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "test",
            DeviceName = "Test",
            BackendType = "Test",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 1.0,
            Status = DeviceHealthStatus.Healthy,
            IsAvailable = true,
            SensorReadings = Array.Empty<SensorReading>()
        };

        // Act
        var result = snapshot.GetSensorReading(SensorType.Temperature);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsSensorAvailable_AvailableSensor_ReturnsTrue()
    {
        // Arrange
        var tempReading = SensorReading.Create(SensorType.Temperature, 65.0);
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "test",
            DeviceName = "Test",
            BackendType = "Test",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 1.0,
            Status = DeviceHealthStatus.Healthy,
            IsAvailable = true,
            SensorReadings = new[] { tempReading }
        };

        // Act
        var result = snapshot.IsSensorAvailable(SensorType.Temperature);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSensorAvailable_UnavailableSensor_ReturnsFalse()
    {
        // Arrange
        var unavailableReading = SensorReading.Unavailable(SensorType.Temperature);
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "test",
            DeviceName = "Test",
            BackendType = "Test",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 1.0,
            Status = DeviceHealthStatus.Healthy,
            IsAvailable = true,
            SensorReadings = new[] { unavailableReading }
        };

        // Act
        var result = snapshot.IsSensorAvailable(SensorType.Temperature);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetSensorValue_AvailableSensor_ReturnsValue()
    {
        // Arrange
        var tempReading = SensorReading.Create(SensorType.Temperature, 65.0);
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "test",
            DeviceName = "Test",
            BackendType = "Test",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 1.0,
            Status = DeviceHealthStatus.Healthy,
            IsAvailable = true,
            SensorReadings = new[] { tempReading }
        };

        // Act
        var result = snapshot.GetSensorValue(SensorType.Temperature);

        // Assert
        result.Should().Be(65.0);
    }

    [Fact]
    public void GetSensorValue_UnavailableSensor_ReturnsNull()
    {
        // Arrange
        var unavailableReading = SensorReading.Unavailable(SensorType.Temperature);
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "test",
            DeviceName = "Test",
            BackendType = "Test",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 1.0,
            Status = DeviceHealthStatus.Healthy,
            IsAvailable = true,
            SensorReadings = new[] { unavailableReading }
        };

        // Act
        var result = snapshot.GetSensorValue(SensorType.Temperature);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(DeviceHealthStatus.Healthy, 0.95)]
    [InlineData(DeviceHealthStatus.Warning, 0.75)]
    [InlineData(DeviceHealthStatus.Critical, 0.55)]
    [InlineData(DeviceHealthStatus.Unknown, 0.0)]
    public void HealthScore_CorrelatesWithStatus(DeviceHealthStatus status, double expectedScore)
    {
        // Arrange & Act
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "test",
            DeviceName = "Test",
            BackendType = "Test",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = expectedScore,
            Status = status,
            IsAvailable = true,
            SensorReadings = Array.Empty<SensorReading>()
        };

        // Assert
        snapshot.HealthScore.Should().BeApproximately(expectedScore, 0.01);
        snapshot.Status.Should().Be(status);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "cuda-0",
            DeviceName = "Test Device",
            BackendType = "CUDA",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 0.95,
            Status = DeviceHealthStatus.Healthy,
            IsAvailable = true,
            SensorReadings = Array.Empty<SensorReading>()
        };

        // Act
        var result = snapshot.ToString();

        // Assert
        result.Should().Contain("Test Device");
        result.Should().Contain("CUDA");
    }

    [Fact]
    public void ErrorTracking_WithErrors_ReturnsErrorInfo()
    {
        // Arrange
        var lastError = "CUDA out of memory";
        var lastErrorTime = DateTimeOffset.UtcNow.AddMinutes(-5);

        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "cuda-0",
            DeviceName = "Test Device",
            BackendType = "CUDA",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 0.6,
            Status = DeviceHealthStatus.Warning,
            IsAvailable = true,
            SensorReadings = Array.Empty<SensorReading>(),
            ErrorCount = 3,
            LastError = lastError,
            LastErrorTimestamp = lastErrorTime,
            ConsecutiveFailures = 2
        };

        // Assert
        snapshot.ErrorCount.Should().Be(3);
        snapshot.LastError.Should().Be(lastError);
        snapshot.LastErrorTimestamp.Should().Be(lastErrorTime);
        snapshot.ConsecutiveFailures.Should().Be(2);
    }

    [Fact]
    public void ThrottlingDetection_WhenThrottling_IsThrottlingTrue()
    {
        // Arrange
        var snapshot = new DeviceHealthSnapshot
        {
            DeviceId = "cuda-0",
            DeviceName = "Test Device",
            BackendType = "CUDA",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = 0.7,
            Status = DeviceHealthStatus.Warning,
            IsAvailable = true,
            SensorReadings = Array.Empty<SensorReading>(),
            IsThrottling = true
        };

        // Assert
        snapshot.IsThrottling.Should().BeTrue();
    }
}
