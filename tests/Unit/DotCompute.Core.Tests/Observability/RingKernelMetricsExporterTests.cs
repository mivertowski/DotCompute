// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DotCompute.Abstractions.Observability;
using DotCompute.Abstractions.RingKernels;
using DotCompute.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests.Observability;

/// <summary>
/// Tests for Ring Kernel metrics exporter functionality.
/// </summary>
public sealed class RingKernelMetricsExporterTests : IDisposable
{
    private readonly Mock<ILogger<RingKernelMetricsExporter>> _loggerMock;
    private readonly Mock<IRingKernelRuntime> _runtimeMock;
    private readonly Mock<IRingKernelHealthCheck> _healthCheckMock;
    private readonly RingKernelMetricsExporterOptions _options;
    private readonly RingKernelMetricsExporter _exporter;

    public RingKernelMetricsExporterTests()
    {
        _loggerMock = new Mock<ILogger<RingKernelMetricsExporter>>();
        _runtimeMock = new Mock<IRingKernelRuntime>();
        _healthCheckMock = new Mock<IRingKernelHealthCheck>();
        _options = new RingKernelMetricsExporterOptions
        {
            CollectionIntervalSeconds = 60,
            ServiceName = "test_service"
        };

        _runtimeMock.Setup(r => r.ListKernelsAsync())
            .ReturnsAsync([]);

        _exporter = new RingKernelMetricsExporter(
            _loggerMock.Object,
            Options.Create(_options),
            _runtimeMock.Object,
            _healthCheckMock.Object);
    }

