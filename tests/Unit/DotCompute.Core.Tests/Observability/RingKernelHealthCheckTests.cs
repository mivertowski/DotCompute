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
/// Tests for Ring Kernel health check functionality.
/// </summary>
public sealed class RingKernelHealthCheckTests : IDisposable
{
    private readonly Mock<ILogger<RingKernelHealthCheck>> _loggerMock;
    private readonly Mock<IRingKernelRuntime> _runtimeMock;
    private readonly Mock<IRingKernelInstrumentation> _instrumentationMock;
    private readonly RingKernelHealthOptions _options;
    private readonly RingKernelHealthCheck _healthCheck;

    public RingKernelHealthCheckTests()
    {
        _loggerMock = new Mock<ILogger<RingKernelHealthCheck>>();
        _runtimeMock = new Mock<IRingKernelRuntime>();
        _instrumentationMock = new Mock<IRingKernelInstrumentation>();
        _options = new RingKernelHealthOptions
        {
            Timeout = TimeSpan.FromSeconds(5),
            StuckThreshold = TimeSpan.FromSeconds(30),
            QueueDepthWarningThreshold = 0.8,
            QueueDepthCriticalThreshold = 0.95,
            CacheDuration = TimeSpan.FromSeconds(1)
        };

        _healthCheck = new RingKernelHealthCheck(
            _loggerMock.Object,
            Options.Create(_options),
            _runtimeMock.Object,
            _instrumentationMock.Object);
    }

