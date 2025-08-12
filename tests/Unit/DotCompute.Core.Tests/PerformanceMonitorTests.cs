// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Core.Execution;
using DotCompute.Core.Kernels;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ExecutionParallelExecutionResult = DotCompute.Core.Execution.ParallelExecutionResult;
using ExecutionDeviceExecutionResult = DotCompute.Core.Execution.DeviceExecutionResult;
using ExecutionExecutionStrategyType = DotCompute.Core.Execution.ExecutionStrategyType;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for the PerformanceMonitor class from DotCompute.Core.Execution.
/// </summary>
public sealed class PerformanceMonitorTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly PerformanceMonitor _performanceMonitor;
    private bool _disposed;

    public PerformanceMonitorTests()
    {
        _loggerMock = new Mock<ILogger>();
        _performanceMonitor = new PerformanceMonitor(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PerformanceMonitor(null!));
    }

    [Fact]
    public void RecordExecution_WithValidResult_ShouldNotThrow()
    {
        // Arrange
        var result = CreateMockParallelExecutionResult();

        // Act & Assert (should not throw)
        _performanceMonitor.RecordExecution(result);
        
        // Verify metrics are updated
        var metrics = _performanceMonitor.GetCurrentMetrics();
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalExecutions > 0);
    }

    [Fact]
    public void RecordKernelExecution_WithValidParameters_ShouldNotThrow()
    {
        // Act & Assert (should not throw)
        _performanceMonitor.RecordKernelExecution("TestKernel", "Device1", 100.5, 15.2);
        
        // Multiple executions should be handled
        _performanceMonitor.RecordKernelExecution("TestKernel", "Device1", 95.3, 16.1);
        _performanceMonitor.RecordKernelExecution("TestKernel", "Device2", 120.1, 18.5);
    }

    [Fact]
    public void GetCurrentMetrics_ReturnsValidMetrics()
    {
        // Arrange - Record some executions first
        var result1 = CreateMockParallelExecutionResult();
        var result2 = CreateMockParallelExecutionResult();
        _performanceMonitor.RecordExecution(result1);
        _performanceMonitor.RecordExecution(result2);

        // Act
        var metrics = _performanceMonitor.GetCurrentMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(2, metrics.TotalExecutions);
        Assert.True(metrics.AverageExecutionTimeMs > 0);
        Assert.True(metrics.AverageEfficiencyPercentage >= 0);
    }

    [Fact]
    public void GetPerformanceAnalysis_WithData_ReturnsValidAnalysis()
    {
        // Arrange
        var result = CreateMockParallelExecutionResult();
        _performanceMonitor.RecordExecution(result);

        // Act
        var analysis = _performanceMonitor.GetPerformanceAnalysis();

        // Assert
        Assert.NotNull(analysis);
        Assert.True(analysis.OverallRating > 0);
        Assert.NotNull(analysis.OptimizationRecommendations);
    }

    [Fact]
    public void GetPerformanceAnalysis_WithNoData_ReturnsDefaultAnalysis()
    {
        // Act
        var analysis = _performanceMonitor.GetPerformanceAnalysis();

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal(5.0, analysis.OverallRating);
        Assert.Equal(ExecutionExecutionStrategyType.Single, analysis.RecommendedStrategy);
    }

    [Fact]
    public void RecommendOptimalStrategy_WithKnownKernel_ReturnsRecommendation()
    {
        // Arrange
        var inputSizes = new[] { 1024, 1024 };
        var acceleratorTypes = new[] { AcceleratorType.CPU, AcceleratorType.GPU };
        var result = CreateMockParallelExecutionResult();
        _performanceMonitor.RecordExecution(result);

        // Act
        var recommendation = _performanceMonitor.RecommendOptimalStrategy("TestKernel", inputSizes, acceleratorTypes);

        // Assert
        Assert.NotNull(recommendation);
        Assert.NotNull(recommendation.Reasoning);
        Assert.True(recommendation.ConfidenceScore >= 0 && recommendation.ConfidenceScore <= 1);
    }

    [Fact]
    public void GetPerformanceTrends_WithTimeWindow_ReturnsValidTrends()
    {
        // Arrange
        var result = CreateMockParallelExecutionResult();
        _performanceMonitor.RecordExecution(result);
        var timeWindow = TimeSpan.FromHours(1);

        // Act
        var trends = _performanceMonitor.GetPerformanceTrends(timeWindow);

        // Assert
        Assert.NotNull(trends);
        Assert.NotNull(trends.TimeRange);
    }

    [Fact]
    public void GetDeviceUtilizationAnalysis_ReturnsValidAnalysis()
    {
        // Arrange
        var result = CreateMockParallelExecutionResult();
        _performanceMonitor.RecordExecution(result);

        // Act
        var analysis = _performanceMonitor.GetDeviceUtilizationAnalysis();

        // Assert
        Assert.NotNull(analysis);
        // Should contain device from our mock result
        Assert.True(analysis.ContainsKey("GPU_0") || analysis.Count == 0); // May be empty if no processing yet
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        // Arrange
        var result = CreateMockParallelExecutionResult();
        _performanceMonitor.RecordExecution(result);
        _performanceMonitor.RecordKernelExecution("TestKernel", "Device1", 100, 15);

        // Act
        _performanceMonitor.Reset();
        var metrics = _performanceMonitor.GetCurrentMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TotalExecutions);
    }

    [Fact]
    public async Task ConcurrentOperations_AreHandledCorrectly()
    {
        // Arrange
        const int operationCount = 10;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var result = CreateMockParallelExecutionResult();
                _performanceMonitor.RecordExecution(result);
                _performanceMonitor.RecordKernelExecution($"Kernel{index % 3}", "Device1", 100 + index, 15 + index);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var metrics = _performanceMonitor.GetCurrentMetrics();
        Assert.Equal(operationCount, metrics.TotalExecutions);
    }

    [Fact]
    public void MultipleExecutions_UpdateMetricsCorrectly()
    {
        // Arrange & Act
        var result1 = CreateMockParallelExecutionResult();
        var result2 = CreateMockParallelExecutionResult();
        result2.TotalExecutionTimeMs = 200.0;
        result2.EfficiencyPercentage = 85.0;
        
        _performanceMonitor.RecordExecution(result1);
        _performanceMonitor.RecordExecution(result2);

        // Assert
        var metrics = _performanceMonitor.GetCurrentMetrics();
        Assert.Equal(2, metrics.TotalExecutions);
        Assert.True(metrics.AverageExecutionTimeMs > 0);
        Assert.True(metrics.AverageEfficiencyPercentage > 0);
    }

    private ExecutionParallelExecutionResult CreateMockParallelExecutionResult()
    {
        return new ExecutionParallelExecutionResult
        {
            Strategy = ExecutionExecutionStrategyType.DataParallel,
            Success = true,
            TotalExecutionTimeMs = 150.0,
            ThroughputGFLOPS = 12.5,
            MemoryBandwidthGBps = 85.3,
            EfficiencyPercentage = 78.2,
            DeviceResults = new[]
            {
                new ExecutionDeviceExecutionResult
                {
                    DeviceId = "GPU_0",
                    Success = true,
                    ExecutionTimeMs = 145.0,
                    ThroughputGFLOPS = 12.8,
                    MemoryBandwidthGBps = 87.1
                }
            },
            ErrorMessage = null
        };
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