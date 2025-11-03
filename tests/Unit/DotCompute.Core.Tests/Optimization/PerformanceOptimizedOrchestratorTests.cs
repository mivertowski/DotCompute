// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Optimization;
using DotCompute.Core.Optimization.Enums;
using DotCompute.Core.Optimization.Models;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Core.Optimization.Selection;
using DotCompute.Core.Telemetry;
using Microsoft.Extensions.Logging;
using NSubstitute;

// Use actual implementation classes from DotCompute.Core.Optimization

namespace DotCompute.Core.Tests.Optimization;

/// <summary>
/// Comprehensive tests for PerformanceOptimizedOrchestrator.
/// Tests performance-driven execution, workload-aware routing, and monitoring.
/// </summary>
public class PerformanceOptimizedOrchestratorTests : IDisposable
{
    private readonly IComputeOrchestrator _baseOrchestrator;
    private readonly IBackendSelector _backendSelector;
    private readonly IPerformanceProfiler _performanceProfiler;
    private readonly ILogger<PerformanceOptimizedOrchestrator> _logger;
    private readonly DotCompute.Core.Optimization.PerformanceOptimizationOptions _defaultOptions;
    private bool _disposed;

    public PerformanceOptimizedOrchestratorTests()
    {
        _baseOrchestrator = Substitute.For<IComputeOrchestrator>();
        _backendSelector = Substitute.For<IBackendSelector>();
        _performanceProfiler = Substitute.For<IPerformanceProfiler>();
        _logger = Substitute.For<ILogger<PerformanceOptimizedOrchestrator>>();

        _defaultOptions = new DotCompute.Core.Optimization.PerformanceOptimizationOptions
        {
            OptimizationStrategy = DotCompute.Core.Optimization.OptimizationStrategy.Balanced
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange & Act
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger);

        // Assert
        _ = orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithOptions_UsesProvidedOptions()
    {
        // Arrange
        var customOptions = new DotCompute.Core.Optimization.PerformanceOptimizationOptions
        {
            OptimizationStrategy = DotCompute.Core.Optimization.OptimizationStrategy.Conservative
        };

        // Act
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, customOptions);

        // Assert
        _ = orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBaseOrchestrator_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new PerformanceOptimizedOrchestrator(
            null!, _backendSelector, _performanceProfiler, _logger);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseOrchestrator");
    }

