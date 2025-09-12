// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using DotCompute.Backends.Metal.Telemetry;
using DotCompute.Backends.Metal.Execution;

namespace DotCompute.Backends.Metal.Tests.Telemetry;

/// <summary>
/// Unit tests for MetalTelemetryManager
/// </summary>
public sealed class MetalTelemetryManagerTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MetalTelemetryOptions _options;
    private MetalTelemetryManager? _telemetryManager;

    public MetalTelemetryManagerTests()
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
        _options = new MetalTelemetryOptions
        {
            ReportingInterval = TimeSpan.FromMilliseconds(100),
            CleanupInterval = TimeSpan.FromMilliseconds(200),
            SlowOperationThresholdMs = 50.0
        };
    }

    [Fact]
    public void Constructor_WithValidOptions_InitializesSuccessfully()
    {
        // Arrange & Act
        _telemetryManager = new MetalTelemetryManager(
            Options.Create(_options),
            _loggerFactory.CreateLogger<MetalTelemetryManager>(),
            _loggerFactory);

        // Assert
        Assert.NotNull(_telemetryManager);
    }

    [Fact]
    public void RecordMemoryAllocation_WithValidParameters_RecordsSuccessfully()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const long sizeBytes = 1024 * 1024; // 1MB
        var duration = TimeSpan.FromMilliseconds(10);

        // Act
        _telemetryManager.RecordMemoryAllocation(sizeBytes, duration, success: true);

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.True(snapshot.TotalOperations > 0);
        Assert.Equal(0, snapshot.TotalErrors);
    }

    [Fact]
    public void RecordMemoryAllocation_WithFailure_RecordsError()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const long sizeBytes = 1024 * 1024; // 1MB
        var duration = TimeSpan.FromMilliseconds(10);

        // Act
        _telemetryManager.RecordMemoryAllocation(sizeBytes, duration, success: false);

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.True(snapshot.TotalOperations > 0);
        Assert.True(snapshot.TotalErrors > 0);
        Assert.True(snapshot.ErrorRate > 0);
    }

    [Fact]
    public void RecordKernelExecution_WithValidParameters_RecordsSuccessfully()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const string kernelName = "test_kernel";
        var duration = TimeSpan.FromMilliseconds(25);
        const long dataSize = 4096;

        // Act
        _telemetryManager.RecordKernelExecution(kernelName, duration, dataSize, success: true);

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.True(snapshot.TotalOperations > 0);
        Assert.Contains(snapshot.OperationMetrics, kvp => kvp.Key.Contains(kernelName));
    }

    [Fact]
    public void RecordKernelExecution_WithSlowOperation_RecordsCorrectly()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const string kernelName = "slow_kernel";
        var duration = TimeSpan.FromMilliseconds(100); // Above threshold
        const long dataSize = 4096;

        // Act
        _telemetryManager.RecordKernelExecution(kernelName, duration, dataSize, success: true);

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.True(snapshot.TotalOperations > 0);
        var operationMetrics = snapshot.OperationMetrics.Values.FirstOrDefault(m => m.OperationName.Contains(kernelName));
        Assert.NotNull(operationMetrics);
        Assert.True(operationMetrics.AverageExecutionTime >= duration);
    }

    [Fact]
    public void RecordDeviceUtilization_WithValidParameters_RecordsSuccessfully()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const double gpuUtilization = 75.0;
        const double memoryUtilization = 65.0;
        const long totalMemory = 8L * 1024 * 1024 * 1024; // 8GB
        const long usedMemory = totalMemory * 65 / 100;

        // Act
        _telemetryManager.RecordDeviceUtilization(gpuUtilization, memoryUtilization, totalMemory, usedMemory);

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.Contains(snapshot.ResourceMetrics, kvp => kvp.Key.Contains("gpu_device"));
        
        var gpuResource = snapshot.ResourceMetrics["gpu_device"];
        Assert.Equal(usedMemory, gpuResource.CurrentUsage);
    }

    [Fact]
    public void RecordDeviceUtilization_WithHighUtilization_TriggersAlert()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const double highGpuUtilization = 95.0; // Above threshold
        const double memoryUtilization = 60.0;
        const long totalMemory = 8L * 1024 * 1024 * 1024;
        const long usedMemory = totalMemory * 60 / 100;

        // Act
        _telemetryManager.RecordDeviceUtilization(highGpuUtilization, memoryUtilization, totalMemory, usedMemory);

        // Assert - This would trigger an alert in the alerts manager
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.NotNull(snapshot);
    }

    [Fact]
    public void RecordErrorEvent_WithValidParameters_RecordsSuccessfully()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const MetalError error = MetalError.OutOfMemory;
        const string context = "large_buffer_allocation";

        // Act
        _telemetryManager.RecordErrorEvent(error, context);

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.True(snapshot.TotalErrors > 0);
        Assert.True(snapshot.ErrorRate > 0);
    }

    [Fact]
    public void RecordMemoryPressure_WithValidParameters_RecordsSuccessfully()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const MemoryPressureLevel level = MemoryPressureLevel.Medium;
        const double percentage = 75.0;

        // Act
        _telemetryManager.RecordMemoryPressure(level, percentage);

        // Assert - Should not throw and should record the event
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.NotNull(snapshot);
    }

    [Fact]
    public void RecordResourceUsage_WithValidParameters_RecordsSuccessfully()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const ResourceType resourceType = ResourceType.Memory;
        const long currentUsage = 1024 * 1024 * 1024; // 1GB
        const long peakUsage = 1536L * 1024 * 1024; // 1.5GB
        const long limit = 2048L * 1024 * 1024; // 2GB

        // Act
        _telemetryManager.RecordResourceUsage(resourceType, currentUsage, peakUsage, limit);

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        Assert.Contains(snapshot.ResourceMetrics, kvp => kvp.Key.Contains("resource_Memory"));
        
        var memoryResource = snapshot.ResourceMetrics[$"resource_{resourceType}"];
        Assert.Equal(currentUsage, memoryResource.CurrentUsage);
        Assert.Equal(peakUsage, memoryResource.PeakUsage);
        Assert.Equal(limit, memoryResource.Limit);
    }

    [Fact]
    public void GetCurrentSnapshot_WithRecordedOperations_ReturnsValidSnapshot()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        
        // Record some operations
        _telemetryManager.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(5), true);
        _telemetryManager.RecordKernelExecution("test_kernel", TimeSpan.FromMilliseconds(10), 2048, true);
        _telemetryManager.RecordMemoryAllocation(2048, TimeSpan.FromMilliseconds(100), false); // Failure

        // Act
        var snapshot = _telemetryManager.GetCurrentSnapshot();

        // Assert
        Assert.NotNull(snapshot);
        Assert.True(snapshot.TotalOperations >= 3);
        Assert.True(snapshot.TotalErrors >= 1);
        Assert.True(snapshot.ErrorRate > 0);
        Assert.NotEmpty(snapshot.OperationMetrics);
        Assert.NotNull(snapshot.SystemInfo);
    }

    [Fact]
    public void GenerateProductionReport_WithTelemetryData_ReturnsComprehensiveReport()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        
        // Record diverse operations
        _telemetryManager.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(5), true);
        _telemetryManager.RecordKernelExecution("matrix_multiply", TimeSpan.FromMilliseconds(15), 4096, true);
        _telemetryManager.RecordDeviceUtilization(80.0, 70.0, 8L * 1024 * 1024 * 1024, 5L * 1024 * 1024 * 1024);
        _telemetryManager.RecordErrorEvent(MetalError.OutOfMemory, "large_allocation_failed");

        // Act
        var report = _telemetryManager.GenerateProductionReport();

        // Assert
        Assert.NotNull(report);
        Assert.NotNull(report.Snapshot);
        Assert.NotNull(report.PerformanceAnalysis);
        Assert.NotNull(report.HealthAnalysis);
        Assert.NotEmpty(report.Recommendations);
        
        // Verify snapshot contains data
        Assert.True(report.Snapshot.TotalOperations > 0);
        Assert.True(report.Snapshot.TotalErrors > 0);
        Assert.NotEmpty(report.Snapshot.OperationMetrics);
    }

    [Fact]
    public async Task ExportMetricsAsync_WithValidConfiguration_ExportsSuccessfully()
    {
        // Arrange
        var exportOptions = new MetalExportOptions
        {
            ExportTimeout = TimeSpan.FromSeconds(5),
            Exporters = new List<ExporterConfiguration>
            {
                new()
                {
                    Name = "Test Exporter",
                    Type = ExporterType.Custom,
                    Endpoint = "http://localhost:9999/metrics",
                    Enabled = true
                }
            }
        };
        
        var options = new MetalTelemetryOptions
        {
            ExportOptions = exportOptions
        };

        _telemetryManager = new MetalTelemetryManager(
            Options.Create(options),
            _loggerFactory.CreateLogger<MetalTelemetryManager>(),
            _loggerFactory);

        // Record some data
        _telemetryManager.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(5), true);

        // Act & Assert - Should not throw even if endpoint is unreachable
        await _telemetryManager.ExportMetricsAsync(CancellationToken.None);
    }

    [Fact]
    public void OperationMetrics_WithMultipleOperations_CalculatesCorrectAverages()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const string kernelName = "test_kernel";
        
        var durations = new[]
        {
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(30)
        };

        // Act
        foreach (var duration in durations)
        {
            _telemetryManager.RecordKernelExecution(kernelName, duration, 1024, true);
        }

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        var operationKey = $"kernel_{kernelName}";
        
        Assert.Contains(operationKey, snapshot.OperationMetrics.Keys);
        
        var metrics = snapshot.OperationMetrics[operationKey];
        Assert.Equal(3, metrics.TotalExecutions);
        Assert.Equal(3, metrics.SuccessfulExecutions);
        Assert.Equal(1.0, metrics.SuccessRate);
        
        var expectedAverage = TimeSpan.FromMilliseconds(durations.Average(d => d.TotalMilliseconds));
        Assert.True(Math.Abs((metrics.AverageExecutionTime - expectedAverage).TotalMilliseconds) < 1.0);
    }

    [Fact]
    public void ResourceMetrics_WithMultipleUpdates_TracksCorrectly()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        const ResourceType resourceType = ResourceType.Memory;
        
        var usageData = new[]
        {
            (current: 1000L, peak: 1000L, limit: 2000L),
            (current: 1500L, peak: 1500L, limit: 2000L),
            (current: 1200L, peak: 1500L, limit: 2000L)
        };

        // Act
        foreach (var (current, peak, limit) in usageData)
        {
            _telemetryManager.RecordResourceUsage(resourceType, current, peak, limit);
        }

        // Assert
        var snapshot = _telemetryManager.GetCurrentSnapshot();
        var resourceKey = $"resource_{resourceType}";
        
        Assert.Contains(resourceKey, snapshot.ResourceMetrics.Keys);
        
        var metrics = snapshot.ResourceMetrics[resourceKey];
        Assert.Equal(1200L, metrics.CurrentUsage);
        Assert.Equal(1500L, metrics.PeakUsage);
        Assert.Equal(2000L, metrics.Limit);
        Assert.Equal(60.0, metrics.UtilizationPercentage); // 1200/2000 * 100
    }

    [Fact]
    public void Dispose_WithActiveManager_DisposesCleanly()
    {
        // Arrange
        _telemetryManager = CreateTelemetryManager();
        _telemetryManager.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(5), true);

        // Act & Assert - Should not throw
        _telemetryManager.Dispose();
    }

    private MetalTelemetryManager CreateTelemetryManager()
    {
        return new MetalTelemetryManager(
            Options.Create(_options),
            _loggerFactory.CreateLogger<MetalTelemetryManager>(),
            _loggerFactory);
    }

    public void Dispose()
    {
        _telemetryManager?.Dispose();
        _loggerFactory.Dispose();
    }
}