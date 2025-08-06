// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Core.Performance;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for the PerformanceMonitor class.
/// </summary>
public sealed class PerformanceMonitorTests : IDisposable
{
    private readonly Mock<ILogger<PerformanceMonitor>> _loggerMock;
    private readonly PerformanceMonitor _performanceMonitor;
    private bool _disposed;

    public PerformanceMonitorTests()
    {
        _loggerMock = new Mock<ILogger<PerformanceMonitor>>();
        _performanceMonitor = new PerformanceMonitor(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PerformanceMonitor(null!));
    }

    [Fact]
    public void StartOperation_ReturnsOperationHandle()
    {
        // Act
        var handle = _performanceMonitor.StartOperation("TestOperation");

        // Assert
        Assert.NotNull(handle);
        Assert.Equal("TestOperation", handle.OperationName);
        Assert.True(handle.IsActive);
    }

    [Fact]
    public void StartOperation_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _performanceMonitor.StartOperation(null!));
    }

    [Fact]
    public void StartOperation_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _performanceMonitor.StartOperation(string.Empty));
    }

    [Fact]
    public async Task EndOperation_RecordsMetrics()
    {
        // Arrange
        var handle = _performanceMonitor.StartOperation("TestOperation");
        await Task.Delay(100); // Simulate some work

        // Act
        _performanceMonitor.EndOperation(handle);
        var metrics = await _performanceMonitor.GetOperationMetricsAsync("TestOperation");

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.Count);
        Assert.True(metrics.AverageDuration.TotalMilliseconds >= 90); // Allow for timing variance
        Assert.Equal(1, metrics.SuccessCount);
        Assert.Equal(0, metrics.FailureCount);
    }

    [Fact]
    public void EndOperation_WithNullHandle_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _performanceMonitor.EndOperation(null!));
    }

    [Fact]
    public void EndOperation_WithInactiveHandle_ThrowsInvalidOperationException()
    {
        // Arrange
        var handle = _performanceMonitor.StartOperation("TestOperation");
        _performanceMonitor.EndOperation(handle);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _performanceMonitor.EndOperation(handle));
    }

    [Fact]
    public void EndOperation_WithSuccess_RecordsSuccess()
    {
        // Arrange
        var handle = _performanceMonitor.StartOperation("TestOperation");

        // Act
        _performanceMonitor.EndOperation(handle, success: true);
        var metrics = _performanceMonitor.GetOperationMetricsAsync("TestOperation").Result;

        // Assert
        Assert.Equal(1, metrics.SuccessCount);
        Assert.Equal(0, metrics.FailureCount);
    }

    [Fact]
    public void EndOperation_WithFailure_RecordsFailure()
    {
        // Arrange
        var handle = _performanceMonitor.StartOperation("TestOperation");

        // Act
        _performanceMonitor.EndOperation(handle, success: false);
        var metrics = _performanceMonitor.GetOperationMetricsAsync("TestOperation").Result;

        // Assert
        Assert.Equal(0, metrics.SuccessCount);
        Assert.Equal(1, metrics.FailureCount);
    }

    [Fact]
    public void RecordMetric_RecordsCustomMetric()
    {
        // Act
        _performanceMonitor.RecordMetric("CustomMetric", 42.5);
        _performanceMonitor.RecordMetric("CustomMetric", 57.5);
        var value = _performanceMonitor.GetMetricAsync("CustomMetric").Result;

        // Assert
        Assert.Equal(50.0, value); // Average of 42.5 and 57.5
    }

    [Fact]
    public void RecordMetric_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _performanceMonitor.RecordMetric(null!, 42));
    }

    [Fact]
    public async Task GetOperationMetricsAsync_ForNonExistentOperation_ReturnsNull()
    {
        // Act
        var metrics = await _performanceMonitor.GetOperationMetricsAsync("NonExistent");

        // Assert
        Assert.Null(metrics);
    }

    [Fact]
    public async Task GetMetricAsync_ForNonExistentMetric_ReturnsNaN()
    {
        // Act
        var value = await _performanceMonitor.GetMetricAsync("NonExistent");

        // Assert
        Assert.True(double.IsNaN(value));
    }

    [Fact]
    public async Task GetAllMetricsAsync_ReturnsAllMetrics()
    {
        // Arrange
        _performanceMonitor.RecordMetric("Metric1", 10);
        _performanceMonitor.RecordMetric("Metric2", 20);
        _performanceMonitor.RecordMetric("Metric3", 30);

        // Act
        var metrics = await _performanceMonitor.GetAllMetricsAsync();

        // Assert
        Assert.Equal(3, metrics.Count);
        Assert.Equal(10, metrics["Metric1"]);
        Assert.Equal(20, metrics["Metric2"]);
        Assert.Equal(30, metrics["Metric3"]);
    }

    [Fact]
    public async Task GetAllOperationMetricsAsync_ReturnsAllOperations()
    {
        // Arrange
        var handle1 = _performanceMonitor.StartOperation("Op1");
        _performanceMonitor.EndOperation(handle1);
        
        var handle2 = _performanceMonitor.StartOperation("Op2");
        _performanceMonitor.EndOperation(handle2);

        // Act
        var operations = await _performanceMonitor.GetAllOperationMetricsAsync();

        // Assert
        Assert.Equal(2, operations.Count);
        Assert.Contains("Op1", operations.Keys);
        Assert.Contains("Op2", operations.Keys);
    }

    [Fact]
    public void ResetMetrics_ClearsAllData()
    {
        // Arrange
        _performanceMonitor.RecordMetric("Metric1", 10);
        var handle = _performanceMonitor.StartOperation("Op1");
        _performanceMonitor.EndOperation(handle);

        // Act
        _performanceMonitor.ResetMetrics();
        var metrics = _performanceMonitor.GetAllMetricsAsync().Result;
        var operations = _performanceMonitor.GetAllOperationMetricsAsync().Result;

        // Assert
        Assert.Empty(metrics);
        Assert.Empty(operations);
    }

    [Fact]
    public async Task ConcurrentOperations_AreHandledCorrectly()
    {
        // Arrange
        const int operationCount = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var handle = _performanceMonitor.StartOperation($"Op{index % 10}");
                await Task.Delay(Random.Shared.Next(10, 50));
                _performanceMonitor.EndOperation(handle, success: index % 2 == 0);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var operations = await _performanceMonitor.GetAllOperationMetricsAsync();
        Assert.Equal(10, operations.Count); // 10 unique operation names
        
        var totalCount = operations.Values.Sum(m => m.Count);
        Assert.Equal(operationCount, totalCount);
    }

    [Fact]
    public void OperationHandle_Timing_IsAccurate()
    {
        // Arrange
        var sw = Stopwatch.StartNew();
        var handle = _performanceMonitor.StartOperation("TimingTest");
        
        // Act
        Thread.Sleep(200); // Sleep for 200ms
        _performanceMonitor.EndOperation(handle);
        sw.Stop();
        
        var metrics = _performanceMonitor.GetOperationMetricsAsync("TimingTest").Result;

        // Assert
        Assert.NotNull(metrics);
        Assert.InRange(metrics.AverageDuration.TotalMilliseconds, 180, 250); // Allow for timing variance
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_ReturnsComprehensiveSummary()
    {
        // Arrange
        _performanceMonitor.RecordMetric("CPU", 75.5);
        _performanceMonitor.RecordMetric("Memory", 2048);
        
        var handle1 = _performanceMonitor.StartOperation("FastOp");
        await Task.Delay(50);
        _performanceMonitor.EndOperation(handle1, success: true);
        
        var handle2 = _performanceMonitor.StartOperation("SlowOp");
        await Task.Delay(150);
        _performanceMonitor.EndOperation(handle2, success: false);

        // Act
        var summary = await _performanceMonitor.GetPerformanceSummaryAsync();

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(2, summary.MetricCount);
        Assert.Equal(2, summary.OperationCount);
        Assert.Equal(1, summary.SuccessfulOperations);
        Assert.Equal(1, summary.FailedOperations);
        Assert.True(summary.AverageOperationDuration.TotalMilliseconds > 0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _performanceMonitor?.Dispose();
            _disposed = true;
        }
    }
}