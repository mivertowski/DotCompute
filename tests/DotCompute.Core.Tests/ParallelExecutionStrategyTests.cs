// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Execution;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive tests for parallel execution strategies with mock GPUs that can run on CI/CD.
/// Tests data parallel, model parallel, pipeline parallel, and work-stealing execution patterns.
/// </summary>
public class ParallelExecutionStrategyTests : IAsyncDisposable
{
    private readonly Mock<IAcceleratorManager> _mockAcceleratorManager;
    private readonly Mock<KernelManager> _mockKernelManager;
    private readonly NullLoggerFactory _loggerFactory;
    private readonly NullLogger<ParallelExecutionStrategy> _logger;
    private readonly List<Mock<IAccelerator>> _mockAccelerators;
    private ParallelExecutionStrategy _strategy;

    public ParallelExecutionStrategyTests()
    {
        _loggerFactory = new NullLoggerFactory();
        _logger = new NullLogger<ParallelExecutionStrategy>();
        _mockAcceleratorManager = new Mock<IAcceleratorManager>();
        _mockKernelManager = new Mock<KernelManager>();
        
        // Create mock GPU accelerators
        _mockAccelerators = CreateMockGPUAccelerators(4);
        SetupMockAcceleratorManager();
        
        _strategy = new ParallelExecutionStrategy(_logger, _mockAcceleratorManager.Object, _mockKernelManager.Object, _loggerFactory);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ParallelExecutionStrategy(null!, _mockAcceleratorManager.Object, _mockKernelManager.Object));
    }

    [Fact]
    public void Constructor_WithNullAcceleratorManager_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ParallelExecutionStrategy(_logger, null!, _mockKernelManager.Object));
    }

    [Fact]
    public void Constructor_WithNullKernelManager_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ParallelExecutionStrategy(_logger, _mockAcceleratorManager.Object, null!));
    }

    [Fact]
    public void AvailableStrategies_WithMultipleGPUs_ShouldIncludeAllStrategies()
    {
        // Act
        var strategies = _strategy.AvailableStrategies;

        // Assert
        Assert.Contains(ExecutionStrategyType.DataParallel, strategies);
        Assert.Contains(ExecutionStrategyType.ModelParallel, strategies);
        Assert.Contains(ExecutionStrategyType.PipelineParallel, strategies);
        Assert.Contains(ExecutionStrategyType.WorkStealing, strategies);
        Assert.Contains(ExecutionStrategyType.Heterogeneous, strategies);
    }

    [Fact]
    public void CurrentMetrics_ShouldReturnValidMetrics()
    {
        // Act
        var metrics = _strategy.CurrentMetrics;

        // Assert
        Assert.NotNull(metrics);
        Assert.True(metrics.TotalExecutions >= 0);
        Assert.True(metrics.AverageExecutionTimeMs >= 0);
        Assert.True(metrics.AverageEfficiencyPercentage >= 0);
        Assert.True(metrics.TotalGFLOPSHours >= 0);
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_WithValidInputs_ShouldExecuteSuccessfully()
    {
        // Arrange
        var inputBuffers = CreateMockBuffers<float>(1024, 2);
        var outputBuffers = CreateMockBuffers<float>(1024, 1);
        var options = new DataParallelismOptions
        {
            MaxDevices = 2,
            EnablePeerToPeer = true,
            LoadBalancing = LoadBalancingStrategy.Adaptive
        };

        // Act
        var result = await _strategy.ExecuteDataParallelAsync("test_kernel", inputBuffers, outputBuffers, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(ExecutionStrategyType.DataParallel, result.Strategy);
        Assert.True(result.TotalExecutionTimeMs > 0);
        Assert.True(result.EfficiencyPercentage >= 0);
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_WithNullInputBuffers_ShouldThrowArgumentNullException()
    {
        // Arrange
        var outputBuffers = CreateMockBuffers<float>(1024, 1);
        var options = new DataParallelismOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _strategy.ExecuteDataParallelAsync("test_kernel", null!, outputBuffers, options).AsTask());
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_WithEmptyOutputBuffers_ShouldThrowArgumentException()
    {
        // Arrange
        var inputBuffers = CreateMockBuffers<float>(1024, 2);
        var options = new DataParallelismOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _strategy.ExecuteDataParallelAsync("test_kernel", inputBuffers, Array.Empty<AbstractionsMemory.IBuffer<float>>(), options).AsTask());
    }

    [Fact]
    public async Task ExecuteModelParallelAsync_WithValidWorkload_ShouldExecuteSuccessfully()
    {
        // Arrange
        var workload = CreateMockModelParallelWorkload<float>();
        var options = new ModelParallelismOptions
        {
            CommunicationBackend = CommunicationBackend.P2P,
            MemoryOptimization = MemoryOptimizationLevel.Balanced
        };

        // Act
        var result = await _strategy.ExecuteModelParallelAsync("model_kernel", workload, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(ExecutionStrategyType.ModelParallel, result.Strategy);
        Assert.NotEmpty(result.DeviceResults);
    }

    [Fact]
    public async Task ExecutePipelineParallelAsync_WithValidPipeline_ShouldExecuteSuccessfully()
    {
        // Arrange
        var pipeline = CreateMockPipelineDefinition<float>();
        var options = new PipelineParallelismOptions
        {
            MicrobatchSize = 32,
            OverlapCommunication = true
        };

        // Act
        var result = await _strategy.ExecutePipelineParallelAsync(pipeline, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(ExecutionStrategyType.PipelineParallel, result.Strategy);
        Assert.True(result.TotalExecutionTimeMs > 0);
    }

    [Fact]
    public async Task ExecuteWithWorkStealingAsync_WithValidWorkload_ShouldExecuteSuccessfully()
    {
        // Arrange
        var workload = CreateMockWorkStealingWorkload<float>();
        var options = new WorkStealingOptions
        {
            StealingStrategy = StealingStrategy.Random,
            WorkItemGranularity = 64
        };

        // Act
        var result = await _strategy.ExecuteWithWorkStealingAsync("work_stealing_kernel", workload, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(ExecutionStrategyType.WorkStealing, result.Strategy);
        Assert.NotEmpty(result.DeviceResults);
    }

    [Fact]
    public async Task SynchronizeAllAsync_WithMultipleAccelerators_ShouldSynchronizeAll()
    {
        // Act & Assert - Should complete without throwing
        await _strategy.SynchronizeAllAsync();
        
        // Verify all accelerators were synchronized
        foreach (var mockAccelerator in _mockAccelerators)
        {
            mockAccelerator.Verify(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }

    [Fact]
    public void GetPerformanceAnalysis_ShouldReturnValidAnalysis()
    {
        // Act
        var analysis = _strategy.GetPerformanceAnalysis();

        // Assert
        Assert.NotNull(analysis);
        Assert.True(analysis.OverallRating >= 0 && analysis.OverallRating <= 10);
        Assert.NotNull(analysis.RecommendedStrategy);
        Assert.NotNull(analysis.Bottlenecks);
        Assert.NotNull(analysis.OptimizationRecommendations);
        Assert.NotNull(analysis.DeviceUtilizationAnalysis);
    }

    [Fact]
    public void OptimizeStrategy_WithValidParameters_ShouldReturnRecommendation()
    {
        // Arrange
        var kernelName = "matrix_multiply";
        var inputSizes = new[] { 2048, 2048 };
        var acceleratorTypes = new[] { AcceleratorType.CUDA, AcceleratorType.OpenCL };

        // Act
        var recommendation = _strategy.OptimizeStrategy(kernelName, inputSizes, acceleratorTypes);

        // Assert
        Assert.NotNull(recommendation);
        Assert.True(Enum.IsDefined(typeof(ExecutionStrategyType), recommendation.Strategy));
        Assert.True(recommendation.ConfidenceScore >= 0 && recommendation.ConfidenceScore <= 1);
        Assert.True(recommendation.ExpectedImprovementPercentage >= 0);
        Assert.NotNull(recommendation.Reasoning);
    }

    [Theory]
    [InlineData(1, new[] { ExecutionStrategyType.Single })]
    [InlineData(2, new[] { ExecutionStrategyType.DataParallel, ExecutionStrategyType.ModelParallel })]
    [InlineData(4, new[] { ExecutionStrategyType.DataParallel, ExecutionStrategyType.ModelParallel, ExecutionStrategyType.PipelineParallel, ExecutionStrategyType.WorkStealing })]
    public void AvailableStrategies_WithDifferentGPUCounts_ShouldAdaptStrategies(int gpuCount, ExecutionStrategyType[] expectedStrategies)
    {
        // Arrange
        var mockAccelerators = CreateMockGPUAccelerators(gpuCount);
        var mockManager = new Mock<IAcceleratorManager>();
        mockManager.Setup(m => m.AvailableAccelerators).Returns(mockAccelerators.Select(m => m.Object).ToArray());
        mockManager.Setup(m => m.Count).Returns(gpuCount);
        mockManager.Setup(m => m.Default).Returns(mockAccelerators.First().Object);

        using var strategy = new ParallelExecutionStrategy(_logger, mockManager.Object, _mockKernelManager.Object, _loggerFactory);

        // Act
        var strategies = strategy.AvailableStrategies;

        // Assert
        Assert.All(expectedStrategies, expectedStrategy => 
            Assert.Contains(expectedStrategy, strategies));
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var inputBuffers = CreateMockBuffers<float>(1024, 2);
        var outputBuffers = CreateMockBuffers<float>(1024, 1);
        var options = new DataParallelismOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _strategy.ExecuteDataParallelAsync("test_kernel", inputBuffers, outputBuffers, options, cts.Token).AsTask());
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_ConcurrentExecutions_ShouldHandleParallelism()
    {
        // Arrange
        var inputBuffers1 = CreateMockBuffers<float>(512, 1);
        var outputBuffers1 = CreateMockBuffers<float>(512, 1);
        var inputBuffers2 = CreateMockBuffers<float>(1024, 1);
        var outputBuffers2 = CreateMockBuffers<float>(1024, 1);
        var options = new DataParallelismOptions();

        // Act - Execute multiple kernels concurrently
        var task1 = _strategy.ExecuteDataParallelAsync("kernel1", inputBuffers1, outputBuffers1, options);
        var task2 = _strategy.ExecuteDataParallelAsync("kernel2", inputBuffers2, outputBuffers2, options);

        var results = await Task.WhenAll(task1.AsTask(), task2.AsTask());

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.True(result.Success);
        });
    }

    [Theory]
    [InlineData(LoadBalancingStrategy.EvenDistribution)]
    [InlineData(LoadBalancingStrategy.Adaptive)]
    [InlineData(LoadBalancingStrategy.WorkloadAware)]
    public async Task ExecuteDataParallelAsync_WithDifferentLoadBalancing_ShouldAdaptStrategy(LoadBalancingStrategy strategy)
    {
        // Arrange
        var inputBuffers = CreateMockBuffers<float>(2048, 1);
        var outputBuffers = CreateMockBuffers<float>(2048, 1);
        var options = new DataParallelismOptions
        {
            LoadBalancing = strategy,
            MaxDevices = 3
        };

        // Act
        var result = await _strategy.ExecuteDataParallelAsync("adaptive_kernel", inputBuffers, outputBuffers, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(ExecutionStrategyType.DataParallel, result.Strategy);
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_WithUnevenBufferSizes_ShouldHandleImbalance()
    {
        // Arrange - Create buffers that don't divide evenly across GPUs
        var inputBuffers = CreateMockBuffers<float>(1000, 1); // 1000 elements across 4 GPUs = 250 per GPU
        var outputBuffers = CreateMockBuffers<float>(1000, 1);
        var options = new DataParallelismOptions
        {
            MaxDevices = 3 // Will create uneven distribution: 334, 333, 333
        };

        // Act
        var result = await _strategy.ExecuteDataParallelAsync("uneven_kernel", inputBuffers, outputBuffers, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        // Should handle the uneven distribution gracefully
    }

    [Fact]
    public async Task ExecuteWithWorkStealingAsync_WithLargeWorkload_ShouldDistributeWorkEfficiently()
    {
        // Arrange
        var workload = CreateLargeWorkStealingWorkload<float>(10000); // Large number of work items
        var options = new WorkStealingOptions
        {
            StealingStrategy = StealingStrategy.RichestVictim,
            WorkItemGranularity = 100
        };

        // Act
        var result = await _strategy.ExecuteWithWorkStealingAsync("large_workload", workload, options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.EfficiencyPercentage > 50); // Should have reasonable efficiency with work stealing
    }

    #region Helper Methods

    private List<Mock<IAccelerator>> CreateMockGPUAccelerators(int count)
    {
        var accelerators = new List<Mock<IAccelerator>>();

        for (int i = 0; i < count; i++)
        {
            var mock = new Mock<IAccelerator>();
            mock.Setup(a => a.Info).Returns(new AcceleratorInfo
            {
                Id = $"gpu_{i}",
                Name = $"Mock GPU {i}",
                DeviceType = i % 2 == 0 ? "CUDA" : "OpenCL",
                ComputeUnits = 2048,
                MaxWorkGroupSize = 1024,
                MaxWorkItemDimensions = 3,
                MaxWorkItemSizes = new[] { 1024, 1024, 64 },
                TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
                LocalMemorySize = 48 * 1024, // 48KB
                Vendor = "Mock Vendor",
                DriverVersion = "1.0"
            });

            mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            accelerators.Add(mock);
        }

        // Add one CPU accelerator for heterogeneous execution
        var cpuMock = new Mock<IAccelerator>();
        cpuMock.Setup(a => a.Info).Returns(new AcceleratorInfo
        {
            Id = "cpu_0",
            Name = "Mock CPU",
            DeviceType = "CPU",
            ComputeUnits = 16,
            MaxWorkGroupSize = 256,
            TotalMemory = 16L * 1024 * 1024 * 1024, // 16GB
            Vendor = "Mock Vendor"
        });
        cpuMock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        accelerators.Add(cpuMock);

        return accelerators;
    }

    private void SetupMockAcceleratorManager()
    {
        var allAccelerators = _mockAccelerators.Select(m => m.Object).ToArray();
        
        _mockAcceleratorManager.Setup(m => m.AvailableAccelerators).Returns(allAccelerators);
        _mockAcceleratorManager.Setup(m => m.Count).Returns(allAccelerators.Length);
        _mockAcceleratorManager.Setup(m => m.Default).Returns(allAccelerators.First());
        
        _mockAcceleratorManager.Setup(m => m.GetAcceleratorById(It.IsAny<string>()))
            .Returns<string>(id => allAccelerators.FirstOrDefault(a => a.Info.Id == id));
    }

    private AbstractionsMemory.IBuffer<T>[] CreateMockBuffers<T>(int elementCount, int bufferCount) where T : unmanaged
    {
        var buffers = new AbstractionsMemory.IBuffer<T>[bufferCount];
        
        for (int i = 0; i < bufferCount; i++)
        {
            var mock = new Mock<AbstractionsMemory.IBuffer<T>>();
            mock.Setup(b => b.SizeInBytes).Returns(elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>());
            mock.Setup(b => b.IsDisposed).Returns(false);
            buffers[i] = mock.Object;
        }
        
        return buffers;
    }

    private ModelParallelWorkload<T> CreateMockModelParallelWorkload<T>() where T : unmanaged
    {
        return new ModelParallelWorkload<T>
        {
            ModelLayers = new List<ModelLayer<T>>
            {
                new ModelLayer<T> { LayerId = 0, LayerType = "Linear", InputShape = new[] { 512, 1024 }, OutputShape = new[] { 512, 512 } },
                new ModelLayer<T> { LayerId = 1, LayerType = "ReLU", InputShape = new[] { 512, 512 }, OutputShape = new[] { 512, 512 } },
                new ModelLayer<T> { LayerId = 2, LayerType = "Linear", InputShape = new[] { 512, 512 }, OutputShape = new[] { 512, 256 } },
                new ModelLayer<T> { LayerId = 3, LayerType = "Softmax", InputShape = new[] { 512, 256 }, OutputShape = new[] { 512, 256 } }
            }
        };
    }

    private PipelineDefinition<T> CreateMockPipelineDefinition<T>() where T : unmanaged
    {
        return new PipelineDefinition<T>
        {
            Stages = new List<PipelineStageDefinition>
            {
                new PipelineStageDefinition { Name = "Preprocessing", KernelName = "preprocess", EstimatedTimeMs = 10 },
                new PipelineStageDefinition { Name = "Computation", KernelName = "compute", EstimatedTimeMs = 50 },
                new PipelineStageDefinition { Name = "Postprocessing", KernelName = "postprocess", EstimatedTimeMs = 15 }
            }
        };
    }

    private WorkStealingWorkload<T> CreateMockWorkStealingWorkload<T>() where T : unmanaged
    {
        return new WorkStealingWorkload<T>
        {
            WorkItems = Enumerable.Range(0, 1000)
                .Select(i => new WorkItem<T> 
                { 
                    Id = i, 
                    EstimatedComplexity = (i % 10) + 1, 
                    Data = default(T) 
                })
                .ToList()
        };
    }

    private WorkStealingWorkload<T> CreateLargeWorkStealingWorkload<T>(int itemCount) where T : unmanaged
    {
        return new WorkStealingWorkload<T>
        {
            WorkItems = Enumerable.Range(0, itemCount)
                .Select(i => new WorkItem<T> 
                { 
                    Id = i, 
                    EstimatedComplexity = (i % 50) + 1,
                    Data = default(T) 
                })
                .ToList()
        };
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_strategy != null)
        {
            await _strategy.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}

#region Mock Parallel Execution Types

// These would normally be defined in the actual implementation
public class DataParallelismOptions
{
    public int? MaxDevices { get; set; }
    public string[]? TargetDevices { get; set; }
    public bool EnablePeerToPeer { get; set; } = true;
    public SynchronizationStrategy SyncStrategy { get; set; } = SynchronizationStrategy.EventBased;
    public LoadBalancingStrategy LoadBalancing { get; set; } = LoadBalancingStrategy.EvenDistribution;
}

public class ModelParallelismOptions
{
    public CommunicationBackend CommunicationBackend { get; set; } = CommunicationBackend.P2P;
    public MemoryOptimizationLevel MemoryOptimization { get; set; } = MemoryOptimizationLevel.Balanced;
    
    public DataParallelismOptions ToDataParallelOptions()
    {
        return new DataParallelismOptions();
    }
}

public class PipelineParallelismOptions
{
    public int MicrobatchSize { get; set; } = 32;
    public bool OverlapCommunication { get; set; } = true;
    
    public DataParallelismOptions ToDataParallelOptions()
    {
        return new DataParallelismOptions();
    }
}

public class WorkStealingOptions
{
    public StealingStrategy StealingStrategy { get; set; } = StealingStrategy.Random;
    public int WorkItemGranularity { get; set; } = 64;
    
    public DataParallelismOptions ToDataParallelOptions()
    {
        return new DataParallelismOptions();
    }
}

public class ModelParallelWorkload<T> where T : unmanaged
{
    public List<ModelLayer<T>> ModelLayers { get; set; } = new();
}

public class ModelLayer<T> where T : unmanaged
{
    public int LayerId { get; set; }
    public string LayerType { get; set; } = "";
    public int[] InputShape { get; set; } = Array.Empty<int>();
    public int[] OutputShape { get; set; } = Array.Empty<int>();
}

public class PipelineDefinition<T> where T : unmanaged
{
    public List<PipelineStageDefinition> Stages { get; set; } = new();
}

public class PipelineStageDefinition
{
    public string Name { get; set; } = "";
    public string KernelName { get; set; } = "";
    public double EstimatedTimeMs { get; set; }
}

public class WorkStealingWorkload<T> where T : unmanaged
{
    public List<WorkItem<T>> WorkItems { get; set; } = new();
}

public class WorkItem<T> where T : unmanaged
{
    public int Id { get; set; }
    public int EstimatedComplexity { get; set; }
    public T Data { get; set; }
}

public enum ExecutionStrategyType
{
    Single,
    DataParallel,
    ModelParallel,
    PipelineParallel,
    WorkStealing,
    Heterogeneous
}

public enum LoadBalancingStrategy
{
    EvenDistribution,
    Adaptive,
    WorkloadAware
}

public enum SynchronizationStrategy
{
    EventBased,
    Barrier,
    LockFree
}

public enum CommunicationBackend
{
    P2P,
    NCCL,
    MPI
}

public enum MemoryOptimizationLevel
{
    Conservative,
    Balanced,
    Aggressive
}

public enum StealingStrategy
{
    Random,
    RichestVictim,
    NearestNeighbor
}

// Placeholder for existing types
public class ParallelExecutionMetrics
{
    public int TotalExecutions { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double AverageEfficiencyPercentage { get; set; }
    public double TotalGFLOPSHours { get; set; }
}

public class ParallelExecutionResult
{
    public bool Success { get; set; }
    public double TotalExecutionTimeMs { get; set; }
    public DeviceExecutionResult[] DeviceResults { get; set; } = Array.Empty<DeviceExecutionResult>();
    public ExecutionStrategyType Strategy { get; set; }
    public double ThroughputGFLOPS { get; set; }
    public double MemoryBandwidthGBps { get; set; }
    public double EfficiencyPercentage { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DeviceExecutionResult
{
    public string DeviceId { get; set; } = "";
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
    public long ElementsProcessed { get; set; }
    public double MemoryBandwidthGBps { get; set; }
    public double ThroughputGFLOPS { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ParallelExecutionAnalysis
{
    public double OverallRating { get; set; } = 7.5;
    public ExecutionStrategyType RecommendedStrategy { get; set; } = ExecutionStrategyType.DataParallel;
    public List<BottleneckAnalysis> Bottlenecks { get; set; } = new();
    public List<string> OptimizationRecommendations { get; set; } = new() { "Consider data locality optimizations", "Evaluate memory bandwidth usage" };
    public Dictionary<string, double> DeviceUtilizationAnalysis { get; set; } = new();
}

public class ExecutionStrategyRecommendation
{
    public ExecutionStrategyType Strategy { get; set; }
    public double ConfidenceScore { get; set; } = 0.8;
    public double ExpectedImprovementPercentage { get; set; } = 25.0;
    public string Reasoning { get; set; } = "Data parallel execution recommended for this workload size";
}

#endregion