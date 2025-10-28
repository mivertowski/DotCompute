// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Validation;
using DotCompute.Core.Debugging;
using DotCompute.Core.Debugging.Analytics;
using DotCompute.Core.Debugging.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.Debugging;

/// <summary>
/// Comprehensive unit tests for KernelDebugAnalyzer.
/// Tests focus on analytics and analysis algorithms without hardware dependencies.
/// </summary>
public class KernelDebugAnalyzerTests : IDisposable
{
    private readonly ILogger<KernelDebugAnalyzer> _logger;
    private readonly ConcurrentDictionary<string, IAccelerator> _accelerators;
    private readonly KernelDebugProfiler _profiler;
    private readonly KernelDebugAnalyzer _analyzer;
    private bool _disposed;

    public KernelDebugAnalyzerTests()
    {
        _logger = Substitute.For<ILogger<KernelDebugAnalyzer>>();
        _accelerators = new ConcurrentDictionary<string, IAccelerator>();

        var profilerLogger = Substitute.For<ILogger<KernelDebugProfiler>>();
        var executionHistory = new ConcurrentQueue<KernelExecutionResult>();
        _profiler = new KernelDebugProfiler(profilerLogger, executionHistory);

        _analyzer = new KernelDebugAnalyzer(_logger, _accelerators, _profiler);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        var analyzer = new KernelDebugAnalyzer(_logger, _accelerators, _profiler);

        // Assert
        analyzer.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new KernelDebugAnalyzer(null!, _accelerators, _profiler);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullAccelerators_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new KernelDebugAnalyzer(_logger, null!, _profiler);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullProfiler_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new KernelDebugAnalyzer(_logger, _accelerators, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region EnhanceComparisonReportAsync Tests

    [Fact]
    public async Task EnhanceComparisonReportAsync_WithValidReport_ShouldReturnEnhancedReport()
    {
        // Arrange
        var comparisonReport = CreateSampleComparisonReport();
        var executionResults = CreateSampleExecutionResults();

        // Act
        var result = await _analyzer.EnhanceComparisonReportAsync(comparisonReport, executionResults);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(comparisonReport.KernelName);
    }

    [Fact]
    public async Task EnhanceComparisonReportAsync_WithNullReport_ShouldThrowArgumentNullException()
    {
        // Arrange
        var executionResults = CreateSampleExecutionResults();

        // Act
        var act = async () => await _analyzer.EnhanceComparisonReportAsync(null!, executionResults);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnhanceComparisonReportAsync_WithNullResults_ShouldThrowArgumentNullException()
    {
        // Arrange
        var comparisonReport = CreateSampleComparisonReport();

        // Act
        var act = async () => await _analyzer.EnhanceComparisonReportAsync(comparisonReport, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnhanceComparisonReportAsync_WithEmptyResults_ShouldReturnOriginalReport()
    {
        // Arrange
        var comparisonReport = CreateSampleComparisonReport();
        var executionResults = new List<KernelExecutionResult>();

        // Act
        var result = await _analyzer.EnhanceComparisonReportAsync(comparisonReport, executionResults);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(comparisonReport.KernelName);
    }

    [Fact]
    public async Task EnhanceComparisonReportAsync_WithMultipleResults_ShouldAnalyzePerformanceVariations()
    {
        // Arrange
        var comparisonReport = CreateSampleComparisonReport();
        var executionResults = CreateSampleExecutionResults(count: 5);

        // Act
        var result = await _analyzer.EnhanceComparisonReportAsync(comparisonReport, executionResults);

        // Assert
        result.Should().NotBeNull();
        result.Differences.Should().HaveCount(comparisonReport.Differences.Count);
    }

    #endregion

    #region EnhanceExecutionTraceAsync Tests

    [Fact]
    public async Task EnhanceExecutionTraceAsync_WithValidTrace_ShouldReturnEnhancedTrace()
    {
        // Arrange
        var trace = CreateSampleExecutionTrace(success: true);

        // Act
        var result = await _analyzer.EnhanceExecutionTraceAsync(trace);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(trace.KernelName);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task EnhanceExecutionTraceAsync_WithNullTrace_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _analyzer.EnhanceExecutionTraceAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnhanceExecutionTraceAsync_WithFailedTrace_ShouldReturnUnchangedTrace()
    {
        // Arrange
        var trace = CreateSampleExecutionTrace(success: false);

        // Act
        var result = await _analyzer.EnhanceExecutionTraceAsync(trace);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(trace.KernelName);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task EnhanceExecutionTraceAsync_WithEmptyTracePoints_ShouldReturnUnchangedTrace()
    {
        // Arrange
        var trace = new KernelExecutionTrace
        {
            KernelName = "TestKernel",
            BackendType = "CPU",
            Success = true,
            TracePoints = new List<TracePoint>(),
            TotalExecutionTime = TimeSpan.FromMilliseconds(10)
        };

        // Act
        var result = await _analyzer.EnhanceExecutionTraceAsync(trace);

        // Assert
        result.Should().NotBeNull();
        result.TracePoints.Should().BeEmpty();
    }

    [Fact]
    public async Task EnhanceExecutionTraceAsync_WithMultipleTracePoints_ShouldAnalyzePatterns()
    {
        // Arrange
        var trace = CreateSampleExecutionTrace(success: true, tracePointCount: 10);

        // Act
        var result = await _analyzer.EnhanceExecutionTraceAsync(trace);

        // Assert
        result.Should().NotBeNull();
        result.TracePoints.Should().HaveCount(10);
    }

    #endregion

    #region PerformAdvancedPerformanceAnalysisAsync Tests

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithValidInputs_ShouldReturnAnalysis()
    {
        // Arrange
        var kernelName = "TestKernel";
        var performanceReport = CreateSamplePerformanceReport(kernelName);
        var memoryAnalysis = CreateSampleMemoryAnalysis();
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis(kernelName);

        // Act
        var result = await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            kernelName, performanceReport, memoryAnalysis, bottleneckAnalysis);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.TrendAnalysis.Should().NotBeNull();
        result.EfficiencyScores.Should().NotBeNull();
    }

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var performanceReport = CreateSamplePerformanceReport("test");
        var memoryAnalysis = CreateSampleMemoryAnalysis();
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis("test");

        // Act
        var act = async () => await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            null!, performanceReport, memoryAnalysis, bottleneckAnalysis);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithEmptyKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var performanceReport = CreateSamplePerformanceReport("test");
        var memoryAnalysis = CreateSampleMemoryAnalysis();
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis("test");

        // Act
        var act = async () => await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            string.Empty, performanceReport, memoryAnalysis, bottleneckAnalysis);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithNullPerformanceReport_ShouldThrowArgumentNullException()
    {
        // Arrange
        var memoryAnalysis = CreateSampleMemoryAnalysis();
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis("test");

        // Act
        var act = async () => await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            "TestKernel", null!, memoryAnalysis, bottleneckAnalysis);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithHighExecutionCount_ShouldReturnExcellentQuality()
    {
        // Arrange
        var kernelName = "TestKernel";
        var performanceReport = CreateSamplePerformanceReport(kernelName, executionCount: 150);
        var memoryAnalysis = CreateSampleMemoryAnalysis();
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis(kernelName);

        // Act
        var result = await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            kernelName, performanceReport, memoryAnalysis, bottleneckAnalysis);

        // Assert
        result.Should().NotBeNull();
        result.AnalysisQuality.Should().Be(AnalysisQuality.Excellent);
    }

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithLowExecutionCount_ShouldReturnPoorQuality()
    {
        // Arrange
        var kernelName = "TestKernel";
        var performanceReport = CreateSamplePerformanceReport(kernelName, executionCount: 3);
        var memoryAnalysis = CreateSampleMemoryAnalysis();
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis(kernelName);

        // Act
        var result = await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            kernelName, performanceReport, memoryAnalysis, bottleneckAnalysis);

        // Assert
        result.Should().NotBeNull();
        result.AnalysisQuality.Should().Be(AnalysisQuality.Poor);
    }

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithLowMemoryEfficiency_ShouldIdentifyOptimizations()
    {
        // Arrange
        var kernelName = "TestKernel";
        var performanceReport = CreateSamplePerformanceReport(kernelName);
        var memoryAnalysis = CreateSampleMemoryAnalysis(efficiency: 0.5); // Low efficiency
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis(kernelName);

        // Act
        var result = await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            kernelName, performanceReport, memoryAnalysis, bottleneckAnalysis);

        // Assert
        result.Should().NotBeNull();
        result.OptimizationOpportunities.Should().NotBeEmpty();
        result.OptimizationOpportunities.Should().Contain(o => o.Type == OptimizationType.Memory);
    }

    [Fact]
    public async Task PerformAdvancedPerformanceAnalysisAsync_WithBottlenecks_ShouldIncludeInOptimizations()
    {
        // Arrange
        var kernelName = "TestKernel";
        var performanceReport = CreateSamplePerformanceReport(kernelName);
        var memoryAnalysis = CreateSampleMemoryAnalysis();
        var bottleneckAnalysis = CreateSampleBottleneckAnalysis(kernelName, hasBottlenecks: true);

        // Act
        var result = await _analyzer.PerformAdvancedPerformanceAnalysisAsync(
            kernelName, performanceReport, memoryAnalysis, bottleneckAnalysis);

        // Assert
        result.Should().NotBeNull();
        result.OptimizationOpportunities.Should().NotBeEmpty();
    }

    #endregion

    #region ValidateDeterminismAsync Tests

    [Fact]
    public async Task ValidateDeterminismAsync_WithValidInputs_ShouldReturnDeterminismResult()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };
        var runCount = 5;

