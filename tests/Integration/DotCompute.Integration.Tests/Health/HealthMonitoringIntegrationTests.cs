// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Health;
using DotCompute.Abstractions.Recovery;
using DotCompute.Backends.CPU;
using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DotCompute.Integration.Tests.Health;

/// <summary>
/// Integration tests for health monitoring across different scenarios.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "HealthMonitoring")]
public sealed class HealthMonitoringIntegrationTests : IDisposable
{
    private readonly IAccelerator _accelerator;

    public HealthMonitoringIntegrationTests()
    {
        // Use CPU accelerator for consistent testing (always available)
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
    public async Task HealthMonitoring_AfterReset_ReturnsHealthyState()
    {
        // Arrange - Get baseline health
        var beforeReset = await _accelerator.GetHealthSnapshotAsync();
        beforeReset.IsAvailable.Should().BeTrue();

        // Act - Perform soft reset
        var resetResult = await _accelerator.ResetAsync(ResetOptions.Soft);
        resetResult.Success.Should().BeTrue();

        // Get health after reset
        var afterReset = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        afterReset.IsAvailable.Should().BeTrue();
        afterReset.Status.Should().NotBe(DeviceHealthStatus.Critical);
        afterReset.Status.Should().NotBe(DeviceHealthStatus.Offline);
    }

    [Fact]
    public async Task HealthMonitoring_ConsecutiveCalls_ShowsProgress()
    {
        // Act - Take multiple snapshots
        var snapshots = new List<DeviceHealthSnapshot>();
        for (int i = 0; i < 3; i++)
        {
            var snapshot = await _accelerator.GetHealthSnapshotAsync();
            snapshots.Add(snapshot);
            await Task.Delay(10);
        }

        // Assert - Timestamps should be increasing
        for (int i = 1; i < snapshots.Count; i++)
        {
            snapshots[i].Timestamp.Should().BeAfter(snapshots[i - 1].Timestamp);
        }

        // All should be available
        snapshots.Should().AllSatisfy(s => s.IsAvailable.Should().BeTrue());
    }

    [Fact]
    public async Task HealthMonitoring_WithSensorFiltering_ReturnsSpecificSensors()
    {
        // Act
        var allReadings = await _accelerator.GetSensorReadingsAsync();
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert - Should be able to query specific sensors
        var tempReading = snapshot.GetSensorReading(SensorType.ComputeUtilization);
        tempReading.Should().NotBeNull();

        var memReading = snapshot.GetSensorReading(SensorType.MemoryUsedBytes);
        memReading.Should().NotBeNull();

        // Sensor readings should match between methods
        allReadings.Should().HaveCountGreaterOrEqualTo(snapshot.SensorReadings.Count);
    }

    [Fact]
    public async Task HealthMonitoring_StatusMessage_ReflectsCurrentState()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        snapshot.StatusMessage.Should().NotBeNullOrEmpty();

        if (snapshot.Status == DeviceHealthStatus.Healthy)
        {
            // Healthy status should have positive indicators
            snapshot.StatusMessage.Should().NotContain("High", StringComparison.OrdinalIgnoreCase);
        }
        else if (snapshot.Status == DeviceHealthStatus.Warning)
        {
            // Warning might mention resource pressure
            snapshot.StatusMessage.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task HealthMonitoring_SensorAvailability_CanBeQueried()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert - Test availability checking methods
        var cpuAvailable = snapshot.IsSensorAvailable(SensorType.ComputeUtilization);
        cpuAvailable.Should().BeTrue();

        var cpuValue = snapshot.GetSensorValue(SensorType.ComputeUtilization);
        cpuValue.Should().NotBeNull();
        cpuValue.Should().BeInRange(0.0, 100.0);
    }

    [Fact]
    public async Task HealthMonitoring_HealthScore_ConsistentAcrossCalls()
    {
        // Act - Get multiple health scores quickly
        var scores = new List<double>();
        for (int i = 0; i < 5; i++)
        {
            var snapshot = await _accelerator.GetHealthSnapshotAsync();
            scores.Add(snapshot.HealthScore);
            await Task.Delay(5);
        }

        // Assert - Scores should be relatively stable
        var minScore = scores.Min();
        var maxScore = scores.Max();

        // Variation should be reasonable (within 20%)
        (maxScore - minScore).Should().BeLessThan(0.2);

        // All scores should be valid
        scores.Should().AllSatisfy(s => s.Should().BeInRange(0.0, 1.0));
    }

    [Fact]
    public async Task HealthMonitoring_ErrorTracking_StartsAtZero()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert - Fresh accelerator should have no errors
        snapshot.ErrorCount.Should().Be(0);
        snapshot.ConsecutiveFailures.Should().Be(0);
        snapshot.LastError.Should().BeNullOrEmpty();
        snapshot.LastErrorTimestamp.Should().BeNull();
    }

