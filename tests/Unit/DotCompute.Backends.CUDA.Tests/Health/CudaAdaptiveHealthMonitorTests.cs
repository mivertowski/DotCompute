// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Health;
using DotCompute.Abstractions.Temporal;
using DotCompute.Backends.CUDA.Health;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Health;

/// <summary>
/// Unit tests for <see cref="CudaAdaptiveHealthMonitor"/>.
/// Validates adaptive monitoring intervals, trend analysis, causal failure analysis,
/// and HLC-based temporal ordering.
/// </summary>
public sealed class CudaAdaptiveHealthMonitorTests : IDisposable
{
    private readonly CudaAdaptiveHealthMonitor _monitor;

    public CudaAdaptiveHealthMonitorTests()
    {
        _monitor = new CudaAdaptiveHealthMonitor("test-monitor");
    }

    public void Dispose()
    {
        _monitor.Dispose();
    }

    [Fact]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Act & Assert
        _monitor.MonitorId.Should().Be("test-monitor");
        _monitor.CurrentMonitoringInterval.Should().Be(TimeSpan.FromSeconds(2)); // Healthy default
        _monitor.TotalSnapshotCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_NullMonitorId_ThrowsArgumentException()
    {
        // Act
        var act = () => new CudaAdaptiveHealthMonitor(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Monitor ID*");
    }

    [Fact]
    public void Constructor_NegativeMaxHistorySize_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new CudaAdaptiveHealthMonitor("test", maxHistorySize: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Max history size*");
    }

    [Fact]
    public async Task RecordSnapshotAsync_SingleSnapshot_IncrementsTotalCount()
    {
        // Arrange
        var snapshot = CreateHealthSnapshot(temperature: 60.0);
        var timestamp = CreateTimestamp(1_000_000_000L);

        // Act
        await _monitor.RecordSnapshotAsync(snapshot, timestamp);

        // Assert
        _monitor.TotalSnapshotCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordSnapshotAsync_MultipleSnapshots_RecordsInOrder()
    {
        // Arrange
        var timestamps = new[]
        {
            CreateTimestamp(1_000_000_000L),
            CreateTimestamp(2_000_000_000L),
            CreateTimestamp(3_000_000_000L)
        };

        // Act
        foreach (var timestamp in timestamps)
        {
            var snapshot = CreateHealthSnapshot(temperature: 60.0);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        // Assert
        _monitor.TotalSnapshotCount.Should().Be(3);

        var history = await _monitor.GetRecentHistoryAsync(10);
        history.Should().HaveCount(3);
        history.Should().BeInAscendingOrder(h => h.Timestamp);
    }

    [Fact]
    public async Task RecordSnapshotAsync_ExceedsMaxHistory_TrimsOldestSnapshots()
    {
        // Arrange
        using var smallMonitor = new CudaAdaptiveHealthMonitor("test", maxHistorySize: 5);

        // Act - Record 10 snapshots
        for (int i = 0; i < 10; i++)
        {
            var snapshot = CreateHealthSnapshot(temperature: 60.0);
            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await smallMonitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        // Assert - Only last 5 should remain
        var history = await smallMonitor.GetRecentHistoryAsync(100);
        history.Should().HaveCount(5);
        history[0].Timestamp.PhysicalTimeNanos.Should().Be(6_000_000_000L);
        history[^1].Timestamp.PhysicalTimeNanos.Should().Be(10_000_000_000L);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_EmptyHistory_ReturnsHealthyStatus()
    {
        // Arrange
        var currentTime = CreateTimestamp(5_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeTrendsAsync(currentTime);

        // Assert
        result.OverallStatus.Should().Be(HealthStatus.Healthy);
        result.RecommendedInterval.Should().Be(TimeSpan.FromSeconds(2));
        result.FailureRiskScore.Should().Be(0.0);
        result.DetectedTrends.Should().BeEmpty();
        result.SnapshotsAnalyzed.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_StableTemperature_ReportsHealthy()
    {
        // Arrange - Record 60 seconds of stable temperature
        for (int i = 0; i < 60; i++)
        {
            var snapshot = CreateHealthSnapshot(temperature: 65.0);
            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        var currentTime = CreateTimestamp(61_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeTrendsAsync(currentTime);

        // Assert
        result.OverallStatus.Should().Be(HealthStatus.Optimal); // Low temp, good memory
        result.FailureRiskScore.Should().BeLessThan(0.3);
        result.DetectedTrends.Should().BeEmpty();
        result.SnapshotsAnalyzed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_RapidTemperatureRise_DetectsTrendAndPrediction()
    {
        // Arrange - Simulate temperature rising 10°C per minute
        double baseTemp = 60.0;
        for (int i = 0; i < 60; i++)
        {
            double temp = baseTemp + (i / 60.0) * 10.0; // +10°C over 60 seconds
            var snapshot = CreateHealthSnapshot(temperature: temp);
            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        var currentTime = CreateTimestamp(61_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeTrendsAsync(currentTime);

        // Assert
        result.DetectedTrends.Should().Contain(t => t.Contains("Temperature rising"));
        result.FailureRiskScore.Should().BeGreaterThan(0.5);
        result.Predictions.Should().NotBeNull();
        result.Predictions.Should().Contain(p => p.Type == FailureType.Overheating);
        result.OverallStatus.Should().BeOneOf(HealthStatus.Degraded, HealthStatus.Critical);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_HighMemoryPressure_DetectsMemoryExhaustion()
    {
        // Arrange - Memory at 95% utilization
        for (int i = 0; i < 10; i++)
        {
            var snapshot = CreateHealthSnapshot(
                temperature: 65.0,
                memoryUsedBytes: 95_000_000_000.0, // 95GB
                memoryTotalBytes: 100_000_000_000.0); // 100GB

            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        var currentTime = CreateTimestamp(11_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeTrendsAsync(currentTime);

        // Assert
        result.DetectedTrends.Should().Contain(t => t.Contains("Memory pressure"));
        result.FailureRiskScore.Should().BeGreaterThan(0.5);
        result.Predictions.Should().NotBeNull();
        result.Predictions.Should().Contain(p => p.Type == FailureType.MemoryExhaustion);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_PerformanceDegradation_DetectsTrend()
    {
        // Arrange - Utilization drops from 90% to 50%
        for (int i = 0; i < 30; i++)
        {
            double utilization = 90.0 - (i / 30.0) * 40.0;
            var snapshot = CreateHealthSnapshot(temperature: 65.0, computeUtilization: utilization);
            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        var currentTime = CreateTimestamp(31_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeTrendsAsync(currentTime);

        // Assert
        result.DetectedTrends.Should().Contain(t => t.Contains("Performance degrading"));
        result.Predictions.Should().NotBeNull();
        result.Predictions.Should().Contain(p => p.Type == FailureType.PerformanceDegradation);
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_CriticalState_RecommendsFastInterval()
    {
        // Arrange - Temperature at 90°C (critical threshold)
        for (int i = 0; i < 10; i++)
        {
            var snapshot = CreateHealthSnapshot(temperature: 90.0);
            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        var currentTime = CreateTimestamp(11_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeTrendsAsync(currentTime);

        // Assert
        result.OverallStatus.Should().Be(HealthStatus.Critical);
        result.RecommendedInterval.Should().Be(TimeSpan.FromMilliseconds(100)); // Fast monitoring
        _monitor.CurrentMonitoringInterval.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task AnalyzeTrendsAsync_OptimalState_RecommendsSlowInterval()
    {
        // Arrange - Optimal conditions: low temp, low memory, high utilization
        for (int i = 0; i < 10; i++)
        {
            var snapshot = CreateHealthSnapshot(
                temperature: 65.0,
                memoryUsedBytes: 50_000_000_000.0,
                memoryTotalBytes: 100_000_000_000.0,
                computeUtilization: 85.0);

            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        var currentTime = CreateTimestamp(11_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeTrendsAsync(currentTime);

        // Assert
        result.OverallStatus.Should().Be(HealthStatus.Optimal);
        result.RecommendedInterval.Should().Be(TimeSpan.FromSeconds(10)); // Slow monitoring
    }

    [Fact]
    public async Task AnalyzeCausalFailuresAsync_NoFailures_ReturnsEmptyAnalysis()
    {
        // Arrange - Record healthy snapshots
        for (int i = 0; i < 10; i++)
        {
            var snapshot = CreateHealthSnapshot(temperature: 65.0);
            var timestamp = CreateTimestamp(1_000_000_000L + i * 1_000_000_000L);
            await _monitor.RecordSnapshotAsync(snapshot, timestamp);
        }

        var failureTime = CreateTimestamp(11_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeCausalFailuresAsync(failureTime);

        // Assert
        result.TargetFailureTime.Should().Be(failureTime);
        result.PrecedingFailures.Should().BeEmpty();
        result.RootCause.Should().BeNull();
        result.CausalChainLength.Should().Be(0);
        result.Summary.Should().Contain("No causal failures");
    }

    [Fact]
    public async Task AnalyzeCausalFailuresAsync_SingleFailure_IdentifiesRootCause()
    {
        // Arrange - One failure event
        var failureSnapshot = CreateHealthSnapshot(temperature: 95.0); // Overheating
        var failureTimestamp = CreateTimestamp(5_000_000_000L);
        await _monitor.RecordSnapshotAsync(failureSnapshot, failureTimestamp);

        var targetTime = CreateTimestamp(10_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeCausalFailuresAsync(targetTime);

        // Assert
        result.PrecedingFailures.Should().HaveCount(1);
        result.RootCause.Should().NotBeNull();
        result.RootCause!.Value.Timestamp.Should().Be(failureTimestamp);
        result.CausalChainLength.Should().Be(1);
        result.PropagationTime.Should().Be(TimeSpan.FromSeconds(5));
        result.Summary.Should().Contain("Root Cause");
        result.Summary.Should().Contain("Overheating");
    }

    [Fact]
    public async Task AnalyzeCausalFailuresAsync_MultipleFailures_BuildsCausalChain()
    {
        // Arrange - Failure cascade: memory → temp → error
        var failures = new[]
        {
            (time: 1_000_000_000L, snapshot: CreateHealthSnapshot(
                temperature: 65.0,
                memoryUsedBytes: 96_000_000_000.0,
                memoryTotalBytes: 100_000_000_000.0)), // Memory failure

            (time: 3_000_000_000L, snapshot: CreateHealthSnapshot(
                temperature: 92.0)), // Temp failure

            (time: 5_000_000_000L, snapshot: CreateHealthSnapshot(
                temperature: 95.0,
                errorCount: 1)) // Error failure
        };

        foreach (var (time, snapshot) in failures)
        {
            await _monitor.RecordSnapshotAsync(snapshot, CreateTimestamp(time));
        }

        var targetTime = CreateTimestamp(6_000_000_000L);

        // Act
        var result = await _monitor.AnalyzeCausalFailuresAsync(targetTime);

        // Assert
        result.PrecedingFailures.Should().HaveCount(3);
        result.RootCause.Should().NotBeNull();
        result.RootCause!.Value.Timestamp.PhysicalTimeNanos.Should().Be(1_000_000_000L);
        result.CausalChainLength.Should().Be(3);
        result.PropagationTime.Should().Be(TimeSpan.FromSeconds(5));
        result.Summary.Should().Contain("3 failures");
        result.Summary.Should().Contain("Memory exhaustion");
    }

    [Fact]
    public async Task AnalyzeCausalFailuresAsync_RespectsLookbackWindow()
    {
        // Arrange - Failure outside lookback window
        var oldFailure = CreateHealthSnapshot(temperature: 95.0);
        await _monitor.RecordSnapshotAsync(oldFailure, CreateTimestamp(1_000_000_000L));

        var recentFailure = CreateHealthSnapshot(temperature: 92.0);
        await _monitor.RecordSnapshotAsync(recentFailure, CreateTimestamp(290_000_000_000L));

        var targetTime = CreateTimestamp(300_000_000_000L);
        var lookback = TimeSpan.FromSeconds(20); // Should only see recent failure

        // Act
        var result = await _monitor.AnalyzeCausalFailuresAsync(targetTime, lookback);

        // Assert
        result.PrecedingFailures.Should().HaveCount(1);
        result.RootCause!.Value.Timestamp.PhysicalTimeNanos.Should().Be(290_000_000_000L);
    }

    [Fact]
    public async Task GetRecentHistoryAsync_ReturnsInChronologicalOrder()
    {
        // Arrange - Record snapshots with varying HLC timestamps
        var timestamps = new[] { 3L, 1L, 5L, 2L, 4L }; // Out of order

        foreach (var time in timestamps)
        {
            var snapshot = CreateHealthSnapshot(temperature: 60.0);
            await _monitor.RecordSnapshotAsync(snapshot, CreateTimestamp(time * 1_000_000_000L));
        }

        // Act
        var history = await _monitor.GetRecentHistoryAsync(10);

        // Assert
        history.Should().HaveCount(5);
        history.Should().BeInAscendingOrder(h => h.Timestamp); // Should be sorted
        history[0].Timestamp.PhysicalTimeNanos.Should().Be(1_000_000_000L);
        history[^1].Timestamp.PhysicalTimeNanos.Should().Be(5_000_000_000L);
    }

    [Fact]
    public async Task GetRecentHistoryAsync_RespectsCountLimit()
    {
        // Arrange - Record 20 snapshots
        for (int i = 0; i < 20; i++)
        {
            var snapshot = CreateHealthSnapshot(temperature: 60.0);
            await _monitor.RecordSnapshotAsync(snapshot, CreateTimestamp((i + 1) * 1_000_000_000L));
        }

        // Act
        var history = await _monitor.GetRecentHistoryAsync(count: 5);

        // Assert
        history.Should().HaveCount(5);
        history[0].Timestamp.PhysicalTimeNanos.Should().Be(16_000_000_000L); // Last 5
        history[^1].Timestamp.PhysicalTimeNanos.Should().Be(20_000_000_000L);
    }

    [Fact]
    public async Task GetRecentHistoryAsync_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => _monitor.GetRecentHistoryAsync(count: -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*Count must be positive*");
    }

    [Fact]
    public async Task GetRecentHistoryAsync_CountExceedsMax_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => _monitor.GetRecentHistoryAsync(count: 1001);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*cannot exceed 1000*");
    }

    [Fact]
    public async Task ResetAsync_ClearsHistoryAndResetsState()
    {
        // Arrange - Record some snapshots and trigger degraded state
        for (int i = 0; i < 10; i++)
        {
            var snapshot = CreateHealthSnapshot(temperature: 90.0); // High temp
            await _monitor.RecordSnapshotAsync(snapshot, CreateTimestamp((i + 1) * 1_000_000_000L));
        }

        await _monitor.AnalyzeTrendsAsync(CreateTimestamp(11_000_000_000L));
        _monitor.TotalSnapshotCount.Should().BeGreaterThan(0);

        // Act
        await _monitor.ResetAsync();

        // Assert
        _monitor.TotalSnapshotCount.Should().Be(0);
        _monitor.CurrentMonitoringInterval.Should().Be(TimeSpan.FromSeconds(2)); // Back to healthy
        var history = await _monitor.GetRecentHistoryAsync(100);
        history.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Act & Assert - Should not throw
        _monitor.Dispose();
        _monitor.Dispose();
    }

    [Fact]
    public async Task DisposedMonitor_ThrowsObjectDisposedException()
    {
        // Arrange
        _monitor.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _monitor.RecordSnapshotAsync(
                CreateHealthSnapshot(temperature: 60.0),
                CreateTimestamp(1_000_000_000L)));
    }

    [Fact]
    public async Task ConcurrentRecording_ThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int snapshotsPerThread = 100;

        // Act - Record snapshots concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (int i = 0; i < snapshotsPerThread; i++)
            {
                var snapshot = CreateHealthSnapshot(temperature: 60.0 + threadId);
                var timestamp = CreateTimestamp(threadId * 1_000_000_000L + i * 10_000_000L);
                await _monitor.RecordSnapshotAsync(snapshot, timestamp);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        _monitor.TotalSnapshotCount.Should().Be(threadCount * snapshotsPerThread);
        var history = await _monitor.GetRecentHistoryAsync(1000);
        history.Should().HaveCount(threadCount * snapshotsPerThread);
    }

    // Helper methods

    private static DeviceHealthSnapshot CreateHealthSnapshot(
        double temperature = 60.0,
        double memoryUsedBytes = 50_000_000_000.0,
        double memoryTotalBytes = 100_000_000_000.0,
        double computeUtilization = 80.0,
        long errorCount = 0)
    {
        var sensorReadings = new List<SensorReading>
        {
            new SensorReading
            {
                SensorType = SensorType.Temperature,
                Value = temperature,
                IsAvailable = true,
                Unit = "°C",
                Timestamp = DateTimeOffset.UtcNow
            },
            new SensorReading
            {
                SensorType = SensorType.MemoryUsedBytes,
                Value = memoryUsedBytes,
                IsAvailable = true,
                Unit = "bytes",
                Timestamp = DateTimeOffset.UtcNow
            },
            new SensorReading
            {
                SensorType = SensorType.MemoryTotalBytes,
                Value = memoryTotalBytes,
                IsAvailable = true,
                Unit = "bytes",
                Timestamp = DateTimeOffset.UtcNow
            },
            new SensorReading
            {
                SensorType = SensorType.ComputeUtilization,
                Value = computeUtilization,
                IsAvailable = true,
                Unit = "%",
                Timestamp = DateTimeOffset.UtcNow
            }
        };

        double healthScore = 1.0;
        if (errorCount > 0)
            healthScore -= 0.3;
        if (temperature > 80.0)
            healthScore -= 0.3;
        if (memoryUsedBytes / memoryTotalBytes > 0.9)
            healthScore -= 0.2;

        healthScore = Math.Max(0.0, Math.Min(1.0, healthScore));

        var status = DeviceHealthStatus.Healthy;
        if (healthScore < 0.5)
            status = DeviceHealthStatus.Critical;
        else if (healthScore < 0.7)
            status = DeviceHealthStatus.Warning;

        return new DeviceHealthSnapshot
        {
            DeviceId = "cuda:0",
            DeviceName = "Test GPU",
            BackendType = "CUDA",
            Timestamp = DateTimeOffset.UtcNow,
            HealthScore = healthScore,
            Status = status,
            IsAvailable = true,
            SensorReadings = sensorReadings,
            ErrorCount = errorCount,
            ConsecutiveFailures = errorCount > 0 ? 1 : 0,
            IsThrottling = temperature > 85.0
        };
    }

    private static HlcTimestamp CreateTimestamp(long physicalTimeNanos, int logicalCounter = 0)
    {
        return new HlcTimestamp
        {
            PhysicalTimeNanos = physicalTimeNanos,
            LogicalCounter = logicalCounter
        };
    }
}
