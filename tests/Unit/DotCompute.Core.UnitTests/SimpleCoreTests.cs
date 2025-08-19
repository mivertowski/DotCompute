using DotCompute.Core.Aot;
using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

/// <summary>
/// Simple focused tests to drive up code coverage on Core types.
/// </summary>
public class SimpleCoreTests
{
    [Fact]
    public void PipelineOptimizationSettings_DefaultsWork()
    {
        var settings = new PipelineOptimizationSettings();
        
        Assert.True(settings.EnableKernelFusion);
        Assert.True(settings.EnableStageReordering);
        Assert.True(settings.EnableMemoryOptimization);
        Assert.True(settings.EnableParallelMerging);
        Assert.Equal(PipelineOptimizationLevel.Balanced, settings.Level);
    }
    
    [Fact]
    public void PipelineOptimizationSettings_CanModify()
    {
        var settings = new PipelineOptimizationSettings();
        
        settings.EnableKernelFusion = false;
        settings.EnableStageReordering = false;
        settings.EnableMemoryOptimization = false;
        settings.EnableParallelMerging = false;
        settings.Level = PipelineOptimizationLevel.Aggressive;
        
        Assert.False(settings.EnableKernelFusion);
        Assert.False(settings.EnableStageReordering);
        Assert.False(settings.EnableMemoryOptimization);
        Assert.False(settings.EnableParallelMerging);
        Assert.Equal(PipelineOptimizationLevel.Aggressive, settings.Level);
    }
    
    [Fact]
    public void PipelineExecutionOptions_DefaultsWork()
    {
        var options = new PipelineExecutionOptions();
        
        Assert.False(options.EnableProfiling);
        Assert.False(options.EnableDetailedLogging);
        Assert.Equal(Environment.ProcessorCount, options.MaxParallelStages);
        Assert.False(options.ContinueOnError);
    }
    
    [Fact]
    public void PipelineExecutionOptions_CanModify()
    {
        var options = new PipelineExecutionOptions();
        
        options.EnableProfiling = true;
        options.EnableDetailedLogging = true;
        options.MaxParallelStages = 8;
        options.ContinueOnError = true;
        
        Assert.True(options.EnableProfiling);
        Assert.True(options.EnableDetailedLogging);
        Assert.Equal(8, options.MaxParallelStages);
        Assert.True(options.ContinueOnError);
    }
    
    [Fact]
    public void ValidationError_Creation()
    {
        var error = new ValidationError
        {
            Code = "TEST001",
            Message = "Test validation error",
            Path = "test.config.path"
        };
        
        Assert.Equal("TEST001", error.Code);
        Assert.Equal("Test validation error", error.Message);
        Assert.Equal("test.config.path", error.Path);
        Assert.Null(error.InvalidValue);
        Assert.Null(error.ExpectedValue);
    }
    
    [Fact]
    public void ValidationError_WithOptionalFields()
    {
        var error = new ValidationError
        {
            Code = "TEST002",
            Message = "Invalid value error",
            Path = "config.value",
            InvalidValue = "badValue",
            ExpectedValue = "goodValue"
        };
        
        Assert.Equal("TEST002", error.Code);
        Assert.Equal("Invalid value error", error.Message);
        Assert.Equal("config.value", error.Path);
        Assert.Equal("badValue", error.InvalidValue);
        Assert.Equal("goodValue", error.ExpectedValue);
    }
    
    [Fact]
    public void ValidationWarning_Creation()
    {
        var warning = new ValidationWarning
        {
            Code = "WARN001",
            Message = "Test validation warning",
            Path = "test.config.warning"
        };
        
        Assert.Equal("WARN001", warning.Code);
        Assert.Equal("Test validation warning", warning.Message);
        Assert.Equal("test.config.warning", warning.Path);
        Assert.Null(warning.Recommendation);
        Assert.Null(warning.PotentialImpact);
    }
    
