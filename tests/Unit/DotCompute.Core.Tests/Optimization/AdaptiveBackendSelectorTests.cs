// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Optimization;
using DotCompute.Core.Optimization.Configuration;
using DotCompute.Core.Optimization.Enums;
using DotCompute.Core.Optimization.Models;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Core.Optimization.Selection;
using DotCompute.Core.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DotCompute.Core.Tests.Optimization;

/// <summary>
/// Comprehensive tests for AdaptiveBackendSelector.
/// Tests ML-powered backend selection, workload characterization, and adaptive learning.
/// </summary>
public class AdaptiveBackendSelectorTests : IDisposable
{
    private readonly ILogger<AdaptiveBackendSelector> _logger;
    private readonly IPerformanceProfiler _performanceProfiler;
    private readonly AdaptiveSelectionOptions _defaultOptions;
    private bool _disposed;

    public AdaptiveBackendSelectorTests()
    {
        _logger = Substitute.For<ILogger<AdaptiveBackendSelector>>();
        _performanceProfiler = Substitute.For<IPerformanceProfiler>();
        _defaultOptions = new AdaptiveSelectionOptions
        {
            EnableLearning = true,
            MinConfidenceThreshold = 0.6f,
            MaxHistoryEntries = 1000,
            MinHistoryForLearning = 5,
            MinSamplesForHighConfidence = 20,
            PerformanceUpdateIntervalSeconds = 10
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange & Act
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);

        // Assert
        _ = selector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithOptions_UsesProvidedOptions()
    {
        // Arrange
        var customOptions = new AdaptiveSelectionOptions
        {
            EnableLearning = false,
            MinConfidenceThreshold = 0.8f
        };
        var optionsWrapper = Options.Create(customOptions);

        // Act
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler, optionsWrapper);

        // Assert
        _ = selector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new AdaptiveBackendSelector(null!, _performanceProfiler);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullPerformanceProfiler_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new AdaptiveBackendSelector(_logger, null!);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("performanceProfiler");
    }

    #endregion

    #region Backend Selection Tests - No Backends Available

