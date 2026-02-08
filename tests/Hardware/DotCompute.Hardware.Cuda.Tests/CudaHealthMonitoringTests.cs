// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Health;
using DotCompute.Backends.CUDA;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests;

/// <summary>
/// Hardware tests for CUDA health monitoring functionality.
/// Requires NVIDIA GPU with NVML support.
/// </summary>
[Collection("Hardware")]
public class CudaHealthMonitoringTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CudaAccelerator> _logger;
    private readonly CudaAccelerator? _accelerator;
    private readonly bool _isAvailable;

    public CudaHealthMonitoringTests(ITestOutputHelper output)
    {
        _output = output;

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        _logger = loggerFactory.CreateLogger<CudaAccelerator>();

        try
        {
            _accelerator = new CudaAccelerator(deviceId: 0, logger: _logger);
            _isAvailable = _accelerator.IsAvailable;

            if (_isAvailable)
            {
                _output.WriteLine($"CUDA Device Available: {_accelerator.Info.Name}");
            }
            else
            {
                _output.WriteLine("CUDA Device not available - tests will be skipped");
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Failed to initialize CUDA: {ex.Message}");
            _isAvailable = false;
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_ReturnsValidSnapshot()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var snapshot = await _accelerator!.GetHealthSnapshotAsync();

        // Assert
        Assert.NotNull(snapshot);
        _output.WriteLine($"Device: {snapshot.DeviceName}");
        _output.WriteLine($"Backend: {snapshot.BackendType}");
        _output.WriteLine($"Health Score: {snapshot.HealthScore:P1}");
        _output.WriteLine($"Status: {snapshot.Status}");
        _output.WriteLine($"Available: {snapshot.IsAvailable}");
        _output.WriteLine($"Throttling: {snapshot.IsThrottling}");
        _output.WriteLine($"Sensor Count: {snapshot.SensorReadings.Count}");

        if (snapshot.IsAvailable)
        {
            Assert.Equal("CUDA", snapshot.BackendType);
            Assert.True(snapshot.HealthScore >= 0.0 && snapshot.HealthScore <= 1.0);
            Assert.NotEqual(DeviceHealthStatus.Unknown, snapshot.Status);
            Assert.NotEmpty(snapshot.SensorReadings);
        }
        else
        {
            _output.WriteLine($"Reason: {snapshot.StatusMessage}");
        }
    }

    [SkippableFact]
    public async Task GetSensorReadingsAsync_Returns14SensorTypes()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var readings = await _accelerator!.GetSensorReadingsAsync();

        // Assert
        Assert.NotNull(readings);
        _output.WriteLine($"Total Sensors: {readings.Count}");

        if (readings.Count > 0)
        {
            // Should have 14 sensor types from NVML
            Assert.Equal(14, readings.Count);

            // Verify all sensor types are present
            var sensorTypes = readings.Select(r => r.SensorType).ToHashSet();
            Assert.Contains(SensorType.Temperature, sensorTypes);
            Assert.Contains(SensorType.PowerDraw, sensorTypes);
            Assert.Contains(SensorType.ComputeUtilization, sensorTypes);
            Assert.Contains(SensorType.MemoryUtilization, sensorTypes);
            Assert.Contains(SensorType.FanSpeed, sensorTypes);
            Assert.Contains(SensorType.GraphicsClock, sensorTypes);
            Assert.Contains(SensorType.MemoryClock, sensorTypes);
            Assert.Contains(SensorType.MemoryUsedBytes, sensorTypes);
            Assert.Contains(SensorType.MemoryTotalBytes, sensorTypes);
            Assert.Contains(SensorType.MemoryFreeBytes, sensorTypes);
            Assert.Contains(SensorType.PcieThroughputTx, sensorTypes);
            Assert.Contains(SensorType.PcieThroughputRx, sensorTypes);
            Assert.Contains(SensorType.ThrottlingStatus, sensorTypes);
            Assert.Contains(SensorType.PowerThrottlingStatus, sensorTypes);

            // Log each sensor reading
            foreach (var reading in readings.Where(r => r.IsAvailable))
            {
                _output.WriteLine($"  {reading.SensorType}: {reading.Value:F2} {reading.Unit}");
            }
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_TemperatureSensorInValidRange()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var snapshot = await _accelerator!.GetHealthSnapshotAsync();

        // Assert
        if (snapshot.IsAvailable)
        {
            var tempReading = snapshot.GetSensorReading(SensorType.Temperature);
            Assert.NotNull(tempReading);

            if (tempReading.IsAvailable)
            {
                _output.WriteLine($"Temperature: {tempReading.Value}°C");

                // Temperature should be in reasonable range (20-95°C)
                Assert.True(tempReading.Value >= 20.0, "Temperature too low");
                Assert.True(tempReading.Value <= 95.0, "Temperature too high");
                Assert.Equal("Celsius", tempReading.Unit);
            }
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_MemoryMetricsConsistent()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var snapshot = await _accelerator!.GetHealthSnapshotAsync();

        // Assert
        if (snapshot.IsAvailable)
        {
            var memUsed = snapshot.GetSensorValue(SensorType.MemoryUsedBytes);
            var memTotal = snapshot.GetSensorValue(SensorType.MemoryTotalBytes);
            var memFree = snapshot.GetSensorValue(SensorType.MemoryFreeBytes);

            if (memUsed.HasValue && memTotal.HasValue && memFree.HasValue)
            {
                _output.WriteLine($"Memory Used: {memUsed.Value / (1024 * 1024 * 1024):F2} GB");
                _output.WriteLine($"Memory Total: {memTotal.Value / (1024 * 1024 * 1024):F2} GB");
                _output.WriteLine($"Memory Free: {memFree.Value / (1024 * 1024 * 1024):F2} GB");

                // Used + Free should approximately equal Total
                var sumDelta = Math.Abs((memUsed.Value + memFree.Value) - memTotal.Value);
                var tolerance = memTotal.Value * 0.05; // 5% tolerance

                Assert.True(sumDelta <= tolerance,
                    $"Memory sum inconsistent: Used({memUsed}) + Free({memFree}) != Total({memTotal})");

                // Used should not exceed Total
                Assert.True(memUsed.Value <= memTotal.Value, "Used memory exceeds total memory");
            }
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_HealthScoreInRange()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var snapshot = await _accelerator!.GetHealthSnapshotAsync();

        // Assert
        if (snapshot.IsAvailable)
        {
            _output.WriteLine($"Health Score: {snapshot.HealthScore:P1}");
            _output.WriteLine($"Status: {snapshot.Status}");

            // Health score should be between 0.0 and 1.0
            Assert.True(snapshot.HealthScore >= 0.0);
            Assert.True(snapshot.HealthScore <= 1.0);

            // Health score should correlate with status
            if (snapshot.HealthScore >= 0.9)
            {
                Assert.Equal(DeviceHealthStatus.Healthy, snapshot.Status);
            }
            else if (snapshot.HealthScore >= 0.7)
            {
                Assert.True(snapshot.Status == DeviceHealthStatus.Healthy ||
                           snapshot.Status == DeviceHealthStatus.Warning);
            }
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_MultipleCalls_ReturnsFreshData()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act - Get two snapshots with a small delay
        var snapshot1 = await _accelerator!.GetHealthSnapshotAsync();
        await Task.Delay(100);
        var snapshot2 = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        if (snapshot1.IsAvailable && snapshot2.IsAvailable)
        {
            // Timestamps should be different
            Assert.NotEqual(snapshot1.Timestamp, snapshot2.Timestamp);
            _output.WriteLine($"First snapshot: {snapshot1.Timestamp}");
            _output.WriteLine($"Second snapshot: {snapshot2.Timestamp}");

            // Both should have sensor readings
            Assert.NotEmpty(snapshot1.SensorReadings);
            Assert.NotEmpty(snapshot2.SensorReadings);
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_IsSensorAvailable_WorksCorrectly()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var snapshot = await _accelerator!.GetHealthSnapshotAsync();

        // Assert
        if (snapshot.IsAvailable)
        {
            // Temperature should always be available on CUDA
            Assert.True(snapshot.IsSensorAvailable(SensorType.Temperature));

            // Check multiple sensors
            var availableSensors = new[]
            {
                SensorType.Temperature,
                SensorType.MemoryUsedBytes,
                SensorType.MemoryTotalBytes
            };

            foreach (var sensorType in availableSensors)
            {
                var available = snapshot.IsSensorAvailable(sensorType);
                _output.WriteLine($"{sensorType}: {(available ? "Available" : "Unavailable")}");
            }
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_StatusMessage_IsInformative()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var snapshot = await _accelerator!.GetHealthSnapshotAsync();

        // Assert
        if (snapshot.IsAvailable)
        {
            Assert.NotNull(snapshot.StatusMessage);
            _output.WriteLine($"Status Message: {snapshot.StatusMessage}");

            // Status message should contain useful information
            if (snapshot.Status == DeviceHealthStatus.Healthy)
            {
                Assert.Contains("normal", snapshot.StatusMessage, StringComparison.OrdinalIgnoreCase);
            }
            else if (snapshot.IsThrottling)
            {
                Assert.Contains("throttl", snapshot.StatusMessage, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [SkippableFact]
    public async Task GetSensorReadingsAsync_AllSensorsHaveUnits()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var readings = await _accelerator!.GetSensorReadingsAsync();

        // Assert
        if (readings.Count > 0)
        {
            foreach (var reading in readings)
            {
                Assert.NotNull(reading.Unit);
                Assert.NotEmpty(reading.Unit);
                _output.WriteLine($"{reading.SensorType}: {reading.Unit}");
            }
        }
    }

    [SkippableFact]
    public async Task GetHealthSnapshotAsync_ToString_ReturnsFormattedString()
    {
        Skip.IfNot(_isAvailable, "CUDA device not available");

        // Act
        var snapshot = await _accelerator!.GetHealthSnapshotAsync();
        var toString = snapshot.ToString();

        // Assert
        Assert.NotNull(toString);
        Assert.NotEmpty(toString);
        _output.WriteLine($"ToString: {toString}");

        // Should contain device name and backend type
        Assert.Contains(snapshot.DeviceName, toString);
        Assert.Contains(snapshot.BackendType, toString);
    }

    public void Dispose()
    {
        _accelerator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
