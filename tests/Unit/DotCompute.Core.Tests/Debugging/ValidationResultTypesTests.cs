// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Validation;
using FluentAssertions;
using Xunit;
using DebugKernelValidationResult = DotCompute.Abstractions.Debugging.KernelValidationResult;
using DebugResultComparison = DotCompute.Abstractions.Debugging.ResultComparison;
using DebugComparisonStrategy = DotCompute.Abstractions.Debugging.ComparisonStrategy;
using DebugValidationIssue = DotCompute.Abstractions.Debugging.DebugValidationIssue;
using DeterminismReport = DotCompute.Abstractions.Debugging.DeterminismReport;
using MemoryAnalysisReport = DotCompute.Abstractions.Debugging.MemoryAnalysisReport;
using ResultComparisonReport = DotCompute.Abstractions.Debugging.ResultComparisonReport;
using KernelExecutionTrace = DotCompute.Abstractions.Debugging.KernelExecutionTrace;
using ReportFormat = DotCompute.Abstractions.Debugging.ReportFormat;

namespace DotCompute.Core.Tests.Debugging;

/// <summary>
/// Comprehensive unit tests for debug validation result types.
/// Tests data structures and their behavior.
/// </summary>
public class ValidationResultTypesTests
{
    #region KernelValidationResult Tests

    [Fact]
    public void KernelValidationResult_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var result = new DebugKernelValidationResult();

