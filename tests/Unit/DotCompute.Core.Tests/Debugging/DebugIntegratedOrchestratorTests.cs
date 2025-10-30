// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Debugging;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Debugging;

/// <summary>
/// Comprehensive unit tests for DebugIntegratedOrchestrator.
/// Tests transparent debugging wrapper functionality.
/// </summary>
public class DebugIntegratedOrchestratorTests
{
    private readonly IComputeOrchestrator _mockBaseOrchestrator;
    private readonly IKernelDebugService _mockDebugService;
    private readonly ILogger<DebugIntegratedOrchestrator> _logger;
    private readonly IAccelerator _mockAccelerator;

    public DebugIntegratedOrchestratorTests()
    {
        _mockBaseOrchestrator = Substitute.For<IComputeOrchestrator>();
        _mockDebugService = Substitute.For<IKernelDebugService>();
        _logger = Substitute.For<ILogger<DebugIntegratedOrchestrator>>();
        _mockAccelerator = Substitute.For<IAccelerator>();

        _ = _mockAccelerator.Type.Returns(AcceleratorType.CPU);
        _ = _mockAccelerator.Info.Returns(new AcceleratorInfo { Name = "TestAccelerator", Id = "test-1", DeviceType = "Test", Vendor = "Test" });
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Act
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        // Assert
        _ = orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithOptions_ShouldInitializeSuccessfully()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true
        };