    public void Dispose()
    {
        _exporter.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        Assert.NotNull(_exporter);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RingKernelMetricsExporter(
                null!,
                Options.Create(_options)));
    }

    [Fact]
    public void Constructor_WithNullOptions_UsesDefaults()
    {
        // Arrange & Act
        using var exporter = new RingKernelMetricsExporter(
            _loggerMock.Object,
            Options.Create<RingKernelMetricsExporterOptions>(null!));

        // Assert
        Assert.NotNull(exporter);
    }

    [Fact]
    public void Constructor_WithoutRuntime_CreatesInstance()
    {
        // Arrange & Act
        using var exporter = new RingKernelMetricsExporter(
            _loggerMock.Object,
            Options.Create(_options));

        // Assert
        Assert.NotNull(exporter);
    }

    #endregion

    #region Registration Tests

    [Fact]
    public void RegisterKernel_WithValidId_AddsKernel()
    {
        // Arrange
        var kernelId = "test-kernel";

        // Act
        _exporter.RegisterKernel(kernelId);

        // Assert - GetSummary should show one kernel
        var summary = _exporter.GetSummary();
        Assert.Equal(1, summary.TotalKernels);
    }

    [Fact]
    public void RegisterKernel_WithNullId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _exporter.RegisterKernel(null!));
    }

    [Fact]
    public void RegisterKernel_MultipleTimes_DoesNotDuplicate()
    {
        // Arrange
        var kernelId = "test-kernel";

        // Act
        _exporter.RegisterKernel(kernelId);
        _exporter.RegisterKernel(kernelId);

        // Assert
        var summary = _exporter.GetSummary();
        Assert.Equal(1, summary.TotalKernels);
    }

    [Fact]
    public void UnregisterKernel_WithRegisteredId_RemovesKernel()
    {
        // Arrange
        var kernelId = "test-kernel";
        _exporter.RegisterKernel(kernelId);

        // Act
        _exporter.UnregisterKernel(kernelId);

        // Assert
        var summary = _exporter.GetSummary();
        Assert.Equal(0, summary.TotalKernels);
    }

    [Fact]
    public void UnregisterKernel_WithNullId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _exporter.UnregisterKernel(null!));
    }

    [Fact]
    public void UnregisterKernel_WithUnregisteredId_DoesNotThrow()
    {
        // Act - Should not throw
        _exporter.UnregisterKernel("non-existent");

        // Assert
        Assert.True(true);
    }

    #endregion

    #region UpdateFromTelemetry Tests

    [Fact]
    public void UpdateFromTelemetry_UpdatesMetrics()
    {
        // Arrange
        var kernelId = "telemetry-kernel";
        _exporter.RegisterKernel(kernelId);

        var telemetry = new RingKernelTelemetry
        {
            MessagesProcessed = 1000,
            MessagesDropped = 5,
            QueueDepth = 50,
            TotalLatencyNanos = 100_000_000, // 100ms total
            MaxLatencyNanos = 10_000_000,
            MinLatencyNanos = 1_000,
            ErrorCode = 0
        };

        // Act
        _exporter.UpdateFromTelemetry(kernelId, telemetry, TimeSpan.FromSeconds(10));

        // Assert
        var summary = _exporter.GetSummary();
        Assert.Equal(1000UL, (ulong)summary.TotalMessagesProcessed);
        Assert.Equal(5UL, (ulong)summary.TotalMessagesDropped);
    }

    [Fact]
    public void UpdateFromTelemetry_WithUnregisteredKernel_CreatesSnapshot()
    {
        // Arrange
        var kernelId = "new-kernel";
        var telemetry = new RingKernelTelemetry
        {
            MessagesProcessed = 500
        };

        // Act
        _exporter.UpdateFromTelemetry(kernelId, telemetry, TimeSpan.FromSeconds(5));

        // Assert
        var summary = _exporter.GetSummary();
        Assert.Equal(1, summary.TotalKernels);
    }

    #endregion

    #region Export Tests

    [Fact]
    public async Task ExportAsync_WithNoKernels_ReturnsEmptyMetrics()
    {
        // Act
        var metrics = await _exporter.ExportAsync();

        // Assert
        Assert.NotNull(metrics);
        Assert.Contains("# HELP", metrics);
        Assert.Contains("# TYPE", metrics);
    }

    [Fact]
    public async Task ExportAsync_WithRegisteredKernel_ReturnsMetricsForKernel()
    {
        // Arrange
        var kernelId = "export-kernel";
        _exporter.RegisterKernel(kernelId);

        var telemetry = new RingKernelTelemetry
        {
            MessagesProcessed = 100,
            QueueDepth = 10
        };
        _exporter.UpdateFromTelemetry(kernelId, telemetry, TimeSpan.FromSeconds(5));

        // Act
        var metrics = await _exporter.ExportAsync();

        // Assert
        Assert.Contains("ring_kernel_messages_processed_total", metrics);
        Assert.Contains(kernelId, metrics);
    }

    [Fact]
    public async Task ExportAsync_IncludesPrometheusFormat()
    {
        // Arrange
        var kernelId = "format-kernel";
        _exporter.RegisterKernel(kernelId);
        _exporter.UpdateFromTelemetry(kernelId, new RingKernelTelemetry(), TimeSpan.FromSeconds(1));

        // Act
        var metrics = await _exporter.ExportAsync();

        // Assert - Check Prometheus format elements
        Assert.Contains("# HELP ring_kernel_messages_processed_total", metrics);
        Assert.Contains("# TYPE ring_kernel_messages_processed_total counter", metrics);
    }

    [Fact]
    public async Task ExportAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _exporter.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _exporter.ExportAsync());
    }

    #endregion

    #region GetSummary Tests

    [Fact]
    public void GetSummary_WithNoKernels_ReturnsEmptySummary()
    {
        // Act
        var summary = _exporter.GetSummary();

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalKernels);
        Assert.Equal(0, summary.ActiveKernels);
        Assert.Equal(0, summary.HealthyKernels);
    }

    [Fact]
    public void GetSummary_WithMultipleKernels_AggregatesCorrectly()
    {
        // Arrange
        _exporter.RegisterKernel("kernel-1");
        _exporter.RegisterKernel("kernel-2");

        var telemetry1 = new RingKernelTelemetry { MessagesProcessed = 500 };
        var telemetry2 = new RingKernelTelemetry { MessagesProcessed = 300 };

        _exporter.UpdateFromTelemetry("kernel-1", telemetry1, TimeSpan.FromSeconds(5));
        _exporter.UpdateFromTelemetry("kernel-2", telemetry2, TimeSpan.FromSeconds(5));

        // Act
        var summary = _exporter.GetSummary();

        // Assert
        Assert.Equal(2, summary.TotalKernels);
        Assert.Equal(800L, summary.TotalMessagesProcessed);
    }

    [Fact]
    public void GetSummary_IncludesTimestamp()
    {
        // Act
        var before = DateTimeOffset.UtcNow;
        var summary = _exporter.GetSummary();
        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(summary.Timestamp >= before && summary.Timestamp <= after);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        using var exporter = new RingKernelMetricsExporter(
            _loggerMock.Object,
            Options.Create(_options));

        // Act & Assert
        exporter.Dispose();
        exporter.Dispose();
    }

    #endregion
}