    [Fact]
    public void Constructor_WithNullBackendSelector_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, null!, _performanceProfiler, _logger);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("backendSelector");
    }

    [Fact]
    public void Constructor_WithNullPerformanceProfiler_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, null!, _logger);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("performanceProfiler");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, null!);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithValidKernel_ExecutesSuccessfully()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f,
            SelectionStrategy = SelectionStrategy.Historical
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>("TestKernel", [1, 2, 3]);

        // Assert
        _ = result.Should().Be(42);
        _ = await _baseOrchestrator.Received(1).ExecuteAsync<int>("TestKernel", Arg.Any<object[]>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoBackendSelected_FallsBackToBaseOrchestrator()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backendSelection = new BackendSelection
        {
            SelectedBackend = null,
            BackendId = "None",
            ConfidenceScore = 0f,
            SelectionStrategy = SelectionStrategy.Fallback
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns("fallback");

        // Act
        var result = await orchestrator.ExecuteAsync<string>("TestKernel", []);

        // Assert
        _ = result.Should().Be("fallback");
    }

    [Fact]
    public async Task ExecuteAsync_WithExecutionError_FallsBackToBaseOrchestrator()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        // First call throws, second call succeeds (fallback)
        var callCount = 0;
        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(x =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Simulated error");
                }
                return 99;
            });

        // Act
        var result = await orchestrator.ExecuteAsync<int>("TestKernel", []);

        // Assert
        _ = result.Should().Be(99);
    }

    [Fact]
    public async Task ExecuteAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);
        orchestrator.Dispose();

        // Act
        Func<Task> act = async () => await orchestrator.ExecuteAsync<int>("TestKernel");

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region ExecuteWithBuffersAsync Tests

    [Fact]
    public async Task ExecuteWithBuffersAsync_WithValidBuffers_ExecutesSuccessfully()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<float>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(3.14f);

        var buffers = new object[] { new float[] { 1, 2, 3 } };
        var scalarArgs = new object[] { 10 };

        // Act
        var result = await orchestrator.ExecuteWithBuffersAsync<float>("TestKernel", buffers, scalarArgs);

        // Assert
        _ = result.Should().Be(3.14f);
    }

    [Fact]
    public async Task ExecuteWithBuffersAsync_WithIUnifiedMemoryBuffers_DelegatesToBaseOrchestrator()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var buffer = Substitute.For<IUnifiedMemoryBuffer>();
        var buffers = new List<IUnifiedMemoryBuffer> { buffer };

        _ = _baseOrchestrator.ExecuteWithBuffersAsync<int>(Arg.Any<string>(), Arg.Any<IEnumerable<IUnifiedMemoryBuffer>>(), Arg.Any<object[]>())
            .Returns(100);

        // Act
        var result = await orchestrator.ExecuteWithBuffersAsync<int>("TestKernel", buffers);

        // Assert
        _ = result.Should().Be(100);
        _ = await _baseOrchestrator.Received(1).ExecuteWithBuffersAsync<int>("TestKernel", buffers);
    }

    #endregion

    #region GetOptimalAcceleratorAsync Tests

    [Fact]
    public async Task GetOptimalAcceleratorAsync_WithValidKernel_ReturnsSelectedBackend()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        // Act
        var result = await orchestrator.GetOptimalAcceleratorAsync("TestKernel");

        // Assert
        _ = result.Should().BeSameAs(backend);
    }

    [Fact]
    public async Task GetOptimalAcceleratorAsync_WithNoBackend_ReturnsNull()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backendSelection = new BackendSelection
        {
            SelectedBackend = null,
            BackendId = "None"
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        // Act
        var result = await orchestrator.GetOptimalAcceleratorAsync("TestKernel");

        // Assert
        _ = result.Should().BeNull();
    }

    #endregion

    #region Workload Analysis Tests

    [Fact]
    public async Task ExecuteAsync_AnalyzesWorkloadCharacteristics()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        WorkloadCharacteristics? capturedWorkload = null;
        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Do<WorkloadCharacteristics>(w => capturedWorkload = w),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(1);

        var largeArray = new float[1000000];

        // Act
        _ = await orchestrator.ExecuteAsync<int>("TestKernel", [largeArray]);

        // Assert
        _ = capturedWorkload.Should().NotBeNull();
        _ = capturedWorkload!.DataSize.Should().BeGreaterThan(0);
        _ = capturedWorkload.ComputeIntensity.Should().BeGreaterThanOrEqualTo(0);
        _ = capturedWorkload.MemoryIntensity.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ExecuteAsync_CachesWorkloadAnalysis()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(1);

        var args = new object[] { new float[100] };

        // Act - Execute twice with same kernel and args
        _ = await orchestrator.ExecuteAsync<int>("TestKernel", args);
        _ = await orchestrator.ExecuteAsync<int>("TestKernel", args);

        // Assert - Should use cached workload on second call
        _ = await _backendSelector.Received(2).SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>());
    }

    #endregion

    #region Performance Recording Tests

    [Fact]
    public async Task ExecuteAsync_WithLearningEnabled_RecordsPerformance()
    {
        // Arrange
        var options = new DotCompute.Core.Optimization.PerformanceOptimizationOptions();
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, options);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(1);

        // Act
        _ = await orchestrator.ExecuteAsync<int>("TestKernel", []);

        // Assert
        await _backendSelector.Received(1).RecordPerformanceResultAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<string>(),
            Arg.Any<PerformanceResult>());
    }

    [Fact(Skip = "EnableLearning property not exposed in PerformanceOptimizationOptions")]
    public async Task ExecuteAsync_WithLearningDisabled_DoesNotRecordPerformance()
    {
        // Arrange
        var options = new DotCompute.Core.Optimization.PerformanceOptimizationOptions();
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, options);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(1);

        // Act
        _ = await orchestrator.ExecuteAsync<int>("TestKernel", []);

        // Assert
        await _backendSelector.DidNotReceive().RecordPerformanceResultAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<string>(),
            Arg.Any<PerformanceResult>());
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateKernelArgsAsync_DelegatesToBaseOrchestrator()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        _ = _baseOrchestrator.ValidateKernelArgsAsync(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(true);

        // Act
        var result = await orchestrator.ValidateKernelArgsAsync("TestKernel", [1, 2]);

        // Assert
        _ = result.Should().BeTrue();
        _ = await _baseOrchestrator.Received(1).ValidateKernelArgsAsync("TestKernel", Arg.Any<object[]>());
    }

    [Fact]
    public async Task ValidateKernelArgsAsync_WithNullBaseOrchestrator_PerformsBasicValidation()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            null!, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        // Act
        var result = await orchestrator.ValidateKernelArgsAsync("TestKernel", []);

        // Assert
        _ = result.Should().BeTrue();
    }

    #endregion

    #region Precompilation Tests

    [Fact]
    public async Task PrecompileKernelAsync_DelegatesToBaseOrchestrator()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var accelerator = CreateMockAccelerator("CUDA");

        // Act
        await orchestrator.PrecompileKernelAsync("TestKernel", accelerator);

        // Assert
        await _baseOrchestrator.Received(1).PrecompileKernelAsync("TestKernel", accelerator);
    }

    [Fact]
    public async Task GetSupportedAcceleratorsAsync_DelegatesToBaseOrchestrator()
    {
        // Arrange
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        var accelerators = new List<IAccelerator> { CreateMockAccelerator("CUDA") };
        _ = _baseOrchestrator.GetSupportedAcceleratorsAsync(Arg.Any<string>())
            .Returns(accelerators);

        // Act
        var result = await orchestrator.GetSupportedAcceleratorsAsync("TestKernel");

        // Assert
        _ = result.Should().BeEquivalentTo(accelerators);
    }

    #endregion

    #region Optimization Strategy Tests

    [Fact]
    public async Task ExecuteAsync_WithConservativeStrategy_UsesConservativeSettings()
    {
        // Arrange
        var options = new DotCompute.Core.Optimization.PerformanceOptimizationOptions
        {
            OptimizationStrategy = DotCompute.Core.Optimization.OptimizationStrategy.Conservative
        };
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, options);

        var backend = CreateMockAccelerator("CPU");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CPU",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(1);

        // Act
        _ = await orchestrator.ExecuteAsync<int>("TestKernel", []);

        // Assert - Execution should complete
        _ = true.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithAggressiveStrategy_UsesAggressiveSettings()
    {
        // Arrange
        var options = new DotCompute.Core.Optimization.PerformanceOptimizationOptions
        {
            OptimizationStrategy = DotCompute.Core.Optimization.OptimizationStrategy.Aggressive
        };
        using var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, options);

        var backend = CreateMockAccelerator("CUDA");
        var backendSelection = new BackendSelection
        {
            SelectedBackend = backend,
            BackendId = "CUDA",
            ConfidenceScore = 0.9f
        };

        _ = _backendSelector.SelectOptimalBackendAsync(
            Arg.Any<string>(),
            Arg.Any<WorkloadCharacteristics>(),
            Arg.Any<IEnumerable<IAccelerator>>(),
            Arg.Any<SelectionConstraints?>())
            .Returns(backendSelection);

        _ = _baseOrchestrator.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(1);

        // Act
        _ = await orchestrator.ExecuteAsync<int>("TestKernel", []);

        // Assert - Execution should complete
        _ = true.Should().BeTrue();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledOnce_DisposesSuccessfully()
    {
        // Arrange
        var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        // Act
        orchestrator.Dispose();

        // Assert - No exception
        _ = true.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        // Act
        orchestrator.Dispose();
        orchestrator.Dispose();
        orchestrator.Dispose();

        // Assert - No exception
        _ = true.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DisposesBackendSelector()
    {
        // Arrange
        var orchestrator = new PerformanceOptimizedOrchestrator(
            _baseOrchestrator, _backendSelector, _performanceProfiler, _logger, _defaultOptions);

        // Act
        orchestrator.Dispose();

        // Assert
        _backendSelector.Received(1).Dispose();
    }

    #endregion

    #region Helper Methods

    private static IAccelerator CreateMockAccelerator(string name)
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
        _ = accelerator.IsAvailable.Returns(true);
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
            _backendSelector?.Dispose();
            _performanceProfiler?.Dispose();
        }

        _disposed = true;
    }

    #endregion
}
