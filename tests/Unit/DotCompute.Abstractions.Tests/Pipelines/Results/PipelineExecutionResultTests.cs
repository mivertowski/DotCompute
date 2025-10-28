// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Models.Pipelines;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Abstractions.Pipelines.Results;

namespace DotCompute.Abstractions.Tests.Pipelines.Results;

/// <summary>
/// Comprehensive tests for PipelineExecutionResult (404 lines).
/// Target: 20+ tests for execution results, success/failure paths.
/// </summary>
public class PipelineExecutionResultTests
{
    [Fact]
    public void PipelineExecutionResult_RequiredProperties_MustBeSet()
    {
        var result = new PipelineExecutionResult
        {
            Success = true,
            PipelineId = "pipeline-1",
            PipelineName = "TestPipeline",
            Outputs = new Dictionary<string, object>(),
            Metrics = new PipelineExecutionMetrics
            {
                ExecutionId = "exec-1",
                PipelineName = "TestPipeline",
                StartTime = DateTimeOffset.UtcNow
            },
            StageResults = new List<StageExecutionResult>()
        };

        result.Success.Should().BeTrue();
        result.PipelineId.Should().Be("pipeline-1");
        result.PipelineName.Should().Be("TestPipeline");
        result.Outputs.Should().NotBeNull();
        result.Metrics.Should().NotBeNull();
        result.StageResults.Should().NotBeNull();
    }

    [Fact]
    public void PipelineExecutionResult_TotalExecutionTime_CalculatedCorrectly()
    {
        var start = DateTime.UtcNow;
        var end = start.AddSeconds(10);

        var result = new PipelineExecutionResult
        {
            Success = true,
            PipelineId = "pipeline-1",
            PipelineName = "Test",
            Outputs = new Dictionary<string, object>(),
            Metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow },
            StageResults = Array.Empty<StageExecutionResult>(),
            StartTime = start,
            EndTime = end
        };

        result.TotalExecutionTime.Should().NotBeNull();
        result.TotalExecutionTime!.Value.Should().BeCloseTo(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void PipelineExecutionResult_TotalExecutionTime_WithoutEndTime_ReturnsNull()
    {
        var result = new PipelineExecutionResult
        {
            Success = true,
            PipelineId = "pipeline-1",
            PipelineName = "Test",
            Outputs = new Dictionary<string, object>(),
            Metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow },
            StageResults = Array.Empty<StageExecutionResult>(),
            StartTime = DateTime.UtcNow
        };

        result.TotalExecutionTime.Should().BeNull();
    }

    [Fact]
    public void PipelineExecutionResult_SuccessfulStages_CountsCorrectly()
    {
        var stageResults = new List<StageExecutionResult>
        {
            new() { Success = true, StageId = "stage-1" },
            new() { Success = true, StageId = "stage-2" },
            new() { Success = false, StageId = "stage-3" }
        };

        var result = new PipelineExecutionResult
        {
            Success = true,
            PipelineId = "pipeline-1",
            PipelineName = "Test",
            Outputs = new Dictionary<string, object>(),
            Metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow },
            StageResults = stageResults
        };

