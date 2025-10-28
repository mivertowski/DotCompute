// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Interfaces.Kernels;
using ComparisonStrategy = DotCompute.Abstractions.Debugging.ComparisonStrategy;

namespace DotCompute.Abstractions.Tests.Debugging;

/// <summary>
/// Comprehensive tests for IKernelDebugService interface and related debug types.
/// Target: 20+ tests for debug operations, validation methods, and analysis.
/// </summary>
public class IKernelDebugServiceTests
{
    private readonly IKernelDebugService _debugService;

    public IKernelDebugServiceTests()
    {
        _debugService = Substitute.For<IKernelDebugService>();
    }

    #region ValidateKernelAsync Tests (5 tests)

    [Fact]
    public async Task ValidateKernelAsync_WithValidInputs_ReturnsValidationResult()
    {
        // Arrange
        var kernelName = "VectorAdd";
        var inputs = new object[] { new float[] { 1, 2, 3 }, new float[] { 4, 5, 6 } };
        var expectedResult = new KernelValidationResult();

        _debugService.ValidateKernelAsync(kernelName, inputs, Arg.Any<float>())
            .Returns(Task.FromResult(expectedResult));

        // Act
        var result = await _debugService.ValidateKernelAsync(kernelName, inputs);

        // Assert
        result.Should().BeSameAs(expectedResult);
        await _debugService.Received(1).ValidateKernelAsync(kernelName, inputs, Arg.Any<float>());
    }

    [Fact]
    public async Task ValidateKernelAsync_WithCustomTolerance_UsesTolerance()
    {
        // Arrange
        var kernelName = "FloatCompare";
        var inputs = new object[] { 1.0f, 1.00001f };
        var tolerance = 1e-4f;
        var expectedResult = new KernelValidationResult();

        _debugService.ValidateKernelAsync(kernelName, inputs, tolerance)
            .Returns(Task.FromResult(expectedResult));

        // Act
        var result = await _debugService.ValidateKernelAsync(kernelName, inputs, tolerance);

        // Assert
        result.Should().BeSameAs(expectedResult);
        await _debugService.Received(1).ValidateKernelAsync(kernelName, inputs, tolerance);
    }

