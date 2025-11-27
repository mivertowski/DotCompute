// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels.Profiling;
using DotCompute.Core.RingKernels.Profiling;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.RingKernels.Profiling;

/// <summary>
/// Unit tests for RingKernelPerformanceProfiler class.
/// Tests profiling lifecycle, metric recording, and baseline comparison.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Profiling")]
public sealed class RingKernelPerformanceProfilerTests : IAsyncDisposable
{
    private readonly ILogger<RingKernelPerformanceProfiler> _logger;
    private readonly RingKernelPerformanceProfiler _profiler;

    public RingKernelPerformanceProfilerTests()
    {
        _logger = Substitute.For<ILogger<RingKernelPerformanceProfiler>>();
        _profiler = new RingKernelPerformanceProfiler(_logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _profiler.DisposeAsync();
    }

    [Fact]
    public async Task StartProfilingAsync_ValidKernelId_StartsSession()
    {
        // Arrange
        const string kernelId = "test-kernel";

        // Act
        await _profiler.StartProfilingAsync(kernelId);

        // Assert
        _profiler.ProfiledKernels.Should().Contain(kernelId);
    }

    [Fact]
    public async Task StartProfilingAsync_DuplicateKernelId_ThrowsException()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act
        var act = async () => await _profiler.StartProfilingAsync(kernelId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already active*");
    }

    [Fact]
    public async Task StartProfilingAsync_NullKernelId_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _profiler.StartProfilingAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StopProfilingAsync_ActiveSession_ReturnsReport()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Record some data
        _profiler.RecordLatency(kernelId, 1000);
        _profiler.RecordThroughput(kernelId, 100, 1_000_000_000);

        // Act
        var report = await _profiler.StopProfilingAsync(kernelId);

        // Assert
        report.Should().NotBeNull();
        report.KernelId.Should().Be(kernelId);
        report.Latency.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StopProfilingAsync_NoActiveSession_ThrowsException()
    {
        // Act
        var act = async () => await _profiler.StopProfilingAsync("nonexistent-kernel");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No active*");
    }

    [Fact]
    public async Task RecordLatency_ActiveSession_RecordsValue()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act
        _profiler.RecordLatency(kernelId, 1000);
        _profiler.RecordLatency(kernelId, 2000);

        // Assert
        var percentiles = _profiler.GetLatencyPercentiles(kernelId);
        percentiles.Should().NotBeNull();
        percentiles!.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task RecordLatency_InactiveSession_IsIgnored()
    {
        // Act
        _profiler.RecordLatency("nonexistent-kernel", 1000);

        // Assert - no exception thrown
        _profiler.ProfiledKernels.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordThroughput_ActiveSession_RecordsValue()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act
        _profiler.RecordThroughput(kernelId, 1000, 1_000_000_000); // 1000 msgs in 1 second

        // Assert
        var snapshot = _profiler.GetSnapshot(kernelId);
        snapshot.Should().NotBeNull();
        snapshot!.Value.CurrentThroughputMps.Should().Be(1000);
    }

    [Fact]
    public async Task RecordAllocation_ActiveSession_RecordsGpuBytes()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act
        _profiler.RecordAllocation(kernelId, 1024, "GPU");
        _profiler.RecordAllocation(kernelId, 2048, "HOST");
        _profiler.RecordAllocation(kernelId, 512, "UNIFIED");

        var report = await _profiler.StopProfilingAsync(kernelId);

        // Assert
        report.Allocations.GpuBytes.Should().Be(1024);
        report.Allocations.HostBytes.Should().Be(2048);
        report.Allocations.UnifiedBytes.Should().Be(512);
        report.Allocations.TotalBytes.Should().Be(1024 + 2048 + 512);
        report.Allocations.AllocationCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSnapshot_ActiveSession_ReturnsCurrentMetrics()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        _profiler.RecordLatency(kernelId, 5000);
        _profiler.RecordThroughput(kernelId, 500, 1_000_000_000);

        // Act
        var snapshot = _profiler.GetSnapshot(kernelId);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.Value.KernelId.Should().Be(kernelId);
        snapshot.Value.TotalMessagesProcessed.Should().Be(500);
    }

    [Fact]
    public void GetSnapshot_InactiveSession_ReturnsNull()
    {
        // Act
        var snapshot = _profiler.GetSnapshot("nonexistent-kernel");

        // Assert
        snapshot.Should().BeNull();
    }