        // Act
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        // Assert
        _ = orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBaseOrchestrator_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new DebugIntegratedOrchestrator(
            null!,
            _mockDebugService,
            _logger);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("baseOrchestrator");
    }

    [Fact]
    public void Constructor_WithNullDebugService_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            null!,
            _logger);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("debugService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            null!);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region ExecuteAsync Tests (Basic)

    [Fact]
    public async Task ExecuteAsync_WithDebugHooksDisabled_ShouldBypassDebugFeatures()
    {
        // Arrange
        var options = new DebugExecutionOptions { EnableDebugHooks = false };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
        _ = await _mockBaseOrchestrator.Received(1).ExecuteAsync<int>(kernelName, args);
    }

    [Fact]
    public async Task ExecuteAsync_WithDebugHooksEnabled_ShouldExecuteWithDebugging()
    {
        // Arrange
        var options = new DebugExecutionOptions { EnableDebugHooks = true };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);
        orchestrator.Dispose();

        // Act
        var act = async () => await orchestrator.ExecuteAsync<int>("TestKernel", []);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region ExecuteAsync Tests (With Backend)

    [Fact]
    public async Task ExecuteAsync_WithPreferredBackend_ShouldUseSpecifiedBackend()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";
        var preferredBackend = "CPU";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, preferredBackend, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, preferredBackend, args);

        // Assert
        _ = result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithAccelerator_ShouldUseSpecifiedAccelerator()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, _mockAccelerator, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, _mockAccelerator, args);

        // Assert
        _ = result.Should().Be(42);
    }

    #endregion

    #region ExecuteWithBuffersAsync Tests

    [Fact]
    public async Task ExecuteWithBuffersAsync_WithDebugHooksDisabled_ShouldBypassDebugFeatures()
    {
        // Arrange
        var options = new DebugExecutionOptions { EnableDebugHooks = false };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var buffers = new List<IUnifiedMemoryBuffer>();
        var scalarArgs = new object[] { 1, 2 };
        _ = _mockBaseOrchestrator.ExecuteWithBuffersAsync<int>(kernelName, buffers, scalarArgs).Returns(42);

        // Act
        var result = await orchestrator.ExecuteWithBuffersAsync<int>(kernelName, buffers, scalarArgs);

        // Assert
        _ = result.Should().Be(42);
        _ = await _mockBaseOrchestrator.Received(1).ExecuteWithBuffersAsync<int>(kernelName, buffers, scalarArgs);
    }

    [Fact]
    public async Task ExecuteWithBuffersAsync_WithDebugHooksEnabled_ShouldExecuteWithDebugging()
    {
        // Arrange
        var options = new DebugExecutionOptions { EnableDebugHooks = true };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var buffers = new List<IUnifiedMemoryBuffer>();
        var scalarArgs = new object[] { 1, 2 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, Arg.Any<object[]>()).Returns(42);

        // Act
        var result = await orchestrator.ExecuteWithBuffersAsync<int>(kernelName, buffers, scalarArgs);

        // Assert
        _ = result.Should().Be(42);
    }

    #endregion

    #region Pre-Execution Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithValidateBeforeExecution_ShouldPerformPreValidation()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };

        _ = _mockDebugService.GetAvailableBackendsAsync().Returns(
        [
            new() { Name = "CPU", IsAvailable = true }
        ]);
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
        _ = await _mockDebugService.Received().GetAvailableBackendsAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullArgument_ShouldDetectValidationIssue()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true,
            FailOnValidationErrors = false
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, null!, 3 };

        _ = _mockDebugService.GetAvailableBackendsAsync().Returns(
        [
            new() { Name = "CPU", IsAvailable = true }
        ]);
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoBackendsAvailable_ShouldHandleGracefully()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true,
            FailOnValidationErrors = false
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };

        _ = _mockDebugService.GetAvailableBackendsAsync().Returns([]);
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailOnValidationErrors_ShouldThrowOnCriticalIssues()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateBeforeExecution = true,
            FailOnValidationErrors = true
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { null! };

        _ = _mockDebugService.GetAvailableBackendsAsync().Returns([]);

        // Act
        var act = async () => await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Post-Execution Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithValidateAfterExecution_ShouldPerformPostValidation()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateAfterExecution = true
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithDeterminismTesting_ShouldValidateDeterminism()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            ValidateAfterExecution = true,
            TestDeterminism = true
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };

        _ = _mockDebugService.ValidateDeterminismAsync(kernelName, args, 3).Returns(
            new DeterminismReport
            {
                KernelName = kernelName,
                IsDeterministic = true,
                ExecutionCount = 3
            });
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
        await Task.Delay(100); // Allow async post-validation to start
    }

    #endregion

    #region Cross-Backend Validation Tests

    [Fact]
    public async Task ExecuteAsync_WithCrossBackendValidation_ShouldTriggerValidation()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            EnableCrossBackendValidation = true,
            CrossValidationProbability = 1.0 // Always validate
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };

        _ = _mockDebugService.ValidateKernelAsync(kernelName, args, Arg.Any<float>()).Returns(
            new KernelValidationResult
            {
                KernelName = kernelName,
                IsValid = true
            });
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
        await Task.Delay(100); // Allow async validation to start
    }

    [Fact]
    public async Task ExecuteAsync_WithZeroProbability_ShouldSkipCrossValidation()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            EnableCrossBackendValidation = true,
            CrossValidationProbability = 0.0 // Never validate
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    #endregion

    #region Performance Monitoring Tests

    [Fact]
    public async Task ExecuteAsync_WithPerformanceMonitoring_ShouldLogMetrics()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            EnablePerformanceMonitoring = true
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithPerformanceHistory_ShouldStoreMetrics()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            EnablePerformanceMonitoring = true,
            StorePerformanceHistory = true
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    #endregion

    #region Error Analysis Tests

    [Fact]
    public async Task ExecuteAsync_WithExecutionError_ShouldAnalyzeError()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            AnalyzeErrorsOnFailure = true
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };

        _ = _mockDebugService.GetAvailableBackendsAsync().Returns(
        [
            new() { Name = "CPU", IsAvailable = true }
        ]);
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args)
            .Returns<int>(_ => throw new InvalidOperationException("Test error"));

        // Act
        var act = async () => await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>();
        await Task.Delay(100); // Allow async error analysis to start
    }

    [Fact]
    public async Task ExecuteAsync_WithErrorAnalysisDisabled_ShouldSkipAnalysis()
    {
        // Arrange
        var options = new DebugExecutionOptions
        {
            EnableDebugHooks = true,
            AnalyzeErrorsOnFailure = false
        };
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger,
            options);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ExecuteAsync<int>(kernelName, args)
            .Returns<int>(_ => throw new InvalidOperationException("Test error"));

        // Act
        var act = async () => await orchestrator.ExecuteAsync<int>(kernelName, args);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public async Task GetOptimalAcceleratorAsync_ShouldDelegateToBaseOrchestrator()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";
        _ = _mockBaseOrchestrator.GetOptimalAcceleratorAsync(kernelName).Returns(_mockAccelerator);

        // Act
        var result = await orchestrator.GetOptimalAcceleratorAsync(kernelName);

        // Assert
        _ = result.Should().Be(_mockAccelerator);
        _ = await _mockBaseOrchestrator.Received(1).GetOptimalAcceleratorAsync(kernelName);
    }

    [Fact]
    public async Task PrecompileKernelAsync_ShouldDelegateToBaseOrchestrator()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";

        // Act
        await orchestrator.PrecompileKernelAsync(kernelName);

        // Assert
        await _mockBaseOrchestrator.Received(1).PrecompileKernelAsync(kernelName, null);
    }

    [Fact]
    public async Task GetSupportedAcceleratorsAsync_ShouldDelegateToBaseOrchestrator()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";
        var accelerators = new List<IAccelerator> { _mockAccelerator };
        _ = _mockBaseOrchestrator.GetSupportedAcceleratorsAsync(kernelName)
            .Returns(accelerators.AsReadOnly());

        // Act
        var result = await orchestrator.GetSupportedAcceleratorsAsync(kernelName);

        // Assert
        _ = result.Should().BeEquivalentTo(accelerators);
        _ = await _mockBaseOrchestrator.Received(1).GetSupportedAcceleratorsAsync(kernelName);
    }

    [Fact]
    public async Task ValidateKernelArgsAsync_WithBaseOrchestrator_ShouldDelegate()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };
        _ = _mockBaseOrchestrator.ValidateKernelArgsAsync(kernelName, args).Returns(true);

        // Act
        var result = await orchestrator.ValidateKernelArgsAsync(kernelName, args);

        // Assert
        _ = result.Should().BeTrue();
        _ = await _mockBaseOrchestrator.Received(1).ValidateKernelArgsAsync(kernelName, args);
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithExecutionParameters_ShouldExecuteWithDebugHooks()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";
        var executionParams = Substitute.For<IKernelExecutionParameters>();
        _ = executionParams.Arguments.Returns([1, 2, 3]);

        _ = _mockBaseOrchestrator.ExecuteAsync<object>(kernelName, Arg.Any<object[]>()).Returns(42);

        // Act
        var result = await orchestrator.ExecuteKernelAsync(kernelName, executionParams);

        // Assert
        _ = result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithArrayArgs_ShouldExecuteWithDebugHooks()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        var kernelName = "TestKernel";
        var args = new object[] { 1, 2, 3 };

        _ = _mockBaseOrchestrator.ExecuteAsync<object>(kernelName, args).Returns(42);

        // Act
        var result = await orchestrator.ExecuteKernelAsync(kernelName, args);

        // Assert
        _ = result.Should().Be(42);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldDisposeResourcesCleanly()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        // Act
        orchestrator.Dispose();

        // Assert
        var act = async () => await orchestrator.ExecuteAsync<int>("Test", []);
        _ = act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            _mockDebugService,
            _logger);

        // Act
        orchestrator.Dispose();
        var act = orchestrator.Dispose;

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ShouldDisposeBaseOrchestratorIfDisposable()
    {
        // Arrange
        var disposableOrchestrator = Substitute.For<IComputeOrchestrator, IDisposable>();
        var orchestrator = new DebugIntegratedOrchestrator(
            disposableOrchestrator,
            _mockDebugService,
            _logger);

        // Act
        orchestrator.Dispose();

        // Assert
        ((IDisposable)disposableOrchestrator).Received(1).Dispose();
    }

    [Fact]
    public void Dispose_ShouldDisposeDebugServiceIfDisposable()
    {
        // Arrange
        var disposableDebugService = Substitute.For<IKernelDebugService, IDisposable>();
        var orchestrator = new DebugIntegratedOrchestrator(
            _mockBaseOrchestrator,
            disposableDebugService,
            _logger);

        // Act
        orchestrator.Dispose();

        // Assert
        ((IDisposable)disposableDebugService).Received(1).Dispose();
    }

    #endregion
}