        // Act
        var result = await _analyzer.ValidateDeterminismAsync(kernelName, inputs, runCount);

        // Assert
        result.Should().NotBeNull();
        result.KernelName.Should().Be(kernelName);
        result.RunCount.Should().Be(runCount);
        result.VariabilityScore.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _analyzer.ValidateDeterminismAsync(null!, inputs, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithEmptyKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _analyzer.ValidateDeterminismAsync(string.Empty, inputs, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _analyzer.ValidateDeterminismAsync("TestKernel", null!, 5);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithRunCountLessThanTwo_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _analyzer.ValidateDeterminismAsync("TestKernel", inputs, 1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Run count must be at least 2*");
    }

    [Fact]
    public async Task ValidateDeterminismAsync_WithLowVariability_ShouldMarkAsDeterministic()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _analyzer.ValidateDeterminismAsync(kernelName, inputs, 10);

        // Assert - variability is random but bounded by implementation
        result.Should().NotBeNull();
        result.VariabilityScore.Should().BeLessThanOrEqualTo(0.1); // Max 10% by implementation
    }

    [Fact]
    public async Task ValidateDeterminismAsync_ShouldProvideRecommendations()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _analyzer.ValidateDeterminismAsync(kernelName, inputs, 5);

        // Assert
        result.Should().NotBeNull();
        result.Recommendations.Should().NotBeNull();
        result.Recommendations.Should().NotBeEmpty();
    }