    [Fact]
    public async Task SetBaseline_ActiveSession_EnablesComparison()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        var baseline = new PerformanceBaseline
        {
            BaselineId = "baseline-v1",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpectedP50Nanos = 1000,
            ExpectedP99Nanos = 5000,
            ExpectedThroughputMps = 10000,
            ExpectedPeakMemoryBytes = 1024 * 1024,
            Tolerance = 0.1
        };

        // Act
        _profiler.SetBaseline(kernelId, baseline);
        _profiler.RecordLatency(kernelId, 2000); // 2x P50

        var comparison = _profiler.CompareToBaseline(kernelId);

        // Assert
        comparison.Should().NotBeNull();
        comparison!.Baseline.BaselineId.Should().Be("baseline-v1");
        comparison.P50LatencyRatio.Should().BeGreaterThan(1);
    }

    [Fact]
    public void CompareToBaseline_NoBaseline_ReturnsNull()
    {
        // Act
        var comparison = _profiler.CompareToBaseline("nonexistent-kernel");

        // Assert
        comparison.Should().BeNull();
    }

    [Fact]
    public async Task AnomalyDetected_LatencySpike_RaisesEvent()
    {
        // Arrange
        const string kernelId = "test-kernel";
        var options = new ProfilingOptions { EnableAnomalyDetection = true, AnomalyThreshold = 2.0 };
        await _profiler.StartProfilingAsync(kernelId, options);

        var baseline = new PerformanceBaseline
        {
            BaselineId = "baseline",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpectedP50Nanos = 1000,
            ExpectedP99Nanos = 2000,
            ExpectedThroughputMps = 10000,
            ExpectedPeakMemoryBytes = 1024
        };

        _profiler.SetBaseline(kernelId, baseline);

        PerformanceAnomalyEventArgs? capturedEvent = null;
        _profiler.AnomalyDetected += (_, e) => capturedEvent = e;

        // Act - Record latency 10x baseline (exceeds 2x threshold)
        _profiler.RecordLatency(kernelId, 20000);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.KernelId.Should().Be(kernelId);
        capturedEvent.Anomaly.Type.Should().Be(AnomalyType.LatencySpike);
    }

    [Fact]
    public async Task AnomalyDetected_ThroughputDrop_RaisesEvent()
    {
        // Arrange
        const string kernelId = "test-kernel";
        var options = new ProfilingOptions { EnableAnomalyDetection = true, AnomalyThreshold = 2.0 };
        await _profiler.StartProfilingAsync(kernelId, options);

        var baseline = new PerformanceBaseline
        {
            BaselineId = "baseline",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpectedP50Nanos = 1000,
            ExpectedP99Nanos = 2000,
            ExpectedThroughputMps = 10000,
            ExpectedPeakMemoryBytes = 1024
        };

        _profiler.SetBaseline(kernelId, baseline);

        PerformanceAnomalyEventArgs? capturedEvent = null;
        _profiler.AnomalyDetected += (_, e) => capturedEvent = e;

        // Act - Record throughput at 1/10th of baseline (below 1/2x threshold)
        _profiler.RecordThroughput(kernelId, 100, 1_000_000_000); // 100 mps vs expected 10000

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.KernelId.Should().Be(kernelId);
        capturedEvent.Anomaly.Type.Should().Be(AnomalyType.ThroughputDrop);
    }

    [Fact]
    public async Task ProfilingOptions_SamplingRate_FiltersRecordings()
    {
        // Arrange
        const string kernelId = "test-kernel";
        var options = new ProfilingOptions { SamplingRate = 0.1 }; // 10% sampling
        await _profiler.StartProfilingAsync(kernelId, options);

        // Act - Record many latencies
        for (var i = 0; i < 1000; i++)
        {
            _profiler.RecordLatency(kernelId, 1000);
        }

        // Assert - Should have approximately 100 samples (10% of 1000)
        var percentiles = _profiler.GetLatencyPercentiles(kernelId);
        percentiles.Should().NotBeNull();
        percentiles!.Value.TotalCount.Should().BeLessThan(500); // With randomness, should be significantly less than all
        percentiles.Value.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DisposeAsync_StopsAllSessions()
    {
        // Arrange
        await _profiler.StartProfilingAsync("kernel1");
        await _profiler.StartProfilingAsync("kernel2");

        // Act
        await _profiler.DisposeAsync();

        // Assert - After dispose, operations should not throw but may be no-ops
        _profiler.ProfiledKernels.Should().BeEmpty();
    }

    [Fact]
    public async Task PerformanceReport_ContainsAllStatistics()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Record various metrics
        for (var i = 1; i <= 100; i++)
        {
            _profiler.RecordLatency(kernelId, i * 1000);
        }

        _profiler.RecordThroughput(kernelId, 1000, 1_000_000_000);
        _profiler.RecordThroughput(kernelId, 2000, 1_000_000_000);
        _profiler.RecordAllocation(kernelId, 4096, "GPU");

        // Act
        var report = await _profiler.StopProfilingAsync(kernelId);

        // Assert
        report.KernelId.Should().Be(kernelId);
        report.Duration.Should().BeGreaterThan(TimeSpan.Zero);

        report.Latency.TotalCount.Should().Be(100);
        report.Latency.P50Nanos.Should().BeGreaterThan(0);
        report.Latency.P99Nanos.Should().BeGreaterThan(report.Latency.P50Nanos);

        report.Throughput.SampleCount.Should().Be(2);
        report.Throughput.AverageMps.Should().Be(1500);
        report.Throughput.PeakMps.Should().Be(2000);
        report.Throughput.MinMps.Should().Be(1000);

        report.Allocations.GpuBytes.Should().Be(4096);
        report.Allocations.AllocationCount.Should().Be(1);

        report.Anomalies.Should().BeEmpty();
    }

    [Fact]
    public async Task BaselineComparison_CalculatesRatiosCorrectly()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        var baseline = new PerformanceBaseline
        {
            BaselineId = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpectedP50Nanos = 1000,
            ExpectedP99Nanos = 5000,
            ExpectedThroughputMps = 1000,
            ExpectedPeakMemoryBytes = 1024
        };

        _profiler.SetBaseline(kernelId, baseline);

        // Record 2x baseline latency
        _profiler.RecordLatency(kernelId, 2000);

        // Record 0.5x baseline throughput
        _profiler.RecordThroughput(kernelId, 500, 1_000_000_000);

        // Record 2x baseline memory
        _profiler.RecordAllocation(kernelId, 2048, "GPU");

        // Act
        var comparison = _profiler.CompareToBaseline(kernelId);

        // Assert
        comparison.Should().NotBeNull();
        comparison!.ThroughputRatio.Should().BeApproximately(0.5, 0.1);
        comparison.MemoryRatio.Should().Be(2.0);
    }

    [Fact]
    public async Task BaselineComparison_DetectsRegressions()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        var baseline = new PerformanceBaseline
        {
            BaselineId = "v1",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpectedP50Nanos = 1000,
            ExpectedP99Nanos = 5000,
            ExpectedThroughputMps = 1000,
            ExpectedPeakMemoryBytes = 1024,
            Tolerance = 0.1 // 10% tolerance
        };

        _profiler.SetBaseline(kernelId, baseline);

        // Record metrics that regress beyond tolerance
        for (var i = 0; i < 100; i++)
        {
            _profiler.RecordLatency(kernelId, 5000); // 5x P50
        }

        _profiler.RecordThroughput(kernelId, 500, 1_000_000_000); // 0.5x throughput
        _profiler.RecordAllocation(kernelId, 2048, "GPU"); // 2x memory

        // Act
        var comparison = _profiler.CompareToBaseline(kernelId);

        // Assert
        comparison.Should().NotBeNull();
        comparison!.HasRegression.Should().BeTrue();
        comparison.ThroughputRegressed.Should().BeTrue();
        comparison.MemoryRegressed.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleSessions_AreIsolated()
    {
        // Arrange
        await _profiler.StartProfilingAsync("kernel1");
        await _profiler.StartProfilingAsync("kernel2");

        // Act
        _profiler.RecordLatency("kernel1", 1000);
        _profiler.RecordLatency("kernel2", 2000);

        // Assert
        var p1 = _profiler.GetLatencyPercentiles("kernel1");
        var p2 = _profiler.GetLatencyPercentiles("kernel2");

        p1.Should().NotBeNull();
        p2.Should().NotBeNull();
        p1!.Value.TotalCount.Should().Be(1);
        p2!.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordDeallocation_ActiveSession_TracksDeallocation()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act
        _profiler.RecordAllocation(kernelId, 1024, "GPU");
        _profiler.RecordDeallocation(kernelId, 512, "GPU");

        var stats = _profiler.GetMemoryStatistics(kernelId);

        // Assert
        stats.Should().NotBeNull();
        stats!.Value.CurrentGpuBytes.Should().Be(512);
        stats.Value.TotalAllocations.Should().Be(1);
        stats.Value.TotalDeallocations.Should().Be(1);
    }

    [Fact]
    public async Task GetMemoryStatistics_ActiveSession_TracksPeakMemory()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act - Allocate, then deallocate, then check peak
        _profiler.RecordAllocation(kernelId, 4096, "GPU");
        _profiler.RecordAllocation(kernelId, 2048, "GPU");
        _profiler.RecordDeallocation(kernelId, 3000, "GPU");

        var stats = _profiler.GetMemoryStatistics(kernelId);

        // Assert
        stats.Should().NotBeNull();
        stats!.Value.PeakGpuBytes.Should().Be(6144); // 4096 + 2048
        stats.Value.CurrentGpuBytes.Should().Be(3144); // 6144 - 3000
    }

    [Fact]
    public async Task GetMemoryStatistics_TracksByMemoryType()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act
        _profiler.RecordAllocation(kernelId, 1000, "GPU");
        _profiler.RecordAllocation(kernelId, 2000, "HOST");
        _profiler.RecordAllocation(kernelId, 3000, "UNIFIED");
        _profiler.RecordDeallocation(kernelId, 500, "GPU");
        _profiler.RecordDeallocation(kernelId, 500, "HOST");

        var stats = _profiler.GetMemoryStatistics(kernelId);

        // Assert
        stats.Should().NotBeNull();
        stats!.Value.CurrentGpuBytes.Should().Be(500);
        stats.Value.CurrentHostBytes.Should().Be(1500);
        stats.Value.CurrentUnifiedBytes.Should().Be(3000);
        stats.Value.CurrentTotalBytes.Should().Be(5000);
        stats.Value.PeakTotalBytes.Should().Be(6000);
    }

    [Fact]
    public async Task GetMemoryStatistics_InactiveSession_ReturnsNull()
    {
        // Act
        var stats = _profiler.GetMemoryStatistics("nonexistent-kernel");

        // Assert
        stats.Should().BeNull();
    }

    [Fact]
    public async Task GetMemoryStatistics_TracksAllocationRate()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act - Make several allocations
        for (var i = 0; i < 10; i++)
        {
            _profiler.RecordAllocation(kernelId, 1024, "GPU");
        }

        var stats = _profiler.GetMemoryStatistics(kernelId);

        // Assert
        stats.Should().NotBeNull();
        stats!.Value.TotalAllocations.Should().Be(10);
        stats.Value.AllocationRatePerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AnomalyDetected_MemorySpike_RaisesEvent()
    {
        // Arrange
        const string kernelId = "test-kernel";
        var options = new ProfilingOptions { EnableAnomalyDetection = true, AnomalyThreshold = 2.0 };
        await _profiler.StartProfilingAsync(kernelId, options);

        var baseline = new PerformanceBaseline
        {
            BaselineId = "baseline",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpectedP50Nanos = 1000,
            ExpectedP99Nanos = 2000,
            ExpectedThroughputMps = 10000,
            ExpectedPeakMemoryBytes = 1024 // 1KB expected
        };

        _profiler.SetBaseline(kernelId, baseline);

        PerformanceAnomalyEventArgs? capturedEvent = null;
        _profiler.AnomalyDetected += (_, e) => capturedEvent = e;

        // Act - Allocate 10x baseline (exceeds 2x threshold)
        _profiler.RecordAllocation(kernelId, 10240, "GPU"); // 10KB vs expected 1KB

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.KernelId.Should().Be(kernelId);
        capturedEvent.Anomaly.Type.Should().Be(AnomalyType.MemorySpike);
    }

    [Fact]
    public async Task RecordDeallocation_InactiveSession_IsIgnored()
    {
        // Act - Should not throw
        _profiler.RecordDeallocation("nonexistent-kernel", 1000, "GPU");

        // Assert
        _profiler.ProfiledKernels.Should().BeEmpty();
    }

    [Fact]
    public async Task MemoryStatistics_PotentialLeak_DetectedCorrectly()
    {
        // Arrange
        const string kernelId = "test-kernel";
        await _profiler.StartProfilingAsync(kernelId);

        // Act - Many allocations, few deallocations
        for (var i = 0; i < 100; i++)
        {
            _profiler.RecordAllocation(kernelId, 1024, "GPU");
        }
        // Only deallocate 5% (potential leak scenario)
        for (var i = 0; i < 5; i++)
        {
            _profiler.RecordDeallocation(kernelId, 1024, "GPU");
        }

        var stats = _profiler.GetMemoryStatistics(kernelId);

        // Assert - PotentialLeak should be detected when deallocations < 90% of allocations
        stats.Should().NotBeNull();
        stats!.Value.TotalAllocations.Should().Be(100);
        stats.Value.TotalDeallocations.Should().Be(5);
        // Note: PotentialLeak also requires CurrentTotalBytes > PeakTotalBytes * 0.8
        // Since we have 95KB current vs 100KB peak, this should be true
        stats.Value.PotentialLeak.Should().BeTrue();
    }
}
