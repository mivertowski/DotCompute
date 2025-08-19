using DotCompute.Core.Aot;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

public class CorePipelineTypesTests
{
    [Fact]
    public void PipelineOptimizationSettings_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var settings = new PipelineOptimizationSettings();
        
        // Assert
        Assert.True(settings.EnableKernelFusion);
        Assert.True(settings.EnableStageReordering);
        Assert.True(settings.EnableMemoryOptimization);
        Assert.True(settings.EnableParallelMerging);
        Assert.Equal(PipelineOptimizationLevel.Balanced, settings.Level);
    }
    
    [Fact]
    public void PipelineExecutionOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new PipelineExecutionOptions();
        
        // Assert
        Assert.False(options.EnableProfiling);
        Assert.False(options.EnableDetailedLogging);
        Assert.Equal(Environment.ProcessorCount, options.MaxParallelStages);
        Assert.False(options.ContinueOnError);
    }
    
    [Theory]
    [InlineData(PipelineOptimizationLevel.None)]
    [InlineData(PipelineOptimizationLevel.Conservative)]
    [InlineData(PipelineOptimizationLevel.Balanced)]
    [InlineData(PipelineOptimizationLevel.Aggressive)]
    public void PipelineOptimizationSettings_SetLevel_ShouldWork(PipelineOptimizationLevel level)
    {
        // Arrange
        var settings = new PipelineOptimizationSettings();
        
        // Act
        settings.Level = level;
        
        // Assert
        Assert.Equal(level, settings.Level);
    }
    
    [Fact]
    public void ValidationError_CreateWithRequiredProperties_ShouldWork()
    {
        // Arrange & Act
        var error = new ValidationError
        {
            Code = "TEST_ERROR",
            Message = "Test error message",
            Path = "test.path"
        };
        
        // Assert
        Assert.Equal("TEST_ERROR", error.Code);
        Assert.Equal("Test error message", error.Message);
        Assert.Equal("test.path", error.Path);
    }
    
    [Fact]
    public void ValidationWarning_CreateWithRequiredProperties_ShouldWork()
    {
        // Arrange & Act
        var warning = new ValidationWarning
        {
            Code = "TEST_WARNING",
            Message = "Test warning message",
            Path = "test.path"
        };
        
        // Assert
        Assert.Equal("TEST_WARNING", warning.Code);
        Assert.Equal("Test warning message", warning.Message);
        Assert.Equal("test.path", warning.Path);
    }
    
    [Fact]
    public void PipelineError_CreateWithAllProperties_ShouldWork()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var exception = new InvalidOperationException("Test exception");
        
        // Act
        var error = new PipelineError
        {
            Code = "PIPELINE_ERROR",
            Message = "Pipeline error occurred",
            StageId = "stage1",
            Severity = ErrorSeverity.Error,
            Timestamp = timestamp,
            Exception = exception
        };
        
        // Assert
        Assert.Equal("PIPELINE_ERROR", error.Code);
        Assert.Equal("Pipeline error occurred", error.Message);
        Assert.Equal("stage1", error.StageId);
        Assert.Equal(ErrorSeverity.Error, error.Severity);
        Assert.Equal(timestamp, error.Timestamp);
        Assert.Equal(exception, error.Exception);
    }
    
    [Theory]
    [InlineData(ErrorSeverity.Information)]
    [InlineData(ErrorSeverity.Warning)]
    [InlineData(ErrorSeverity.Error)]
    [InlineData(ErrorSeverity.Critical)]
    [InlineData(ErrorSeverity.Fatal)]
    public void PipelineError_SetSeverity_ShouldWork(ErrorSeverity severity)
    {
        // Arrange & Act
        var error = new PipelineError
        {
            Code = "TEST",
            Message = "Test",
            Severity = severity,
            Timestamp = DateTime.UtcNow
        };
        
        // Assert
        Assert.Equal(severity, error.Severity);
    }
    
    [Fact]
    public void PipelineValidationResult_WithErrors_ShouldNotBeValid()
    {
        // Arrange
        var errors = new List<ValidationError>
        {
            new ValidationError { Code = "E001", Message = "Error 1", Path = "path1" }
        };
        
        // Act
        var result = new PipelineValidationResult
        {
            IsValid = false,
            Errors = errors
        };
        
        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Errors);
        Assert.Single(result.Errors);
        Assert.Equal("E001", result.Errors[0].Code);
    }
    
    [Fact]
    public void MemoryUsageStats_CreateWithAllProperties_ShouldWork()
    {
        // Arrange & Act
        var stats = new MemoryUsageStats
        {
            AllocatedBytes = 1024,
            PeakBytes = 2048,
            AllocationCount = 10,
            DeallocationCount = 5
        };
        
        // Assert
        Assert.Equal(1024, stats.AllocatedBytes);
        Assert.Equal(2048, stats.PeakBytes);
        Assert.Equal(10, stats.AllocationCount);
        Assert.Equal(5, stats.DeallocationCount);
    }
    
    [Fact]
    public void PipelineEvent_CreateWithRequiredProperties_ShouldWork()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        
        // Act
        var pipelineEvent = new PipelineEvent
        {
            Type = PipelineEventType.Started,
            Timestamp = timestamp,
            Message = "Pipeline started",
            StageId = "stage1"
        };
        
        // Assert
        Assert.Equal(PipelineEventType.Started, pipelineEvent.Type);
        Assert.Equal(timestamp, pipelineEvent.Timestamp);
        Assert.Equal("Pipeline started", pipelineEvent.Message);
        Assert.Equal("stage1", pipelineEvent.StageId);
    }
}
}