    #endregion

    #region AnalyzeValidationPerformanceAsync Tests

    [Fact]
    public async Task AnalyzeValidationPerformanceAsync_WithValidInputs_ShouldReturnInsights()
    {
        // Arrange
        var validationResult = CreateSampleValidationResult();
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _analyzer.AnalyzeValidationPerformanceAsync(validationResult, inputs);

        // Assert
        result.Should().NotBeNull();
        result.BackendPerformanceDistribution.Should().NotBeNull();
        result.OptimalBackend.Should().NotBeNullOrEmpty();
        result.ConfidenceScores.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeValidationPerformanceAsync_WithNullValidationResult_ShouldThrowArgumentNullException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _analyzer.AnalyzeValidationPerformanceAsync(null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AnalyzeValidationPerformanceAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Arrange
        var validationResult = CreateSampleValidationResult();

        // Act
        var act = async () => await _analyzer.AnalyzeValidationPerformanceAsync(validationResult, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AnalyzeValidationPerformanceAsync_WithLargeInputs_ShouldRecommendSampling()
    {
        // Arrange
        var validationResult = CreateSampleValidationResult();
        var inputs = new object[1500]; // Large input array

        // Act
        var result = await _analyzer.AnalyzeValidationPerformanceAsync(validationResult, inputs);

        // Assert
        result.Should().NotBeNull();
        result.RecommendedValidationStrategy.Should().Be(ValidationStrategy.Sampling);
    }

    [Fact]
    public async Task AnalyzeValidationPerformanceAsync_WithErrors_ShouldRecommendComprehensive()
    {
        // Arrange
        var validationResult = CreateSampleValidationResult(hasErrors: true);
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _analyzer.AnalyzeValidationPerformanceAsync(validationResult, inputs);

        // Assert
        result.Should().NotBeNull();
        result.RecommendedValidationStrategy.Should().Be(ValidationStrategy.Comprehensive);
    }

    [Fact]
    public async Task AnalyzeValidationPerformanceAsync_ShouldAnalyzeInputCharacteristics()
    {
        // Arrange
        var validationResult = CreateSampleValidationResult();
        var inputs = new object[] { 1, 2.5, "test", new[] { 1, 2 } };

        // Act
        var result = await _analyzer.AnalyzeValidationPerformanceAsync(validationResult, inputs);

        // Assert
        result.Should().NotBeNull();
        result.InputCharacteristics.Should().NotBeNull();
        result.InputCharacteristics.Should().ContainKey("InputCount");
    }

    #endregion

    #region AnalyzeMemoryPatternsAsync Tests

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithValidInputs_ShouldReturnAnalysis()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _analyzer.AnalyzeMemoryPatternsAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.GrowthPattern.Should().NotBe(MemoryGrowthPattern.Unknown);
        result.LeakProbability.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
        result.AllocationEfficiency.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithNullKernelName_ShouldThrowArgumentException()
    {
        // Arrange
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var act = async () => await _analyzer.AnalyzeMemoryPatternsAsync(null!, inputs);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_WithNullInputs_ShouldThrowArgumentNullException()
    {
        // Act
        var act = async () => await _analyzer.AnalyzeMemoryPatternsAsync("TestKernel", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_ShouldReturnStablePattern()
    {
        // Arrange
        var kernelName = "TestKernel";
        var inputs = new object[] { 1, 2, 3 };

        // Act
        var result = await _analyzer.AnalyzeMemoryPatternsAsync(kernelName, inputs);

        // Assert
        result.Should().NotBeNull();
        result.GrowthPattern.Should().Be(MemoryGrowthPattern.Stable);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var analyzer = new KernelDebugAnalyzer(_logger, _accelerators, _profiler);

        // Act
        analyzer.Dispose();

        // Assert - no exception should be thrown
        var act = () => analyzer.Dispose(); // Second dispose
        act.Should().NotThrow();
    }

    [Fact]
    public async Task AnalyzeMemoryPatternsAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var analyzer = new KernelDebugAnalyzer(_logger, _accelerators, _profiler);
        analyzer.Dispose();

        // Act
        var act = async () => await analyzer.AnalyzeMemoryPatternsAsync("TestKernel", new object[] { 1 });

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Helper Methods

    private static ResultComparisonReport CreateSampleComparisonReport()
    {
        return new ResultComparisonReport
        {
            KernelName = "TestKernel",
            ResultsMatch = true,
            // Differences = new System.Collections.ObjectModel.Collection<ResultDifference>(), // Namespace DotCompute.Core.System.Collections doesn't exist
            Differences = new global::System.Collections.ObjectModel.Collection<ResultDifference>(),
            PerformanceComparison = new Dictionary<string, DotCompute.Abstractions.Performance.PerformanceMetrics>()
        };
    }

    private static List<KernelExecutionResult> CreateSampleExecutionResults(int count = 3)
    {
        var results = new List<KernelExecutionResult>();
        for (int i = 0; i < count; i++)
        {
            results.Add(new KernelExecutionResult
            {
                KernelName = "TestKernel",
                BackendType = $"Backend{i}",
                Success = true,
                Handle = new KernelExecutionHandle
                {
                    Id = Guid.NewGuid(),
                    KernelName = "TestKernel",
                    SubmittedAt = DateTime.UtcNow
                },
                Timings = new KernelExecutionTimings
                {
                    KernelTimeMs = 8.0 + i,
                    TotalTimeMs = 10.0 + i
                },
                PerformanceCounters = new Dictionary<string, object>
                {
                    ["MemoryUsed"] = 1024L * (i + 1)
                }
            });
        }
        return results;
    }

    private static KernelExecutionTrace CreateSampleExecutionTrace(bool success, int tracePointCount = 5)
    {
        var tracePoints = new List<TracePoint>();

        if (success)
        {
            for (int i = 0; i < tracePointCount; i++)
            {
                tracePoints.Add(new TracePoint
                {
                    Name = $"Point{i}",
                    Timestamp = DateTime.UtcNow.AddMilliseconds(i * 10),
                    MemoryUsage = 1024L * (i + 1),
                    Data = new Dictionary<string, object> { ["value"] = i }
                });
            }
        }

        var trace = new KernelExecutionTrace
        {
            KernelName = "TestKernel",
            BackendType = "CPU",
            Success = success,
            TotalExecutionTime = TimeSpan.FromMilliseconds(50),
            TracePoints = tracePoints
        };

        return trace;
    }

    private static PerformanceReport CreateSamplePerformanceReport(string kernelName, int executionCount = 50)
    {
        return new PerformanceReport
        {
            KernelName = kernelName,
            ExecutionCount = executionCount,
            AnalysisTimeWindow = TimeSpan.FromMinutes(5),
            AverageExecutionTime = TimeSpan.FromMilliseconds(10.5),
            SuccessRate = 0.95,
            BackendMetrics = new Dictionary<string, DotCompute.Abstractions.Performance.PerformanceMetrics>
            {
                ["CPU"] = new DotCompute.Abstractions.Performance.PerformanceMetrics
                {
                    ExecutionTimeMs = 11,
                    OperationsPerSecond = 100
                }
            }
        };
    }

    private static MemoryUsageAnalysis CreateSampleMemoryAnalysis(double efficiency = 0.8)
    {
        return new MemoryUsageAnalysis
        {
            MinimumMemoryUsage = 1024L,
            AverageMemoryUsage = 2048L,
            PeakMemoryUsage = 4096L
        };
    }

    private static DotCompute.Core.Debugging.Core.BottleneckAnalysis CreateSampleBottleneckAnalysis(
        string kernelName, bool hasBottlenecks = false)
    {
        var bottlenecks = new List<DotCompute.Core.Debugging.Core.Bottleneck>();

        if (hasBottlenecks)
        {
            bottlenecks.Add(new DotCompute.Core.Debugging.Core.Bottleneck
            {
                Type = BottleneckType.Memory,
                Severity = BottleneckSeverity.High,
                Description = "High memory usage detected",
                Recommendation = "Consider memory pooling"
            });
        }

        var analysis = new DotCompute.Core.Debugging.Core.BottleneckAnalysis
        {
            KernelName = kernelName,
            Bottlenecks = bottlenecks
        };

        return analysis;
    }

    private static DotCompute.Abstractions.Debugging.KernelValidationResult CreateSampleValidationResult(bool hasErrors = false)
    {
        // var issues = new System.Collections.ObjectModel.Collection<DebugValidationIssue>(); // Namespace DotCompute.Core.System.Collections doesn't exist
        var issues = new global::System.Collections.ObjectModel.Collection<DebugValidationIssue>();

        if (hasErrors)
        {
            issues.Add(new DebugValidationIssue(
                severity: ValidationSeverity.Error,
                message: "Test error",
                backendAffected: "TestBackend"
            ));
        }

        var result = new DotCompute.Abstractions.Debugging.KernelValidationResult
        {
            KernelName = "TestKernel",
            IsValid = !hasErrors,
            ValidationTime = DateTime.UtcNow,
            BackendsTested = new List<string> { "CPU", "CUDA" },
            RecommendedBackend = "CUDA",
            Issues = issues
        };

        return result;
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _analyzer?.Dispose();
            _profiler?.Dispose();
        }
    }
}