    [Fact]
    public void ValidationWarning_WithOptionalFields()
    {
        var warning = new ValidationWarning
        {
            Code = "WARN002",
            Message = "Performance warning",
            Path = "config.performance",
            Recommendation = "Consider optimizing",
            PotentialImpact = "May cause slowdown"
        };
        
        Assert.Equal("WARN002", warning.Code);
        Assert.Equal("Performance warning", warning.Message);
        Assert.Equal("config.performance", warning.Path);
        Assert.Equal("Consider optimizing", warning.Recommendation);
        Assert.Equal("May cause slowdown", warning.PotentialImpact);
    }
    
    [Fact]
    public void PipelineError_Creation()
    {
        var timestamp = DateTime.UtcNow;
        var error = new PipelineError
        {
            Code = "ERR001",
            Message = "Pipeline execution failed",
            Severity = ErrorSeverity.Error,
            Timestamp = timestamp
        };
        
        Assert.Equal("ERR001", error.Code);
        Assert.Equal("Pipeline execution failed", error.Message);
        Assert.Equal(ErrorSeverity.Error, error.Severity);
        Assert.Equal(timestamp, error.Timestamp);
        Assert.Null(error.StageId);
        Assert.Null(error.Exception);
    }
    
    [Fact]
    public void PipelineError_WithAllFields()
    {
        var timestamp = DateTime.UtcNow;
        var exception = new InvalidOperationException("Test error");
        
        var error = new PipelineError
        {
            Code = "ERR002",
            Message = "Stage failed",
            StageId = "stage1",
            Severity = ErrorSeverity.Critical,
            Timestamp = timestamp,
            Exception = exception
        };
        
        Assert.Equal("ERR002", error.Code);
        Assert.Equal("Stage failed", error.Message);
        Assert.Equal("stage1", error.StageId);
        Assert.Equal(ErrorSeverity.Critical, error.Severity);
        Assert.Equal(timestamp, error.Timestamp);
        Assert.Equal(exception, error.Exception);
    }
    
    [Theory]
    [InlineData(ErrorSeverity.Information)]
    [InlineData(ErrorSeverity.Warning)]
    [InlineData(ErrorSeverity.Error)]
    [InlineData(ErrorSeverity.Critical)]
    [InlineData(ErrorSeverity.Fatal)]
    public void PipelineError_AllSeverityLevels(ErrorSeverity severity)
    {
        var error = new PipelineError
        {
            Code = "TEST",
            Message = "Test",
            Severity = severity,
            Timestamp = DateTime.UtcNow
        };
        
        Assert.Equal(severity, error.Severity);
    }
    
    [Fact]
    public void MemoryUsageStats_Creation()
    {
        var stats = new MemoryUsageStats
        {
            AllocatedBytes = 1024,
            PeakBytes = 2048,
            AllocationCount = 5,
            DeallocationCount = 3
        };
        
        Assert.Equal(1024, stats.AllocatedBytes);
        Assert.Equal(2048, stats.PeakBytes);
        Assert.Equal(5, stats.AllocationCount);
        Assert.Equal(3, stats.DeallocationCount);
        Assert.Null(stats.UsageByType);
    }
    
    [Fact]
    public void PipelineEvent_Creation()
    {
        var timestamp = DateTime.UtcNow;
        var evt = new PipelineEvent
        {
            Type = PipelineEventType.Started,
            Timestamp = timestamp,
            Message = "Pipeline started"
        };
        
        Assert.Equal(PipelineEventType.Started, evt.Type);
        Assert.Equal(timestamp, evt.Timestamp);
        Assert.Equal("Pipeline started", evt.Message);
        Assert.Null(evt.StageId);
        Assert.Null(evt.Data);
    }
    
    [Fact]
    public void CompilationOptions_AbsractionsType()
    {
        var options = new CompilationOptions();
        
        Assert.Equal(OptimizationLevel.Default, options.OptimizationLevel);
    }
    
    [Fact]
    public void KernelArguments_Empty()
    {
        var args = new KernelArguments();
        
        Assert.Equal(0, args.Length);
    }
    
    [Fact]
    public void AcceleratorType_EnumValues()
    {
        Assert.True(Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.CPU));
        Assert.True(Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.GPU));
        Assert.True(Enum.IsDefined(typeof(AcceleratorType), AcceleratorType.DirectML));
    }
}
}
