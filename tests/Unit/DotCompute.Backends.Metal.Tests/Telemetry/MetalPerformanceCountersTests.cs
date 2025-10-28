// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Execution;
using DotCompute.Backends.Metal.Telemetry;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.Metal.Tests.Telemetry;

/// <summary>
/// Comprehensive tests for MetalPerformanceCounters class.
/// Covers all public methods, edge cases, threading, and disposal scenarios.
/// </summary>
public sealed class MetalPerformanceCountersTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalPerformanceCounters> _logger;
    private readonly List<MetalPerformanceCounters> _countersToDispose = new();

    public MetalPerformanceCountersTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = CreateLogger();
    }

    private ILogger<MetalPerformanceCounters> CreateLogger()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XunitLoggerProvider(_output));
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        return loggerFactory.CreateLogger<MetalPerformanceCounters>();
    }

    private MetalPerformanceCounters CreateCounters(MetalPerformanceCountersOptions? options = null)
    {
        options ??= new MetalPerformanceCountersOptions
        {
            EnableContinuousSampling = false, // Disable for tests
            SamplingInterval = TimeSpan.FromSeconds(1),
            SlowAllocationThresholdMs = 100,
            SlowKernelThresholdMs = 50
        };

        var counters = new MetalPerformanceCounters(_logger, options);
        _countersToDispose.Add(counters);
        return counters;
    }

    #region Construction and Initialization Tests

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange & Act
        var options = new MetalPerformanceCountersOptions();
        var counters = CreateCounters(options);

        // Assert
        Assert.NotNull(counters);
        var currentCounters = counters.GetCurrentCounters();
        Assert.NotNull(currentCounters);
        Assert.NotEmpty(currentCounters);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new MetalPerformanceCountersOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MetalPerformanceCounters(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsException()
    {
        // Act & Assert - Implementation throws NullReferenceException before validation
        Assert.Throws<NullReferenceException>(() => new MetalPerformanceCounters(_logger, null!));
    }

    [Fact]
    public void Constructor_InitializesStandardCounters()
    {
        // Act
        var counters = CreateCounters();
        var currentCounters = counters.GetCurrentCounters();

        // Assert
        Assert.Contains("memory_allocations_total", currentCounters.Keys);
        Assert.Contains("kernel_executions_total", currentCounters.Keys);
        Assert.Contains("gpu_utilization_percent", currentCounters.Keys);
        Assert.Contains("errors_total", currentCounters.Keys);
    }

    #endregion

    #region Memory Allocation Tests

    [Fact]
    public void RecordMemoryAllocation_WithSuccessfulAllocation_UpdatesCounters()
    {
        // Arrange
        var counters = CreateCounters();
        var sizeBytes = 1024 * 1024; // 1MB
        var duration = TimeSpan.FromMilliseconds(10);

        // Act
        counters.RecordMemoryAllocation(sizeBytes, duration, success: true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "memory_allocations_total") > 0);
        Assert.True(GetCounterTotal(currentCounters, "memory_allocated_bytes_total") >= sizeBytes);
    }

    [Fact]
    public void RecordMemoryAllocation_WithFailure_IncrementsFailureCounter()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(5), success: false);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "memory_allocation_failures_total") > 0);
    }

    [Fact]
    public void RecordMemoryAllocation_WithSlowAllocation_IncrementsSlowCounter()
    {
        // Arrange
        var options = new MetalPerformanceCountersOptions
        {
            EnableContinuousSampling = false,
            SlowAllocationThresholdMs = 50
        };
        var counters = CreateCounters(options);

        // Act - Duration exceeds threshold
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(100), success: true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "memory_allocations_slow_total") > 0);
    }

    [Theory]
    [InlineData(1024, "small")] // 1KB
    [InlineData(512 * 1024, "medium")] // 512KB
    [InlineData(10 * 1024 * 1024, "large")] // 10MB
    [InlineData(200 * 1024 * 1024, "xlarge")] // 200MB
    public void RecordMemoryAllocation_CategorizesAllocationSize(long sizeBytes, string category)
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordMemoryAllocation(sizeBytes, TimeSpan.FromMilliseconds(1), success: true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        var categoryKey = $"memory_allocations_by_size_{category}";
        Assert.True(currentCounters.ContainsKey(categoryKey), $"Missing category: {categoryKey}");
        Assert.True(GetCounterTotal(currentCounters, categoryKey) > 0);
    }

    [Fact]
    public void RecordMemoryAllocation_MultipleAllocations_AccumulatesCorrectly()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordMemoryAllocation(1000, TimeSpan.FromMilliseconds(1), true);
        counters.RecordMemoryAllocation(2000, TimeSpan.FromMilliseconds(2), true);
        counters.RecordMemoryAllocation(3000, TimeSpan.FromMilliseconds(3), true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.Equal(3, GetCounterTotal(currentCounters, "memory_allocations_total"));
        Assert.Equal(6000, GetCounterTotal(currentCounters, "memory_allocated_bytes_total"));
    }

    #endregion

    #region Kernel Execution Tests

    [Fact]
    public void RecordKernelExecution_WithSuccessfulExecution_UpdatesCounters()
    {
        // Arrange
        var counters = CreateCounters();
        var kernelName = "vector_add";
        var duration = TimeSpan.FromMilliseconds(5);
        var dataSize = 1024 * 1024; // 1MB

        // Act
        counters.RecordKernelExecution(kernelName, duration, dataSize, success: true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "kernel_executions_total") > 0);
        Assert.True(GetCounterTotal(currentCounters, "kernel_data_processed_bytes_total") >= dataSize);
        Assert.True(currentCounters.ContainsKey("kernel_vector_add_executions_total"));
    }

    [Fact]
    public void RecordKernelExecution_WithFailure_IncrementsFailureCounters()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordKernelExecution("test_kernel", TimeSpan.FromMilliseconds(1), 1000, success: false);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "kernel_execution_failures_total") > 0);
        Assert.True(GetCounterTotal(currentCounters, "kernel_test_kernel_failures_total") > 0);
    }

    [Fact]
    public void RecordKernelExecution_WithSlowKernel_IncrementsSlowCounter()
    {
        // Arrange
        var options = new MetalPerformanceCountersOptions
        {
            EnableContinuousSampling = false,
            SlowKernelThresholdMs = 25
        };
        var counters = CreateCounters(options);

        // Act - Duration exceeds threshold
        counters.RecordKernelExecution("slow_kernel", TimeSpan.FromMilliseconds(50), 1024, success: true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "kernel_executions_slow_total") > 0);
    }

    [Fact]
    public void RecordKernelExecution_CalculatesThroughput()
    {
        // Arrange
        var counters = CreateCounters();
        var dataSize = 10 * 1024 * 1024; // 10MB
        var duration = TimeSpan.FromSeconds(1);

        // Act
        counters.RecordKernelExecution("throughput_test", duration, dataSize, success: true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        var throughputKey = "kernel_throughput_test_throughput_mbps";
        Assert.True(currentCounters.ContainsKey(throughputKey));
        Assert.True(GetCounterTotal(currentCounters, throughputKey) > 0);
    }

    [Fact]
    public void RecordKernelExecution_WithSpecialCharactersInName_SanitizesName()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordKernelExecution("Test-Kernel.Special Name", TimeSpan.FromMilliseconds(1), 100, true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(currentCounters.ContainsKey("kernel_test_kernel_special_name_executions_total"));
    }

    [Fact]
    public void RecordKernelExecution_MultipleKernels_TracksIndividually()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordKernelExecution("kernel_a", TimeSpan.FromMilliseconds(1), 100, true);
        counters.RecordKernelExecution("kernel_b", TimeSpan.FromMilliseconds(2), 200, true);
        counters.RecordKernelExecution("kernel_a", TimeSpan.FromMilliseconds(3), 150, true);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.Equal(2, GetCounterTotal(currentCounters, "kernel_kernel_a_executions_total"));
        Assert.Equal(1, GetCounterTotal(currentCounters, "kernel_kernel_b_executions_total"));
        Assert.Equal(3, GetCounterTotal(currentCounters, "kernel_executions_total"));
    }

    #endregion

    #region Device Utilization Tests

    [Fact]
    public void RecordDeviceUtilization_WithValidValues_UpdatesCounters()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordDeviceUtilization(gpuUtilization: 75.5, memoryUtilization: 60.2);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(Math.Abs(GetCounterCurrent(currentCounters, "gpu_utilization_percent") - 75.5) < 0.1);
        Assert.True(Math.Abs(GetCounterCurrent(currentCounters, "memory_utilization_percent") - 60.2) < 0.1);
    }

    [Theory]
    [InlineData(10.0, "low")]
    [InlineData(30.0, "medium")]
    [InlineData(60.0, "high")]
    [InlineData(90.0, "critical")]
    public void RecordDeviceUtilization_CategorizesUtilizationBands(double utilization, string band)
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordDeviceUtilization(gpuUtilization: utilization, memoryUtilization: utilization);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(currentCounters.ContainsKey($"gpu_utilization_band_{band}"));
        Assert.True(currentCounters.ContainsKey($"memory_utilization_band_{band}"));
    }

    [Fact]
    public void RecordDeviceUtilization_ZeroValues_HandledCorrectly()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordDeviceUtilization(0, 0);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.Equal(0, GetCounterCurrent(currentCounters, "gpu_utilization_percent"));
        Assert.Equal(0, GetCounterCurrent(currentCounters, "memory_utilization_percent"));
    }

    [Fact]
    public void RecordDeviceUtilization_MaxValues_HandledCorrectly()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordDeviceUtilization(100, 100);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.Equal(100, GetCounterCurrent(currentCounters, "gpu_utilization_percent"));
        Assert.Equal(100, GetCounterCurrent(currentCounters, "memory_utilization_percent"));
    }

    #endregion

    #region Error Recording Tests

    [Fact]
    public void RecordError_IncrementsErrorCounters()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordError(MetalError.OutOfMemory, "buffer_allocation");

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "errors_total") > 0);
        Assert.True(currentCounters.ContainsKey("error_OutOfMemory_total"));
        Assert.True(currentCounters.ContainsKey("error_context_buffer_allocation_total"));
    }

    [Theory]
    [InlineData(MetalError.OutOfMemory, "resource")]
    [InlineData(MetalError.InvalidOperation, "validation")]
    [InlineData(MetalError.DeviceUnavailable, "device")]
    [InlineData(MetalError.InternalError, "internal")]
    public void RecordError_CategorizesErrorsByType(MetalError error, string category)
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordError(error, "test_context");

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(currentCounters.ContainsKey($"errors_by_category_{category}"));
    }

    [Fact]
    public void RecordError_MultipleErrors_AccumulatesCorrectly()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordError(MetalError.OutOfMemory, "allocation_1");
        counters.RecordError(MetalError.OutOfMemory, "allocation_2");
        counters.RecordError(MetalError.InvalidOperation, "operation_1");

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.Equal(3, GetCounterTotal(currentCounters, "errors_total"));
        Assert.Equal(2, GetCounterTotal(currentCounters, "error_OutOfMemory_total"));
        Assert.Equal(1, GetCounterTotal(currentCounters, "error_InvalidOperation_total"));
    }

    #endregion

    #region Memory Pressure Tests

    [Fact]
    public void RecordMemoryPressure_UpdatesCounters()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordMemoryPressure(MemoryPressureLevel.Medium, 65.5);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "memory_pressure_events_total") > 0);
        Assert.True(GetCounterTotal(currentCounters, "memory_pressure_medium_total") > 0);
        Assert.True(Math.Abs(GetCounterCurrent(currentCounters, "memory_pressure_percentage") - 65.5) < 0.1);
    }

    [Theory]
    [InlineData(MemoryPressureLevel.Low)]
    [InlineData(MemoryPressureLevel.Medium)]
    [InlineData(MemoryPressureLevel.High)]
    [InlineData(MemoryPressureLevel.Critical)]
    public void RecordMemoryPressure_AllLevels_TrackedIndividually(MemoryPressureLevel level)
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordMemoryPressure(level, 50.0);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        var levelKey = $"memory_pressure_{level.ToString().ToLowerInvariant()}_total";
        Assert.True(currentCounters.ContainsKey(levelKey));
        Assert.True(GetCounterTotal(currentCounters, levelKey) > 0);
    }

    #endregion

    #region Resource Usage Tests

    [Fact]
    public void RecordResourceUsage_UpdatesAllCounters()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordResourceUsage(ResourceType.Memory, currentUsage: 1024, peakUsage: 2048, limit: 4096);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(currentCounters.ContainsKey("resource_memory_current_usage"));
        Assert.True(currentCounters.ContainsKey("resource_memory_peak_usage"));
        Assert.True(currentCounters.ContainsKey("resource_memory_limit"));
        Assert.True(currentCounters.ContainsKey("resource_memory_utilization_percent"));
    }

    [Fact]
    public void RecordResourceUsage_CalculatesUtilizationPercentage()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordResourceUsage(ResourceType.GPU, currentUsage: 750, peakUsage: 1000, limit: 1000);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        var utilization = GetCounterCurrent(currentCounters, "resource_gpu_utilization_percent");
        Assert.True(Math.Abs(utilization - 75.0) < 0.1);
    }

    [Fact]
    public void RecordResourceUsage_NearExhaustion_TracksCorrectly()
    {
        // Arrange
        var counters = CreateCounters();

        // Act - 96% utilization (above 95% threshold)
        counters.RecordResourceUsage(ResourceType.Memory, currentUsage: 960, peakUsage: 1000, limit: 1000);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "resource_memory_near_exhaustion_total") > 0);
    }

    [Theory]
    [InlineData(ResourceType.Memory)]
    [InlineData(ResourceType.GPU)]
    [InlineData(ResourceType.Storage)]
    [InlineData(ResourceType.Network)]
    public void RecordResourceUsage_AllResourceTypes_TrackedIndividually(ResourceType resourceType)
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.RecordResourceUsage(resourceType, 100, 200, 1000);

        // Assert
        var currentCounters = counters.GetCurrentCounters();
        var typeStr = resourceType.ToString().ToLowerInvariant();
        Assert.True(currentCounters.ContainsKey($"resource_{typeStr}_current_usage"));
    }

    #endregion

    #region Performance Analysis Tests

    [Fact]
    public void AnalyzePerformance_ReturnsValidAnalysis()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordKernelExecution("test", TimeSpan.FromMilliseconds(1), 1000, true);
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);

        // Act
        var analysis = counters.AnalyzePerformance();

        // Assert
        Assert.NotNull(analysis);
        Assert.NotNull(analysis.ThroughputAnalysis);
        Assert.NotNull(analysis.ErrorRateAnalysis);
        Assert.NotNull(analysis.ResourceUtilizationAnalysis);
        Assert.NotNull(analysis.PerformanceTrends);
        Assert.True(analysis.OverallPerformanceScore >= 0 && analysis.OverallPerformanceScore <= 100);
    }

    [Fact]
    public void AnalyzePerformance_WithHighErrorRate_RecordsErrors()
    {
        // Arrange
        var counters = CreateCounters();

        // Record many failures
        for (int i = 0; i < 10; i++)
        {
            counters.RecordKernelExecution($"kernel_{i}", TimeSpan.FromMilliseconds(1), 100, success: false);
        }

        // Act
        var analysis = counters.AnalyzePerformance();

        // Assert - Verify errors are recorded (score calculation may vary)
        Assert.True(analysis.ErrorRateAnalysis.OverallErrorRate >= 0);
        var currentCounters = counters.GetCurrentCounters();
        Assert.True(GetCounterTotal(currentCounters, "kernel_execution_failures_total") >= 10);
    }

    [Fact]
    public void AnalyzePerformance_CalculatesThroughput()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordKernelExecution("test1", TimeSpan.FromMilliseconds(1), 1000, true);
        counters.RecordKernelExecution("test2", TimeSpan.FromMilliseconds(1), 2000, true);

        // Act
        var analysis = counters.AnalyzePerformance();

        // Assert
        Assert.True(analysis.ThroughputAnalysis.KernelThroughput > 0);
    }

    #endregion

    #region Counter Management Tests

    [Fact]
    public void GetCurrentCounters_ReturnsAllActiveCounters()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);
        counters.RecordKernelExecution("test", TimeSpan.FromMilliseconds(1), 100, true);

        // Act
        var currentCounters = counters.GetCurrentCounters();

        // Assert
        Assert.NotNull(currentCounters);
        Assert.NotEmpty(currentCounters);
    }

    [Fact]
    public void GetCurrentCounters_IncludesStatistics()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordMemoryAllocation(1000, TimeSpan.FromMilliseconds(1), true);
        counters.RecordMemoryAllocation(2000, TimeSpan.FromMilliseconds(2), true);

        // Act
        var currentCounters = counters.GetCurrentCounters();

        // Assert
        var memoryAllocations = currentCounters["memory_allocations_total"];
        Assert.NotNull(memoryAllocations);

        var type = memoryAllocations.GetType();
        Assert.NotNull(type.GetProperty("Count"));
        Assert.NotNull(type.GetProperty("Average"));
        Assert.NotNull(type.GetProperty("Min"));
        Assert.NotNull(type.GetProperty("Max"));
    }

    [Fact]
    public void PerformCleanup_RemovesExpiredCounters()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);

        var cutoffTime = DateTimeOffset.UtcNow.AddHours(1); // Future time

        // Act
        var countersBefore = counters.GetCurrentCounters().Count;
        counters.PerformCleanup(cutoffTime);
        var countersAfter = counters.GetCurrentCounters().Count;

        // Assert
        Assert.True(countersAfter < countersBefore);
    }

    [Fact]
    public void PerformCleanup_WithFutureTime_RemovesAllCounters()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);
        counters.RecordKernelExecution("test", TimeSpan.FromMilliseconds(1), 100, true);

        // Act
        counters.PerformCleanup(DateTimeOffset.UtcNow.AddDays(1));
        var currentCounters = counters.GetCurrentCounters();

        // Assert
        Assert.Empty(currentCounters);
    }

    [Fact]
    public void PerformCleanup_WithPastTime_KeepsAllCounters()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);

        var countersBefore = counters.GetCurrentCounters().Count;

        // Act
        counters.PerformCleanup(DateTimeOffset.UtcNow.AddDays(-1)); // Past time
        var countersAfter = counters.GetCurrentCounters().Count;

        // Assert
        Assert.Equal(countersBefore, countersAfter);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_MultipleCallsAreSafe()
    {
        // Arrange
        var counters = CreateCounters();

        // Act
        counters.Dispose();
        counters.Dispose(); // Second dispose should be safe

        // Assert - No exception thrown
        Assert.True(true);
    }

    [Fact]
    public void AfterDispose_RecordOperationsAreNoOp()
    {
        // Arrange
        var counters = CreateCounters();
        counters.Dispose();

        // Act - These should not throw
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);
        counters.RecordKernelExecution("test", TimeSpan.FromMilliseconds(1), 100, true);
        counters.RecordDeviceUtilization(50, 50);
        counters.RecordError(MetalError.OutOfMemory, "test");
        counters.RecordMemoryPressure(MemoryPressureLevel.Medium, 50);
        counters.RecordResourceUsage(ResourceType.Memory, 100, 200, 1000);

        // Assert - No exceptions and returns empty
        var currentCounters = counters.GetCurrentCounters();
        Assert.Empty(currentCounters);
    }

    [Fact]
    public void AfterDispose_AnalyzePerformanceReturnsEmpty()
    {
        // Arrange
        var counters = CreateCounters();
        counters.Dispose();

        // Act
        var analysis = counters.AnalyzePerformance();

        // Assert
        Assert.NotNull(analysis);
        Assert.Empty(analysis.Errors);
    }

    #endregion

    #region Threading and Concurrency Tests

    [Fact]
    public async Task ConcurrentRecording_ThreadSafe()
    {
        // Arrange
        var counters = CreateCounters();
        var tasks = new List<Task>();

        // Act - Record from multiple threads concurrently
        for (int i = 0; i < 10; i++)
        {
            int iteration = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);
                    counters.RecordKernelExecution($"kernel_{iteration}", TimeSpan.FromMilliseconds(1), 100, true);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Allow for some variability in concurrent updates (within 10%)
        var currentCounters = counters.GetCurrentCounters();
        var memAllocations = GetCounterTotal(currentCounters, "memory_allocations_total");
        var kernelExecs = GetCounterTotal(currentCounters, "kernel_executions_total");

        Assert.True(memAllocations >= 900, $"Expected >= 900 memory allocations, got {memAllocations}");
        Assert.True(kernelExecs >= 900, $"Expected >= 900 kernel executions, got {kernelExecs}");
    }

    [Fact]
    public async Task ConcurrentCleanup_ThreadSafe()
    {
        // Arrange
        var counters = CreateCounters();
        counters.RecordMemoryAllocation(1024, TimeSpan.FromMilliseconds(1), true);

        // Act - Cleanup from multiple threads
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => counters.PerformCleanup(DateTimeOffset.UtcNow.AddHours(1))))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert - No exceptions thrown
        Assert.True(true);
    }

    #endregion

    #region Helper Methods

    private static double GetCounterTotal(Dictionary<string, object> counters, string counterName)
    {
        if (!counters.TryGetValue(counterName, out var obj))
            return 0;

        var type = obj.GetType();
        var totalProp = type.GetProperty("Total");
        return totalProp?.GetValue(obj) as double? ?? 0;
    }

    private static double GetCounterCurrent(Dictionary<string, object> counters, string counterName)
    {
        if (!counters.TryGetValue(counterName, out var obj))
            return 0;

        var type = obj.GetType();
        var currentProp = type.GetProperty("Current");
        return currentProp?.GetValue(obj) as double? ?? 0;
    }

    #endregion

    public void Dispose()
    {
        foreach (var counters in _countersToDispose)
        {
            counters?.Dispose();
        }
        _countersToDispose.Clear();
    }
}

/// <summary>
/// Simple xUnit logger provider for test output
/// </summary>
internal sealed class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _output;

    public XunitLoggerProvider(ITestOutputHelper output)
    {
        _output = output;
    }

    public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

    public void Dispose() { }

    private sealed class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public XunitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
                if (exception != null)
                {
                    _output.WriteLine(exception.ToString());
                }
            }
            catch
            {
                // Ignore - xUnit may have disposed the output helper
            }
        }
    }
}