        result.SuccessfulStages.Should().Be(2);
        result.FailedStages.Should().Be(1);
        result.TotalStages.Should().Be(3);
    }

    [Fact]
    public void PipelineExecutionResult_HasSkippedStages_DetectsSkips()
    {
        var stageResults = new List<StageExecutionResult>
        {
            new() { Success = true, Skipped = false, StageId = "stage-1" },
            new() { Success = true, Skipped = true, StageId = "stage-2" }
        };

        var result = new PipelineExecutionResult
        {
            Success = true,
            PipelineId = "pipeline-1",
            PipelineName = "Test",
            Outputs = new Dictionary<string, object>(),
            Metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow },
            StageResults = stageResults
        };

        result.HasSkippedStages.Should().BeTrue();
    }

    [Fact]
    public void PipelineExecutionResult_CreateSuccess_ReturnsSuccessfulResult()
    {
        var outputs = new Dictionary<string, object> { ["result"] = 42 };
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-1",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };
        var stageResults = new List<StageExecutionResult>();

        var result = PipelineExecutionResult.CreateSuccess(
            "pipeline-1",
            "TestPipeline",
            outputs,
            metrics,
            stageResults
        );

        result.Success.Should().BeTrue();
        result.PipelineId.Should().Be("pipeline-1");
        result.PipelineName.Should().Be("TestPipeline");
        result.Outputs.Should().BeSameAs(outputs);
        result.Metrics.Should().BeSameAs(metrics);
        result.Errors.Should().BeNull();
    }

    [Fact]
    public void PipelineExecutionResult_CreateFailure_WithErrors_ReturnsFailedResult()
    {
        var errors = new List<PipelineError>
        {
            new()
            {
                ErrorType = PipelineErrorType.ExecutionError,
                Message = "Test error",
                Timestamp = DateTime.UtcNow,
                StageId = "stage-1"
            }
        };
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-1",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };
        var stageResults = new List<StageExecutionResult>();

        var result = PipelineExecutionResult.CreateFailure(
            "pipeline-1",
            "TestPipeline",
            errors,
            metrics,
            stageResults
        );

        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors![0].Message.Should().Be("Test error");
        result.Outputs.Should().BeEmpty();
    }

    [Fact]
    public void PipelineExecutionResult_CreateFailure_WithException_ConvertsToError()
    {
        var exception = new InvalidOperationException("Test exception");
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-1",
            PipelineName = "Test",
            StartTime = DateTimeOffset.UtcNow
        };
        var stageResults = new List<StageExecutionResult>();

        var result = PipelineExecutionResult.CreateFailure(
            "pipeline-1",
            "TestPipeline",
            exception,
            metrics,
            stageResults
        );

        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors![0].Message.Should().Be("Test exception");
        result.Errors[0].Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public void PipelineExecutionResult_GetSummaryReport_GeneratesCorrectFormat()
    {
        var outputs = new Dictionary<string, object> { ["result"] = 42 };
        var metrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec-1",
            PipelineName = "TestPipeline",
            StartTime = DateTimeOffset.UtcNow
        };
        var stageResults = new List<StageExecutionResult>
        {
            new() { Success = true, StageId = "stage-1" },
            new() { Success = true, StageId = "stage-2" }
        };

        var result = PipelineExecutionResult.CreateSuccess(
            "pipeline-1",
            "TestPipeline",
            outputs,
            metrics,
            stageResults,
            DateTime.UtcNow.AddSeconds(-5),
            DateTime.UtcNow
        );

        var summary = result.GetSummaryReport();

        summary.Should().Contain("TestPipeline");
        summary.Should().Contain("SUCCESS");
        summary.Should().Contain("2/2 successful");
    }

    [Fact]
    public void PipelineExecutionResult_GetFirstError_ReturnsFirstError()
    {
        var errors = new List<PipelineError>
        {
            new() { ErrorType = PipelineErrorType.ExecutionError, Message = "Error 1", Timestamp = DateTime.UtcNow, StageId = "1" },
            new() { ErrorType = PipelineErrorType.ValidationError, Message = "Error 2", Timestamp = DateTime.UtcNow, StageId = "2" }
        };
        var metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow };
        var stageResults = new List<StageExecutionResult>();

        var result = PipelineExecutionResult.CreateFailure("1", "Test", errors, metrics, stageResults);

        var firstError = result.GetFirstError();
        firstError.Should().NotBeNull();
        firstError!.Message.Should().Be("Error 1");
    }

    [Fact]
    public void PipelineExecutionResult_GetErrorsByType_FiltersCorrectly()
    {
        var errors = new List<PipelineError>
        {
            new() { ErrorType = PipelineErrorType.ExecutionError, Message = "Exec error", Timestamp = DateTime.UtcNow, StageId = "1" },
            new() { ErrorType = PipelineErrorType.ValidationError, Message = "Val error", Timestamp = DateTime.UtcNow, StageId = "2" },
            new() { ErrorType = PipelineErrorType.ExecutionError, Message = "Exec error 2", Timestamp = DateTime.UtcNow, StageId = "3" }
        };
        var metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow };
        var stageResults = new List<StageExecutionResult>();

        var result = PipelineExecutionResult.CreateFailure("1", "Test", errors, metrics, stageResults);

        var execErrors = result.GetErrorsByType(PipelineErrorType.ExecutionError).ToList();
        execErrors.Should().HaveCount(2);
        execErrors.Should().AllSatisfy(e => e.ErrorType.Should().Be(PipelineErrorType.ExecutionError));
    }

    [Fact]
    public void PipelineExecutionResult_GetPerformanceScore_WithSuccess_ReturnsPositiveScore()
    {
        var outputs = new Dictionary<string, object>();
        var metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow };
        var stageResults = new List<StageExecutionResult>
        {
            new() { Success = true, StageId = "stage-1" },
            new() { Success = true, StageId = "stage-2" }
        };

        var result = PipelineExecutionResult.CreateSuccess(
            "pipeline-1",
            "Test",
            outputs,
            metrics,
            stageResults,
            DateTime.UtcNow.AddSeconds(-1),
            DateTime.UtcNow
        );

        var score = result.GetPerformanceScore();
        score.Should().BeGreaterThan(0);
        score.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void PipelineExecutionResult_GetPerformanceScore_WithFailure_ReturnsZero()
    {
        var errors = new List<PipelineError>
        {
            new() { ErrorType = PipelineErrorType.ExecutionError, Message = "Error", Timestamp = DateTime.UtcNow, StageId = "1" }
        };
        var metrics = new PipelineExecutionMetrics { ExecutionId = "1", PipelineName = "Test", StartTime = DateTimeOffset.UtcNow };
        var stageResults = new List<StageExecutionResult>();

        var result = PipelineExecutionResult.CreateFailure("1", "Test", errors, metrics, stageResults);

        result.GetPerformanceScore().Should().Be(0.0);
    }

    [Fact]
    public void PipelineResourceUsage_GetEfficiencyScore_WithHighUtilization_ReturnsHighScore()
    {
        var usage = new PipelineResourceUsage
        {
            AverageResourceUtilization = 90.0,
            CpuCoresUsed = 8,
            GpuDevicesUsed = 2
        };

        var score = usage.GetEfficiencyScore();
        score.Should().BeGreaterThan(10);
        score.Should().BeLessThanOrEqualTo(20);
    }

    [Fact]
    public void PipelineResourceUsage_AllProperties_CanBeSet()
    {
        var usage = new PipelineResourceUsage
        {
            PeakCpuUsage = 95.0,
            PeakMemoryUsage = 1024 * 1024 * 1024,
            PeakGpuMemoryUsage = 512 * 1024 * 1024,
            CpuCoresUsed = 4,
            GpuDevicesUsed = 1,
            TotalComputeOperations = 1000000,
            AverageResourceUtilization = 75.0
        };

        usage.PeakCpuUsage.Should().Be(95.0);
        usage.PeakMemoryUsage.Should().Be(1024 * 1024 * 1024);
        usage.CpuCoresUsed.Should().Be(4);
        usage.GpuDevicesUsed.Should().Be(1);
    }
}
