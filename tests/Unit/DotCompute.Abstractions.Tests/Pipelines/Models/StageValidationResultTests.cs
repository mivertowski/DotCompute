// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions.Tests.Pipelines.Models;

/// <summary>
/// Comprehensive tests for StageValidationResult (499 lines).
/// Target: 25+ tests for validation framework, error handling, and reporting.
/// </summary>
public class StageValidationResultTests
{
    [Fact]
    public void StageValidationResult_DefaultConstructor_InitializesCollections()
    {
        var result = new StageValidationResult();

        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(ErrorSeverity.Info);
        result.Errors.Should().NotBeNull().And.BeEmpty();
        result.Warnings.Should().NotBeNull().And.BeEmpty();
        result.Information.Should().NotBeNull().And.BeEmpty();
        result.Suggestions.Should().NotBeNull().And.BeEmpty();
        result.BackendCompatibility.Should().NotBeNull().And.BeEmpty();
        result.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void StageValidationResult_ValidationTime_IsSetToUtcNow()
    {
        var result = new StageValidationResult();
        result.ValidationTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void StageValidationResult_AddError_AddsToCollection()
    {
        var result = new StageValidationResult();
        result.AddError("Test error", "ERR001");

        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be("ERR001");
        result.Errors[0].Message.Should().Be("Test error");
        result.Errors[0].Severity.Should().Be(ValidationSeverity.Error);
    }

    [Fact]
    public void StageValidationResult_AddError_UpdatesSeverity()
    {
        var result = new StageValidationResult { Severity = ErrorSeverity.Info };
        result.AddError("Test error");

        result.Severity.Should().Be(ErrorSeverity.Error);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void StageValidationResult_AddWarning_AddsToCollection()
    {
        var result = new StageValidationResult();
        result.AddWarning("Test warning", "WARN001");

        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Code.Should().Be("WARN001");
        result.Warnings[0].Message.Should().Be("Test warning");
        result.Warnings[0].Severity.Should().Be(ValidationSeverity.Warning);
    }

    [Fact]
    public void StageValidationResult_AddWarning_UpdatesSeverityIfNotError()
    {
        var result = new StageValidationResult { Severity = ErrorSeverity.Info };
        result.AddWarning("Test warning");

        result.Severity.Should().Be(ErrorSeverity.Warning);
    }

    [Fact]
    public void StageValidationResult_AddInformation_AddsToCollection()
    {
        var result = new StageValidationResult();
        result.AddInformation("Test info", "INFO001");

        result.Information.Should().HaveCount(1);
        result.Information[0].Code.Should().Be("INFO001");
        result.Information[0].Message.Should().Be("Test info");
        result.Information[0].Severity.Should().Be(ValidationSeverity.Info);
    }

    [Fact]
    public void StageValidationResult_AddSuggestion_AddsToCollection()
    {
        var result = new StageValidationResult();
        result.AddSuggestion("Consider optimization", SuggestionCategory.Performance, SuggestionPriority.High);

        result.Suggestions.Should().HaveCount(1);
        result.Suggestions[0].Message.Should().Be("Consider optimization");
        result.Suggestions[0].Category.Should().Be(SuggestionCategory.Performance);
        result.Suggestions[0].Priority.Should().Be(SuggestionPriority.High);
    }

    [Fact]
    public void StageValidationResult_GetTotalIssueCount_ReturnsCorrectCount()
    {
        var result = new StageValidationResult();
        result.AddError("Error 1");
        result.AddError("Error 2");
        result.AddWarning("Warning 1");

        result.GetTotalIssueCount().Should().Be(3);
    }

    [Fact]
    public void StageValidationResult_HasCriticalIssues_WithErrors_ReturnsTrue()
    {
        var result = new StageValidationResult();
        result.AddError("Critical error");

        result.HasCriticalIssues().Should().BeTrue();
    }

    [Fact]
    public void StageValidationResult_HasCriticalIssues_WithWarnings_ReturnsTrue()
    {
        var result = new StageValidationResult();
        result.AddWarning("Warning");

        result.HasCriticalIssues().Should().BeTrue();
    }

    [Fact]
    public void StageValidationResult_HasCriticalIssues_WithoutIssues_ReturnsFalse()
    {
        var result = new StageValidationResult { IsValid = true };
        result.AddInformation("Just info");

        result.HasCriticalIssues().Should().BeFalse();
    }

    [Fact]
    public void StageValidationResult_Merge_CombinesErrors()
    {
        var result1 = new StageValidationResult();
        result1.AddError("Error 1");

        var result2 = new StageValidationResult();
        result2.AddError("Error 2");

        result1.Merge(result2);

        result1.Errors.Should().HaveCount(2);
        result1.Errors.Should().Contain(e => e.Message == "Error 1");
        result1.Errors.Should().Contain(e => e.Message == "Error 2");
    }

    [Fact]
    public void StageValidationResult_Merge_UpdatesValidity()
    {
        var result1 = new StageValidationResult { IsValid = true };
        var result2 = new StageValidationResult { IsValid = false };

        result1.Merge(result2);

        result1.IsValid.Should().BeFalse();
    }

    [Fact]
    public void StageValidationResult_Merge_UpdatesSeverity()
    {
        var result1 = new StageValidationResult { Severity = ErrorSeverity.Info };
        var result2 = new StageValidationResult { Severity = ErrorSeverity.Error };

        result1.Merge(result2);

        result1.Severity.Should().Be(ErrorSeverity.Error);
    }

    [Fact]
    public void StageValidationResult_Merge_CombinesMetadata()
    {
        var result1 = new StageValidationResult();
        result1.Metadata["key1"] = "value1";

        var result2 = new StageValidationResult();
        result2.Metadata["key2"] = "value2";

        result1.Merge(result2);

        result1.Metadata.Should().HaveCount(2);
        result1.Metadata["key1"].Should().Be("value1");
        result1.Metadata["key2"].Should().Be("value2");
    }

    [Fact]
    public void StageValidationResult_Success_CreatesValidResult()
    {
        var result = StageValidationResult.Success("TestStage");

        result.IsValid.Should().BeTrue();
        result.StageName.Should().Be("TestStage");
        result.Severity.Should().Be(ErrorSeverity.Info);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void StageValidationResult_Failure_CreatesInvalidResult()
    {
        var result = StageValidationResult.Failure("TestStage", "Fatal error", "ERR_FATAL");

        result.IsValid.Should().BeFalse();
        result.StageName.Should().Be("TestStage");
        result.Severity.Should().Be(ErrorSeverity.Error);
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Message.Should().Be("Fatal error");
        result.Errors[0].Code.Should().Be("ERR_FATAL");
    }

    [Fact]
    public void ValidationSuggestion_AllProperties_CanBeSet()
    {
        var suggestion = new ValidationSuggestion
        {
            Message = "Optimize memory usage",
            Category = SuggestionCategory.Memory,
            Priority = SuggestionPriority.Critical,
            EstimatedImpact = "30% memory reduction",
            Effort = ImplementationEffort.High
        };

        suggestion.Message.Should().Be("Optimize memory usage");
        suggestion.Category.Should().Be(SuggestionCategory.Memory);
        suggestion.Priority.Should().Be(SuggestionPriority.Critical);
        suggestion.EstimatedImpact.Should().Be("30% memory reduction");
        suggestion.Effort.Should().Be(ImplementationEffort.High);
    }

    [Theory]
    [InlineData(SuggestionCategory.Performance)]
    [InlineData(SuggestionCategory.Memory)]
    [InlineData(SuggestionCategory.Quality)]
    [InlineData(SuggestionCategory.Security)]
    [InlineData(SuggestionCategory.Maintainability)]
    [InlineData(SuggestionCategory.Compatibility)]
    [InlineData(SuggestionCategory.Configuration)]
    public void SuggestionCategory_AllValues_Exist(SuggestionCategory category)
    {
        category.Should().BeDefined();
    }

    [Theory]
    [InlineData(ImplementationEffort.Low)]
    [InlineData(ImplementationEffort.Medium)]
    [InlineData(ImplementationEffort.High)]
    [InlineData(ImplementationEffort.VeryHigh)]
    public void ImplementationEffort_AllValues_Exist(ImplementationEffort effort)
    {
        effort.Should().BeDefined();
    }

    [Fact]
    public void ResourceEstimate_AllProperties_CanBeSet()
    {
        var estimate = new ResourceEstimate
        {
            EstimatedMemoryUsage = 1024 * 1024,
            EstimatedExecutionTime = TimeSpan.FromSeconds(2),
            EstimatedCpuUtilization = 75.0,
            EstimatedGpuUtilization = 60.0,
            ConfidenceLevel = 0.85
        };

        estimate.EstimatedMemoryUsage.Should().Be(1024 * 1024);
        estimate.EstimatedExecutionTime.Should().Be(TimeSpan.FromSeconds(2));
        estimate.EstimatedCpuUtilization.Should().Be(75.0);
        estimate.EstimatedGpuUtilization.Should().Be(60.0);
        estimate.ConfidenceLevel.Should().Be(0.85);
    }

    [Fact]
    public void PerformanceEstimate_DefaultConfidenceLevel_IsFiftyPercent()
    {
        var estimate = new PerformanceEstimate();
        estimate.ConfidenceLevel.Should().Be(0.5);
    }

    [Fact]
    public void BackendCompatibility_AllProperties_CanBeSet()
    {
        var compatibility = new BackendCompatibility
        {
            IsCompatible = true,
            Level = CompatibilityLevel.Full
        };

        compatibility.Limitations.Add("Requires CUDA 12.0+");

        compatibility.IsCompatible.Should().BeTrue();
        compatibility.Level.Should().Be(CompatibilityLevel.Full);
        compatibility.Limitations.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(CompatibilityLevel.None)]
    [InlineData(CompatibilityLevel.Basic)]
    [InlineData(CompatibilityLevel.Good)]
    [InlineData(CompatibilityLevel.Full)]
    public void CompatibilityLevel_AllValues_Exist(CompatibilityLevel level)
    {
        level.Should().BeDefined();
    }
}
