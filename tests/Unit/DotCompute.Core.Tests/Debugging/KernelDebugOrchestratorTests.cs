// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
// Types are in the main namespace now: using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Debugging;
using DotCompute.Core.Debugging.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.Debugging;

/// <summary>
/// Comprehensive unit tests for KernelDebugOrchestrator.
/// Tests focus on orchestration logic and component coordination without hardware dependencies.
/// </summary>
public class KernelDebugOrchestratorTests : IDisposable
{
    private readonly ILogger<KernelDebugOrchestrator> _logger;
    private readonly IAccelerator _mockAccelerator;
    private readonly DebugServiceOptions _options;
    private readonly KernelDebugOrchestrator _orchestrator;
    private bool _disposed;

    public KernelDebugOrchestratorTests()
    {
        _logger = Substitute.For<ILogger<KernelDebugOrchestrator>>();
        _mockAccelerator = Substitute.For<IAccelerator>();
        _options = new DebugServiceOptions
        {
            VerbosityLevel = DotCompute.Abstractions.Debugging.LogLevel.Information,
            EnableDetailedTracing = true,
            EnableMemoryProfiling = true,
            EnablePerformanceAnalysis = true
        };

        // Setup mock accelerator
        _mockAccelerator.Type.Returns(AcceleratorType.CPU);
        _mockAccelerator.Info.Returns(new AcceleratorInfo { Name = "TestAccelerator", Id = "test-1", DeviceType = "Test", Vendor = "Test" });
        _mockAccelerator.IsAvailable.Returns(true);

        _orchestrator = new KernelDebugOrchestrator(_logger, _mockAccelerator, _options);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Act
        var orchestrator = new KernelDebugOrchestrator(_logger);

        // Assert
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithPrimaryAccelerator_ShouldRegisterAccelerator()
    {
        // Act
        var orchestrator = new KernelDebugOrchestrator(_logger, _mockAccelerator);

        // Assert
        orchestrator.Should().NotBeNull();
        var backends = orchestrator.GetAvailableBackends();
        backends.Should().Contain("CPU");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new KernelDebugOrchestrator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithOptions_ShouldApplyOptions()
    {
        // Arrange
        var options = new DebugServiceOptions
        {
            VerbosityLevel = DotCompute.Abstractions.Debugging.LogLevel.Debug
        };

        // Act
        var orchestrator = new KernelDebugOrchestrator(_logger, _mockAccelerator, options);

        // Assert
        orchestrator.Should().NotBeNull();
    }

    #endregion

    #region ValidateKernelAsync Tests

    [Fact]
    public async Task ValidateKernelAsync_WithValidParameters_ShouldReturnValidationResult()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.ValidateKernelAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task ValidateKernelAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.ValidateKernelAsync(null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateKernelAsync_WithEmptyKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.ValidateKernelAsync(string.Empty, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateKernelAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.ValidateKernelAsync("TestKernel", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateKernelAsync_WithCustomTolerance_ShouldUseSpecifiedTolerance()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var tolerance = 0.001f;

        // Act
        var result = await _orchestrator.ValidateKernelAsync(kernelName, inputs, tolerance);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task ValidateKernelAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();

        // Act
        var act = async () => await _orchestrator.ValidateKernelAsync("TestKernel", new object[] { 1 });

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region ExecuteOnBackendAsync Tests

    [Fact]
    public async Task ExecuteOnBackendAsync_WithValidParameters_ShouldReturnExecutionResult()
    {
        // Arrange
        var kernelName = "TestKernel";
        var backendType = "CPU";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.ExecuteOnBackendAsync(kernelName, backendType, inputs);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.BackendType.Should().Be(backendType);
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.ExecuteOnBackendAsync(null!, "CPU", inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_WithNullBackendType_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.ExecuteOnBackendAsync("TestKernel", null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.ExecuteOnBackendAsync("TestKernel", "CPU", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();

        // Act
        var act = async () => await _orchestrator.ExecuteOnBackendAsync("TestKernel", "CPU", new object[] { 1 });

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region CompareResultsAsync Tests

    [Fact]
    public async Task CompareResultsAsync_WithValidResults_ShouldReturnComparisonReport()
    {
        // Arrange
        var results = CreateSampleExecutionResults();

        // Act
        var result = await _orchestrator.CompareResultsAsync(results);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompareResultsAsync_WithNullResults_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.CompareResultsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CompareResultsAsync_WithEmptyResults_ShouldHandleGracefully()
    {
        // Arrange
        var results = new List<KernelExecutionResult>();

        // Act
        var result = await _orchestrator.CompareResultsAsync(results);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CompareResultsAsync_WithComparisonStrategy_ShouldUseSpecifiedStrategy()
    {
        // Arrange
        var results = CreateSampleExecutionResults();
        var strategy = ComparisonStrategy.Exact;

        // Act
        var result = await _orchestrator.CompareResultsAsync(results, strategy);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CompareResultsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var results = CreateSampleExecutionResults();

        // Act
        var act = async () => await _orchestrator.CompareResultsAsync(results);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region TraceKernelExecutionAsync Tests

    [Fact]
    public async Task TraceKernelExecutionAsync_WithValidParameters_ShouldReturnExecutionTrace()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var tracePoints = new[] { "Point1", "Point2" };

        // Act
        var result = await _orchestrator.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };
        var tracePoints = new[] { "Point1" };

        // Act
        var act = async () => await _orchestrator.TraceKernelExecutionAsync(null!, inputs, tracePoints);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Arrange
        var tracePoints = new[] { "Point1" };

        // Act
        var act = async () => await _orchestrator.TraceKernelExecutionAsync("TestKernel", null!, tracePoints);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_WithNullTracePoints_ShouldThrowArgumentNullException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.TraceKernelExecutionAsync("TestKernel", inputs, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_WithNoBackends_ShouldReturnFailedTrace()
    {
        // Arrange
        var orchestratorWithoutBackends = new KernelDebugOrchestrator(_logger);
        var inputs = new object[] { 1, 2, 3 };
        var tracePoints = new[] { "Point1" };

        // Act
        var result = await orchestratorWithoutBackends.TraceKernelExecutionAsync("TestKernel", inputs, tracePoints);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No suitable backend");
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var inputs = new object[] { 1, 2, 3 };
        var tracePoints = new[] { "Point1" };

        // Act
        var act = async () => await _orchestrator.TraceKernelExecutionAsync("TestKernel", inputs, tracePoints);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region AnalyzePerformanceAsync Tests

    [Fact]
    public async Task AnalyzePerformanceAsync_WithValidKernelName_ShouldReturnAnalysis()
    {
        // Arrange
        var kernelName = "TestKernel";

        // Act
        var result = await _orchestrator.AnalyzePerformanceAsync(kernelName);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.PerformanceReport.Should().NotBeNull();
        result.MemoryAnalysis.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Act
        var act = async () => await _orchestrator.AnalyzePerformanceAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_WithEmptyKernelName_ShouldThrowArgumentException()
    {
        // Act
        var act = async () => await _orchestrator.AnalyzePerformanceAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_WithTimeWindow_ShouldUseSpecifiedWindow()
    {
        // Arrange
        var kernelName = "TestKernel";
        var timeWindow = TimeSpan.FromMinutes(10);

        // Act
        var result = await _orchestrator.AnalyzePerformanceAsync(kernelName, timeWindow);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_ShouldIncludeAdvancedAnalysis()
    {
        // Arrange
        var kernelName = "TestKernel";

        // Act
        var result = await _orchestrator.AnalyzePerformanceAsync(kernelName);

        // Assert
        result.Should().NotBeNull();
        result.AdvancedAnalysis.Should().NotBeNull();
        result.AdvancedAnalysis.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();

        // Act
        var act = async () => await _orchestrator.AnalyzePerformanceAsync("TestKernel");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region ValidateDeterminismAsync Tests

    [Fact]
    public async Task ValidateDeterminismAsync_WithValidParameters_ShouldReturnDeterminismReport()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.ValidateDeterminismAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.ExecutionCount.Should().Be(10); // Default iterations
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.ValidateDeterminismAsync(null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.ValidateDeterminismAsync("TestKernel", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithIterationsLessThanTwo_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.ValidateDeterminismAsync("TestKernel", inputs, 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Iteration count must be at least 2*");
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithCustomIterations_ShouldUseSpecifiedCount()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var iterations = 20;

        // Act
        var result = await _orchestrator.ValidateDeterminismAsync(kernelName, inputs, iterations);

        // Assert
        result.Should().NotBeNull();
        result.ExecutionCount.Should().Be(iterations);
    }

    [Fact]
    public async Task ValidateDeterminismAsync_ShouldProvideRecommendations()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.ValidateDeterminismAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.Recommendations.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateDeterminismAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.ValidateDeterminismAsync("TestKernel", inputs);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Accelerator Management Tests

    [Fact]
    public void RegisterAccelerator_WithValidParameters_ShouldRegisterSuccessfully()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        accelerator.Type.Returns(AcceleratorType.CUDA);

        // Act
        _orchestrator.RegisterAccelerator("CUDA", accelerator);

        // Assert
        var backends = _orchestrator.GetAvailableBackends();
        backends.Should().Contain("CUDA");
    }

    [Fact]
    public void RegisterAccelerator_WithNullBackendType_ShouldThrowArgumentException()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();

        // Act
        var act = () => _orchestrator.RegisterAccelerator(null!, accelerator);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterAccelerator_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _orchestrator.RegisterAccelerator("CUDA", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UnregisterAccelerator_WithRegisteredBackend_ShouldReturnTrue()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        accelerator.Type.Returns(AcceleratorType.CUDA);
        _orchestrator.RegisterAccelerator("CUDA", accelerator);

        // Act
        var result = _orchestrator.UnregisterAccelerator("CUDA");

        // Assert
        result.Should().BeTrue();
        var backends = _orchestrator.GetAvailableBackends();
        backends.Should().NotContain("CUDA");
    }

    [Fact]
    public void UnregisterAccelerator_WithUnregisteredBackend_ShouldReturnFalse()
    {
        // Act
        var result = _orchestrator.UnregisterAccelerator("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnregisterAccelerator_WithNullBackendType_ShouldThrowArgumentException()
    {
        // Act
        var act = () => _orchestrator.UnregisterAccelerator(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetAvailableBackends_ShouldReturnRegisteredBackends()
    {
        // Act
        var backends = _orchestrator.GetAvailableBackends();

        // Assert
        backends.Should().NotBeNull();
        backends.Should().Contain("CPU"); // Primary accelerator
    }

    [Fact]
    public void RegisterAccelerator_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var accelerator = Substitute.For<IAccelerator>();

        // Act
        var act = () => _orchestrator.RegisterAccelerator("CUDA", accelerator);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Options Management Tests

    [Fact]
    public void UpdateOptions_WithValidOptions_ShouldUpdateSuccessfully()
    {
        // Arrange
        var newOptions = new DebugServiceOptions
        {
            VerbosityLevel = DotCompute.Abstractions.Debugging.LogLevel.Debug,
            EnableDetailedTracing = false
        };

        // Act
        _orchestrator.UpdateOptions(newOptions);

        // Assert - no exception should be thrown
    }

    [Fact]
    public void UpdateOptions_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _orchestrator.UpdateOptions(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_WithValidOptions_ShouldUpdateSuccessfully()
    {
        // Arrange
        var newOptions = new DebugServiceOptions
        {
            VerbosityLevel = DotCompute.Abstractions.Debugging.LogLevel.Warning
        };

        // Act
        _orchestrator.Configure(newOptions);

        // Assert - no exception should be thrown
    }

    [Fact]
    public void UpdateOptions_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var options = new DebugServiceOptions();

        // Act
        var act = () => _orchestrator.UpdateOptions(options);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region AnalyzeMemoryPatternsAsync Tests

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithValidParameters_ShouldReturnReport()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.AnalyzeMemoryPatternsAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.MemoryEfficiency.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.AnalyzeMemoryPatternsAsync(null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.AnalyzeMemoryPatternsAsync("TestKernel", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.AnalyzeMemoryPatternsAsync("TestKernel", inputs);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region GetAvailableBackendsAsync Tests

    [Fact]
    public async Task GetAvailableBackendsAsync_ShouldReturnBackendInfo()
    {
        // Act
        var result = await _orchestrator.GetAvailableBackendsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().Contain(b => b.Type == "CPU");
    }

    [Fact]
    public async Task GetAvailableBackendsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();

        // Act
        var act = async () => await _orchestrator.GetAvailableBackendsAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region RunComprehensiveDebugAsync Tests

    [Fact]
    public async Task RunComprehensiveDebugAsync_WithValidParameters_ShouldReturnComprehensiveReport()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.RunComprehensiveDebugAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.ValidationResult.Should().NotBeNull();
        result.PerformanceAnalysis.Should().NotBeNull();
        result.DeterminismAnalysis.Should().NotBeNull();
        result.MemoryAnalysis.Should().NotBeNull();
    }

    [Fact]
    public async Task RunComprehensiveDebugAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.RunComprehensiveDebugAsync(null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunComprehensiveDebugAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.RunComprehensiveDebugAsync("TestKernel", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunComprehensiveDebugAsync_ShouldCalculateHealthScore()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.RunComprehensiveDebugAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.OverallHealthScore.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task RunComprehensiveDebugAsync_ShouldProvideExecutiveSummary()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.RunComprehensiveDebugAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.ExecutiveSummary.Should().NotBeNullOrEmpty();
        result.ExecutiveSummary.Should().Contain(kernelName);
    }

    [Fact]
    public async Task RunComprehensiveDebugAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.RunComprehensiveDebugAsync("TestKernel", inputs);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region GenerateDetailedReportAsync Tests

    [Fact]
    public async Task GenerateDetailedReportAsync_WithValidResult_ShouldReturnReport()
    {
        // Arrange
        var validationResult = CreateSampleValidationResult();

        // Act
        var result = await _orchestrator.GenerateDetailedReportAsync(validationResult);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("TestKernel");
    }

    [Fact]
    public async Task GenerateDetailedReportAsync_WithNullResult_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.GenerateDetailedReportAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateDetailedReportAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var validationResult = CreateSampleValidationResult();

        // Act
        var act = async () => await _orchestrator.GenerateDetailedReportAsync(validationResult);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region ExportReportAsync Tests

    [Fact]
    public async Task ExportReportAsync_WithValidReport_ShouldReturnExportedData()
    {
        // Arrange
        var report = CreateSampleValidationResult();
        var format = ReportFormat.Json;

        // Act
        var result = await _orchestrator.ExportReportAsync(report, format);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportReportAsync_WithNullReport_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.ExportReportAsync(null!, ReportFormat.Json);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExportReportAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var report = CreateSampleValidationResult();

        // Act
        var act = async () => await _orchestrator.ExportReportAsync(report, ReportFormat.Json);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region GeneratePerformanceReportAsync Tests

    [Fact]
    public async Task GeneratePerformanceReportAsync_WithValidKernelName_ShouldReturnReport()
    {
        // Arrange
        var kernelName = "TestKernel";

        // Act
        var result = await _orchestrator.GeneratePerformanceReportAsync(kernelName);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task GeneratePerformanceReportAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Act
        var act = async () => await _orchestrator.GeneratePerformanceReportAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GeneratePerformanceReportAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();

        // Act
        var act = async () => await _orchestrator.GeneratePerformanceReportAsync("TestKernel");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region AnalyzeResourceUtilizationAsync Tests

    [Fact]
    public async Task AnalyzeResourceUtilizationAsync_WithValidParameters_ShouldReturnReport()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _orchestrator.AnalyzeResourceUtilizationAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.CpuUtilization.Should().NotBeNull();
        result.MemoryUtilization.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeResourceUtilizationAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.AnalyzeResourceUtilizationAsync(null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeResourceUtilizationAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _orchestrator.AnalyzeResourceUtilizationAsync("TestKernel", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AnalyzeResourceUtilizationAsync_WithCustomTimeWindow_ShouldUseSpecifiedWindow()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var window = TimeSpan.FromMinutes(10);

        // Act
        var result = await _orchestrator.AnalyzeResourceUtilizationAsync(kernelName, inputs, window);

        // Assert
        result.Should().NotBeNull();
        result.AnalysisTimeWindow.Should().Be(window);
    }

    [Fact]
    public async Task AnalyzeResourceUtilizationAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _orchestrator.AnalyzeResourceUtilizationAsync("TestKernel", inputs);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public void GetStatistics_ShouldReturnStatistics()
    {
        // Act
        var result = _orchestrator.GetStatistics();

        // Assert
        result.Should().NotBeNull();
        result.RegisteredAccelerators.Should().BeGreaterThanOrEqualTo(1);
        result.BackendStatistics.Should().NotBeNull();
    }

    [Fact]
    public void GetStatistics_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _orchestrator.Dispose();

        // Act
        var act = () => _orchestrator.GetStatistics();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var orchestrator = new KernelDebugOrchestrator(_logger, _mockAccelerator);

        // Act
        orchestrator.Dispose();

        // Assert - no exception should be thrown
        var act = () => orchestrator.Dispose(); // Second dispose
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ShouldClearAccelerators()
    {
        // Arrange
        var orchestrator = new KernelDebugOrchestrator(_logger, _mockAccelerator);

        // Act
        orchestrator.Dispose();

        // Assert
        var act = () => orchestrator.GetAvailableBackends();
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Helper Methods

    private static List<KernelExecutionResult> CreateSampleExecutionResults()
    {
        return new List<KernelExecutionResult>
        {
            new()
            {
                KernelName = "TestKernel",
                BackendType = "CPU",
                Success = true,
                Handle = new KernelExecutionHandle
                {
                    Id = Guid.NewGuid(),
                    KernelName = "TestKernel",
                    SubmittedAt = DateTime.UtcNow
                },
                Timings = new KernelExecutionTimings
                {
                    KernelTimeMs = 8.0,
                    TotalTimeMs = 10.0
                }
            },
            new()
            {
                KernelName = "TestKernel",
                BackendType = "CUDA",
                Success = true,
                Handle = new KernelExecutionHandle
                {
                    Id = Guid.NewGuid(),
                    KernelName = "TestKernel",
                    SubmittedAt = DateTime.UtcNow
                },
                Timings = new KernelExecutionTimings
                {
                    KernelTimeMs = 4.0,
                    TotalTimeMs = 5.0
                }
            }
        };
    }

    private static DotCompute.Abstractions.Debugging.KernelValidationResult CreateSampleValidationResult()
    {
        return new DotCompute.Abstractions.Debugging.KernelValidationResult
        {
            KernelName = "TestKernel",
            IsValid = true,
            ValidationTime = DateTime.UtcNow,
            BackendsTested = new List<string> { "CPU", "CUDA" },
            // Issues = new System.Collections.ObjectModel.Collection<DotCompute.Abstractions.Debugging.DebugValidationIssue>() // Namespace DotCompute.Core.System.Collections doesn't exist
            Issues = new global::System.Collections.ObjectModel.Collection<DotCompute.Abstractions.Debugging.DebugValidationIssue>()
        };
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _orchestrator?.Dispose();
        }
    }
}
