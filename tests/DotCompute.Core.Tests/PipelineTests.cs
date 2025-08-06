// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Pipelines;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for Pipeline classes.
/// </summary>
public sealed class PipelineTests
{
    [Fact]
    public void KernelPipelineBuilder_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KernelPipelineBuilder(null!));
    }

    [Fact]
    public void KernelPipelineBuilder_AddStage_AddsStageSuccessfully()
    {
        // Arrange
        var logger = Mock.Of<ILogger<KernelPipelineBuilder>>();
        var builder = new KernelPipelineBuilder(logger);
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
        var logger = Mock.Of<ILogger<KernelPipelineBuilder>>();
        var builder = new KernelPipelineBuilder(logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.AddStage(null!));
    }

    [Fact]
    public void KernelPipelineBuilder_WithMemoryManager_SetsMemoryManager()
    {
        // Arrange
        var logger = Mock.Of<ILogger<KernelPipelineBuilder>>();
        var builder = new KernelPipelineBuilder(logger);
        var memoryManager = Mock.Of<IPipelineMemoryManager>();

        // Act
        var result = builder.WithMemoryManager(memoryManager);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void KernelPipelineBuilder_WithOptimizer_SetsOptimizer()
    {
        // Arrange
        var logger = Mock.Of<ILogger<KernelPipelineBuilder>>();
        var builder = new KernelPipelineBuilder(logger);
        var optimizer = Mock.Of<IPipelineOptimizer>();

        // Act
        var result = builder.WithOptimizer(optimizer);

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void KernelPipelineBuilder_WithMetrics_EnablesMetrics()
    {
        // Arrange
        var logger = Mock.Of<ILogger<KernelPipelineBuilder>>();
        var builder = new KernelPipelineBuilder(logger);

        // Act
        var result = builder.WithMetrics();

        // Assert
        Assert.Same(builder, result);
    }

    [Fact]
    public void KernelPipelineBuilder_Build_CreatesPipeline()
    {
        // Arrange
        var logger = Mock.Of<ILogger<KernelPipelineBuilder>>();
        var builder = new KernelPipelineBuilder(logger);
        var stage1 = Mock.Of<IPipelineStage>(s => s.Name == "Stage1");
        var stage2 = Mock.Of<IPipelineStage>(s => s.Name == "Stage2");

        builder.AddStage(stage1).AddStage(stage2);

        // Act
        var pipeline = builder.Build();

        // Assert
        Assert.NotNull(pipeline);
        Assert.IsType<KernelPipeline>(pipeline);
    }

    [Fact]
    public void KernelPipelineBuilder_Build_WithNoStages_ThrowsInvalidOperationException()
    {
        // Arrange
        var logger = Mock.Of<ILogger<KernelPipelineBuilder>>();
        var builder = new KernelPipelineBuilder(logger);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_ExecutesAllStages()
    {
        // Arrange
        var context = new PipelineContext();
        var executedStages = new List<string>();
        
        var stage1 = new Mock<IPipelineStage>();
        stage1.Setup(s => s.Name).Returns("Stage1");
        stage1.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executedStages.Add("Stage1"))
            .Returns(ValueTask.CompletedTask);

        var stage2 = new Mock<IPipelineStage>();
        stage2.Setup(s => s.Name).Returns("Stage2");
        stage2.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executedStages.Add("Stage2"))
            .Returns(ValueTask.CompletedTask);

        var logger = Mock.Of<ILogger<KernelPipeline>>();
        var pipeline = new KernelPipeline(logger, new[] { stage1.Object, stage2.Object });

        // Act
        await pipeline.ExecuteAsync(context);

        // Assert
        Assert.Equal(new[] { "Stage1", "Stage2" }, executedStages);
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_WithCancellation_StopsExecution()
    {
        // Arrange
        var context = new PipelineContext();
        var cts = new CancellationTokenSource();
        var executedStages = new List<string>();
        
        var stage1 = new Mock<IPipelineStage>();
        stage1.Setup(s => s.Name).Returns("Stage1");
        stage1.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                executedStages.Add("Stage1");
                cts.Cancel();
            })
            .Returns(ValueTask.CompletedTask);

        var stage2 = new Mock<IPipelineStage>();
        stage2.Setup(s => s.Name).Returns("Stage2");
        stage2.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => executedStages.Add("Stage2"))
            .Returns(ValueTask.CompletedTask);

        var logger = Mock.Of<ILogger<KernelPipeline>>();
        var pipeline = new KernelPipeline(logger, new[] { stage1.Object, stage2.Object });

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            pipeline.ExecuteAsync(context, cts.Token).AsTask());
        
        Assert.Equal(new[] { "Stage1" }, executedStages);
    }

    [Fact]
    public async Task KernelPipeline_ExecuteAsync_WithFailingStage_PropagatesException()
    {
        // Arrange
        var context = new PipelineContext();
        var expectedException = new InvalidOperationException("Stage failed");
        
        var stage1 = new Mock<IPipelineStage>();
        stage1.Setup(s => s.Name).Returns("Stage1");
        stage1.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var logger = Mock.Of<ILogger<KernelPipeline>>();
        var pipeline = new KernelPipeline(logger, new[] { stage1.Object });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            pipeline.ExecuteAsync(context).AsTask());
        
        Assert.Same(expectedException, exception);
    }

    [Fact]
    public void PipelineOptimizer_OptimizePipeline_ReordersStagesForEfficiency()
    {
        // Arrange
        var logger = Mock.Of<ILogger<PipelineOptimizer>>();
        var optimizer = new PipelineOptimizer(logger);
        
        var stages = new List<IPipelineStage>
        {
            Mock.Of<IPipelineStage>(s => s.Name == "Heavy" && s.EstimatedCost == 100),
            Mock.Of<IPipelineStage>(s => s.Name == "Light" && s.EstimatedCost == 10),
            Mock.Of<IPipelineStage>(s => s.Name == "Medium" && s.EstimatedCost == 50)
        };

        // Act
        var optimized = optimizer.OptimizePipeline(stages);

        // Assert
        Assert.Equal(3, optimized.Count());
        var optimizedList = optimized.ToList();
        Assert.Equal("Light", optimizedList[0].Name);
        Assert.Equal("Medium", optimizedList[1].Name);
        Assert.Equal("Heavy", optimizedList[2].Name);
    }

    [Fact]
    public void PipelineOptimizer_OptimizePipeline_WithDependencies_RespectsOrder()
    {
        // Arrange
        var logger = Mock.Of<ILogger<PipelineOptimizer>>();
        var optimizer = new PipelineOptimizer(logger);
        
        var stage1 = Mock.Of<IPipelineStage>(s => s.Name == "Stage1" && s.EstimatedCost == 100);
        var stage2 = Mock.Of<IPipelineStage>(s => 
            s.Name == "Stage2" && 
            s.EstimatedCost == 10 && 
            s.Dependencies == new[] { "Stage1" });
        
        var stages = new List<IPipelineStage> { stage2, stage1 };

        // Act
        var optimized = optimizer.OptimizePipeline(stages);

        // Assert
        var optimizedList = optimized.ToList();
        Assert.Equal("Stage1", optimizedList[0].Name);
        Assert.Equal("Stage2", optimizedList[1].Name);
    }

    [Fact]
    public async Task PipelineMetrics_RecordStageExecution_TracksMetrics()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var stageName = "TestStage";
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        metrics.RecordStageExecution(stageName, duration, success: true);
        var stageMetrics = await metrics.GetStageMetricsAsync(stageName);

        // Assert
        Assert.NotNull(stageMetrics);
        Assert.Equal(1, stageMetrics.ExecutionCount);
        Assert.Equal(100, stageMetrics.AverageExecutionTime.TotalMilliseconds);
        Assert.Equal(1, stageMetrics.SuccessCount);
        Assert.Equal(0, stageMetrics.FailureCount);
    }

    [Fact]
    public async Task PipelineMetrics_RecordMemoryUsage_TracksMemoryMetrics()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        const long memoryUsed = 1024 * 1024; // 1MB

        // Act
        metrics.RecordMemoryUsage(memoryUsed);
        var memoryMetrics = await metrics.GetMemoryMetricsAsync();

        // Assert
        Assert.Equal(memoryUsed, memoryMetrics.CurrentUsage);
        Assert.Equal(memoryUsed, memoryMetrics.PeakUsage);
    }

    [Fact]
    public async Task PipelineMetrics_GetOverallMetrics_ReturnsAggregatedData()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        
        metrics.RecordStageExecution("Stage1", TimeSpan.FromMilliseconds(100), success: true);
        metrics.RecordStageExecution("Stage2", TimeSpan.FromMilliseconds(200), success: true);
        metrics.RecordStageExecution("Stage3", TimeSpan.FromMilliseconds(150), success: false);
        metrics.RecordMemoryUsage(1024 * 1024);

        // Act
        var overall = await metrics.GetOverallMetricsAsync();

        // Assert
        Assert.Equal(3, overall.TotalExecutions);
        Assert.Equal(2, overall.SuccessfulExecutions);
        Assert.Equal(1, overall.FailedExecutions);
        Assert.Equal(450, overall.TotalExecutionTime.TotalMilliseconds);
        Assert.Equal(150, overall.AverageExecutionTime.TotalMilliseconds);
        Assert.Equal(1024 * 1024, overall.PeakMemoryUsage);
    }

    [Fact]
    public void PipelineContext_SetAndGetData_WorksCorrectly()
    {
        // Arrange
        var context = new PipelineContext();
        var testData = new { Value = 42, Name = "Test" };

        // Act
        context.SetData("test", testData);
        var retrieved = context.GetData<object>("test");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(42, ((dynamic)retrieved).Value);
        Assert.Equal("Test", ((dynamic)retrieved).Name);
    }

    [Fact]
    public void PipelineContext_GetData_WithMissingKey_ReturnsNull()
    {
        // Arrange
        var context = new PipelineContext();

        // Act
        var result = context.GetData<string>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void PipelineContext_Kernel_SetAndGet_WorksCorrectly()
    {
        // Arrange
        var context = new PipelineContext();
        var kernel = Mock.Of<ICompiledKernel>();

        // Act
        context.Kernel = kernel;

        // Assert
        Assert.Same(kernel, context.Kernel);
    }
}