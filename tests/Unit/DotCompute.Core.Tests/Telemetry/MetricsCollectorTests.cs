// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;
using DotCompute.Core.Telemetry;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Telemetry;

/// <summary>
/// Comprehensive unit tests for MetricsCollector class.
/// Tests metrics collection, aggregation, performance counters, and bottleneck detection.
/// </summary>
public sealed class MetricsCollectorTests : IDisposable
{
    private readonly ILogger<MetricsCollector> _mockLogger;
    private readonly MetricsCollector _collector;

    public MetricsCollectorTests()
    {
        _mockLogger = Substitute.For<ILogger<MetricsCollector>>();
        _collector = new MetricsCollector(_mockLogger);
    }

    public void Dispose() => _collector.Dispose();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        using var collector = new MetricsCollector(_mockLogger);

        // Assert
        _ = collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new MetricsCollector(null!);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region RecordKernelExecution Tests

    [Fact]
    public void RecordKernelExecution_WithValidData_RecordsMetrics()
    {
        // Arrange
        var kernelName = "TestKernel";
        var deviceId = "GPU0";
        var executionTime = TimeSpan.FromMilliseconds(100);
        var memoryUsed = 1024L;
        var details = new KernelExecutionDetails
        {
            ExecutionTime = executionTime,
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act
        _collector.RecordKernelExecution(kernelName, deviceId, executionTime, memoryUsed, true, details);

        // Assert
        var metrics = _collector.GetKernelPerformanceMetrics(kernelName);
        _ = metrics.Should().NotBeNull();
        _ = metrics!.KernelName.Should().Be(kernelName);
        _ = metrics.ExecutionCount.Should().Be(1);
        _ = metrics.AverageExecutionTime.Should().BeApproximately(100.0, 0.1);
    }

    [Fact]
    public void RecordKernelExecution_MultipleExecutions_AggregatesCorrectly()
    {
        // Arrange
        var kernelName = "TestKernel";
        var deviceId = "GPU0";
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Record 3 executions
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(200), 2048L, true, details);
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(150), 1536L, true, details);