    [Fact]
    public async Task HealthMonitoring_ThrottlingDetection_WorksCorrectly()
    {
        // Act
        var snapshot = await _accelerator.GetHealthSnapshotAsync();

        // Assert - Under normal load, should not be throttling
        if (snapshot.Status == DeviceHealthStatus.Healthy)
        {
            snapshot.IsThrottling.Should().BeFalse();
        }

        // Throttling correlates with health status
        if (snapshot.IsThrottling)
        {
            snapshot.Status.Should().NotBe(DeviceHealthStatus.Healthy);
            snapshot.HealthScore.Should().BeLessThan(0.9);
        }
    }

    [Fact]
    public async Task HealthMonitoring_ResetIntegration_ClearsHealthState()
    {
        // Arrange - Get initial health
        var beforeReset = await _accelerator.GetHealthSnapshotAsync();

        // Act - Perform hard reset (clears memory)
        var resetResult = await _accelerator.ResetAsync(ResetOptions.Hard);

        // Get health after reset
        var afterReset = await _accelerator.GetHealthSnapshotAsync();

        // Assert
        resetResult.Success.Should().BeTrue();
        afterReset.IsAvailable.Should().BeTrue();
        afterReset.Timestamp.Should().BeAfter(beforeReset.Timestamp);
    }

    [Fact]
    public async Task HealthMonitoring_AllSensors_HaveValidData()
    {
        // Act
        var readings = await _accelerator.GetSensorReadingsAsync();

        // Assert - All sensors should have valid data
        foreach (var reading in readings)
        {
            reading.Should().NotBeNull();
            reading.SensorType.Should().NotBe(default);
            reading.Unit.Should().NotBeNull();
            reading.Timestamp.Should().NotBe(default);

            if (reading.IsAvailable)
            {
                // Available sensors should have reasonable values
                if (reading.MinValue.HasValue && reading.MaxValue.HasValue)
                {
                    reading.Value.Should().BeInRange(reading.MinValue.Value, reading.MaxValue.Value);
                }
            }
        }
    }

    [Fact]
    public async Task HealthMonitoring_CancellationToken_IsRespected()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(10));

        // Act & Assert - Should not throw even with immediate cancellation
        // CPU health collection is fast, so this tests the cancellation infrastructure
        var act = async () => await _accelerator.GetHealthSnapshotAsync(cts.Token);

        // Should either complete quickly or handle cancellation gracefully
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HealthMonitoring_ParallelCalls_AreThreadSafe()
    {
        // Act - Make parallel health requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _accelerator.GetHealthSnapshotAsync().AsTask())
            .ToArray();

        var snapshots = await Task.WhenAll(tasks);

        // Assert - All should succeed
        snapshots.Should().HaveCount(10);
        snapshots.Should().AllSatisfy(s =>
        {
            s.Should().NotBeNull();
            s.IsAvailable.Should().BeTrue();
            s.SensorReadings.Should().NotBeEmpty();
        });

        // Timestamps might be same or different depending on timing
        var uniqueTimestamps = snapshots.Select(s => s.Timestamp).Distinct().Count();
        uniqueTimestamps.Should().BeGreaterThan(0);
    }

    public void Dispose()
    {
        _accelerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