    public void Dispose()
    {
        _healthCheck.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        Assert.NotNull(_healthCheck);
        Assert.Empty(_healthCheck.RegisteredKernels);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RingKernelHealthCheck(
                null!,
                Options.Create(_options),
                _runtimeMock.Object));
    }

    [Fact]
    public void Constructor_WithNullRuntime_CreatesInstance()
    {
        // Arrange & Act
        using var healthCheck = new RingKernelHealthCheck(
            _loggerMock.Object,
            Options.Create(_options),
            null);

        // Assert
        Assert.NotNull(healthCheck);
    }

    #endregion

    #region Registration Tests

    [Fact]
    public void RegisterKernel_WithValidId_AddsToRegisteredKernels()
    {
        // Arrange
        var kernelId = "test-kernel";

        // Act
        _healthCheck.RegisterKernel(kernelId);

        // Assert
        Assert.Contains(kernelId, _healthCheck.RegisteredKernels);
    }

    [Fact]
    public void RegisterKernel_WithNullId_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _healthCheck.RegisterKernel(null!));
    }

    [Fact]
    public void RegisterKernel_WithCustomOptions_UsesKernelOptions()
    {
        // Arrange
        var kernelId = "test-kernel";
        var kernelOptions = new RingKernelHealthOptions
        {
            StuckThreshold = TimeSpan.FromSeconds(60)
        };

        // Act
        _healthCheck.RegisterKernel(kernelId, kernelOptions);

        // Assert
        Assert.Contains(kernelId, _healthCheck.RegisteredKernels);
    }

    [Fact]
    public void UnregisterKernel_WithRegisteredId_RemovesFromRegisteredKernels()
    {
        // Arrange
        var kernelId = "test-kernel";
        _healthCheck.RegisterKernel(kernelId);

        // Act
        _healthCheck.UnregisterKernel(kernelId);

        // Assert
        Assert.DoesNotContain(kernelId, _healthCheck.RegisteredKernels);
    }

    [Fact]
    public void UnregisterKernel_WithUnregisteredId_DoesNotThrow()
    {
        // Act - Should not throw
        _healthCheck.UnregisterKernel("non-existent");

        // Assert
        Assert.True(true);
    }

    #endregion

    #region Health Check Tests

    [Fact]
    public async Task CheckHealthAsync_WithNoKernels_ReturnsHealthyReport()
    {
        // Act
        var report = await _healthCheck.CheckHealthAsync();

        // Assert
        Assert.NotNull(report);
        Assert.Equal(RingKernelHealthStatus.Healthy, report.Status);
        Assert.Empty(report.Entries);
    }

    [Fact]
    public async Task CheckHealthAsync_WithHealthyKernel_ReturnsHealthyStatus()
    {
        // Arrange
        var kernelId = "healthy-kernel";
        _healthCheck.RegisterKernel(kernelId);

        SetupHealthyKernel(kernelId);

        // Act
        var report = await _healthCheck.CheckHealthAsync();

        // Assert
        Assert.Equal(RingKernelHealthStatus.Healthy, report.Status);
        Assert.Single(report.Entries);
        Assert.Equal(RingKernelHealthStatus.Healthy, report.Entries[kernelId].Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WithKernelError_ReturnsUnhealthyStatus()
    {
        // Arrange
        var kernelId = "error-kernel";
        _healthCheck.RegisterKernel(kernelId);

        SetupKernelWithError(kernelId, 42);

        // Act
        var report = await _healthCheck.CheckHealthAsync();

        // Assert
        Assert.Equal(RingKernelHealthStatus.Unhealthy, report.Status);
        Assert.Equal(RingKernelHealthStatus.Unhealthy, report.Entries[kernelId].Status);
    }

    [Fact]
    public async Task CheckKernelHealthAsync_WithHealthyKernel_ReturnsHealthyEntry()
    {
        // Arrange
        var kernelId = "healthy-kernel";
        _healthCheck.RegisterKernel(kernelId);
        SetupHealthyKernel(kernelId);

        // Act
        var entry = await _healthCheck.CheckKernelHealthAsync(kernelId);

        // Assert
        Assert.NotNull(entry);
        Assert.Equal(kernelId, entry.KernelId);
        Assert.Equal(RingKernelHealthStatus.Healthy, entry.Status);
    }

    [Fact]
    public async Task CheckKernelHealthAsync_WithNullId_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _healthCheck.CheckKernelHealthAsync(null!));
    }

    [Fact]
    public async Task CheckKernelHealthAsync_WithRuntimeException_ReturnsUnhealthyEntry()
    {
        // Arrange
        var kernelId = "exception-kernel";
        _healthCheck.RegisterKernel(kernelId);

        _runtimeMock.Setup(r => r.GetStatusAsync(kernelId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Runtime error"));

        // Act
        var entry = await _healthCheck.CheckKernelHealthAsync(kernelId);

        // Assert
        Assert.Equal(RingKernelHealthStatus.Unhealthy, entry.Status);
        Assert.NotNull(entry.Exception);
    }

    #endregion

    #region Cached Health Tests

    [Fact]
    public void GetCachedHealth_WithNoCache_ReturnsNull()
    {
        // Arrange
        var kernelId = "uncached-kernel";

        // Act
        var cached = _healthCheck.GetCachedHealth(kernelId);

        // Assert
        Assert.Null(cached);
    }

    [Fact]
    public async Task GetCachedHealth_AfterCheck_ReturnsCachedEntry()
    {
        // Arrange
        var kernelId = "cached-kernel";
        _healthCheck.RegisterKernel(kernelId);
        SetupHealthyKernel(kernelId);

        await _healthCheck.CheckKernelHealthAsync(kernelId);

        // Act
        var cached = _healthCheck.GetCachedHealth(kernelId);

        // Assert
        Assert.NotNull(cached);
        Assert.Equal(kernelId, cached.KernelId);
    }

    #endregion

    #region Health Changed Event Tests

    [Fact]
    public async Task HealthChanged_WhenStatusChanges_RaisesEvent()
    {
        // Arrange
        var kernelId = "changing-kernel";
        // Use zero cache duration to ensure each check is performed
        var noCacheOptions = new RingKernelHealthOptions { CacheDuration = TimeSpan.Zero };
        _healthCheck.RegisterKernel(kernelId, noCacheOptions);
        SetupHealthyKernel(kernelId);

        RingKernelHealthChangedEventArgs? eventArgs = null;
        _healthCheck.HealthChanged += (s, e) => eventArgs = e;

        // First check - healthy
        await _healthCheck.CheckKernelHealthAsync(kernelId);

        // Change to unhealthy
        SetupKernelWithError(kernelId, 1);

        // Act
        await _healthCheck.CheckKernelHealthAsync(kernelId);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(kernelId, eventArgs.KernelId);
        Assert.Equal(RingKernelHealthStatus.Healthy, eventArgs.PreviousStatus);
        Assert.Equal(RingKernelHealthStatus.Unhealthy, eventArgs.CurrentStatus);
    }

    #endregion

    #region Report Statistics Tests

    [Fact]
    public async Task HealthReport_HasCorrectCounts()
    {
        // Arrange
        _healthCheck.RegisterKernel("healthy-1");
        _healthCheck.RegisterKernel("healthy-2");
        _healthCheck.RegisterKernel("unhealthy-1");

        SetupHealthyKernel("healthy-1");
        SetupHealthyKernel("healthy-2");
        SetupKernelWithError("unhealthy-1", 1);

        // Act
        var report = await _healthCheck.CheckHealthAsync();

        // Assert
        Assert.Equal(2, report.HealthyCount);
        Assert.Equal(1, report.UnhealthyCount);
        Assert.Equal(0, report.DegradedCount);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var healthCheck = new RingKernelHealthCheck(
            _loggerMock.Object,
            Options.Create(_options));

        // Act & Assert
        healthCheck.Dispose();
        healthCheck.Dispose();
    }

    [Fact]
    public async Task CheckHealthAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _healthCheck.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _healthCheck.CheckHealthAsync());
    }

    #endregion

    #region Helper Methods

    private void SetupHealthyKernel(string kernelId)
    {
        var status = new RingKernelStatus
        {
            KernelId = kernelId,
            IsLaunched = true,
            IsActive = true,
            IsTerminating = false,
            MessagesPending = 10,
            MessagesProcessed = 1000,
            Uptime = TimeSpan.FromMinutes(5)
        };

        var telemetry = new RingKernelTelemetry
        {
            MessagesProcessed = 1000,
            MessagesDropped = 0,
            QueueDepth = 10,
            ErrorCode = 0,
            TotalLatencyNanos = 1_000_000,
            MaxLatencyNanos = 10_000,
            MinLatencyNanos = 1_000,
            LastProcessedTimestamp = DateTimeOffset.UtcNow.Ticks * 100
        };

        var metrics = new RingKernelMetrics
        {
            ThroughputMsgsPerSec = 100,
            InputQueueUtilization = 0.1,
            OutputQueueUtilization = 0.05
        };

        _runtimeMock.Setup(r => r.GetStatusAsync(kernelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        _runtimeMock.Setup(r => r.GetTelemetryAsync(kernelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(telemetry);

        _runtimeMock.Setup(r => r.GetMetricsAsync(kernelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metrics);
    }

    private void SetupKernelWithError(string kernelId, int errorCode)
    {
        var status = new RingKernelStatus
        {
            KernelId = kernelId,
            IsLaunched = true,
            IsActive = true,
            IsTerminating = false,
            MessagesProcessed = 500,
            Uptime = TimeSpan.FromMinutes(2)
        };

        var telemetry = new RingKernelTelemetry
        {
            MessagesProcessed = 500,
            ErrorCode = (ushort)errorCode
        };

        _runtimeMock.Setup(r => r.GetStatusAsync(kernelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        _runtimeMock.Setup(r => r.GetTelemetryAsync(kernelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(telemetry);
    }

    #endregion
}

/// <summary>
/// Tests for Ring Kernel health options.
/// </summary>
public sealed class RingKernelHealthOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new RingKernelHealthOptions();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), options.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.StuckThreshold);
        Assert.Equal(0.8, options.QueueDepthWarningThreshold);
        Assert.Equal(0.95, options.QueueDepthCriticalThreshold);
        Assert.Equal(100, options.LatencyWarningThresholdMs);
        Assert.Equal(500, options.LatencyCriticalThresholdMs);
        Assert.True(options.IncludeDetailedDiagnostics);
    }

    [Fact]
    public void Tags_CanBeAdded()
    {
        // Arrange
        var options = new RingKernelHealthOptions();

        // Act
        options.Tags.Add("environment");
        options.Tags.Add("critical");

        // Assert
        Assert.Equal(2, options.Tags.Count);
    }
}

/// <summary>
/// Tests for Ring Kernel health entry.
/// </summary>
public sealed class RingKernelHealthEntryTests
{
    [Fact]
    public void HealthEntry_InitializesCorrectly()
    {
        // Arrange & Act
        var entry = new RingKernelHealthEntry
        {
            KernelId = "test-kernel",
            Status = RingKernelHealthStatus.Healthy,
            Description = "Kernel is healthy",
            IsActive = true,
            Uptime = TimeSpan.FromMinutes(5),
            Throughput = 1000
        };

        // Assert
        Assert.Equal("test-kernel", entry.KernelId);
        Assert.Equal(RingKernelHealthStatus.Healthy, entry.Status);
        Assert.True(entry.IsActive);
        Assert.NotNull(entry.Uptime);
        Assert.Equal(1000, entry.Throughput);
    }
}

/// <summary>
/// Tests for Ring Kernel health check factory.
/// </summary>
public sealed class RingKernelHealthCheckFactoryTests
{
    [Fact]
    public void CreateHealthCheck_WithDefaults_ReturnsInstance()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger<RingKernelHealthCheck>>());

        var factory = new RingKernelHealthCheckFactory(loggerFactory.Object);

        // Act
        var healthCheck = factory.CreateHealthCheck();

        // Assert
        Assert.NotNull(healthCheck);
    }

    [Fact]
    public void CreateHealthCheck_WithCustomOptions_ReturnsInstance()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(Mock.Of<ILogger<RingKernelHealthCheck>>());

        var factory = new RingKernelHealthCheckFactory(loggerFactory.Object);
        var options = new RingKernelHealthOptions
        {
            StuckThreshold = TimeSpan.FromMinutes(1)
        };

        // Act
        var healthCheck = factory.CreateHealthCheck(options);

        // Assert
        Assert.NotNull(healthCheck);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RingKernelHealthCheckFactory(null!));
    }
}
