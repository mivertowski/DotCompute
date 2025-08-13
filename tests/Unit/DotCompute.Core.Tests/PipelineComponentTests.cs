// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Pipelines;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace DotCompute.Core.Tests.Pipelines;

/// <summary>
/// Comprehensive unit tests for pipeline components with 90% coverage target.
/// Tests stage execution, optimization, and error handling.
/// </summary>
public class PipelineComponentTests : IDisposable
{
    private readonly Mock<ILogger<KernelPipeline>> _mockLogger;
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<IPipelineMemoryManager> _mockMemoryManager;
    private bool _disposed;

    public PipelineComponentTests()
    {
        _mockLogger = new Mock<ILogger<KernelPipeline>>();
        _mockAccelerator = new Mock<IAccelerator>();
        _mockMemoryManager = new Mock<IPipelineMemoryManager>();
    }

    #region Interface Contract Tests

    [Fact]
    public void IKernelPipeline_ShouldDefineRequiredMethods()
    {
        // Arrange & Act
        var interfaceType = typeof(IKernelPipeline);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.GetMethod("AddStage").Should().NotBeNull();
        interfaceType.GetMethod("ExecuteAsync").Should().NotBeNull();
        interfaceType.GetProperty("Stages").Should().NotBeNull();
        interfaceType.GetProperty("IsOptimized").Should().NotBeNull();
    }

    [Fact]
    public void IPipelineStage_ShouldDefineRequiredMembers()
    {
        // Arrange & Act
        var interfaceType = typeof(IPipelineStage);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.GetProperty("Name").Should().NotBeNull();
        interfaceType.GetProperty("ExecutionOrder").Should().NotBeNull();
        interfaceType.GetProperty("Dependencies").Should().NotBeNull();
        interfaceType.GetMethod("ExecuteAsync").Should().NotBeNull();
        interfaceType.GetMethod("CanExecute").Should().NotBeNull();
    }

    [Fact]
    public void IPipelineMemoryManager_ShouldDefineRequiredMethods()
    {
        // Arrange & Act
        var interfaceType = typeof(IPipelineMemoryManager);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.GetMethod("AllocateIntermediateBuffer").Should().NotBeNull();
        interfaceType.GetMethod("ReleaseBuffer").Should().NotBeNull();
        interfaceType.GetProperty("TotalAllocatedMemory").Should().NotBeNull();
    }

    [Fact]
    public void IPipelineMetrics_ShouldDefineRequiredProperties()
    {
        // Arrange & Act
        var interfaceType = typeof(IPipelineMetrics);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.GetProperty("TotalExecutionTime").Should().NotBeNull();
        interfaceType.GetProperty("StageExecutionTimes").Should().NotBeNull();
        interfaceType.GetProperty("MemoryUsage").Should().NotBeNull();
        interfaceType.GetProperty("ThroughputMBps").Should().NotBeNull();
    }

    #endregion

    #region KernelPipeline Tests

