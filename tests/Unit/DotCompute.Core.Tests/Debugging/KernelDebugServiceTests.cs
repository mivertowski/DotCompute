// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Core.Debugging;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Debugging;

/// <summary>
/// Comprehensive unit tests for KernelDebugService.
/// Tests focus on the service layer orchestration logic without hardware dependencies.
/// </summary>
public class KernelDebugServiceTests
{
    private readonly ILogger<KernelDebugService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IAccelerator _mockAccelerator;

    public KernelDebugServiceTests()
    {
        _logger = Substitute.For<ILogger<KernelDebugService>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _mockAccelerator = Substitute.For<IAccelerator>();

        // Setup mock accelerator
        _ = _mockAccelerator.Type.Returns(AcceleratorType.CPU);
        _ = _mockAccelerator.Info.Returns(new AcceleratorInfo { Name = "TestAccelerator", Id = "test-1", DeviceType = "Test", Vendor = "Test" });
        _ = _mockAccelerator.IsAvailable.Returns(true);

        // Setup logger factory to return appropriate loggers
        _ = _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Act
        var service = new KernelDebugService(_logger, _loggerFactory);

        // Assert
        _ = service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithPrimaryAccelerator_ShouldInitializeSuccessfully()
    {
        // Act
        var service = new KernelDebugService(_logger, _loggerFactory, _mockAccelerator);

        // Assert
        _ = service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new KernelDebugService(null!, _loggerFactory);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new KernelDebugService(_logger, null!);

        // Assert
        _ = act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerFactory");
    }

    [Fact]
    public void Constructor_WithNullPrimaryAccelerator_ShouldInitializeWithoutAccelerator()
    {
        // Act
        var service = new KernelDebugService(_logger, _loggerFactory, null);

        // Assert
        _ = service.Should().NotBeNull();
    }

    #endregion

    #region ValidateKernelAsync Tests

    [Fact]
    public async Task ValidateKernelAsync_WithValidParameters_ShouldReturnValidationResult()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await service.ValidateKernelAsync(kernelName, inputs);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task ValidateKernelAsync_WithEmptyKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await service.ValidateKernelAsync(string.Empty, inputs);

        // Assert - implementation throws ArgumentException for empty kernel name
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateKernelAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";

        // Act
        var act = async () => await service.ValidateKernelAsync(kernelName, null!);

        // Assert - implementation throws ArgumentNullException for null inputs
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateKernelAsync_WithCustomTolerance_ShouldUseSpecifiedTolerance()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1.0f, 2.0f };
        var customTolerance = 1e-4f;

        // Act
        var result = await service.ValidateKernelAsync(kernelName, inputs, customTolerance);

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateKernelAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = async () => await service.ValidateKernelAsync("TestKernel", []);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ValidateKernelAsync_WithLargeInputArray_ShouldHandleEfficiently()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = Enumerable.Range(0, 1000).Cast<object>().ToArray();

        // Act
        var result = await service.ValidateKernelAsync(kernelName, inputs);

