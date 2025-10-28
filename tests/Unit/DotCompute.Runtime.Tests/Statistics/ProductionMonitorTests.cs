// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: ProductionMonitor class does not exist in Runtime module yet
// TODO: Uncomment when ProductionMonitor is implemented in DotCompute.Runtime.Services.Monitoring
/*
using DotCompute.Runtime.Services.Monitoring;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Statistics;

/// <summary>
/// Tests for ProductionMonitor
/// </summary>
public sealed class ProductionMonitorTests : IDisposable
{
    private readonly ILogger<ProductionMonitor> _mockLogger;
    private ProductionMonitor? _monitor;

    public ProductionMonitorTests()
    {
        _mockLogger = Substitute.For<ILogger<ProductionMonitor>>();
    }

    public void Dispose()
    {
        _monitor?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ProductionMonitor(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task StartMonitoringAsync_InitializesMonitoring()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);

        // Act
        await _monitor.StartMonitoringAsync();

        // Assert
        _monitor.IsMonitoring.Should().BeTrue();
    }

    [Fact]
    public async Task StopMonitoringAsync_StopsMonitoring()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);
        await _monitor.StartMonitoringAsync();

        // Act
        await _monitor.StopMonitoringAsync();

        // Assert
        _monitor.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public async Task RecordMetric_StoresMetric()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);
        await _monitor.StartMonitoringAsync();

        // Act
        _monitor.RecordMetric("test_metric", 42.0);

        // Assert
        var metrics = _monitor.GetMetrics();
        metrics.Should().ContainKey("test_metric");
    }

    [Fact]
    public async Task GetMetrics_ReturnsAllRecordedMetrics()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);
        await _monitor.StartMonitoringAsync();
        _monitor.RecordMetric("metric1", 10.0);
        _monitor.RecordMetric("metric2", 20.0);

        // Act
        var metrics = _monitor.GetMetrics();

        // Assert
        metrics.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAverageMetric_CalculatesCorrectly()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);
        await _monitor.StartMonitoringAsync();
        _monitor.RecordMetric("test", 10.0);
        _monitor.RecordMetric("test", 20.0);
        _monitor.RecordMetric("test", 30.0);

        // Act
        var average = _monitor.GetAverageMetric("test");

        // Assert
        average.Should().BeApproximately(20.0, 0.01);
    }

    [Fact]
    public async Task ResetMetrics_ClearsAllMetrics()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);
        await _monitor.StartMonitoringAsync();
        _monitor.RecordMetric("test", 42.0);

        // Act
        _monitor.ResetMetrics();

        // Assert
        _monitor.GetMetrics().Should().BeEmpty();
    }

    [Fact]
    public void RecordMetric_BeforeStarting_ThrowsInvalidOperationException()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);

        // Act
        var action = () => _monitor.RecordMetric("test", 42.0);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task GetMetricHistory_ReturnsHistoricalData()
    {
        // Arrange
        _monitor = new ProductionMonitor(_mockLogger);
        await _monitor.StartMonitoringAsync();
        _monitor.RecordMetric("test", 10.0);
        _monitor.RecordMetric("test", 20.0);

        // Act
        var history = _monitor.GetMetricHistory("test");

        // Assert
        history.Should().HaveCount(2);
    }
}
*/