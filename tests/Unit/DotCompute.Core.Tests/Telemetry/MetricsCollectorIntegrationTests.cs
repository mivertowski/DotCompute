// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Core.Telemetry;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Telemetry;

/// <summary>
/// Integration tests for MetricsCollector demonstrating real-world usage scenarios.
/// </summary>
public sealed class MetricsCollectorIntegrationTests : IDisposable
{
    private readonly ILogger<MetricsCollector> _mockLogger;
    private readonly MetricsCollector _collector;

    public MetricsCollectorIntegrationTests()
    {
        _mockLogger = Substitute.For<ILogger<MetricsCollector>>();
        _collector = new MetricsCollector(_mockLogger);
    }

    public void Dispose() => _collector.Dispose();

    [Fact]
    public async Task CompleteWorkflow_RecordsAndAnalyzesMetrics()
    {
        // Arrange - Simulate a complete compute workflow
        var kernelDetails = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(150),
            OperationsPerformed = 10000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        var memoryDetails = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L * 100L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };

        // Act - Record kernel executions
        _collector.RecordKernelExecution("MatrixMultiply", "GPU0", TimeSpan.FromMilliseconds(150), 1024L * 1024L, true, kernelDetails);
        _collector.RecordKernelExecution("MatrixMultiply", "GPU0", TimeSpan.FromMilliseconds(145), 1024L * 1024L, true, kernelDetails);
        _collector.RecordKernelExecution("VectorAdd", "GPU0", TimeSpan.FromMilliseconds(50), 512L * 1024L, true, kernelDetails);

        // Record memory operations
        _collector.RecordMemoryOperation("Transfer", "GPU0", 1024L * 1024L * 10L, TimeSpan.FromMilliseconds(10), true, memoryDetails);
        _collector.RecordMemoryOperation("Allocation", "GPU0", 1024L * 1024L * 50L, TimeSpan.FromMilliseconds(5), true, memoryDetails);

        // Collect all metrics
        var metrics = await _collector.CollectAllMetricsAsync();

        // Assert
        _ = metrics.Counters["total_kernel_executions"].Should().Be(3);
        _ = metrics.Counters["total_memory_allocations"].Should().Be(2);
        _ = metrics.Gauges["average_kernel_duration_ms"].Should().BeGreaterThan(0);

        var matrixMetrics = _collector.GetKernelPerformanceMetrics("MatrixMultiply");
        _ = matrixMetrics.Should().NotBeNull();
        _ = matrixMetrics!.ExecutionCount.Should().Be(2);

