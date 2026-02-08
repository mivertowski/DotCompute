// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Health;
using DotCompute.Backends.CPU;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DotCompute.Backends.CPU.Tests;

/// <summary>
/// Unit tests for CPU accelerator health monitoring.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "CpuHealth")]
public sealed class CpuAcceleratorHealthTests : IDisposable
{
    private readonly CpuAccelerator _accelerator;

    public CpuAcceleratorHealthTests()
    {
        var acceleratorOptions = Options.Create(new CpuAcceleratorOptions
        {
            EnableAutoVectorization = true,
            PreferPerformanceOverPower = true,
            MaxWorkGroupSize = Environment.ProcessorCount
        });

        var threadPoolOptions = Options.Create(new CpuThreadPoolOptions
        {
            WorkerThreads = Environment.ProcessorCount
        });

        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<CpuAccelerator>();

        _accelerator = new CpuAccelerator(acceleratorOptions, threadPoolOptions, logger);
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_ReturnsValidSnapshot()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.DeviceId.Should().Be(_accelerator.Info.Id);
        snapshot.DeviceName.Should().Be(_accelerator.Info.Name);
        snapshot.BackendType.Should().Be("CPU");
        snapshot.IsAvailable.Should().BeTrue();
        snapshot.HealthScore.Should().BeInRange(0.0, 1.0);
        snapshot.Status.Should().NotBe(DeviceHealthStatus.Unknown);
        snapshot.SensorReadings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_AlwaysAvailable()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert - CPU should always be available
        snapshot.IsAvailable.Should().BeTrue();
        snapshot.Status.Should().NotBe(DeviceHealthStatus.Offline);
        snapshot.HealthScore.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task GetSensorReadingsAsync_ReturnsExpectedSensors()
    {
        // Act
        var readings = await _accelerator.GetSensorReadingsAsync();

        // Assert
        readings.Should().NotBeNull();
        readings.Should().NotBeEmpty();
        readings.Count.Should().BeGreaterThanOrEqualTo(9); // At least 9 sensors

        // Verify key sensor types are present
        var sensorTypes = readings.Select(r => r.SensorType).ToHashSet();
        sensorTypes.Should().Contain(SensorType.ComputeUtilization);
        sensorTypes.Should().Contain(SensorType.MemoryUsedBytes);
    }

    [Fact]
    public async Task GetSensorReadingsAsync_CpuUtilization_InValidRange()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();
        var cpuReading = snapshot.GetSensorReading(SensorType.ComputeUtilization);

        // Assert
        cpuReading.Should().NotBeNull();
        cpuReading!.IsAvailable.Should().BeTrue();
        cpuReading.Value.Should().BeInRange(0.0, 100.0);
        cpuReading.Unit.Should().Be("Percent");
    }

    [Fact]
    public async Task GetSensorReadingsAsync_MemoryMetrics_ArePositive()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        var workingSet = snapshot.GetSensorValue(SensorType.MemoryUsedBytes);
        workingSet.Should().NotBeNull();
        workingSet!.Value.Should().BeGreaterThan(0);

        // Check for other memory sensors
        var readings = snapshot.SensorReadings;
        var memoryReadings = readings.Where(r =>
            r.Name?.Contains("Memory", StringComparison.OrdinalIgnoreCase) == true ||
            r.Name?.Contains("GC", StringComparison.OrdinalIgnoreCase) == true
        ).ToList();

        memoryReadings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSensorReadingsAsync_ThreadCount_IsPositive()
    {
        // Act
        var readings = await _accelerator.GetSensorReadingsAsync();

        // Assert
        var threadCountReading = readings.FirstOrDefault(r => r.Name?.Contains("Thread") == true);
        threadCountReading.Should().NotBeNull();
        threadCountReading!.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSensorReadingsAsync_GCMetrics_AreIncluded()
    {
        // Act
        var readings = await _accelerator.GetSensorReadingsAsync();

        // Assert
        var gcReadings = readings.Where(r => r.Name?.Contains("GC") == true).ToList();
        gcReadings.Should().HaveCountGreaterThanOrEqualTo(4); // Gen0, Gen1, Gen2, Total Memory

        // All GC metrics should be non-negative
        foreach (var reading in gcReadings)
        {
            reading.Value.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_HealthScore_IsReasonable()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        snapshot.HealthScore.Should().BeInRange(0.0, 1.0);

        // Under normal conditions, CPU should be healthy
        snapshot.HealthScore.Should().BeGreaterThanOrEqualTo(0.5);
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_StatusMessage_IsInformative()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        snapshot.StatusMessage.Should().NotBeNullOrEmpty();

        // Should contain CPU usage info
        snapshot.StatusMessage.Should().Contain("CPU:");

        // Should contain memory info
        snapshot.StatusMessage.Should().Contain("Memory:");

        // Should contain thread count
        snapshot.StatusMessage.Should().Contain("Threads:");

        // Should contain SIMD info
        snapshot.StatusMessage.Should().Contain("SIMD:");
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_MultipleCalls_ReturnsFreshData()
    {
        // Act
        var snapshot1 = await _accelerator.GetHealthSnapshotAsync();
        await Task.Delay(50); // Small delay
        var snapshot2 = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        snapshot1.Timestamp.Should().BeBefore(snapshot2.Timestamp);
        snapshot1.Should().NotBeSameAs(snapshot2);
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_ConsecutiveFailures_IsZero()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert - CPU backend doesn't track failures separately
        snapshot.ConsecutiveFailures.Should().Be(0);
        snapshot.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSensorReadingsAsync_AllSensorsHaveUnits()
    {
        // Act
        var readings = await _accelerator.GetSensorReadingsAsync();

        // Assert
        foreach (var reading in readings)
        {
            reading.Unit.Should().NotBeNull();
            // Unit can be empty string for dimensionless quantities
        }
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_ToString_ReturnsFormattedString()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();
        var toString = snapshot.ToString();

        // Assert
        toString.Should().NotBeNullOrEmpty();
        toString.Should().Contain(snapshot.DeviceName);
        toString.Should().Contain("CPU");
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_SensorReadings_HaveTimestamps()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        foreach (var reading in snapshot.SensorReadings)
        {
            reading.Timestamp.Should().BeCloseTo(snapshot.Timestamp, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task GetHealthSnapshotAsync_HealthStatus_CorrelatesWithScore()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        if (snapshot.HealthScore >= 0.8)
        {
            snapshot.Status.Should().Be(DeviceHealthStatus.Healthy);
        }
        else if (snapshot.HealthScore >= 0.5)
        {
            snapshot.Status.Should().Be(DeviceHealthStatus.Warning);
        }
        else
        {
            snapshot.Status.Should().Be(DeviceHealthStatus.Critical);
        }
    }

    public void Dispose()
    {
        _accelerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
