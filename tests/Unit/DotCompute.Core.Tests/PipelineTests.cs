// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests;


/// <summary>
/// Tests for Pipeline classes.
/// </summary>
public sealed class PipelineTests
{
    private static readonly string[] ExpectedStages = { "Stage1", "Stage2" };

    [Fact]
    public void KernelPipelineBuilder_Create_CreatesPipelineBuilder()
    {
        // Act
        var builder = KernelPipelineBuilder.Create();

        // Assert
        Assert.NotNull(builder);
        _ = Assert.IsAssignableFrom<IKernelPipelineBuilder>(builder);
    }

    [Fact]
    public void KernelPipelineBuilder_AddStage_AddsStageSuccessfully()
    {
        // Arrange
        var builder = KernelPipelineBuilder.Create();
        var stage = Mock.Of<IPipelineStage>();

        // Act
        var result = builder.AddStage(stage);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void KernelPipelineBuilder_AddStage_WithNullStage_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = KernelPipelineBuilder.Create();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddStage(null!));
    }

    [Fact]
    public void KernelPipelineBuilder_WithName_SetsName()
    {
        // Arrange
        var builder = KernelPipelineBuilder.Create();
        var name = "TestPipeline";

        // Act
        var result = builder.WithName(name);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void KernelPipelineBuilder_WithOptimization_SetsOptimizationSettings()
    {
        // Arrange
        var builder = KernelPipelineBuilder.Create();

        // Act
        var result = builder.WithOptimization(settings => settings.EnableKernelFusion = false);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void KernelPipelineBuilder_WithMetadata_AddsMetadata()
    {
        // Arrange
        var builder = KernelPipelineBuilder.Create();

        // Act
        var result = builder.WithMetadata("key", "value");

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void KernelPipelineBuilder_Build_CreatesPipeline()
    {
        // Arrange
        var builder = KernelPipelineBuilder.Create();
        var stage1 = Mock.Of<IPipelineStage>(s => s.Name == "Stage1" && s.Id == "stage1");
        var stage2 = Mock.Of<IPipelineStage>(s => s.Name == "Stage2" && s.Id == "stage2");

        _ = builder.AddStage(stage1).AddStage(stage2);

        // Act
        var pipeline = builder.Build();

        // Assert
        Assert.NotNull(pipeline);
        _ = Assert.IsAssignableFrom<IKernelPipeline>(pipeline);
    }

    [Fact]
    public void KernelPipelineBuilder_Build_WithNoStages_CreatesEmptyPipeline()
    {
        // Arrange
        var builder = KernelPipelineBuilder.Create();

        // Act
        var pipeline = builder.Build();

        // Assert
        Assert.NotNull(pipeline);
        Assert.Empty(pipeline.Stages);
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_ExecutesAllStages()
    {
        // Arrange
        var executedStages = new List<string>();

        var stage1 = new Mock<IPipelineStage>();
        _ = stage1.Setup(s => s.Id).Returns("stage1");
        _ = stage1.Setup(s => s.Name).Returns("Stage1");
        _ = stage1.Setup(s => s.Type).Returns(PipelineStageType.Custom);
        _ = stage1.Setup(s => s.Dependencies).Returns(new List<string>());
        _ = stage1.Setup(s => s.Metadata).Returns(new Dictionary<string, object>());
        _ = stage1.Setup(s => s.ExecuteAsync(It.IsAny<PipelineExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executedStages.Add("Stage1"))
            .ReturnsAsync(new StageExecutionResult
            {
                StageId = "stage1",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            });
        _ = stage1.Setup(s => s.Validate()).Returns(new StageValidationResult { IsValid = true });
        _ = stage1.Setup(s => s.GetMetrics()).Returns(Mock.Of<IStageMetrics>());

        var stage2 = new Mock<IPipelineStage>();
        _ = stage2.Setup(s => s.Id).Returns("stage2");
        _ = stage2.Setup(s => s.Name).Returns("Stage2");
        _ = stage2.Setup(s => s.Type).Returns(PipelineStageType.Custom);
        _ = stage2.Setup(s => s.Dependencies).Returns(new List<string>());
        _ = stage2.Setup(s => s.Metadata).Returns(new Dictionary<string, object>());
        _ = stage2.Setup(s => s.ExecuteAsync(It.IsAny<PipelineExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executedStages.Add("Stage2"))
            .ReturnsAsync(new StageExecutionResult
            {
                StageId = "stage2",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            });
        _ = stage2.Setup(s => s.Validate()).Returns(new StageValidationResult { IsValid = true });
        _ = stage2.Setup(s => s.GetMetrics()).Returns(Mock.Of<IStageMetrics>());

        var pipeline = KernelPipelineBuilder.Create()
            .AddStage(stage1.Object)
            .AddStage(stage2.Object)
            .Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = Mock.Of<IPipelineMemoryManager>(),
            Device = Mock.Of<IComputeDevice>()
        };

        // Act
        var result = await pipeline.ExecuteAsync(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(ExpectedStages, executedStages);
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_WithCancellation_StopsExecution()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var executedStages = new List<string>();

        var stage1 = new Mock<IPipelineStage>();
        _ = stage1.Setup(s => s.Id).Returns("stage1");
        _ = stage1.Setup(s => s.Name).Returns("Stage1");
        _ = stage1.Setup(s => s.Type).Returns(PipelineStageType.Custom);
        _ = stage1.Setup(s => s.Dependencies).Returns(new List<string>());
        _ = stage1.Setup(s => s.Metadata).Returns(new Dictionary<string, object>());
        _ = stage1.Setup(s => s.ExecuteAsync(It.IsAny<PipelineExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                executedStages.Add("Stage1");
                cts.Cancel();
            })
            .ReturnsAsync(new StageExecutionResult
            {
                StageId = "stage1",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            });
        _ = stage1.Setup(s => s.Validate()).Returns(new StageValidationResult { IsValid = true });
        _ = stage1.Setup(s => s.GetMetrics()).Returns(Mock.Of<IStageMetrics>());

        var stage2 = new Mock<IPipelineStage>();
        _ = stage2.Setup(s => s.Id).Returns("stage2");
        _ = stage2.Setup(s => s.Name).Returns("Stage2");
        _ = stage2.Setup(s => s.Type).Returns(PipelineStageType.Custom);
        _ = stage2.Setup(s => s.Dependencies).Returns(new List<string>());
        _ = stage2.Setup(s => s.Metadata).Returns(new Dictionary<string, object>());
        _ = stage2.Setup(s => s.ExecuteAsync(It.IsAny<PipelineExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executedStages.Add("Stage2"))
            .ReturnsAsync(new StageExecutionResult
            {
                StageId = "stage2",
                Success = true,
                Duration = TimeSpan.FromMilliseconds(10)
            });
        _ = stage2.Setup(s => s.Validate()).Returns(new StageValidationResult { IsValid = true });
        _ = stage2.Setup(s => s.GetMetrics()).Returns(Mock.Of<IStageMetrics>());

        var pipeline = KernelPipelineBuilder.Create()
            .AddStage(stage1.Object)
            .AddStage(stage2.Object)
            .Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = Mock.Of<IPipelineMemoryManager>(),
            Device = Mock.Of<IComputeDevice>()
        };

        // Act
        var result = await pipeline.ExecuteAsync(context, cts.Token);

        // Assert
        Assert.False(result.Success);
        _ = Assert.Single(result.Errors!);
        Assert.Equal("EXECUTION_CANCELLED", result.Errors![0].Code);
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_WithFailingStage_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Stage failed");

        var stage1 = new Mock<IPipelineStage>();
        _ = stage1.Setup(s => s.Id).Returns("stage1");
        _ = stage1.Setup(s => s.Name).Returns("Stage1");
        _ = stage1.Setup(s => s.Type).Returns(PipelineStageType.Custom);
        _ = stage1.Setup(s => s.Dependencies).Returns(new List<string>());
        _ = stage1.Setup(s => s.Metadata).Returns(new Dictionary<string, object>());
        _ = stage1.Setup(s => s.ExecuteAsync(It.IsAny<PipelineExecutionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        _ = stage1.Setup(s => s.Validate()).Returns(new StageValidationResult { IsValid = true });
        _ = stage1.Setup(s => s.GetMetrics()).Returns(Mock.Of<IStageMetrics>());

        var pipeline = KernelPipelineBuilder.Create()
            .AddStage(stage1.Object)
            .Build();

        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = Mock.Of<IPipelineMemoryManager>(),
            Device = Mock.Of<IComputeDevice>()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
            pipeline.ExecuteAsync(context).AsTask());

        Assert.Contains("Stage failed", exception.Message, StringComparison.Ordinal);
        Assert.Same(expectedException, exception.InnerException);
    }

    [Fact]
    public void KernelPipeline_Validate_WithEmptyStages_ReturnsInvalid()
    {
        // Arrange
        var pipeline = KernelPipelineBuilder.Create()
            .WithName("EmptyPipeline")
            .Build();

        // Act
        var result = pipeline.Validate();

        // Assert
        Assert.False(result.IsValid);  // Empty pipeline should be invalid
        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors, e => e.Code == "NO_STAGES");
    }

    [Fact]
    public void KernelPipeline_Validate_WithValidStages_ReturnsValid()
    {
        // Arrange
        var stage = Mock.Of<IPipelineStage>(s =>
            s.Id == "stage1" &&
            s.Name == "Stage1" &&
            s.Type == PipelineStageType.Custom &&
            s.Dependencies == new List<string>() &&
            s.Metadata == new Dictionary<string, object>() &&
            s.Validate() == new StageValidationResult { IsValid = true } &&
            s.GetMetrics() == Mock.Of<IStageMetrics>());

        var pipeline = KernelPipelineBuilder.Create()
            .AddStage(stage)
            .Build();

        // Act
        var result = pipeline.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Errors);
    }

    [Fact]
    public void PipelineMetrics_Properties_ReflectInitialState()
    {
        // Arrange & Act
        var metrics = new PipelineMetrics("test-pipeline");

        // Assert
        Assert.Equal("test-pipeline", metrics.PipelineId);
        Assert.Equal(0, metrics.ExecutionCount);
        Assert.Equal(0, metrics.SuccessfulExecutionCount);
        Assert.Equal(0, metrics.FailedExecutionCount);
        Assert.Equal(TimeSpan.Zero, metrics.AverageExecutionTime);
        Assert.Equal(TimeSpan.Zero, metrics.TotalExecutionTime);
        Assert.Equal(0, metrics.Throughput);
        Assert.Equal(0, metrics.SuccessRate);
        Assert.Equal(0, metrics.AverageMemoryUsage);
        Assert.Equal(0, metrics.PeakMemoryUsage);
        Assert.Empty(metrics.StageMetrics);
        Assert.Empty(metrics.CustomMetrics);
    }

    [Fact]
    public void PipelineMetrics_RecordExecution_UpdatesMetrics()
    {
        // Arrange
        var metrics = new PipelineMetrics("test-pipeline");
        var executionMetrics = new PipelineExecutionMetrics
        {
            ExecutionId = "exec1",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddMilliseconds(100),
            Duration = TimeSpan.FromMilliseconds(100),
            MemoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = 1024 * 1024,
                PeakBytes = 2 * 1024 * 1024,
                AllocationCount = 1,
                DeallocationCount = 0
            },
            ComputeUtilization = 0.8,
            MemoryBandwidthUtilization = 0.6,
            StageExecutionTimes = new Dictionary<string, TimeSpan> { ["stage1"] = TimeSpan.FromMilliseconds(50) },
            DataTransferTimes = new Dictionary<string, TimeSpan>()
        };

        // Act
        metrics.RecordExecution(executionMetrics, success: true);

        // Assert
        Assert.Equal(1, metrics.ExecutionCount);
        Assert.Equal(1, metrics.SuccessfulExecutionCount);
        Assert.Equal(0, metrics.FailedExecutionCount);
        Assert.Equal(100, metrics.AverageExecutionTime.TotalMilliseconds);
        Assert.Equal(100, metrics.TotalExecutionTime.TotalMilliseconds);
        Assert.Equal(1024 * 1024, metrics.AverageMemoryUsage);
        Assert.Equal(2 * 1024 * 1024, metrics.PeakMemoryUsage);
        _ = Assert.Single(metrics.StageMetrics);
        Assert.Contains("stage1", metrics.StageMetrics.Keys);
    }

    [Fact]
    public void PipelineMetrics_RecordCustomMetric_StoresMetric()
    {
        // Arrange
        var metrics = new PipelineMetrics("test-pipeline");
        const string metricName = "TestMetric";
        const double metricValue = 42.5;

        // Act
        metrics.RecordCustomMetric(metricName, metricValue);

        // Assert
        _ = Assert.Single(metrics.CustomMetrics);
        Assert.Equal(metricValue, metrics.CustomMetrics[metricName]);
    }

    [Fact]
    public void PipelineExecutionContext_State_WorksCorrectly()
    {
        // Arrange
        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = Mock.Of<IPipelineMemoryManager>(),
            Device = Mock.Of<IComputeDevice>()
        };
        var testData = new TestStateData { Value = 42, Name = "Test" };

        // Act
        context.State["test"] = testData;
        var retrieved = context.State["test"];

        // Assert
        Assert.NotNull(retrieved);
        var retrievedData = Assert.IsType<TestStateData>(retrieved);
        Assert.Equal(42, retrievedData.Value);
        Assert.Equal("Test", retrievedData.Name);
    }

    [Fact]
    public void PipelineExecutionContext_State_WithMissingKey_ReturnsNull()
    {
        // Arrange
        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = Mock.Of<IPipelineMemoryManager>(),
            Device = Mock.Of<IComputeDevice>()
        };

        // Act
        var result = context.State.TryGetValue("nonexistent", out var value) ? value as string : null;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void PipelineExecutionContext_Device_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var device = Mock.Of<IComputeDevice>();

        // Act
        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object>(),
            MemoryManager = Mock.Of<IPipelineMemoryManager>(),
            Device = device
        };

        // Assert
        Assert.Same(device, context.Device);
    }
}

/// <summary>
/// Test data class for state testing
/// </summary>
public sealed class TestStateData
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}