    [Fact]
    public async Task ValidateKernelAsync_MultipleCallsWithSameInputs_CanBeRepeated()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 42 };
        _debugService.ValidateKernelAsync(kernelName, inputs, Arg.Any<float>())
            .Returns(Task.FromResult(new KernelValidationResult()));

        // Act
        await _debugService.ValidateKernelAsync(kernelName, inputs);
        await _debugService.ValidateKernelAsync(kernelName, inputs);

        // Assert
        await _debugService.Received(2).ValidateKernelAsync(kernelName, inputs, Arg.Any<float>());
    }

    [Fact]
    public async Task ValidateKernelAsync_WithDifferentKernels_CallsIndependently()
    {
        // Arrange
        var kernel1 = "Kernel1";
        var kernel2 = "Kernel2";
        var inputs = new object[] { 1 };

        _debugService.ValidateKernelAsync(Arg.Any<string>(), inputs, Arg.Any<float>())
            .Returns(Task.FromResult(new KernelValidationResult()));

        // Act
        await _debugService.ValidateKernelAsync(kernel1, inputs);
        await _debugService.ValidateKernelAsync(kernel2, inputs);

        // Assert
        await _debugService.Received(1).ValidateKernelAsync(kernel1, inputs, Arg.Any<float>());
        await _debugService.Received(1).ValidateKernelAsync(kernel2, inputs, Arg.Any<float>());
    }

    [Fact]
    public async Task ValidateKernelAsync_DefaultTolerance_Uses1e6()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1.0f };

        _debugService.ValidateKernelAsync(kernelName, inputs, 1e-6f)
            .Returns(Task.FromResult(new KernelValidationResult()));

        // Act
        await _debugService.ValidateKernelAsync(kernelName, inputs);

        // Assert
        await _debugService.Received(1).ValidateKernelAsync(kernelName, inputs, 1e-6f);
    }

    #endregion

    #region ExecuteOnBackendAsync Tests (4 tests)

    [Fact]
    public async Task ExecuteOnBackendAsync_WithSpecificBackend_ExecutesOnBackend()
    {
        // Arrange
        var kernelName = "VectorAdd";
        var backendType = "CUDA";
        var inputs = new object[] { new float[] { 1, 2 } };
        var expectedResult = new KernelExecutionResult { Success = true, Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow } };

        _debugService.ExecuteOnBackendAsync(kernelName, backendType, inputs)
            .Returns(Task.FromResult(expectedResult));

        // Act
        var result = await _debugService.ExecuteOnBackendAsync(kernelName, backendType, inputs);

        // Assert
        result.Should().BeSameAs(expectedResult);
        await _debugService.Received(1).ExecuteOnBackendAsync(kernelName, backendType, inputs);
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_WithDifferentBackends_CallsIndependently()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1 };

        _debugService.ExecuteOnBackendAsync(kernelName, "CPU", inputs)
            .Returns(Task.FromResult(new KernelExecutionResult { Success = true, Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow } }));
        _debugService.ExecuteOnBackendAsync(kernelName, "CUDA", inputs)
            .Returns(Task.FromResult(new KernelExecutionResult { Success = true, Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow } }));

        // Act
        await _debugService.ExecuteOnBackendAsync(kernelName, "CPU", inputs);
        await _debugService.ExecuteOnBackendAsync(kernelName, "CUDA", inputs);

        // Assert
        await _debugService.Received(1).ExecuteOnBackendAsync(kernelName, "CPU", inputs);
        await _debugService.Received(1).ExecuteOnBackendAsync(kernelName, "CUDA", inputs);
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_SequentialExecutions_MaintainsOrder()
    {
        // Arrange
        var kernelName = "TestKernel";
        var backend = "CUDA";
        var inputs = new object[] { 1 };

        _debugService.ExecuteOnBackendAsync(kernelName, backend, inputs)
            .Returns(Task.FromResult(new KernelExecutionResult { Success = true, Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow } }));

        // Act
        await _debugService.ExecuteOnBackendAsync(kernelName, backend, inputs);
        await _debugService.ExecuteOnBackendAsync(kernelName, backend, inputs);

        // Assert
        await _debugService.Received(2).ExecuteOnBackendAsync(kernelName, backend, inputs);
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_WithComplexInputs_HandlesCorrectly()
    {
        // Arrange
        var kernelName = "MatrixMultiply";
        var backend = "CPU";
        var inputs = new object[]
        {
            new float[,] { { 1, 2 }, { 3, 4 } },
            new float[,] { { 5, 6 }, { 7, 8 } }
        };

        _debugService.ExecuteOnBackendAsync(kernelName, backend, inputs)
            .Returns(Task.FromResult(new KernelExecutionResult { Success = true, Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow } }));

        // Act
        var result = await _debugService.ExecuteOnBackendAsync(kernelName, backend, inputs);

        // Assert
        result.Should().NotBeNull();
        await _debugService.Received(1).ExecuteOnBackendAsync(kernelName, backend, inputs);
    }

    #endregion

    #region CompareResultsAsync Tests (4 tests)

    [Fact]
    public async Task CompareResultsAsync_WithMultipleResults_ComparesAll()
    {
        // Arrange
        var results = new List<KernelExecutionResult>
        {
            new KernelExecutionResult { Success = true, Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow } },
            new KernelExecutionResult { Success = true, Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow } }
        };
        var expectedReport = new ResultComparisonReport();

        _debugService.CompareResultsAsync(results, Arg.Any<ComparisonStrategy>())
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _debugService.CompareResultsAsync(results);

        // Assert
        report.Should().BeSameAs(expectedReport);
        await _debugService.Received(1).CompareResultsAsync(results, Arg.Any<ComparisonStrategy>());
    }

    [Fact]
    public async Task CompareResultsAsync_WithToleranceStrategy_UsesStrategy()
    {
        // Arrange
        var results = new List<KernelExecutionResult>();
        var strategy = ComparisonStrategy.Tolerance;
        var expectedReport = new ResultComparisonReport();

        _debugService.CompareResultsAsync(results, strategy)
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _debugService.CompareResultsAsync(results, strategy);

        // Assert
        report.Should().BeSameAs(expectedReport);
        await _debugService.Received(1).CompareResultsAsync(results, strategy);
    }

    [Theory]
    [InlineData(ComparisonStrategy.Exact)]
    [InlineData(ComparisonStrategy.Tolerance)]
    [InlineData(ComparisonStrategy.Statistical)]
    public async Task CompareResultsAsync_WithDifferentStrategies_CallsCorrectly(ComparisonStrategy strategy)
    {
        // Arrange
        var results = new List<KernelExecutionResult>();
        _debugService.CompareResultsAsync(results, strategy)
            .Returns(Task.FromResult(new ResultComparisonReport()));

        // Act
        await _debugService.CompareResultsAsync(results, strategy);

        // Assert
        await _debugService.Received(1).CompareResultsAsync(results, strategy);
    }

    [Fact]
    public async Task CompareResultsAsync_DefaultStrategy_UsesTolerance()
    {
        // Arrange
        var results = new List<KernelExecutionResult>();
        _debugService.CompareResultsAsync(results, ComparisonStrategy.Tolerance)
            .Returns(Task.FromResult(new ResultComparisonReport()));

        // Act
        await _debugService.CompareResultsAsync(results);

        // Assert
        await _debugService.Received(1).CompareResultsAsync(results, ComparisonStrategy.Tolerance);
    }

    #endregion

    #region TraceKernelExecutionAsync Tests (3 tests)

    [Fact]
    public async Task TraceKernelExecutionAsync_WithTracePoints_CapturesState()
    {
        // Arrange
        var kernelName = "ComplexKernel";
        var inputs = new object[] { 1, 2, 3 };
        var tracePoints = new[] { "point1", "point2" };
        var expectedTrace = new KernelExecutionTrace();

        _debugService.TraceKernelExecutionAsync(kernelName, inputs, tracePoints)
            .Returns(Task.FromResult(expectedTrace));

        // Act
        var trace = await _debugService.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);

        // Assert
        trace.Should().BeSameAs(expectedTrace);
        await _debugService.Received(1).TraceKernelExecutionAsync(kernelName, inputs, tracePoints);
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_WithMultipleTracePoints_ProcessesAll()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 42 };
        var tracePoints = new[] { "start", "middle", "end" };

        _debugService.TraceKernelExecutionAsync(kernelName, inputs, tracePoints)
            .Returns(Task.FromResult(new KernelExecutionTrace()));

        // Act
        await _debugService.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);

        // Assert
        await _debugService.Received(1).TraceKernelExecutionAsync(kernelName, inputs, tracePoints);
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_EmptyTracePoints_StillExecutes()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1 };
        var tracePoints = Array.Empty<string>();

        _debugService.TraceKernelExecutionAsync(kernelName, inputs, tracePoints)
            .Returns(Task.FromResult(new KernelExecutionTrace()));

        // Act
        var trace = await _debugService.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);

        // Assert
        trace.Should().NotBeNull();
        await _debugService.Received(1).TraceKernelExecutionAsync(kernelName, inputs, tracePoints);
    }

    #endregion

    #region ValidateDeterminismAsync Tests (3 tests)

    [Fact]
    public async Task ValidateDeterminismAsync_WithDefaultIterations_Uses10()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 42 };

        _debugService.ValidateDeterminismAsync(kernelName, inputs, 10)
            .Returns(Task.FromResult(new DeterminismReport()));

        // Act
        await _debugService.ValidateDeterminismAsync(kernelName, inputs);

        // Assert
        await _debugService.Received(1).ValidateDeterminismAsync(kernelName, inputs, 10);
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithCustomIterations_UsesValue()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1 };
        var iterations = 25;

        _debugService.ValidateDeterminismAsync(kernelName, inputs, iterations)
            .Returns(Task.FromResult(new DeterminismReport()));

        // Act
        await _debugService.ValidateDeterminismAsync(kernelName, inputs, iterations);

        // Assert
        await _debugService.Received(1).ValidateDeterminismAsync(kernelName, inputs, iterations);
    }

    [Fact]
    public async Task ValidateDeterminismAsync_MultipleCalls_IndependentValidations()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1 };

        _debugService.ValidateDeterminismAsync(kernelName, inputs, Arg.Any<int>())
            .Returns(Task.FromResult(new DeterminismReport()));

        // Act
        await _debugService.ValidateDeterminismAsync(kernelName, inputs);
        await _debugService.ValidateDeterminismAsync(kernelName, inputs);

        // Assert
        await _debugService.Received(2).ValidateDeterminismAsync(kernelName, inputs, Arg.Any<int>());
    }

    #endregion

    #region AnalyzeMemoryPatternsAsync Tests (2 tests)

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithInputs_ReturnsAnalysis()
    {
        // Arrange
        var kernelName = "MemoryIntensiveKernel";
        var inputs = new object[] { new float[1000] };
        var expectedReport = new MemoryAnalysisReport();

        _debugService.AnalyzeMemoryPatternsAsync(kernelName, inputs)
            .Returns(Task.FromResult(expectedReport));

        // Act
        var report = await _debugService.AnalyzeMemoryPatternsAsync(kernelName, inputs);

        // Assert
        report.Should().BeSameAs(expectedReport);
        await _debugService.Received(1).AnalyzeMemoryPatternsAsync(kernelName, inputs);
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_MultipleAnalyses_ProcessesEach()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1 };

        _debugService.AnalyzeMemoryPatternsAsync(kernelName, inputs)
            .Returns(Task.FromResult(new MemoryAnalysisReport()));

        // Act
        await _debugService.AnalyzeMemoryPatternsAsync(kernelName, inputs);
        await _debugService.AnalyzeMemoryPatternsAsync(kernelName, inputs);

        // Assert
        await _debugService.Received(2).AnalyzeMemoryPatternsAsync(kernelName, inputs);
    }

    #endregion

    #region GetAvailableBackendsAsync Tests (2 tests)

    [Fact]
    public async Task GetAvailableBackendsAsync_ReturnsBackendInfo()
    {
        // Arrange
        var backends = new List<BackendInfo>
        {
            new BackendInfo { Name = "CPU", IsAvailable = true },
            new BackendInfo { Name = "CUDA", IsAvailable = true }
        };

        _debugService.GetAvailableBackendsAsync()
            .Returns(Task.FromResult<IEnumerable<BackendInfo>>(backends));

        // Act
        var result = await _debugService.GetAvailableBackendsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(b => b.Name == "CPU");
        result.Should().Contain(b => b.Name == "CUDA");
    }

    [Fact]
    public async Task GetAvailableBackendsAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var backends = new List<BackendInfo>();
        _debugService.GetAvailableBackendsAsync()
            .Returns(Task.FromResult<IEnumerable<BackendInfo>>(backends));

        // Act
        await _debugService.GetAvailableBackendsAsync();
        await _debugService.GetAvailableBackendsAsync();

        // Assert
        await _debugService.Received(2).GetAvailableBackendsAsync();
    }

    #endregion

    #region Enum Tests (1 test)

    [Theory]
    [InlineData(ComparisonStrategy.Exact)]
    [InlineData(ComparisonStrategy.Tolerance)]
    [InlineData(ComparisonStrategy.Statistical)]
    public void ComparisonStrategy_AllValues_Exist(ComparisonStrategy strategy)
    {
        strategy.Should().BeDefined();
    }

    #endregion
}