/// <summary>
/// Tests for Ring Kernel metrics exporter options.
/// </summary>
public sealed class RingKernelMetricsExporterOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new RingKernelMetricsExporterOptions();

        // Assert
        Assert.Equal(15, options.CollectionIntervalSeconds);
        Assert.True(options.EnableHistograms);
        Assert.NotEmpty(options.LatencyBuckets);
        Assert.Equal(TimeSpan.FromSeconds(30), options.StuckKernelThreshold);
        Assert.Equal("dotcompute_ring_kernels", options.ServiceName);
    }

    [Fact]
    public void LatencyBuckets_ContainsExpectedValues()
    {
        // Arrange
        var options = new RingKernelMetricsExporterOptions();

        // Assert - Check it has reasonable bucket values
        Assert.Contains(0.001, options.LatencyBuckets);
        Assert.Contains(1.0, options.LatencyBuckets);
        Assert.Contains(10.0, options.LatencyBuckets);
    }

    [Fact]
    public void GlobalLabels_CanBeAdded()
    {
        // Arrange
        var options = new RingKernelMetricsExporterOptions();

        // Act
        options.GlobalLabels.Add("environment", "production");
        options.GlobalLabels.Add("region", "us-west-2");

        // Assert
        Assert.Equal(2, options.GlobalLabels.Count);
    }

    [Fact]
    public void LatencyBuckets_CanBeCustomized()
    {
        // Arrange
        var options = new RingKernelMetricsExporterOptions();
        var customBuckets = new List<double> { 0.01, 0.1, 1.0, 10.0 };

        // Act
        options.LatencyBuckets = customBuckets;

        // Assert
        Assert.Equal(4, options.LatencyBuckets.Count);
    }
}

/// <summary>
/// Tests for Ring Kernel metrics summary.
/// </summary>
public sealed class RingKernelMetricsSummaryTests
{
    [Fact]
    public void Summary_InitializesCorrectly()
    {
        // Arrange & Act
        var summary = new RingKernelMetricsSummary
        {
            TotalKernels = 10,
            ActiveKernels = 8,
            HealthyKernels = 7,
            TotalMessagesProcessed = 1_000_000,
            TotalMessagesDropped = 100,
            AverageThroughput = 10_000.0,
            AverageLatencyNanos = 50_000.0,
            MaxQueueDepth = 1000
        };

        // Assert
        Assert.Equal(10, summary.TotalKernels);
        Assert.Equal(8, summary.ActiveKernels);
        Assert.Equal(7, summary.HealthyKernels);
        Assert.Equal(1_000_000L, summary.TotalMessagesProcessed);
        Assert.Equal(100L, summary.TotalMessagesDropped);
        Assert.Equal(10_000.0, summary.AverageThroughput);
        Assert.Equal(50_000.0, summary.AverageLatencyNanos);
        Assert.Equal(1000, summary.MaxQueueDepth);
    }

    [Fact]
    public void Summary_HasTimestamp()
    {
        // Arrange & Act
        var summary = new RingKernelMetricsSummary();

        // Assert
        Assert.NotEqual(default, summary.Timestamp);
    }
}