        var memoryAnalysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));
        _ = memoryAnalysis.TotalOperations.Should().Be(2);
    }

    [Fact]
    public void MultiDeviceScenario_TracksDevicesSeparately()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Record executions on multiple devices
        _collector.RecordKernelExecution("Kernel1", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution("Kernel2", "GPU1", TimeSpan.FromMilliseconds(120), 2048L, true, details);
        _collector.RecordKernelExecution("Kernel3", "CPU", TimeSpan.FromMilliseconds(200), 512L, true, details);

        // Assert
        var gpu0Metrics = _collector.GetDevicePerformanceMetrics("GPU0");
        var gpu1Metrics = _collector.GetDevicePerformanceMetrics("GPU1");
        var cpuMetrics = _collector.GetDevicePerformanceMetrics("CPU");

        _ = gpu0Metrics.Should().NotBeNull();
        _ = gpu1Metrics.Should().NotBeNull();
        _ = cpuMetrics.Should().NotBeNull();

        _ = gpu0Metrics!.TotalOperations.Should().Be(1);
        _ = gpu1Metrics!.TotalOperations.Should().Be(1);
        _ = cpuMetrics!.TotalOperations.Should().Be(1);
    }

    [Fact]
    public void HighThroughputScenario_HandlesLargeVolume()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(10),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Record 1000 kernel executions
        for (var i = 0; i < 1000; i++)
        {
            _collector.RecordKernelExecution($"Kernel{i % 10}", "GPU0", TimeSpan.FromMilliseconds(10 + i % 50), 1024L, true, details);
        }

        // Assert - Should handle high volume without issues
        var totalExecutions = 0L;
        for (var i = 0; i < 10; i++)
        {
            var metrics = _collector.GetKernelPerformanceMetrics($"Kernel{i}");
            if (metrics != null)
            {
                totalExecutions += metrics.ExecutionCount;
            }
        }

        _ = totalExecutions.Should().Be(1000);
    }

    [Fact]
    public void ErrorHandlingScenario_TracksFailuresCorrectly()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Mix of successes and failures
        _collector.RecordKernelExecution("UnstableKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution("UnstableKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, false, details);
        _collector.RecordKernelExecution("UnstableKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution("UnstableKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, false, details);
        _collector.RecordKernelExecution("UnstableKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, false, details);

        // Assert
        var metrics = _collector.GetKernelPerformanceMetrics("UnstableKernel");
        _ = metrics.Should().NotBeNull();
        _ = metrics!.ExecutionCount.Should().Be(5);
        _ = metrics.SuccessRate.Should().BeApproximately(0.4, 0.01); // 2 successes out of 5

        var bottlenecks = _collector.DetectBottlenecks();
        _ = bottlenecks.Should().Contain(b => b.KernelName == "UnstableKernel" && b.Type == BottleneckType.KernelFailures);
    }

    [Fact]
    public void MemoryAccessPatternAnalysis_IdentifiesPatterns()
    {
        // Arrange
        var sequentialDetails = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.95
        };

        var randomDetails = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L,
            AccessPattern = "Random",
            CoalescingEfficiency = 0.5
        };

        // Act - Record different access patterns
        for (var i = 0; i < 10; i++)
        {
            _collector.RecordMemoryOperation("SeqTransfer", "GPU0", 1024L * 1024L, TimeSpan.FromMilliseconds(5), true, sequentialDetails);
        }

        for (var i = 0; i < 3; i++)
        {
            _collector.RecordMemoryOperation("RandTransfer", "GPU0", 1024L * 1024L, TimeSpan.FromMilliseconds(15), true, randomDetails);
        }

        // Assert
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));
        _ = analysis.AccessPatterns["Sequential"].Should().Be(10);
        _ = analysis.AccessPatterns["Random"].Should().Be(3);
        _ = analysis.AverageCoalescingEfficiency.Should().BeGreaterThan(0.7); // Weighted average
    }

    [Fact]
    public void PerformanceDegradationDetection_IdentifiesTrends()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Simulate performance degradation over time
        _collector.RecordKernelExecution("DegradingKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution("DegradingKernel", "GPU0", TimeSpan.FromMilliseconds(150), 1024L, true, details);
        _collector.RecordKernelExecution("DegradingKernel", "GPU0", TimeSpan.FromMilliseconds(200), 1024L, true, details);
        _collector.RecordKernelExecution("DegradingKernel", "GPU0", TimeSpan.FromMilliseconds(250), 1024L, true, details);

        // Assert
        var metrics = _collector.GetKernelPerformanceMetrics("DegradingKernel");
        _ = metrics.Should().NotBeNull();
        _ = metrics!.MinExecutionTime.Should().BeLessThan(metrics.MaxExecutionTime);
        _ = metrics.MaxExecutionTime.Should().BeApproximately(250.0, 1.0);
    }

    [Fact]
    public async Task PeriodicMetricsCollection_CapturesSystemState()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Simulate continuous operation with periodic collection
        for (var i = 0; i < 5; i++)
        {
            _collector.RecordKernelExecution($"PeriodicKernel{i}", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
            await Task.Delay(10); // Small delay to simulate real operations
        }

        var metrics = await _collector.CollectAllMetricsAsync();

        // Assert
        _ = metrics.Counters["total_kernel_executions"].Should().Be(5);
        _ = metrics.Gauges.Should().ContainKey("average_kernel_duration_ms");
        _ = metrics.Gauges.Should().ContainKey("device_utilization_percentage");
    }
}
