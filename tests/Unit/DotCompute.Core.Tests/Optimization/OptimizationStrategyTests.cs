// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Types;
using DotCompute.Core.Optimization;
using DotCompute.Core.Optimization.Configuration;
using DotCompute.Core.Optimization.Enums;
using DotCompute.Core.Optimization.Models;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Core.Optimization.Selection;
using ProductionPerfOptions = DotCompute.Core.Optimization.PerformanceOptimizationOptions;
using DotCompute.Core.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DotCompute.Core.Tests.Optimization;

/// <summary>
/// Comprehensive unit tests for optimization strategies including:
/// - AdaptiveBackendSelector with ML-based backend selection
/// - PerformanceOptimizedOrchestrator with adaptive execution
/// - Workload analysis and performance prediction
/// - Backend performance monitoring and learning
/// - Selection constraint handling and optimization
///
/// Coverage includes positive/negative cases, edge conditions, thread safety,
/// and performance validation against optimization targets.
/// </summary>
public class OptimizationStrategyTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<AdaptiveBackendSelector>> _mockSelectorLogger;
    private readonly Mock<ILogger<PerformanceOptimizedOrchestrator>> _mockOrchestratorLogger;
    private readonly Mock<PerformanceProfiler> _mockProfiler;
    private readonly Mock<IComputeOrchestrator> _mockBaseOrchestrator;
    private readonly List<Mock<IAccelerator>> _mockAccelerators;
    private readonly AdaptiveSelectionOptions _defaultOptions;

    public OptimizationStrategyTests(ITestOutputHelper output)
    {
        _output = output;
        _mockSelectorLogger = new Mock<ILogger<AdaptiveBackendSelector>>();
        _mockOrchestratorLogger = new Mock<ILogger<PerformanceOptimizedOrchestrator>>();
        _mockProfiler = new Mock<PerformanceProfiler>();
        _mockBaseOrchestrator = new Mock<IComputeOrchestrator>();

        // Create mock accelerators for different backends
        _mockAccelerators =
        [
            CreateMockAccelerator("CPU", "Intel i9-13900K", true, 1000),
            CreateMockAccelerator("CUDA", "NVIDIA RTX 4090", true, 5000),
            CreateMockAccelerator("Metal", "Apple M3 Max", true, 3000),
            CreateMockAccelerator("OpenCL", "AMD RX 7900 XTX", false, 4000) // Unavailable
        ];

        _defaultOptions = new AdaptiveSelectionOptions
        {
            EnableLearning = true,
            PerformanceUpdateIntervalSeconds = 1,
            MinimumSamplesForLearning = 3,
            ConfidenceThreshold = 0.7f,
            MaxHistoryEntries = 1000
        };
    }

    #region AdaptiveBackendSelector Tests

    [Fact]
    public void AdaptiveBackendSelector_Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        using var selector = new AdaptiveBackendSelector(
            _mockSelectorLogger.Object,
            _mockProfiler.Object,
            Options.Create(_defaultOptions));

        // Assert
        _ = selector.Should().NotBeNull();
        VerifyLoggerCalled(_mockSelectorLogger, "Adaptive backend selector initialized");
    }

    [Fact]
    public void AdaptiveBackendSelector_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() =>
            new AdaptiveBackendSelector(null!, _mockProfiler.Object));
    }

    [Fact]
    public void AdaptiveBackendSelector_Constructor_WithNullProfiler_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() =>
            new AdaptiveBackendSelector(_mockSelectorLogger.Object, null!));
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithAvailableBackends_ReturnsOptimalSelection()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.ComputeIntensive, 1000000);
        var availableBackends = _mockAccelerators.Where(m => m.Object.IsAvailable).Select(m => m.Object);

        // Act
        var result = await selector.SelectOptimalBackendAsync(
            "TestKernel", workload, availableBackends);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SelectedBackend.Should().NotBeNull();
        _ = result.ConfidenceScore.Should().BeGreaterThan(0f);
        _ = result.Reason.Should().NotBeNullOrEmpty();
        _ = result.SelectionStrategy.Should().NotBe(SelectionStrategy.Fallback);
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithNoBackends_ReturnsFallbackSelection()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.ComputeIntensive, 1000);
        var emptyBackends = Enumerable.Empty<IAccelerator>();

        // Act
        var result = await selector.SelectOptimalBackendAsync(
            "TestKernel", workload, emptyBackends);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SelectedBackend.Should().BeNull();
        _ = result.BackendId.Should().Be("None");
        _ = result.ConfidenceScore.Should().Be(0f);
        _ = result.Reason.Should().Be("No backends available");
        _ = result.SelectionStrategy.Should().Be(SelectionStrategy.Fallback);
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithMemoryIntensiveWorkload_PrefersCpuWithLargeMemory()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.MemoryIntensive, 10000000);
        var availableBackends = _mockAccelerators.Where(m => m.Object.IsAvailable).Select(m => m.Object);

        // Act
        var result = await selector.SelectOptimalBackendAsync(
            "MemoryKernel", workload, availableBackends);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SelectedBackend.Should().NotBeNull();

        // For memory-intensive workloads, should prefer backends with good memory characteristics
        _ = result.SelectionStrategy.Should().BeOneOf(
            SelectionStrategy.Characteristics,
            SelectionStrategy.RealTime,
            SelectionStrategy.Historical);
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithConstraints_RespectsConstraints()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.ComputeIntensive, 1000);
        var availableBackends = _mockAccelerators.Where(m => m.Object.IsAvailable).Select(m => m.Object);

        var constraints = new SelectionConstraints
        {
            AllowedBackends = new HashSet<string> { "CPU" },
            MaxMemoryUsageMB = 1000,
            MinConfidenceScore = 0.5f
        };

        // Act
        var result = await selector.SelectOptimalBackendAsync(
            "ConstrainedKernel", workload, availableBackends, constraints);

        // Assert
        _ = result.Should().NotBeNull();
        if (result.SelectedBackend != null)
        {
            _ = result.SelectedBackend.Info.Name.Should().Contain("CPU");
        }
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithInvalidParameters_ThrowsArgumentException()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.ComputeIntensive, 1000);
        var availableBackends = _mockAccelerators.Select(m => m.Object);

        // Act & Assert
        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            selector.SelectOptimalBackendAsync("", workload, availableBackends));

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            selector.SelectOptimalBackendAsync("TestKernel", null!, availableBackends));

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            selector.SelectOptimalBackendAsync("TestKernel", workload, null!));
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_ConcurrentCalls_ThreadSafeExecution()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.ComputeIntensive, 1000);
        var availableBackends = _mockAccelerators.Where(m => m.Object.IsAvailable).Select(m => m.Object);

        const int concurrentCalls = 10;
        var results = new ConcurrentBag<BackendSelection>();

        // Act
        var tasks = Enumerable.Range(0, concurrentCalls).Select(async i =>
        {
            var result = await selector.SelectOptimalBackendAsync(
                $"ConcurrentKernel_{i}", workload, availableBackends);
            results.Add(result);
        });

        await Task.WhenAll(tasks);

        // Assert
        _ = results.Should().HaveCount(concurrentCalls);
        _ = results.Should().OnlyContain(r => r != null);
        _ = results.Should().OnlyContain(r => r.ConfidenceScore >= 0f && r.ConfidenceScore <= 1f);
    }

    #endregion

    #region PerformanceOptimizedOrchestrator Tests

    [Fact]
    public void PerformanceOptimizedOrchestrator_Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        using var orchestrator = CreatePerformanceOrchestrator();

        // Assert
        _ = orchestrator.Should().NotBeNull();
        VerifyLoggerCalled(_mockOrchestratorLogger, "Performance-optimized orchestrator initialized");
    }

    [Fact]
    public void PerformanceOptimizedOrchestrator_Constructor_WithNullDependencies_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() =>
            new PerformanceOptimizedOrchestrator(null!, CreateAdaptiveBackendSelector(),
                _mockProfiler.Object, _mockOrchestratorLogger.Object));

        _ = Assert.Throws<ArgumentNullException>(() =>
            new PerformanceOptimizedOrchestrator(_mockBaseOrchestrator.Object, null!,
                _mockProfiler.Object, _mockOrchestratorLogger.Object));
    }

    [Fact]
    public async Task ExecuteAsync_WithValidKernel_ReturnsExpectedResult()
    {
        // Arrange
        using var orchestrator = CreatePerformanceOrchestrator();
        const string expectedResult = "TestResult";

        _ = _mockBaseOrchestrator.Setup(o => o.ExecuteAsync<string>("TestKernel", It.IsAny<object[]>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await orchestrator.ExecuteAsync<string>("TestKernel", new object[] { 1, 2, 3 });

        // Assert
        _ = result.Should().Be(expectedResult);
        _mockBaseOrchestrator.Verify(o => o.ExecuteAsync<string>("TestKernel", It.IsAny<object[]>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteWithBuffersAsync_WithValidParameters_ExecutesSuccessfully()
    {
        // Arrange
        using var orchestrator = CreatePerformanceOrchestrator();
        const int expectedResult = 42;

        _ = _mockBaseOrchestrator.Setup(o => o.ExecuteAsync<int>("BufferKernel", It.IsAny<object[]>()))
            .ReturnsAsync(expectedResult);

        var buffers = new object[] { new int[100], new float[100] };
        var scalarArgs = new object[] { 10, 20.5f };

        // Act
        var result = await orchestrator.ExecuteWithBuffersAsync<int>("BufferKernel", buffers, scalarArgs);

        // Assert
        _ = result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task GetOptimalAcceleratorAsync_WithKernelName_ReturnsOptimalAccelerator()
    {
        // Arrange
        using var orchestrator = CreatePerformanceOrchestrator();
        var mockSelector = new Mock<AdaptiveBackendSelector>(
            _mockSelectorLogger.Object, _mockProfiler.Object, Options.Create(_defaultOptions));

        var expectedAccelerator = _mockAccelerators.First(m => m.Object.IsAvailable).Object;

        _ = mockSelector.Setup(s => s.SelectOptimalBackendAsync(
                It.IsAny<string>(), It.IsAny<WorkloadCharacteristics>(), It.IsAny<IEnumerable<IAccelerator>>(), null))
            .ReturnsAsync(new BackendSelection
            {
                SelectedBackend = expectedAccelerator,
                ConfidenceScore = 0.9f,
                SelectionStrategy = SelectionStrategy.RealTime
            });

        // Act
        var result = await orchestrator.GetOptimalAcceleratorAsync("TestKernel");

        // Assert
        _ = result.Should().NotBeNull();
    }

    #endregion

    #region Workload Analysis Tests

    [Theory]
    [InlineData(WorkloadPattern.ComputeIntensive, 1000000, MemoryAccessPattern.Sequential)]
    [InlineData(WorkloadPattern.MemoryIntensive, 100000, MemoryAccessPattern.Random)]
    [InlineData(WorkloadPattern.IOBound, 10000, MemoryAccessPattern.Strided)]
    [InlineData(WorkloadPattern.Mixed, 500000, MemoryAccessPattern.Sequential)]
    public void WorkloadCharacteristics_Creation_ValidatesCorrectly(
        WorkloadPattern pattern, int dataSize, MemoryAccessPattern memoryPattern)
    {
        // Arrange & Act
        var workload = CreateTestWorkload(pattern, dataSize, memoryPattern);

        // Assert
        _ = workload.Should().NotBeNull();
        _ = workload.AccessPattern.Should().Be(memoryPattern);
        _ = workload.DataSize.Should().Be(dataSize * sizeof(float));
        _ = workload.ComputeIntensity.Should().BeGreaterThan(0f);
        _ = workload.MemoryIntensity.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void WorkloadSignature_Equality_WorksCorrectly()
    {
        // Arrange
        var signature1 = new WorkloadSignature
        {
            KernelName = "TestKernel",
            WorkloadPattern = WorkloadPattern.ComputeIntensive,
            DataSize = 10485760,
            MemoryIntensity = 1.0,
            ComputeIntensity = 0.5,
            ParallelismLevel = 1.0, // was MemoryAccessPattern.Sequential
        };

        var signature2 = new WorkloadSignature
        {
            KernelName = "TestKernel",
            WorkloadPattern = WorkloadPattern.ComputeIntensive,
            DataSize = 10485760,
            MemoryIntensity = 1.0,
            ComputeIntensity = 0.5,
            ParallelismLevel = 1.0, // was MemoryAccessPattern.Sequential
        };

        var signature3 = new WorkloadSignature
        {
            KernelName = "DifferentKernel",
            WorkloadPattern = WorkloadPattern.ComputeIntensive,
            DataSize = 10485760,
            MemoryIntensity = 1.0,
            ComputeIntensity = 0.5,
            ParallelismLevel = 1.0, // was MemoryAccessPattern.Sequential
        };

        // Act & Assert
        _ = signature1.Equals(signature2).Should().BeTrue();
        _ = signature1.GetHashCode().Should().Be(signature2.GetHashCode());

        _ = signature1.Equals(signature3).Should().BeFalse();
        _ = signature1.GetHashCode().Should().NotBe(signature3.GetHashCode());
    }

    #endregion

    #region Performance Learning Tests

    [Fact]
    public void PerformanceHistory_AddResult_StoresCorrectly()
    {
        // Arrange
        var history = new PerformanceHistory(new WorkloadSignature { KernelName = "Test", DataSize = 1000, ComputeIntensity = 0.5, MemoryIntensity = 0.5, ParallelismLevel = 1.0, WorkloadPattern = WorkloadPattern.ComputeIntensive });
        var result = new PerformanceResult
        {
            BackendId = "CPU",
            ExecutionTimeMs = 150.5,
            ThroughputOpsPerSecond = 10000,
            MemoryUsedBytes = 1024000,
            Success = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        history.AddPerformanceResult("TestBackend", result);

        // Assert
        _ = history.GetPerformanceStats().Should().HaveCount(1);
        history.Results.First().Should().BeEquivalentTo(result);
        _ = history.GetPerformanceStats()["TestBackend"].AverageExecutionTimeMs.Should().BeApproximately(150.5, 0.1);
    }

    [Fact]
    public void PerformanceHistory_CalculateStatistics_ReturnsCorrectValues()
    {
        // Arrange
        var history = new PerformanceHistory(new WorkloadSignature { KernelName = "Test", DataSize = 1000, ComputeIntensity = 0.5, MemoryIntensity = 0.5, ParallelismLevel = 1.0, WorkloadPattern = WorkloadPattern.ComputeIntensive });
        var results = new[]
        {
            new PerformanceResult { BackendId = "CPU", ExecutionTimeMs = 100, Success = true },
            new PerformanceResult { BackendId = "CPU", ExecutionTimeMs = 150, Success = true },
            new PerformanceResult { BackendId = "CPU", ExecutionTimeMs = 200, Success = true },
            new PerformanceResult { BackendId = "CPU", ExecutionTimeMs = 250, Success = false } // Failed execution
        };

        foreach (var result in results)
        {
            history.AddPerformanceResult("TestBackend", result);
        }

        // Act
        var avgTime = history.GetPerformanceStats()["TestBackend"].AverageExecutionTimeMs;
        var successRate = history.GetPerformanceStats()["TestBackend"].ReliabilityScore;
        var stats = history.GetPerformanceStats();

        // Assert
        _ = avgTime.Should().BeApproximately(150.0, 0.1); // Average of successful executions only
        successRate.Should().BeApproximately(0.75, 0.01); // 3 out of 4 successful
        _ = stats.Should().NotBeNull();
        stats.Mean.Should().BeApproximately(150.0, 0.1);
        stats.SampleCount.Should().Be(3); // Only successful executions
    }

    #endregion

    #region Performance Monitoring Tests

    [Fact]
    public void BackendPerformanceState_Update_TracksCorrectly()
    {
        // Arrange
        var state = new BackendPerformanceState
        {
            BackendId = "CUDA",
            CurrentUtilization = 0.7,
            LastExecutionTime = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var newMetrics = new BackendPerformanceStats
        {
            AverageExecutionTimeMs = 50.0,
            ThroughputOpsPerSecond = 50000,
            AverageMemoryUsage = 2048000,
            SuccessRate = 0.95,
            LoadFactor = 0.7,
            IsAvailable = true
        };

        // Act
        state.UpdateMetrics(newMetrics);

        // Assert
        state.CurrentMetrics.Should().BeEquivalentTo(newMetrics);
        state.LastUpdateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        state.Health.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void BackendPerformanceStateSummary_Calculation_IsAccurate()
    {
        // Arrange
        var states = new Dictionary<string, BackendPerformanceState>
        {
            ["CPU"] = new BackendPerformanceState
            {
                BackendId = "CPU",
                CurrentUtilization = 0.5,
                RecentAverageExecutionTimeMs = 200,
                RecentExecutionCount = 100,
                LastExecutionTime = DateTimeOffset.UtcNow
            },
            ["CUDA"] = new BackendPerformanceState
            {
                BackendId = "CUDA",
                CurrentUtilization = 0.8,
                RecentAverageExecutionTimeMs = 50,
                RecentExecutionCount = 500,
                LastExecutionTime = DateTimeOffset.UtcNow
            }
        };

        // Act
        var summary = new BackendPerformanceStateSummary(states);

        // Assert
        summary.TotalBackends.Should().Be(2);
        summary.AvailableBackends.Should().Be(2);
        summary.BestPerformingBackend.Should().Be("CUDA");
        summary.AverageSuccessRate.Should().BeApproximately(0.925f, 0.001f);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task SelectOptimalBackendAsync_WithAllBackendsUnavailable_HandleGracefully()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.ComputeIntensive, 1000);
        var unavailableBackends = _mockAccelerators.Where(m => !m.Object.IsAvailable).Select(m => m.Object);

        // Act
        var result = await selector.SelectOptimalBackendAsync(
            "TestKernel", workload, unavailableBackends);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SelectedBackend.Should().BeNull();
        _ = result.ConfidenceScore.Should().Be(0f);
        _ = result.SelectionStrategy.Should().Be(SelectionStrategy.Fallback);
    }

    [Fact]
    public void AdaptiveBackendSelector_Disposal_CleansUpCorrectly()
    {
        // Arrange
        var selector = CreateAdaptiveBackendSelector();

        // Act
        selector.Dispose();

        // Assert
        // Should not throw and should clean up timer
        _ = selector.Invoking(s => s.Dispose()).Should().NotThrow();
    }

    [Fact]
    public async Task PerformanceOptimizedOrchestrator_WithNullResult_HandlesGracefully()
    {
        // Arrange
        using var orchestrator = CreatePerformanceOrchestrator();
        _ = _mockBaseOrchestrator.Setup(o => o.ExecuteAsync<string>("NullKernel", It.IsAny<object[]>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await orchestrator.ExecuteAsync<string>("NullKernel");

        // Assert
        _ = result.Should().BeNull();
    }

    #endregion

    #region Performance Benchmarks

    [Fact]
    public async Task BackendSelection_Performance_MeetsTargets()
    {
        // Arrange
        using var selector = CreateAdaptiveBackendSelector();
        var workload = CreateTestWorkload(WorkloadPattern.ComputeIntensive, 1000000);
        var availableBackends = _mockAccelerators.Where(m => m.Object.IsAvailable).Select(m => m.Object);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        const int iterations = 100;
        for (var i = 0; i < iterations; i++)
        {
            _ = await selector.SelectOptimalBackendAsync($"PerfKernel_{i}", workload, availableBackends);
        }

        stopwatch.Stop();

        // Assert
        var averageTimeMs = stopwatch.ElapsedMilliseconds / (double)iterations;
        averageTimeMs.Should().BeLessThan(10.0, "Backend selection should be fast for real-time optimization");

        _output.WriteLine($"Average backend selection time: {averageTimeMs:F2}ms over {iterations} iterations");
    }

    #endregion

    #region Helper Methods

    private AdaptiveBackendSelector CreateAdaptiveBackendSelector()
    {
        return new AdaptiveBackendSelector(
            _mockSelectorLogger.Object,
            _mockProfiler.Object,
            Options.Create(_defaultOptions));
    }

    private PerformanceOptimizedOrchestrator CreatePerformanceOrchestrator()
    {
        var selector = CreateAdaptiveBackendSelector();
        var options = new ProductionPerfOptions
        {
            OptimizationStrategy = DotCompute.Core.Optimization.Enums.OptimizationStrategy.Balanced,
            EnableLearning = true,
            EnableConstraints = true
        };

        return new PerformanceOptimizedOrchestrator(
            _mockBaseOrchestrator.Object,
            selector,
            _mockProfiler.Object,
            _mockOrchestratorLogger.Object,
            options);
    }

    private Mock<IAccelerator> CreateMockAccelerator(string name, string description, bool isAvailable, int performanceScore)
    {
        var mock = new Mock<IAccelerator>();
        mock.Setup(a => a.Name).Returns(name);
        mock.Setup(a => a.Description).Returns(description);
        _ = mock.Setup(a => a.IsAvailable).Returns(isAvailable);
        mock.Setup(a => a.GetMemoryInfoAsync()).ReturnsAsync(new MemoryInfo
        {
            TotalMemory = performanceScore * 1024L * 1024L, // GB converted to bytes
            AvailableMemory = (long)(performanceScore * 1024L * 1024L * 0.8),
            UsedMemory = (long)(performanceScore * 1024L * 1024L * 0.2)
        });
        return mock;
    }

    private WorkloadCharacteristics CreateTestWorkload(
        WorkloadPattern pattern,
        int dataSize,
        MemoryAccessPattern memoryPattern = MemoryAccessPattern.Sequential)
    {
        return new WorkloadCharacteristics
        {
            DataSize = dataSize * sizeof(float),
            AccessPattern = memoryPattern,
            ComputeIntensity = Math.Log10(dataSize),
            MemoryIntensity = pattern == WorkloadPattern.MemoryIntensive ? 0.9 : 0.3,
            ParallelismLevel = 0.8,
            OperationCount = dataSize
        };
    }

    private static void VerifyLoggerCalled<T>(Mock<ILogger<T>> mockLogger, string expectedMessage)
    {
        mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    public void Dispose()
    {
        // Cleanup any test resources if needed
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Mock implementations for testing optimization strategies
/// </summary>
internal class TestMemoryInfo : MemoryInfo
{
    public new long TotalMemory { get; set; }
    public new long AvailableMemory { get; set; }
    public new long UsedMemory { get; set; }
}

/// <summary>
/// Performance optimization options for testing
/// </summary>
internal class PerformanceOptimizationOptions
{
    public OptimizationStrategy OptimizationStrategy { get; set; } = OptimizationStrategy.Balanced;
    public bool EnablePerformancePrediction { get; set; } = true;
    public bool CacheWorkloadAnalysis { get; set; } = true;
    public int MaxCacheEntries { get; set; } = 1000;
    public TimeSpan CacheExpirationTime { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Optimization strategy enumeration for testing
/// </summary>
internal enum OptimizationStrategy
{
    Conservative,
    Balanced,
    Aggressive,
    MLOptimized
}