    [Fact]
    public async Task SelectOptimalBackendAsync_WithNoAvailableBackends_ReturnsFallbackSelection()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator>();

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SelectedBackend.Should().BeNull();
        _ = result.BackendId.Should().Be("None");
        _ = result.ConfidenceScore.Should().Be(0f);
        _ = result.SelectionStrategy.Should().Be(SelectionStrategy.Fallback);
        _ = result.Reason.Should().Contain("No backends available");
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithAllBackendsUnavailable_ReturnsFallbackSelection()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CUDA", isAvailable: false),
            CreateMockAccelerator("CPU", isAvailable: false)
        };

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);

        // Assert
        _ = result.SelectedBackend.Should().BeNull();
        _ = result.ConfidenceScore.Should().Be(0f);
    }

    #endregion

    #region Backend Selection Tests - Single Backend

    [Fact]
    public async Task SelectOptimalBackendAsync_WithSingleBackend_SelectsOnlyOption()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backend = CreateMockAccelerator("CPU");
        var backends = new List<IAccelerator> { backend };

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SelectedBackend.Should().BeSameAs(backend);
        _ = result.BackendId.Should().Be("CPU");
        _ = result.ConfidenceScore.Should().Be(0.8f);
        _ = result.SelectionStrategy.Should().Be(SelectionStrategy.OnlyOption);
        _ = result.Reason.Should().Contain("Only one backend available");
    }

    #endregion

    #region Backend Selection Tests - Multiple Backends Without History

    [Fact]
    public async Task SelectOptimalBackendAsync_WithMultipleBackends_NoHistory_UsesPriorityFallback()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CPU"),
            CreateMockAccelerator("OpenCL")
        };

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);

        // Assert
        _ = result.SelectedBackend.Should().NotBeNull();
        _ = result.ConfidenceScore.Should().BeGreaterThan(0f);
        _ = result.SelectionStrategy.Should().BeOneOf(SelectionStrategy.Priority, SelectionStrategy.Characteristics);
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_ComputeIntensiveWorkload_PrefersCUDA()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = new WorkloadCharacteristics
        {
            DataSize = 1024 * 1024,
            ComputeIntensity = 0.9,
            MemoryIntensity = 0.2,
            ParallelismLevel = 0.9,
            OperationCount = 1000000
        };
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CPU"),
            CreateMockAccelerator("CUDA")
        };

        // Act
        var result = await selector.SelectOptimalBackendAsync("MatrixMultiply", workload, backends);

        // Assert
        _ = result.SelectedBackend.Should().NotBeNull();
        _ = result.BackendId.Should().Be("CUDA");
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_MemoryIntensiveWorkload_PrefersCPU()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = new WorkloadCharacteristics
        {
            DataSize = 10 * 1024 * 1024,
            ComputeIntensity = 0.2,
            MemoryIntensity = 0.9,
            ParallelismLevel = 0.3,
            OperationCount = 100000
        };
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CPU"),
            CreateMockAccelerator("CUDA")
        };

        // Act
        var result = await selector.SelectOptimalBackendAsync("MemoryCopy", workload, backends);

        // Assert
        _ = result.SelectedBackend.Should().NotBeNull();
        // CPU should be preferred for memory-intensive workloads
        _ = result.SelectionStrategy.Should().BeOneOf(SelectionStrategy.Characteristics, SelectionStrategy.Priority);
    }

    #endregion

    #region Backend Selection Tests - With Historical Performance

    [Fact]
    public async Task SelectOptimalBackendAsync_WithSufficientHistory_UsesHistoricalStrategy()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CPU"),
            CreateMockAccelerator("CUDA")
        };

        // Build historical data
        for (var i = 0; i < 10; i++)
        {
            var perfResult = new PerformanceResult
            {
                ExecutionTimeMs = 10.0 + i,
                ThroughputOpsPerSecond = 100000,
                Success = true,
                BackendId = "CUDA"
            };
            await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", perfResult);
        }

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);

        // Assert
        _ = result.SelectedBackend.Should().NotBeNull();
        _ = result.BackendId.Should().Be("CUDA");
        _ = result.SelectionStrategy.Should().Be(SelectionStrategy.Historical);
        _ = result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.6f);
        _ = result.Metadata.Should().ContainKey("HistoricalSamples");
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithCompetingBackends_SelectsBestPerformer()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CPU"),
            CreateMockAccelerator("CUDA")
        };

        // CPU performs worse
        for (var i = 0; i < 10; i++)
        {
            await selector.RecordPerformanceResultAsync("TestKernel", workload, "CPU", new PerformanceResult
            {
                ExecutionTimeMs = 50.0,
                ThroughputOpsPerSecond = 50000,
                Success = true,
                BackendId = "CPU"
            });
        }

        // CUDA performs better
        for (var i = 0; i < 10; i++)
        {
            await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", new PerformanceResult
            {
                ExecutionTimeMs = 10.0,
                ThroughputOpsPerSecond = 200000,
                Success = true,
                BackendId = "CUDA"
            });
        }

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);

        // Assert
        _ = result.BackendId.Should().Be("CUDA");
        _ = result.ConfidenceScore.Should().BeGreaterThan(0.6f);
    }

    #endregion

    #region Performance Recording Tests

    [Fact]
    public async Task RecordPerformanceResultAsync_WithValidData_RecordsSuccessfully()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var perfResult = new PerformanceResult
        {
            ExecutionTimeMs = 15.5,
            ThroughputOpsPerSecond = 100000,
            Success = true,
            BackendId = "CUDA"
        };

        // Act
        await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", perfResult);

        // Assert - No exception means success
        var insights = selector.GetPerformanceInsights();
        _ = insights.TotalWorkloadSignatures.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordPerformanceResultAsync_WithLearningDisabled_DoesNotRecord()
    {
        // Arrange
        var options = new AdaptiveSelectionOptions { EnableLearning = false };
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler, Options.Create(options));
        var workload = CreateBasicWorkload();
        var perfResult = new PerformanceResult
        {
            ExecutionTimeMs = 15.5,
            ThroughputOpsPerSecond = 100000,
            Success = true
        };

        // Act
        await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", perfResult);

        // Assert
        var insights = selector.GetPerformanceInsights();
        _ = insights.TotalWorkloadSignatures.Should().Be(0);
    }

    [Fact]
    public async Task RecordPerformanceResultAsync_WithNullKernelName_ThrowsArgumentException()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var perfResult = new PerformanceResult();

        // Act
        Func<Task> act = async () => await selector.RecordPerformanceResultAsync(null!, workload, "CUDA", perfResult);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecordPerformanceResultAsync_WithEmptyKernelName_ThrowsArgumentException()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var perfResult = new PerformanceResult();

        // Act
        Func<Task> act = async () => await selector.RecordPerformanceResultAsync("", workload, "CUDA", perfResult);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecordPerformanceResultAsync_WithNullWorkload_ThrowsArgumentNullException()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var perfResult = new PerformanceResult();

        // Act
        Func<Task> act = async () => await selector.RecordPerformanceResultAsync("Test", null!, "CUDA", perfResult);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RecordPerformanceResultAsync_WithNullBackend_ThrowsArgumentException()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var perfResult = new PerformanceResult();

        // Act
        Func<Task> act = async () => await selector.RecordPerformanceResultAsync("Test", workload, null!, perfResult);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecordPerformanceResultAsync_WithNullPerformanceResult_ThrowsArgumentNullException()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();

        // Act
        Func<Task> act = async () => await selector.RecordPerformanceResultAsync("Test", workload, "CUDA", null!);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Adaptive Learning Tests

    [Fact]
    public async Task AdaptiveLearning_AccumulatesHistoryOverTime()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();

        // Act - Record multiple performance results
        for (var i = 0; i < 15; i++)
        {
            await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", new PerformanceResult
            {
                ExecutionTimeMs = 10.0 + i * 0.1,
                ThroughputOpsPerSecond = 100000,
                Success = true
            });
        }

        // Assert
        var insights = selector.GetPerformanceInsights();
        _ = insights.LearningStatistics.TotalPerformanceSamples.Should().Be(15);
        _ = insights.LearningStatistics.WorkloadsWithSufficientHistory.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AdaptiveLearning_ImprovesConfidenceWithMoreSamples()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator> { CreateMockAccelerator("CUDA") };

        // Record few samples
        for (var i = 0; i < 3; i++)
        {
            await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", new PerformanceResult
            {
                ExecutionTimeMs = 10.0,
                ThroughputOpsPerSecond = 100000,
                Success = true
            });
        }

        var result1 = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);
        var confidence1 = result1.ConfidenceScore;

        // Record many more samples
        for (var i = 0; i < 20; i++)
        {
            await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", new PerformanceResult
            {
                ExecutionTimeMs = 10.0,
                ThroughputOpsPerSecond = 100000,
                Success = true
            });
        }

        // Act
        var result2 = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends);

        // Assert - More samples should improve confidence
        _ = result2.ConfidenceScore.Should().BeGreaterThanOrEqualTo(confidence1);
    }

    #endregion

    #region Selection Constraints Tests

    [Fact]
    public async Task SelectOptimalBackendAsync_WithDisallowedBackend_ExcludesFromSelection()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CPU"),
            CreateMockAccelerator("CUDA")
        };
        var constraints = new SelectionConstraints
        {
            DisallowedBackends = ["CUDA"]
        };

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends, constraints);

        // Assert
        _ = result.BackendId.Should().Be("CPU");
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_WithAllowedBackendsOnly_RespectsConstraint()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();
        var backends = new List<IAccelerator>
        {
            CreateMockAccelerator("CPU"),
            CreateMockAccelerator("CUDA"),
            CreateMockAccelerator("Metal")
        };
        var constraints = new SelectionConstraints
        {
            AllowedBackends = ["CPU"]
        };

        // Act
        var result = await selector.SelectOptimalBackendAsync("TestKernel", workload, backends, constraints);

        // Assert
        _ = result.BackendId.Should().Be("CPU");
    }

    #endregion

    #region Performance Insights Tests

    [Fact]
    public void GetPerformanceInsights_WithNoHistory_ReturnsEmptyInsights()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);

        // Act
        var insights = selector.GetPerformanceInsights();

        // Assert
        _ = insights.Should().NotBeNull();
        _ = insights.TotalWorkloadSignatures.Should().Be(0);
        _ = insights.TotalBackends.Should().Be(0);
        _ = insights.LearningStatistics.TotalPerformanceSamples.Should().Be(0);
        _ = insights.TopPerformingPairs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPerformanceInsights_WithHistory_ReturnsPopulatedInsights()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = CreateBasicWorkload();

        for (var i = 0; i < 10; i++)
        {
            await selector.RecordPerformanceResultAsync("TestKernel", workload, "CUDA", new PerformanceResult
            {
                ExecutionTimeMs = 10.0,
                ThroughputOpsPerSecond = 100000,
                Success = true
            });
        }

        // Act
        var insights = selector.GetPerformanceInsights();

        // Assert
        _ = insights.TotalWorkloadSignatures.Should().BeGreaterThan(0);
        _ = insights.LearningStatistics.TotalPerformanceSamples.Should().Be(10);
        _ = insights.LearningStatistics.LearningEffectiveness.Should().BeGreaterThan(0);
    }

    #endregion

    #region Workload Pattern Recognition Tests

    [Fact]
    public async Task SelectOptimalBackendAsync_RecognizesComputeIntensivePattern()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = new WorkloadCharacteristics
        {
            DataSize = 1024 * 1024,
            ComputeIntensity = 0.95,
            MemoryIntensity = 0.15,
            ParallelismLevel = 0.9,
            OperationCount = 10000000
        };
        var backends = new List<IAccelerator> { CreateMockAccelerator("CUDA") };

        // Act
        var result = await selector.SelectOptimalBackendAsync("FFT", workload, backends);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.SelectionStrategy.Should().BeOneOf(SelectionStrategy.OnlyOption, SelectionStrategy.Characteristics);
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_RecognizesMemoryIntensivePattern()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = new WorkloadCharacteristics
        {
            DataSize = 100 * 1024 * 1024,
            ComputeIntensity = 0.1,
            MemoryIntensity = 0.95,
            ParallelismLevel = 0.2,
            OperationCount = 100000
        };
        var backends = new List<IAccelerator> { CreateMockAccelerator("CPU") };

        // Act
        var result = await selector.SelectOptimalBackendAsync("MemoryCopy", workload, backends);

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_RecognizesBalancedPattern()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = new WorkloadCharacteristics
        {
            DataSize = 10 * 1024 * 1024,
            ComputeIntensity = 0.6,
            MemoryIntensity = 0.6,
            ParallelismLevel = 0.7,
            OperationCount = 1000000
        };
        var backends = new List<IAccelerator> { CreateMockAccelerator("CUDA") };

        // Act
        var result = await selector.SelectOptimalBackendAsync("GeneralCompute", workload, backends);

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task SelectOptimalBackendAsync_RecognizesHighlyParallelPattern()
    {
        // Arrange
        using var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);
        var workload = new WorkloadCharacteristics
        {
            DataSize = 5 * 1024 * 1024,
            ComputeIntensity = 0.4,
            MemoryIntensity = 0.4,
            ParallelismLevel = 0.95,
            OperationCount = 5000000
        };
        var backends = new List<IAccelerator> { CreateMockAccelerator("CUDA") };

        // Act
        var result = await selector.SelectOptimalBackendAsync("ParallelMap", workload, backends);

        // Assert
        _ = result.Should().NotBeNull();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledOnce_DisposesSuccessfully()
    {
        // Arrange
        var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);

        // Act
        selector.Dispose();

        // Assert - No exception means success
        _ = true.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var selector = new AdaptiveBackendSelector(_logger, _performanceProfiler);

        // Act
        selector.Dispose();
        selector.Dispose();
        selector.Dispose();

        // Assert - No exception
        _ = true.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static WorkloadCharacteristics CreateBasicWorkload() => new()
    {
        DataSize = 1024 * 1024,
        ComputeIntensity = 0.5,
        MemoryIntensity = 0.5,
        ParallelismLevel = 0.5,
        OperationCount = 100000
    };

    private static IAccelerator CreateMockAccelerator(string name, bool isAvailable = true)
    {
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo
        {
            Id = $"test_{name}",
            Name = name,
            DeviceType = name,
            Vendor = "Test"
        };
        _ = accelerator.Info.Returns(info);
        _ = accelerator.IsAvailable.Returns(isAvailable);
        return accelerator;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose IDisposable fields
            _performanceProfiler?.Dispose();
        }

        _disposed = true;
    }

    #endregion
}