    [Fact]
    public void KernelPipeline_Constructor_ShouldInitializeSuccessfully()
    {
        // Act
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);

        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Stages.Should().NotBeNull();
        pipeline.Stages.Should().BeEmpty();
        pipeline.IsOptimized.Should().BeFalse();
    }

    [Fact]
    public void KernelPipeline_Constructor_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new KernelPipeline(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("accelerator");
    }

    [Fact]
    public void KernelPipeline_Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new KernelPipeline(_mockAccelerator.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void AddStage_WithValidStage_ShouldAddToStages()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);
        var mockStage = CreateMockPipelineStage("TestStage", 1);

        // Act
        pipeline.AddStage(mockStage.Object);

        // Assert
        pipeline.Stages.Should().HaveCount(1);
        pipeline.Stages.First().Should().Be(mockStage.Object);
    }

    [Fact]
    public void AddStage_WithNullStage_ShouldThrowArgumentNullException()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);

        // Act & Assert
        pipeline.Invoking(p => p.AddStage(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("stage");
    }

    [Fact]
    public void AddStage_MultipleStagesToDifferentPositions_ShouldOrderByExecutionOrder()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);
        var stage3 = CreateMockPipelineStage("Stage3", 3);
        var stage1 = CreateMockPipelineStage("Stage1", 1);
        var stage2 = CreateMockPipelineStage("Stage2", 2);

        // Act
        pipeline.AddStage(stage3.Object);
        pipeline.AddStage(stage1.Object);
        pipeline.AddStage(stage2.Object);

        // Assert
        pipeline.Stages.Should().HaveCount(3);
        pipeline.Stages.ElementAt(0).Name.Should().Be("Stage1");
        pipeline.Stages.ElementAt(1).Name.Should().Be("Stage2");
        pipeline.Stages.ElementAt(2).Name.Should().Be("Stage3");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task ExecuteAsync_WithValidStages_ShouldExecuteAllStages(int stageCount)
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);
        var stages = new List<Mock<IPipelineStage>>();
        
        for (int i = 1; i <= stageCount; i++)
        {
            var stage = CreateMockPipelineStage($"Stage{i}", i);
            stage.Setup(s => s.CanExecute(It.IsAny<PipelineContext>())).Returns(true);
            stage.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            stages.Add(stage);
            pipeline.AddStage(stage.Object);
        }

        var context = CreateMockPipelineContext();

        // Act
        await pipeline.ExecuteAsync(context.Object);

        // Assert
        foreach (var stage in stages)
        {
            stage.Verify(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);

        // Act & Assert
        await pipeline.Invoking(p => p.ExecuteAsync(null!))
            .Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task ExecuteAsync_WithStageThatCannotExecute_ShouldSkipStage()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);
        var canExecuteStage = CreateMockPipelineStage("CanExecute", 1);
        var cannotExecuteStage = CreateMockPipelineStage("CannotExecute", 2);

        canExecuteStage.Setup(s => s.CanExecute(It.IsAny<PipelineContext>())).Returns(true);
        canExecuteStage.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        cannotExecuteStage.Setup(s => s.CanExecute(It.IsAny<PipelineContext>())).Returns(false);

        pipeline.AddStage(canExecuteStage.Object);
        pipeline.AddStage(cannotExecuteStage.Object);

        var context = CreateMockPipelineContext();

        // Act
        await pipeline.ExecuteAsync(context.Object);

        // Assert
        canExecuteStage.Verify(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
        cannotExecuteStage.Verify(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);
        var stage = CreateMockPipelineStage("TestStage", 1);
        stage.Setup(s => s.CanExecute(It.IsAny<PipelineContext>())).Returns(true);
        stage.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        pipeline.AddStage(stage.Object);
        var context = CreateMockPipelineContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await pipeline.Invoking(p => p.ExecuteAsync(context.Object, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region KernelPipelineBuilder Tests

    [Fact]
    public void KernelPipelineBuilder_Constructor_ShouldInitializeSuccessfully()
    {
        // Act
        var builder = new KernelPipelineBuilder(_mockAccelerator.Object);

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void KernelPipelineBuilder_Constructor_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new KernelPipelineBuilder(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("accelerator");
    }

    [Fact]
    public void AddStage_WithValidStage_ShouldReturnBuilderForChaining()
    {
        // Arrange
        var builder = new KernelPipelineBuilder(_mockAccelerator.Object);
        var mockStage = CreateMockPipelineStage("TestStage", 1);

        // Act
        var result = builder.AddStage(mockStage.Object);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void AddStage_WithNullStage_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new KernelPipelineBuilder(_mockAccelerator.Object);

        // Act & Assert
        builder.Invoking(b => b.AddStage(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("stage");
    }

    [Fact]
    public void Build_ShouldCreatePipelineWithAddedStages()
    {
        // Arrange
        var builder = new KernelPipelineBuilder(_mockAccelerator.Object);
        var stage1 = CreateMockPipelineStage("Stage1", 1);
        var stage2 = CreateMockPipelineStage("Stage2", 2);

        builder.AddStage(stage1.Object).AddStage(stage2.Object);

        // Act
        var pipeline = builder.Build();

        // Assert
        pipeline.Should().NotBeNull();
        pipeline.Stages.Should().HaveCount(2);
        pipeline.Stages.Should().Contain(stage1.Object);
        pipeline.Stages.Should().Contain(stage2.Object);
    }

    [Fact]
    public void Build_CalledMultipleTimes_ShouldCreateDifferentInstances()
    {
        // Arrange
        var builder = new KernelPipelineBuilder(_mockAccelerator.Object);
        var stage = CreateMockPipelineStage("TestStage", 1);
        builder.AddStage(stage.Object);

        // Act
        var pipeline1 = builder.Build();
        var pipeline2 = builder.Build();

        // Assert
        pipeline1.Should().NotBeSameAs(pipeline2);
        pipeline1.Stages.Should().BeEquivalentTo(pipeline2.Stages);
    }

    #endregion

    #region PipelineMemoryManager Tests

    [Fact]
    public void PipelineMemoryManager_Constructor_ShouldInitializeSuccessfully()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var mockMemoryManager = new Mock<IMemoryManager>();
        mockAccelerator.Setup(a => a.Memory).Returns(mockMemoryManager.Object);

        // Act
        var memoryManager = new PipelineMemoryManager(mockAccelerator.Object);

        // Assert
        memoryManager.Should().NotBeNull();
        memoryManager.TotalAllocatedMemory.Should().Be(0);
    }

    [Fact]
    public void PipelineMemoryManager_Constructor_WithNullAccelerator_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new PipelineMemoryManager(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("accelerator");
    }

    [Fact]
    public async Task AllocateIntermediateBuffer_WithValidSize_ShouldReturnBuffer()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var mockMemoryManager = new Mock<IMemoryManager>();
        var mockBuffer = new Mock<IBuffer<float>>();

        mockAccelerator.Setup(a => a.Memory).Returns(mockMemoryManager.Object);
        mockMemoryManager.Setup(m => m.CreateBufferAsync<float>(
                It.IsAny<int>(), 
                It.IsAny<MemoryLocation>(), 
                It.IsAny<MemoryAccess>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBuffer.Object);

        var memoryManager = new PipelineMemoryManager(mockAccelerator.Object);

        // Act
        var buffer = await memoryManager.AllocateIntermediateBuffer<float>(1024, MemoryLocation.Device);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Should().Be(mockBuffer.Object);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task AllocateIntermediateBuffer_WithInvalidSize_ShouldThrowArgumentException(int size)
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var mockMemoryManager = new Mock<IMemoryManager>();
        mockAccelerator.Setup(a => a.Memory).Returns(mockMemoryManager.Object);

        var memoryManager = new PipelineMemoryManager(mockAccelerator.Object);

        // Act & Assert
        await memoryManager.Invoking(m => m.AllocateIntermediateBuffer<float>(size, MemoryLocation.Device))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*must be positive*");
    }

    [Fact]
    public async Task ReleaseBuffer_WithValidBuffer_ShouldDisposeBuffer()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var mockMemoryManager = new Mock<IMemoryManager>();
        var mockBuffer = new Mock<IBuffer<float>>();

        mockAccelerator.Setup(a => a.Memory).Returns(mockMemoryManager.Object);
        mockBuffer.Setup(b => b.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var memoryManager = new PipelineMemoryManager(mockAccelerator.Object);

        // Act
        await memoryManager.ReleaseBuffer(mockBuffer.Object);

        // Assert
        mockBuffer.Verify(b => b.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task ReleaseBuffer_WithNullBuffer_ShouldNotThrow()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var mockMemoryManager = new Mock<IMemoryManager>();
        mockAccelerator.Setup(a => a.Memory).Returns(mockMemoryManager.Object);

        var memoryManager = new PipelineMemoryManager(mockAccelerator.Object);

        // Act & Assert
        await memoryManager.Invoking(m => m.ReleaseBuffer<float>(null!))
            .Should().NotThrowAsync();
    }

    #endregion

    #region PipelineMetrics Tests

    [Fact]
    public void PipelineMetrics_Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var metrics = new PipelineMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.TotalExecutionTime.Should().Be(TimeSpan.Zero);
        metrics.StageExecutionTimes.Should().NotBeNull();
        metrics.StageExecutionTimes.Should().BeEmpty();
        metrics.MemoryUsage.Should().Be(0);
        metrics.ThroughputMBps.Should().Be(0);
    }

    [Fact]
    public void RecordStageExecution_WithValidData_ShouldUpdateMetrics()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var stageName = "TestStage";
        var executionTime = TimeSpan.FromMilliseconds(100);

        // Act
        metrics.RecordStageExecution(stageName, executionTime);

        // Assert
        metrics.StageExecutionTimes.Should().ContainKey(stageName);
        metrics.StageExecutionTimes[stageName].Should().Be(executionTime);
        metrics.TotalExecutionTime.Should().Be(executionTime);
    }

    [Fact]
    public void RecordStageExecution_WithMultipleStages_ShouldAccumulateTime()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var stage1Time = TimeSpan.FromMilliseconds(100);
        var stage2Time = TimeSpan.FromMilliseconds(200);

        // Act
        metrics.RecordStageExecution("Stage1", stage1Time);
        metrics.RecordStageExecution("Stage2", stage2Time);

        // Assert
        metrics.StageExecutionTimes.Should().HaveCount(2);
        metrics.TotalExecutionTime.Should().Be(stage1Time + stage2Time);
    }

    [Fact]
    public void UpdateMemoryUsage_WithValidValue_ShouldUpdateProperty()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var memoryUsage = 1024L * 1024 * 100; // 100MB

        // Act
        metrics.UpdateMemoryUsage(memoryUsage);

        // Assert
        metrics.MemoryUsage.Should().Be(memoryUsage);
    }

    [Fact]
    public void CalculateThroughput_WithValidData_ShouldReturnCorrectValue()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var dataProcessed = 1024L * 1024 * 100; // 100MB
        var executionTime = TimeSpan.FromSeconds(1);

        metrics.RecordStageExecution("TestStage", executionTime);

        // Act
        var throughput = metrics.CalculateThroughput(dataProcessed);

        // Assert
        throughput.Should().BeApproximately(100.0, 0.1); // 100 MB/s
        metrics.ThroughputMBps.Should().BeApproximately(100.0, 0.1);
    }

    [Fact]
    public void CalculateThroughput_WithZeroTime_ShouldReturnZero()
    {
        // Arrange
        var metrics = new PipelineMetrics();
        var dataProcessed = 1024L * 1024 * 100; // 100MB

        // Act
        var throughput = metrics.CalculateThroughput(dataProcessed);

        // Assert
        throughput.Should().Be(0);
        metrics.ThroughputMBps.Should().Be(0);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Pipeline_WithStageException_ShouldPropagateException()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);
        var stage = CreateMockPipelineStage("FailingStage", 1);
        var expectedException = new InvalidOperationException("Stage failed");

        stage.Setup(s => s.CanExecute(It.IsAny<PipelineContext>())).Returns(true);
        stage.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        pipeline.AddStage(stage.Object);
        var context = CreateMockPipelineContext();

        // Act & Assert
        await pipeline.Invoking(p => p.ExecuteAsync(context.Object))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stage failed");
    }

    [Fact]
    public async Task Pipeline_WithStageTimeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var pipeline = new KernelPipeline(_mockAccelerator.Object, _mockLogger.Object);
        var stage = CreateMockPipelineStage("SlowStage", 1);

        stage.Setup(s => s.CanExecute(It.IsAny<PipelineContext>())).Returns(true);
        stage.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (PipelineContext ctx, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct); // Long delay
            });

        pipeline.AddStage(stage.Object);
        var context = CreateMockPipelineContext();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await pipeline.Invoking(p => p.ExecuteAsync(context.Object, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Helper Methods

    private Mock<IPipelineStage> CreateMockPipelineStage(string name, int executionOrder)
    {
        var mockStage = new Mock<IPipelineStage>();
        mockStage.Setup(s => s.Name).Returns(name);
        mockStage.Setup(s => s.ExecutionOrder).Returns(executionOrder);
        mockStage.Setup(s => s.Dependencies).Returns(new List<string>());
        return mockStage;
    }

    private Mock<PipelineContext> CreateMockPipelineContext()
    {
        var mockContext = new Mock<PipelineContext>();
        mockContext.Setup(c => c.MemoryManager).Returns(_mockMemoryManager.Object);
        mockContext.Setup(c => c.Metrics).Returns(new PipelineMetrics());
        mockContext.Setup(c => c.Properties).Returns(new Dictionary<string, object>());
        return mockContext;
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Tests for pipeline optimization and advanced features.
/// </summary>
public class PipelineOptimizationTests
{
    private readonly Mock<ILogger<PipelineOptimizer>> _mockLogger;
    private readonly Mock<IAccelerator> _mockAccelerator;

    public PipelineOptimizationTests()
    {
        _mockLogger = new Mock<ILogger<PipelineOptimizer>>();
        _mockAccelerator = new Mock<IAccelerator>();
    }

    [Fact]
    public void PipelineOptimizer_Constructor_ShouldInitializeSuccessfully()
    {
        // Act
        var optimizer = new PipelineOptimizer(_mockLogger.Object);

        // Assert
        optimizer.Should().NotBeNull();
    }

    [Fact]
    public void PipelineOptimizer_Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Action act = () => new PipelineOptimizer(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void OptimizePipeline_WithValidPipeline_ShouldReturnOptimizedPipeline()
    {
        // Arrange
        var optimizer = new PipelineOptimizer(_mockLogger.Object);
        var mockPipeline = new Mock<IKernelPipeline>();
        var stages = new List<IPipelineStage>();
        mockPipeline.Setup(p => p.Stages).Returns(stages);
        mockPipeline.Setup(p => p.IsOptimized).Returns(false);

        // Act
        var optimizedPipeline = optimizer.OptimizePipeline(mockPipeline.Object);

        // Assert
        optimizedPipeline.Should().NotBeNull();
        // Additional assertions would depend on the actual optimization implementation
    }

    [Fact]
    public void OptimizePipeline_WithNullPipeline_ShouldThrowArgumentNullException()
    {
        // Arrange
        var optimizer = new PipelineOptimizer(_mockLogger.Object);

        // Act & Assert
        optimizer.Invoking(o => o.OptimizePipeline(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }

    [Fact]
    public void OptimizePipeline_WithAlreadyOptimizedPipeline_ShouldReturnSamePipeline()
    {
        // Arrange
        var optimizer = new PipelineOptimizer(_mockLogger.Object);
        var mockPipeline = new Mock<IKernelPipeline>();
        mockPipeline.Setup(p => p.IsOptimized).Returns(true);

        // Act
        var result = optimizer.OptimizePipeline(mockPipeline.Object);

        // Assert
        result.Should().Be(mockPipeline.Object);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void AnalyzeDependencies_WithVariousStages_ShouldDetectDependencies(int stageCount)
    {
        // Arrange
        var optimizer = new PipelineOptimizer(_mockLogger.Object);
        var stages = new List<IPipelineStage>();

        for (int i = 0; i < stageCount; i++)
        {
            var mockStage = new Mock<IPipelineStage>();
            mockStage.Setup(s => s.Name).Returns($"Stage{i}");
            mockStage.Setup(s => s.Dependencies).Returns(
                i > 0 ? new List<string> { $"Stage{i-1}" } : new List<string>());
            stages.Add(mockStage.Object);
        }

        // Act
        var dependencies = optimizer.AnalyzeDependencies(stages);

        // Assert
        dependencies.Should().NotBeNull();
        dependencies.Should().HaveCount(stageCount);
    }

    [Fact]
    public void AnalyzeDependencies_WithCircularDependency_ShouldDetectCircularReference()
    {
        // Arrange
        var optimizer = new PipelineOptimizer(_mockLogger.Object);
        var stage1 = new Mock<IPipelineStage>();
        var stage2 = new Mock<IPipelineStage>();

        stage1.Setup(s => s.Name).Returns("Stage1");
        stage1.Setup(s => s.Dependencies).Returns(new List<string> { "Stage2" });

        stage2.Setup(s => s.Name).Returns("Stage2");
        stage2.Setup(s => s.Dependencies).Returns(new List<string> { "Stage1" });

        var stages = new List<IPipelineStage> { stage1.Object, stage2.Object };

        // Act & Assert
        optimizer.Invoking(o => o.AnalyzeDependencies(stages))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*circular dependency*");
    }
}