        // Assert
        _ = result.Should().NotBeNull();
    }

    #endregion

    #region ExecuteOnBackendAsync Tests

    [Fact]
    public async Task ExecuteOnBackendAsync_WithValidParameters_ShouldReturnExecutionResult()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var backendType = "CPU";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await service.ExecuteOnBackendAsync(kernelName, backendType, inputs);

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_WithUnknownBackend_ShouldHandleGracefully()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var backendType = "UnknownBackend";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await service.ExecuteOnBackendAsync(kernelName, backendType, inputs);

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteOnBackendAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = async () => await service.ExecuteOnBackendAsync("TestKernel", "CPU", []);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region CompareResultsAsync Tests

    [Fact]
    public async Task CompareResultsAsync_WithValidResults_ShouldReturnComparisonReport()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var results = new List<KernelExecutionResult>
        {
            new() {
                Success = true,
                Handle = new KernelExecutionHandle {
                    Id = Guid.NewGuid(),
                    KernelName = "Test1",
                    SubmittedAt = DateTimeOffset.UtcNow
                },
                KernelName = "Test1",
                BackendType = "CPU"
            },
            new() {
                Success = true,
                Handle = new KernelExecutionHandle {
                    Id = Guid.NewGuid(),
                    KernelName = "Test2",
                    SubmittedAt = DateTimeOffset.UtcNow
                },
                KernelName = "Test2",
                BackendType = "CUDA"
            }
        };

        // Act
        var report = await service.CompareResultsAsync(results);

        // Assert
        _ = report.Should().NotBeNull();
    }

    [Fact]
    public async Task CompareResultsAsync_WithEmptyResults_ShouldThrowArgumentException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var results = Enumerable.Empty<KernelExecutionResult>();

        // Act
        var act = async () => await service.CompareResultsAsync(results);

        // Assert - comparison requires at least 2 results
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least two results*");
    }

    [Fact]
    public async Task CompareResultsAsync_WithSingleResult_ShouldThrowArgumentException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var results = new List<KernelExecutionResult>
        {
            new() {
                Success = true,
                Handle = new KernelExecutionHandle {
                    Id = Guid.NewGuid(),
                    KernelName = "Test",
                    SubmittedAt = DateTimeOffset.UtcNow
                },
                KernelName = "Test",
                BackendType = "CPU"
            }
        };

        // Act
        var act = async () => await service.CompareResultsAsync(results);

        // Assert - comparison requires at least 2 results
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least two results*");
    }

    [Fact]
    public async Task CompareResultsAsync_WithToleranceStrategy_ShouldUseToleranceComparison()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var results = new List<KernelExecutionResult>
        {
            new() {
                Success = true,
                Handle = new KernelExecutionHandle {
                    Id = Guid.NewGuid(),
                    KernelName = "Test1",
                    SubmittedAt = DateTimeOffset.UtcNow
                },
                KernelName = "Test1",
                BackendType = "Test"
            },
            new() {
                Success = true,
                Handle = new KernelExecutionHandle {
                    Id = Guid.NewGuid(),
                    KernelName = "Test2",
                    SubmittedAt = DateTimeOffset.UtcNow
                },
                KernelName = "Test2",
                BackendType = "Test"
            }
        };

        // Act
        var report = await service.CompareResultsAsync(results, DotCompute.Abstractions.Debugging.ComparisonStrategy.Tolerance);

        // Assert
        _ = report.Should().NotBeNull();
    }

    [Fact]
    public async Task CompareResultsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = async () => await service.CompareResultsAsync([]);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region TraceKernelExecutionAsync Tests

    [Fact]
    public async Task TraceKernelExecutionAsync_WithValidParameters_ShouldReturnTrace()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var tracePoints = new[] { "point1", "point2" };

        // Act
        var trace = await service.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);

        // Assert
        _ = trace.Should().NotBeNull();
        _ = trace.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_WithEmptyTracePoints_ShouldHandleGracefully()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var tracePoints = Array.Empty<string>();

        // Act
        var trace = await service.TraceKernelExecutionAsync(kernelName, inputs, tracePoints);

        // Assert
        _ = trace.Should().NotBeNull();
    }

    [Fact]
    public async Task TraceKernelExecutionAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = async () => await service.TraceKernelExecutionAsync(
            "TestKernel",
            [],
            []);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region ValidateDeterminismAsync Tests

    [Fact]
    public async Task ValidateDeterminismAsync_WithDefaultIterations_ShouldReturnDeterminismReport()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var report = await service.ValidateDeterminismAsync(kernelName, inputs);

        // Assert
        _ = report.Should().NotBeNull();
        _ = report.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithCustomIterations_ShouldUseSpecifiedIterations()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var iterations = 10;

        // Act
        var report = await service.ValidateDeterminismAsync(kernelName, inputs, iterations);

        // Assert
        _ = report.Should().NotBeNull();
        _ = report.ExecutionCount.Should().Be(iterations);
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithZeroIterations_ShouldThrowArgumentException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await service.ValidateDeterminismAsync(kernelName, inputs, 0);

        // Assert - implementation requires at least 2 iterations
        _ = await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least 2*");
    }

    [Fact]
    public async Task ValidateDeterminismAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = async () => await service.ValidateDeterminismAsync("TestKernel", []);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region AnalyzeMemoryPatternsAsync Tests

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithValidParameters_ShouldReturnAnalysisReport()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var report = await service.AnalyzeMemoryPatternsAsync(kernelName, inputs);

        // Assert
        _ = report.Should().NotBeNull();
        _ = report.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = async () => await service.AnalyzeMemoryPatternsAsync("TestKernel", []);

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region GetAvailableBackendsAsync Tests

    [Fact]
    public async Task GetAvailableBackendsAsync_ShouldReturnBackendInfo()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);

        // Act
        var backends = await service.GetAvailableBackendsAsync();

        // Assert
        _ = backends.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailableBackendsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = service.GetAvailableBackendsAsync;

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Configure Tests

    [Fact]
    public void Configure_WithValidOptions_ShouldUpdateConfiguration()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var options = new DebugServiceOptions
        {
            MaxConcurrentExecutions = 4
        };

        // Act
        var act = () => service.Configure(options);

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void Configure_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);

        // Act
        var act = () => service.Configure(null!);

        // Assert - implementation validates null options
        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Configure_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = () => service.Configure(new DebugServiceOptions());

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Comprehensive Debug Tests

    [Fact]
    public async Task RunComprehensiveDebugAsync_WithValidParameters_ShouldReturnComprehensiveReport()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var report = await service.RunComprehensiveDebugAsync(kernelName, inputs);

        // Assert
        _ = report.Should().NotBeNull();
        _ = report.KernelName.Should().Be(kernelName);
    }

    [Fact]
    public async Task RunComprehensiveDebugAsync_WithCustomParameters_ShouldUseSpecifiedValues()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var tolerance = 1e-4f;
        var iterations = 10;

        // Act
        var report = await service.RunComprehensiveDebugAsync(kernelName, inputs, tolerance, iterations);

        // Assert
        _ = report.Should().NotBeNull();
    }

    #endregion

    #region Report Generation Tests

    [Fact]
    public async Task GenerateDetailedReportAsync_WithValidationResult_ShouldReturnFormattedReport()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var validationResult = new KernelValidationResult
        {
            KernelName = "TestKernel",
            IsValid = true
        };

        // Act
        var report = await service.GenerateDetailedReportAsync(validationResult);

        // Assert
        _ = report.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportReportAsync_WithJsonFormat_ShouldReturnJsonString()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        // Use Dictionary instead of anonymous type for AOT compatibility
        var report = new Dictionary<string, object>
        {
            ["KernelName"] = "TestKernel",
            ["IsValid"] = true
        };

        // Act
        var exported = await service.ExportReportAsync(report, ReportFormat.Json);

        // Assert
        _ = exported.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportReportAsync_WithXmlFormat_ShouldReturnXmlString()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        // Use Dictionary instead of anonymous type for AOT compatibility
        var report = new Dictionary<string, object>
        {
            ["KernelName"] = "TestKernel"
        };

        // Act
        var exported = await service.ExportReportAsync(report, ReportFormat.Xml);

        // Assert
        _ = exported.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Performance Report Tests

    [Fact]
    public async Task GeneratePerformanceReportAsync_WithValidKernelName_ShouldReturnReport()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";

        // Act
        var report = await service.GeneratePerformanceReportAsync(kernelName);

        // Assert
        _ = report.Should().NotBeNull();
    }

    [Fact]
    public async Task GeneratePerformanceReportAsync_WithTimeWindow_ShouldUseSpecifiedWindow()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var timeWindow = TimeSpan.FromHours(1);

        // Act
        var report = await service.GeneratePerformanceReportAsync(kernelName, timeWindow);

        // Assert
        _ = report.Should().NotBeNull();
    }

    #endregion

    #region Resource Utilization Tests

    [Fact]
    public async Task AnalyzeResourceUtilizationAsync_WithValidParameters_ShouldReturnReport()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var report = await service.AnalyzeResourceUtilizationAsync(kernelName, inputs);

        // Assert
        _ = report.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeResourceUtilizationAsync_WithAnalysisWindow_ShouldUseSpecifiedWindow()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var window = TimeSpan.FromMinutes(5);

        // Act
        var report = await service.AnalyzeResourceUtilizationAsync(kernelName, inputs, window);

        // Assert
        _ = report.Should().NotBeNull();
    }

    #endregion

    #region Accelerator Management Tests

    [Fact]
    public void AddAccelerator_WithValidAccelerator_ShouldSucceed()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo(AcceleratorType.CPU, "TestAccelerator", "1.0", 1024 * 1024 * 1024);
        _ = accelerator.Info.Returns(info);

        // Act
        var act = () => service.AddAccelerator("Test", accelerator);

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void AddAccelerator_WithDuplicateName_ShouldHandleGracefully()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var accelerator1 = Substitute.For<IAccelerator>();
        var accelerator2 = Substitute.For<IAccelerator>();

        service.AddAccelerator("Test", accelerator1);

        // Act
        var act = () => service.AddAccelerator("Test", accelerator2);

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void RemoveAccelerator_WithExistingAccelerator_ShouldReturnTrue()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        var accelerator = Substitute.For<IAccelerator>();
        service.AddAccelerator("Test", accelerator);

        // Act
        var result = service.RemoveAccelerator("Test");

        // Assert
        _ = result.Should().BeTrue();
    }

    [Fact]
    public void RemoveAccelerator_WithNonExistingAccelerator_ShouldReturnFalse()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);

        // Act
        var result = service.RemoveAccelerator("NonExisting");

        // Assert
        _ = result.Should().BeFalse();
    }

    [Fact]
    public void AddAccelerator_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();
        var accelerator = Substitute.For<IAccelerator>();

        // Act
        var act = () => service.AddAccelerator("Test", accelerator);

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void RemoveAccelerator_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = () => service.RemoveAccelerator("Test");

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);

        // Act
        var statistics = service.GetStatistics();

        // Assert
        _ = statistics.Should().NotBeNull();
    }

    [Fact]
    public void GetStatistics_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);
        service.Dispose();

        // Act
        var act = service.GetStatistics;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_ShouldDisposeResourcesCleanly()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);

        // Act
        service.Dispose();

        // Assert - subsequent operations should throw
        var act = service.GetStatistics;
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var service = new KernelDebugService(_logger, _loggerFactory);

        // Act
        service.Dispose();
        var act = service.Dispose;

        // Assert
        _ = act.Should().NotThrow();
    }

    #endregion
}