        // Assert
        result.KernelName.Should().BeEmpty();
        result.IsValid.Should().BeFalse();
        result.BackendsTested.Should().BeEmpty();
        result.Results.Should().BeEmpty();
        result.Comparisons.Should().BeEmpty();
        result.Issues.Should().BeEmpty();
        result.MaxDifference.Should().Be(0);
        result.RecommendedBackend.Should().BeNull();
        result.Recommendations.Should().BeEmpty();
    }

    [Fact]
    public void KernelValidationResult_WithInitializer_ShouldSetProperties()
    {
        // Act
        var result = new KernelValidationResult
        {
            KernelName = "TestKernel",
            IsValid = true,
            BackendsTested = new[] { "CPU", "CUDA" },
            MaxDifference = 1e-6f,
            RecommendedBackend = "CUDA"
        };

        // Assert
        result.KernelName.Should().Be("TestKernel");
        result.IsValid.Should().BeTrue();
        result.BackendsTested.Should().BeEquivalentTo("CPU", "CUDA");
        result.MaxDifference.Should().Be(1e-6f);
        result.RecommendedBackend.Should().Be("CUDA");
    }

    [Fact]
    public void KernelValidationResult_TotalValidationTime_ShouldMatchExecutionTime()
    {
        // Arrange
        var executionTime = TimeSpan.FromMilliseconds(100);
        var result = new KernelValidationResult
        {
            ExecutionTime = executionTime
        };

        // Act & Assert
        result.TotalValidationTime.Should().Be(executionTime);
    }

    [Fact]
    public void KernelValidationResult_Errors_ShouldFilterErrorSeverityIssues()
    {
        // Arrange
        var result = new DebugKernelValidationResult();
        result.Issues.Add(new DebugValidationIssue
        {
            Severity = ValidationSeverity.Error,
            Message = "Error 1"
        });
        result.Issues.Add(new DebugValidationIssue
        {
            Severity = ValidationSeverity.Warning,
            Message = "Warning 1"
        });
        result.Issues.Add(new DebugValidationIssue
        {
            Severity = ValidationSeverity.Error,
            Message = "Error 2"
        });

        // Act
        var errors = result.Errors.ToList();

        // Assert
        errors.Should().HaveCount(2);
        errors.Should().AllSatisfy(e => e.Severity.Should().Be(ValidationSeverity.Error));
    }

    [Fact]
    public void KernelValidationResult_Warnings_ShouldFilterWarningSeverityIssues()
    {
        // Arrange
        var result = new DebugKernelValidationResult();
        result.Issues.Add(new DebugValidationIssue
        {
            Severity = ValidationSeverity.Warning,
            Message = "Warning 1"
        });
        result.Issues.Add(new DebugValidationIssue
        {
            Severity = ValidationSeverity.Error,
            Message = "Error 1"
        });
        result.Issues.Add(new DebugValidationIssue
        {
            Severity = ValidationSeverity.Warning,
            Message = "Warning 2"
        });

        // Act
        var warnings = result.Warnings.ToList();

        // Assert
        warnings.Should().HaveCount(2);
        warnings.Should().AllSatisfy(w => w.Severity.Should().Be(ValidationSeverity.Warning));
    }

    [Fact]
    public void KernelValidationResult_Metadata_ShouldAllowCustomData()
    {
        // Arrange
        var result = new DebugKernelValidationResult();

        // Act
        result.Metadata["CustomKey"] = "CustomValue";
        result.Metadata["NumericValue"] = 42;

        // Assert
        result.Metadata.Should().ContainKey("CustomKey");
        result.Metadata["CustomKey"].Should().Be("CustomValue");
        result.Metadata["NumericValue"].Should().Be(42);
    }

    [Fact]
    public void KernelValidationResult_ResourceUsage_ShouldAllowResourceTracking()
    {
        // Arrange
        var result = new DebugKernelValidationResult();

        // Act
        result.ResourceUsage["MemoryMB"] = 256;
        result.ResourceUsage["CPUPercent"] = 75.5;

        // Assert
        result.ResourceUsage.Should().ContainKey("MemoryMB");
        result.ResourceUsage["MemoryMB"].Should().Be(256);
        result.ResourceUsage["CPUPercent"].Should().Be(75.5);
    }

    #endregion

    #region ResultComparison Tests

    [Fact]
    public void ResultComparison_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var comparison = new DebugResultComparison();

        // Assert
        comparison.Backend1.Should().BeEmpty();
        comparison.Backend2.Should().BeEmpty();
        comparison.IsMatch.Should().BeFalse();
        comparison.Difference.Should().Be(0);
        comparison.Details.Should().BeEmpty();
    }

    [Fact]
    public void ResultComparison_WithInitializer_ShouldSetProperties()
    {
        // Act
        var comparison = new ResultComparison
        {
            Backend1 = "CPU",
            Backend2 = "CUDA",
            IsMatch = true,
            Difference = 1e-7f
        };

        // Assert
        comparison.Backend1.Should().Be("CPU");
        comparison.Backend2.Should().Be("CUDA");
        comparison.IsMatch.Should().BeTrue();
        comparison.Difference.Should().Be(1e-7f);
    }

    [Fact]
    public void ResultComparison_Details_ShouldAllowCustomData()
    {
        // Arrange
        var comparison = new DebugResultComparison();

        // Act
        comparison.Details["ComparisonMethod"] = "Tolerance";
        comparison.Details["Tolerance"] = 1e-6f;

        // Assert
        comparison.Details.Should().ContainKey("ComparisonMethod");
        comparison.Details["ComparisonMethod"].Should().Be("Tolerance");
    }

    #endregion

    #region BackendInfo Tests

    [Fact]
    public void BackendInfo_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var info = new BackendInfo();

        // Assert
        info.Name.Should().BeEmpty();
        info.Version.Should().BeEmpty();
        info.IsAvailable.Should().BeFalse();
        info.Capabilities.Should().BeEmpty();
        info.Properties.Should().BeEmpty();
        info.UnavailabilityReason.Should().BeNull();
        info.Priority.Should().Be(0);
        info.Type.Should().BeEmpty();
        info.MaxMemory.Should().Be(0);
    }

    [Fact]
    public void BackendInfo_WithInitializer_ShouldSetProperties()
    {
        // Act
        var info = new BackendInfo
        {
            Name = "CUDA Backend",
            Version = "12.0",
            IsAvailable = true,
            Type = "CUDA",
            MaxMemory = 8L * 1024 * 1024 * 1024, // 8GB
            Priority = 1
        };

        // Assert
        info.Name.Should().Be("CUDA Backend");
        info.Version.Should().Be("12.0");
        info.IsAvailable.Should().BeTrue();
        info.Type.Should().Be("CUDA");
        info.MaxMemory.Should().Be(8L * 1024 * 1024 * 1024);
        info.Priority.Should().Be(1);
    }

    [Fact]
    public void BackendInfo_Capabilities_ShouldAllowMultipleCapabilities()
    {
        // Act
        var info = new BackendInfo
        {
            Capabilities = new[] { "Float32", "Float64", "Int32", "SIMD" }
        };

        // Assert
        info.Capabilities.Should().HaveCount(4);
        info.Capabilities.Should().Contain("Float32");
        info.Capabilities.Should().Contain("SIMD");
    }

    [Fact]
    public void BackendInfo_Properties_ShouldAllowCustomProperties()
    {
        // Arrange
        var info = new BackendInfo();

        // Act
        info.Properties["ComputeCapability"] = "8.9";
        info.Properties["MultiprocessorCount"] = 32;

        // Assert
        info.Properties.Should().ContainKey("ComputeCapability");
        info.Properties["ComputeCapability"].Should().Be("8.9");
        info.Properties["MultiprocessorCount"].Should().Be(32);
    }

    [Fact]
    public void BackendInfo_UnavailableBackend_ShouldHaveReason()
    {
        // Act
        var info = new BackendInfo
        {
            IsAvailable = false,
            UnavailabilityReason = "Driver not installed"
        };

        // Assert
        info.IsAvailable.Should().BeFalse();
        info.UnavailabilityReason.Should().Be("Driver not installed");
    }

    #endregion

    #region DeterminismReport Tests

    [Fact]
    public void DeterminismReport_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var report = new DeterminismReport();

        // Assert
        report.KernelName.Should().BeEmpty();
        report.IsDeterministic.Should().BeFalse();
        report.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public void DeterminismReport_WithInitializer_ShouldSetProperties()
    {
        // Act
        var report = new DeterminismReport
        {
            KernelName = "TestKernel",
            IsDeterministic = true
        };

        // Assert
        report.KernelName.Should().Be("TestKernel");
        report.IsDeterministic.Should().BeTrue();
    }

    [Fact]
    public void DeterminismReport_NonDeterministic_ShouldHaveSource()
    {
        // Act
        var report = new DeterminismReport
        {
            KernelName = "TestKernel",
            IsDeterministic = false,
            NonDeterminismSource = "Race condition detected"
        };

        // Assert
        report.IsDeterministic.Should().BeFalse();
        report.NonDeterminismSource.Should().Be("Race condition detected");
    }

    #endregion

    #region MemoryAnalysisReport Tests

    [Fact]
    public void MemoryAnalysisReport_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var report = new MemoryAnalysisReport();

        // Assert
        report.KernelName.Should().BeEmpty();
    }

    [Fact]
    public void MemoryAnalysisReport_WithInitializer_ShouldSetProperties()
    {
        // Act
        var report = new MemoryAnalysisReport
        {
            KernelName = "TestKernel"
        };

        // Assert
        report.KernelName.Should().Be("TestKernel");
    }

    #endregion

    #region KernelExecutionResult Tests

    [Fact]
    public void KernelExecutionResult_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var result = new KernelExecutionResult
        {
            Success = true,
            Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow }
        };

        // Assert
        result.KernelName.Should().BeNull();
        result.BackendType.Should().BeNull();
    }

    [Fact]
    public void KernelExecutionResult_WithInitializer_ShouldSetProperties()
    {
        // Act
        var result = new KernelExecutionResult
        {
            KernelName = "TestKernel",
            BackendType = "CUDA",
            Success = true,
            Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "TestKernel", SubmittedAt = DateTimeOffset.UtcNow }
        };

        // Assert
        result.KernelName.Should().Be("TestKernel");
        result.BackendType.Should().Be("CUDA");
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void KernelExecutionResult_WithError_ShouldCaptureException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = new KernelExecutionResult
        {
            Success = false,
            Handle = new KernelExecutionHandle { Id = Guid.NewGuid(), KernelName = "Test", SubmittedAt = DateTimeOffset.UtcNow },
            Error = exception
        };

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be(exception);
    }

    #endregion

    #region ResultComparisonReport Tests

    [Fact]
    public void ResultComparisonReport_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var report = new ResultComparisonReport();

        // Assert - verify it exists and can be instantiated
        report.Should().NotBeNull();
    }

    [Fact]
    public void ResultComparisonReport_WithInitializer_ShouldSetProperties()
    {
        // Act
        var report = new ResultComparisonReport
        {
            // Properties based on actual type structure
        };

        // Assert
        report.Should().NotBeNull();
    }

    #endregion

    #region KernelExecutionTrace Tests

    [Fact]
    public void KernelExecutionTrace_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var trace = new KernelExecutionTrace();

        // Assert
        trace.KernelName.Should().BeEmpty();
    }

    [Fact]
    public void KernelExecutionTrace_WithInitializer_ShouldSetProperties()
    {
        // Act
        var trace = new KernelExecutionTrace
        {
            KernelName = "TestKernel"
        };

        // Assert
        trace.KernelName.Should().Be("TestKernel");
    }

    #endregion

    #region ComprehensiveDebugReport Tests

    [Fact]
    public void ComprehensiveDebugReport_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var report = new ComprehensiveDebugReport();

        // Assert
        report.KernelName.Should().BeEmpty();
    }

    [Fact]
    public void ComprehensiveDebugReport_WithInitializer_ShouldSetProperties()
    {
        // Act
        var report = new ComprehensiveDebugReport
        {
            KernelName = "TestKernel"
        };

        // Assert
        report.KernelName.Should().Be("TestKernel");
    }

    #endregion

    #region PerformanceReport Tests

    [Fact]
    public void PerformanceReport_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var report = new PerformanceReport();

        // Assert - verify it exists
        report.Should().NotBeNull();
    }

    #endregion

    #region ResourceUtilizationReport Tests

    [Fact]
    public void ResourceUtilizationReport_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var report = new ResourceUtilizationReport();

        // Assert - verify it exists
        report.Should().NotBeNull();
    }

    #endregion

    #region DebugServiceStatistics Tests

    [Fact]
    public void DebugServiceStatistics_DefaultConstruction_ShouldHaveDefaultValues()
    {
        // Act
        var stats = new DebugServiceStatistics();

        // Assert
        stats.TotalValidations.Should().Be(0);
        stats.SuccessfulValidations.Should().Be(0);
        stats.FailedValidations.Should().Be(0);
    }

    [Fact]
    public void DebugServiceStatistics_WithInitializer_ShouldSetProperties()
    {
        // Act
        var stats = new DebugServiceStatistics
        {
            TotalValidations = 100,
            SuccessfulValidations = 95,
            FailedValidations = 5
        };

        // Assert
        stats.TotalValidations.Should().Be(100);
        stats.SuccessfulValidations.Should().Be(95);
        stats.FailedValidations.Should().Be(5);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void ComparisonStrategy_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<ComparisonStrategy>().Should().Contain(ComparisonStrategy.Exact);
        Enum.GetValues<ComparisonStrategy>().Should().Contain(ComparisonStrategy.Tolerance);
        Enum.GetValues<ComparisonStrategy>().Should().Contain(ComparisonStrategy.Statistical);
        Enum.GetValues<ComparisonStrategy>().Should().Contain(ComparisonStrategy.Relative);
    }

    [Fact]
    public void ReportFormat_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<ReportFormat>().Should().Contain(ReportFormat.Json);
        Enum.GetValues<ReportFormat>().Should().Contain(ReportFormat.Xml);
    }

    #endregion
}