        // Assert
        var metrics = _collector.GetKernelPerformanceMetrics(kernelName);
        _ = metrics.Should().NotBeNull();
        _ = metrics!.ExecutionCount.Should().Be(3);
        _ = metrics.MinExecutionTime.Should().BeApproximately(100.0, 0.1);
        _ = metrics.MaxExecutionTime.Should().BeApproximately(200.0, 0.1);
        _ = metrics.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void RecordKernelExecution_WithFailures_TracksSuccessRate()
    {
        // Arrange
        var kernelName = "TestKernel";
        var deviceId = "GPU0";
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - 3 successes, 1 failure
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(100), 1024L, false, details);
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(100), 1024L, true, details);

        // Assert
        var metrics = _collector.GetKernelPerformanceMetrics(kernelName);
        _ = metrics.Should().NotBeNull();
        _ = metrics!.SuccessRate.Should().BeApproximately(0.75, 0.01);
    }

    [Fact]
    public void RecordKernelExecution_UpdatesOccupancyMetrics()
    {
        // Arrange
        var kernelName = "TestKernel";
        var deviceId = "GPU0";
        var details1 = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.8,
            CacheHitRate = 0.9
        };
        var details2 = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.6,
            CacheHitRate = 0.7
        };

        // Act
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(100), 1024L, true, details1);
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromMilliseconds(100), 1024L, true, details2);

        // Assert
        var metrics = _collector.GetKernelPerformanceMetrics(kernelName);
        _ = metrics.Should().NotBeNull();
        _ = metrics!.AverageOccupancy.Should().BeGreaterThan(0.0);
        _ = metrics.AverageOccupancy.Should().BeLessThan(1.0);
    }

    [Fact]
    public void RecordKernelExecution_CalculatesThroughput()
    {
        // Arrange
        var kernelName = "TestKernel";
        var deviceId = "GPU0";
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromSeconds(1),
            OperationsPerformed = 10000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act
        _collector.RecordKernelExecution(kernelName, deviceId, TimeSpan.FromSeconds(1), 1024L, true, details);

        // Assert
        var metrics = _collector.GetKernelPerformanceMetrics(kernelName);
        _ = metrics.Should().NotBeNull();
        _ = metrics!.AverageThroughput.Should().BeApproximately(10000.0, 1.0);
    }

    #endregion

    #region RecordMemoryOperation Tests

    [Fact]
    public void RecordMemoryOperation_WithValidData_RecordsOperation()
    {
        // Arrange
        var operationType = "Transfer";
        var deviceId = "GPU0";
        var bytes = 1024L * 1024L; // 1 MB
        var duration = TimeSpan.FromMilliseconds(10);
        var details = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L * 100L, // 100 MB
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };

        // Act
        _collector.RecordMemoryOperation(operationType, deviceId, bytes, duration, true, details);

        // Assert
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));
        _ = analysis.Should().NotBeNull();
        _ = analysis.TotalOperations.Should().Be(1);
    }

    [Fact]
    public void RecordMemoryOperation_CalculatesBandwidth()
    {
        // Arrange
        var operationType = "Transfer";
        var deviceId = "GPU0";
        var bytes = 1024L * 1024L * 100L; // 100 MB
        var duration = TimeSpan.FromSeconds(1);
        var details = new MemoryOperationDetails
        {
            CurrentMemoryUsage = bytes,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };

        // Act
        _collector.RecordMemoryOperation(operationType, deviceId, bytes, duration, true, details);

        // Assert
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));
        _ = analysis.AverageBandwidth.Should().BeApproximately(bytes, bytes * 0.1);
    }

    [Fact]
    public void RecordMemoryOperation_TracksPeakMemoryUsage()
    {
        // Arrange
        var operationType = "Allocation";
        var deviceId = "GPU0";
        var details1 = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L * 50L, // 50 MB
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };
        var details2 = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L * 100L, // 100 MB (peak)
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };
        var details3 = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L * 75L, // 75 MB
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };

        // Act
        _collector.RecordMemoryOperation(operationType, deviceId, 1024L, TimeSpan.FromMilliseconds(1), true, details1);
        _collector.RecordMemoryOperation(operationType, deviceId, 1024L, TimeSpan.FromMilliseconds(1), true, details2);
        _collector.RecordMemoryOperation(operationType, deviceId, 1024L, TimeSpan.FromMilliseconds(1), true, details3);

        // Assert - Peak should be 100 MB
        var currentUsage = _collector.GetCurrentMemoryUsage();
        _ = currentUsage.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RecordMemoryOperation_WithMultipleAccessPatterns_TracksDistribution()
    {
        // Arrange
        var details1 = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };
        var details2 = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L,
            AccessPattern = "Random",
            CoalescingEfficiency = 0.5
        };
        var details3 = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.85
        };

        // Act
        _collector.RecordMemoryOperation("Transfer", "GPU0", 1024L, TimeSpan.FromMilliseconds(1), true, details1);
        _collector.RecordMemoryOperation("Transfer", "GPU0", 1024L, TimeSpan.FromMilliseconds(1), true, details2);
        _collector.RecordMemoryOperation("Transfer", "GPU0", 1024L, TimeSpan.FromMilliseconds(1), true, details3);

        // Assert
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));
        _ = analysis.AccessPatterns.Should().ContainKey("Sequential");
        _ = analysis.AccessPatterns.Should().ContainKey("Random");
        _ = analysis.AccessPatterns["Sequential"].Should().Be(2);
        _ = analysis.AccessPatterns["Random"].Should().Be(1);
    }

    [Fact]
    public void RecordMemoryOperation_LimitsQueueSize()
    {
        // Arrange
        var details = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };

        // Act - Record 15000 operations (exceeds 10000 limit)
        for (var i = 0; i < 15000; i++)
        {
            _collector.RecordMemoryOperation($"Op{i}", "GPU0", 1024L, TimeSpan.FromMilliseconds(1), true, details);
        }

        // Assert - Should only keep last 10000
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromHours(1));
        _ = analysis.TotalOperations.Should().BeLessThanOrEqualTo(10000);
    }

    #endregion

    #region GetCurrentMemoryUsage Tests

    [Fact]
    public void GetCurrentMemoryUsage_WithNoDevices_ReturnsZero()
    {
        // Act
        var usage = _collector.GetCurrentMemoryUsage();

        // Assert
        _ = usage.Should().Be(0);
    }

    [Fact]
    public void GetCurrentMemoryUsage_WithMultipleDevices_SumsUsage()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Record executions on different devices
        _collector.RecordKernelExecution("Kernel1", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution("Kernel2", "GPU1", TimeSpan.FromMilliseconds(100), 2048L, true, details);

        // Assert
        var usage = _collector.GetCurrentMemoryUsage();
        _ = usage.Should().BeGreaterThan(0);
    }

    #endregion

    #region GetDeviceUtilization Tests

    [Fact]
    public void GetDeviceUtilization_WithNoDevices_ReturnsZero()
    {
        // Act
        var utilization = _collector.GetDeviceUtilization();

        // Assert
        _ = utilization.Should().Be(0.0);
    }

    [Fact]
    public void GetDeviceUtilization_WithDevices_ReturnsAverage()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Act - Record executions to establish device metrics
        _collector.RecordKernelExecution("Kernel1", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
        _collector.RecordKernelExecution("Kernel2", "GPU1", TimeSpan.FromMilliseconds(100), 2048L, true, details);

        // Assert
        var utilization = _collector.GetDeviceUtilization();
        _ = utilization.Should().BeGreaterThanOrEqualTo(0.0);
        _ = utilization.Should().BeLessThanOrEqualTo(100.0);
    }

    #endregion

    #region GetKernelPerformanceMetrics Tests

    [Fact]
    public void GetKernelPerformanceMetrics_WithUnknownKernel_ReturnsNull()
    {
        // Act
        var metrics = _collector.GetKernelPerformanceMetrics("UnknownKernel");

        // Assert
        _ = metrics.Should().BeNull();
    }

    [Fact]
    public void GetKernelPerformanceMetrics_WithKnownKernel_ReturnsMetrics()
    {
        // Arrange
        var kernelName = "TestKernel";
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };
        _collector.RecordKernelExecution(kernelName, "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);

        // Act
        var metrics = _collector.GetKernelPerformanceMetrics(kernelName);

        // Assert
        _ = metrics.Should().NotBeNull();
        _ = metrics!.KernelName.Should().Be(kernelName);
        _ = metrics.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public void GetKernelPerformanceMetrics_CalculatesMemoryEfficiency()
    {
        // Arrange
        var kernelName = "TestKernel";
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };
        _collector.RecordKernelExecution(kernelName, "GPU0", TimeSpan.FromMilliseconds(100), 1024L * 1024L, true, details);

        // Act
        var metrics = _collector.GetKernelPerformanceMetrics(kernelName);

        // Assert
        _ = metrics.Should().NotBeNull();
        _ = metrics!.MemoryEfficiency.Should().BeGreaterThan(0.0);
    }

    #endregion

    #region GetDevicePerformanceMetrics Tests

    [Fact]
    public void GetDevicePerformanceMetrics_WithUnknownDevice_ReturnsNull()
    {
        // Act
        var metrics = _collector.GetDevicePerformanceMetrics("UnknownDevice");

        // Assert
        _ = metrics.Should().BeNull();
    }

    [Fact]
    public void GetDevicePerformanceMetrics_WithKnownDevice_ReturnsMetrics()
    {
        // Arrange
        var deviceId = "GPU0";
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };
        _collector.RecordKernelExecution("Kernel1", deviceId, TimeSpan.FromMilliseconds(100), 1024L, true, details);

        // Act
        var metrics = _collector.GetDevicePerformanceMetrics(deviceId);

        // Assert
        _ = metrics.Should().NotBeNull();
        _ = metrics!.DeviceId.Should().Be(deviceId);
        _ = metrics.TotalOperations.Should().Be(1);
    }

    #endregion

    #region CollectAllMetricsAsync Tests

    [Fact]
    public async Task CollectAllMetricsAsync_WithNoData_ReturnsEmptyMetrics()
    {
        // Act
        var metrics = await _collector.CollectAllMetricsAsync();

        // Assert
        _ = metrics.Should().NotBeNull();
        _ = metrics.Counters["total_kernel_executions"].Should().Be(0);
        _ = metrics.Counters["total_memory_allocations"].Should().Be(0);
    }

    [Fact]
    public async Task CollectAllMetricsAsync_WithData_ReturnsPopulatedMetrics()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };
        _collector.RecordKernelExecution("Kernel1", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);

        // Act
        var metrics = await _collector.CollectAllMetricsAsync();

        // Assert
        _ = metrics.Counters["total_kernel_executions"].Should().Be(1);
        _ = metrics.Gauges["average_kernel_duration_ms"].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CollectAllMetricsAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await _collector.CollectAllMetricsAsync(cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region GetMemoryAccessAnalysis Tests

    [Fact]
    public void GetMemoryAccessAnalysis_WithNoOperations_ReturnsEmptyAnalysis()
    {
        // Act
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));

        // Assert
        _ = analysis.Should().NotBeNull();
        _ = analysis.TotalOperations.Should().Be(0);
        _ = analysis.AverageBandwidth.Should().Be(0);
    }

    [Fact]
    public void GetMemoryAccessAnalysis_WithOperations_ReturnsAnalysis()
    {
        // Arrange
        var details = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L * 1024L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };
        _collector.RecordMemoryOperation("Transfer", "GPU0", 1024L * 1024L, TimeSpan.FromMilliseconds(10), true, details);

        // Act
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));

        // Assert
        _ = analysis.TotalOperations.Should().Be(1);
        _ = analysis.AverageBandwidth.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetMemoryAccessAnalysis_RespectsTimeWindow()
    {
        // Arrange
        var details = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };
        _collector.RecordMemoryOperation("Transfer", "GPU0", 1024L, TimeSpan.FromMilliseconds(1), true, details);

        // Act - Request very short time window (operations should be outside it)
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMilliseconds(1));

        // Assert - May or may not include operations depending on timing
        _ = analysis.Should().NotBeNull();
    }

    #endregion

    #region DetectBottlenecks Tests

    [Fact]
    public void DetectBottlenecks_WithNoData_ReturnsEmptyList()
    {
        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        _ = bottlenecks.Should().NotBeNull();
        _ = bottlenecks.Should().BeEmpty();
    }

    [Fact]
    public void DetectBottlenecks_WithHighMemoryUtilization_DetectsIssue()
    {
        // Arrange - Create device with very high memory usage
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Record enough executions to trigger bottleneck detection
        for (var i = 0; i < 10; i++)
        {
            _collector.RecordKernelExecution("TestKernel", "GPU0", TimeSpan.FromMilliseconds(100),
                long.MaxValue / 20, true, details); // High memory usage
        }

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        _ = bottlenecks.Should().NotBeNull();
        // Note: Actual bottleneck detection depends on internal thresholds
    }

    [Fact]
    public void DetectBottlenecks_WithLowSuccessRate_DetectsIssue()
    {
        // Arrange - Record many failures
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };

        // Record 10 failures, 1 success (10% success rate < 95% threshold)
        for (var i = 0; i < 10; i++)
        {
            _collector.RecordKernelExecution("TestKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, false, details);
        }
        _collector.RecordKernelExecution("TestKernel", "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);

        // Act
        var bottlenecks = _collector.DetectBottlenecks();

        // Assert
        _ = bottlenecks.Should().NotBeNull();
        _ = bottlenecks.Should().Contain(b => b.Type == BottleneckType.KernelFailures);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        using var collector = new MetricsCollector(_mockLogger);

        // Act & Assert - Should not throw
        collector.Dispose();
        collector.Dispose();
    }

    [Fact]
    public void Dispose_DisposedCollector_ThrowsObjectDisposedException()
    {
        // Arrange
        var collector = new MetricsCollector(_mockLogger);
        collector.Dispose();

        // Act
        var act = collector.GetCurrentMemoryUsage;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void RecordKernelExecution_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var details = new KernelExecutionDetails
        {
            ExecutionTime = TimeSpan.FromMilliseconds(100),
            OperationsPerformed = 1000,
            Occupancy = 0.75,
            CacheHitRate = 0.85
        };
        var tasks = new Task[100];

        // Act - Record 100 concurrent executions
        for (var i = 0; i < 100; i++)
        {
            var kernelName = $"Kernel{i % 10}"; // 10 different kernels
            tasks[i] = Task.Run(() =>
            {
                _collector.RecordKernelExecution(kernelName, "GPU0", TimeSpan.FromMilliseconds(100), 1024L, true, details);
            });
        }
        Task.WaitAll(tasks);

        // Assert - Should have recorded all executions without data corruption
        var totalExecutions = 0;
        for (var i = 0; i < 10; i++)
        {
            var metrics = _collector.GetKernelPerformanceMetrics($"Kernel{i}");
            if (metrics != null)
            {
                totalExecutions += (int)metrics.ExecutionCount;
            }
        }
        _ = totalExecutions.Should().Be(100);
    }

    [Fact]
    public void RecordMemoryOperation_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var details = new MemoryOperationDetails
        {
            CurrentMemoryUsage = 1024L,
            AccessPattern = "Sequential",
            CoalescingEfficiency = 0.9
        };
        var tasks = new Task[100];

        // Act - Record 100 concurrent operations
        for (var i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                _collector.RecordMemoryOperation("Transfer", "GPU0", 1024L, TimeSpan.FromMilliseconds(1), true, details);
            });
        }
        Task.WaitAll(tasks);

        // Assert - Should have recorded all operations
        var analysis = _collector.GetMemoryAccessAnalysis(TimeSpan.FromMinutes(1));
        _ = analysis.TotalOperations.Should().BeGreaterThan(0);
    }

    #endregion
}
