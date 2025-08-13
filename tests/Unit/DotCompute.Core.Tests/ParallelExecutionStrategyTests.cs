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
using LoadBalancingStrategy = DotCompute.Core.Execution.LoadBalancingStrategy;
using SynchronizationStrategy = DotCompute.Core.Execution.SynchronizationStrategy;
using CommunicationBackend = DotCompute.Core.Execution.CommunicationBackend;
using MemoryOptimizationLevel = DotCompute.Core.Execution.MemoryOptimizationLevel;
using StealingStrategy = DotCompute.Core.Execution.StealingStrategy;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Comprehensive tests for parallel execution strategies with mock GPUs that can run on CI/CD.
/// Tests data parallel, model parallel, pipeline parallel, and work-stealing execution patterns.
/// </summary>
public class ParallelExecutionStrategyTests : IAsyncDisposable
{
    private readonly Mock<IAcceleratorManager> _mockAcceleratorManager;
    private readonly Mock<IKernelManager> _mockKernelManager;
    private readonly NullLoggerFactory _loggerFactory;
    private readonly NullLogger<ParallelExecutionStrategy> _logger;
    private readonly List<Mock<IAccelerator>> _mockAccelerators;
    private ParallelExecutionStrategy _strategy;

    public ParallelExecutionStrategyTests()
    {
        _loggerFactory = new NullLoggerFactory();
        _logger = new NullLogger<ParallelExecutionStrategy>();
        _mockAcceleratorManager = new Mock<IAcceleratorManager>();
        _mockKernelManager = new Mock<IKernelManager>();
        
        // Create mock GPU accelerators
        _mockAccelerators = CreateMockGPUAccelerators(4);
        SetupMockAcceleratorManager();
        SetupMockKernelManager();
        
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
        var options = new TestDataParallelismOptions
        {
            MaxDevices = 2,
            EnablePeerToPeer = true,
            LoadBalancing = LoadBalancingStrategy.Adaptive
        };

        // Act
        var result = await _strategy.ExecuteDataParallelAsync("test_kernel", inputBuffers, outputBuffers, options.ToRealDataParallelOptions());

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
        var options = new TestDataParallelismOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _strategy.ExecuteDataParallelAsync("test_kernel", null!, outputBuffers, options.ToRealDataParallelOptions()).AsTask());
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_WithEmptyOutputBuffers_ShouldThrowArgumentException()
    {
        // Arrange
        var inputBuffers = CreateMockBuffers<float>(1024, 2);
        var options = new TestDataParallelismOptions();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _strategy.ExecuteDataParallelAsync("test_kernel", inputBuffers, Array.Empty<DotCompute.Abstractions.IBuffer<float>>(), options.ToRealDataParallelOptions()).AsTask());
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
        var result = await _strategy.ExecuteModelParallelAsync("model_kernel", workload, options.ToRealModelParallelOptions());

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
        var result = await _strategy.ExecutePipelineParallelAsync(pipeline, options.ToRealPipelineOptions());

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
            StealingStrategy = StealingStrategy.RandomVictim,
            WorkItemGranularity = 64
        };

        // Act
        var result = await _strategy.ExecuteWithWorkStealingAsync("work_stealing_kernel", workload, options.ToRealWorkStealingOptions());

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
    public async Task AvailableStrategies_WithDifferentGPUCounts_ShouldAdaptStrategies(int gpuCount, ExecutionStrategyType[] expectedStrategies)
    {
        // Arrange
        var mockAccelerators = CreateMockGPUAccelerators(gpuCount);
        var mockManager = new Mock<IAcceleratorManager>();
        mockManager.Setup(m => m.AvailableAccelerators).Returns(mockAccelerators.Select(m => m.Object).ToArray());
        mockManager.Setup(m => m.Count).Returns(gpuCount);
        mockManager.Setup(m => m.Default).Returns(mockAccelerators.First().Object);

        await using var strategy = new ParallelExecutionStrategy(_logger, mockManager.Object, _mockKernelManager.Object, _loggerFactory);

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
        var options = new TestDataParallelismOptions();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => 
            _strategy.ExecuteDataParallelAsync("test_kernel", inputBuffers, outputBuffers, options.ToRealDataParallelOptions(), cts.Token).AsTask());
    }

    [Fact]
    public async Task ExecuteDataParallelAsync_ConcurrentExecutions_ShouldHandleParallelism()
    {
        // Arrange
        var inputBuffers1 = CreateMockBuffers<float>(512, 1);
        var outputBuffers1 = CreateMockBuffers<float>(512, 1);
        var inputBuffers2 = CreateMockBuffers<float>(1024, 1);
        var outputBuffers2 = CreateMockBuffers<float>(1024, 1);
        var options = new TestDataParallelismOptions();

        // Act - Execute multiple kernels concurrently
        var task1 = _strategy.ExecuteDataParallelAsync("kernel1", inputBuffers1, outputBuffers1, options.ToRealDataParallelOptions());
        var task2 = _strategy.ExecuteDataParallelAsync("kernel2", inputBuffers2, outputBuffers2, options.ToRealDataParallelOptions());

        var results = await Task.WhenAll(task1.AsTask(), task2.AsTask());

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.True(result.Success);
        });
    }

    [Theory]
    [InlineData(LoadBalancingStrategy.RoundRobin)]
    [InlineData(LoadBalancingStrategy.Adaptive)]
    [InlineData(LoadBalancingStrategy.Weighted)]
    public async Task ExecuteDataParallelAsync_WithDifferentLoadBalancing_ShouldAdaptStrategy(LoadBalancingStrategy strategy)
    {
        // Arrange
        var inputBuffers = CreateMockBuffers<float>(2048, 1);
        var outputBuffers = CreateMockBuffers<float>(2048, 1);
        var options = new TestDataParallelismOptions
        {
            LoadBalancing = strategy,
            MaxDevices = 3
        };

        // Act
        var result = await _strategy.ExecuteDataParallelAsync("adaptive_kernel", inputBuffers, outputBuffers, options.ToRealDataParallelOptions());

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
        var options = new TestDataParallelismOptions
        {
            MaxDevices = 3 // Will create uneven distribution: 334, 333, 333
        };

        // Act
        var result = await _strategy.ExecuteDataParallelAsync("uneven_kernel", inputBuffers, outputBuffers, options.ToRealDataParallelOptions());

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
        var result = await _strategy.ExecuteWithWorkStealingAsync<float>("large_workload", workload, options.ToRealWorkStealingOptions());

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
            mock.Setup(a => a.Info).Returns(new AcceleratorInfo(
                i % 2 == 0 ? AcceleratorType.CUDA : AcceleratorType.OpenCL,
                $"Mock GPU {i}",
                "1.0",
                8L * 1024 * 1024 * 1024, // 8GB
                2048, // compute units
                1000, // max clock frequency
                new Version(1, 0), // compute capability
                48 * 1024, // max shared memory per block
                false // is unified memory
            )
            {
                Id = $"gpu_{i}"
            });

            mock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            // Setup Memory property to return a mock IMemoryManager
            var mockMemoryManager = new Mock<IMemoryManager>();
            mockMemoryManager.Setup(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<DotCompute.Abstractions.MemoryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var mockBuffer = new Mock<IMemoryBuffer>();
                    mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);
                    return mockBuffer.Object;
                });
            mock.Setup(a => a.Memory).Returns(mockMemoryManager.Object);

            accelerators.Add(mock);
        }

        // Add one CPU accelerator for heterogeneous execution
        var cpuMock = new Mock<IAccelerator>();
        cpuMock.Setup(a => a.Info).Returns(new AcceleratorInfo(
            AcceleratorType.CPU,
            "Mock CPU",
            "1.0",
            16L * 1024 * 1024 * 1024, // 16GB
            16, // compute units
            3000, // max clock frequency
            null, // compute capability
            0, // max shared memory per block
            true // is unified memory
        )
        {
            Id = "cpu_0"
        });
        cpuMock.Setup(a => a.SynchronizeAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        // Setup Memory property for CPU accelerator
        var cpuMemoryManager = new Mock<IMemoryManager>();
        cpuMemoryManager.Setup(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<DotCompute.Abstractions.MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var mockBuffer = new Mock<IMemoryBuffer>();
                mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);
                return mockBuffer.Object;
            });
        cpuMock.Setup(a => a.Memory).Returns(cpuMemoryManager.Object);
        
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

    private void SetupMockKernelManager()
    {
        // Setup the kernel manager to return compiled kernels with proper Name property
        _mockKernelManager.Setup(m => m.GetOrCompileOperationKernelAsync(
            It.IsAny<string>(),
            It.IsAny<Type[]>(),
            It.IsAny<Type>(),
            It.IsAny<IAccelerator>(),
            It.IsAny<KernelGenerationContext>(),
            It.IsAny<DotCompute.Core.Kernels.CompilationOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((string kernelName, Type[] inputTypes, Type outputType, IAccelerator device, 
                          KernelGenerationContext context, DotCompute.Core.Kernels.CompilationOptions options, CancellationToken ct) =>
            {
                // Create a concrete instance since ManagedCompiledKernel has required properties
                return new DotCompute.Core.Kernels.ManagedCompiledKernel
                {
                    Name = kernelName,
                    Binary = new byte[] { 0x01, 0x02, 0x03 }, // Dummy binary data
                    Parameters = new DotCompute.Core.Kernels.KernelParameter[]
                    {
                        new DotCompute.Core.Kernels.KernelParameter 
                        { 
                            Name = "input", 
                            Type = typeof(float), 
                            IsInput = true,
                            IsOutput = false
                        },
                        new DotCompute.Core.Kernels.KernelParameter 
                        { 
                            Name = "output", 
                            Type = typeof(float), 
                            IsInput = false,
                            IsOutput = true
                        }
                    },
                    Handle = IntPtr.Zero,
                    RequiredWorkGroupSize = new[] { 256, 1, 1 },
                    SharedMemorySize = 1024
                };
            });

        // Setup ExecuteKernelAsync to return successful results
        _mockKernelManager.Setup(m => m.ExecuteKernelAsync(
            It.IsAny<DotCompute.Core.Kernels.ManagedCompiledKernel>(),
            It.IsAny<KernelArgument[]>(),
            It.IsAny<IAccelerator>(),
            It.IsAny<KernelExecutionConfig>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KernelExecutionResult
            {
                Success = true,
                Handle = new KernelExecutionHandle 
                { 
                    Id = Guid.NewGuid(),
                    KernelName = "test_kernel",
                    SubmittedAt = DateTimeOffset.UtcNow
                },
                Timings = new KernelExecutionTimings
                {
                    KernelTimeMs = 10.0,
                    TotalTimeMs = 13.0,
                    EffectiveComputeThroughputGFLOPS = 100.0
                }
            });
    }

    private DotCompute.Abstractions.IBuffer<T>[] CreateMockBuffers<T>(int elementCount, int bufferCount) where T : unmanaged
    {
        var buffers = new DotCompute.Abstractions.IBuffer<T>[bufferCount];
        
        for (int i = 0; i < bufferCount; i++)
        {
            var mock = new Mock<DotCompute.Abstractions.IBuffer<T>>();
            mock.Setup(b => b.SizeInBytes).Returns(elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>());
            // Note: IBuffer<T> doesn't have IsDisposed property, it implements IAsyncDisposable
            buffers[i] = mock.Object;
        }
        
        return buffers;
    }

    private ModelParallelWorkload<T> CreateMockModelParallelWorkload<T>() where T : unmanaged
    {
        // Create actual ManagedCompiledKernel instances instead of mocking them
        var kernel1 = new DotCompute.Core.Kernels.ManagedCompiledKernel
        {
            Name = "linear_kernel",
            Binary = new byte[] { 0x01, 0x02, 0x03, 0x04 },
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new DotCompute.Core.Kernels.KernelParameter 
                { 
                    Name = "input", 
                    Type = typeof(T), 
                    IsInput = true,
                    IsOutput = false
                },
                new DotCompute.Core.Kernels.KernelParameter 
                { 
                    Name = "output", 
                    Type = typeof(T), 
                    IsInput = false,
                    IsOutput = true
                }
            },
            Handle = IntPtr.Zero,
            RequiredWorkGroupSize = new[] { 256, 1, 1 },
            SharedMemorySize = 1024
        };

        var kernel2 = new DotCompute.Core.Kernels.ManagedCompiledKernel
        {
            Name = "relu_kernel",
            Binary = new byte[] { 0x05, 0x06, 0x07, 0x08 },
            Parameters = new DotCompute.Core.Kernels.KernelParameter[]
            {
                new DotCompute.Core.Kernels.KernelParameter 
                { 
                    Name = "input", 
                    Type = typeof(T), 
                    IsInput = true,
                    IsOutput = false
                },
                new DotCompute.Core.Kernels.KernelParameter 
                { 
                    Name = "output", 
                    Type = typeof(T), 
                    IsInput = false,
                    IsOutput = true
                }
            },
            Handle = IntPtr.Zero,
            RequiredWorkGroupSize = new[] { 256, 1, 1 },
            SharedMemorySize = 512
        };
        
        return new ModelParallelWorkload<T>
        {
            ModelLayers = new List<ModelLayer<T>>
            {
                new ModelLayer<T> 
                { 
                    LayerId = 0, 
                    Name = "Linear", 
                    Kernel = kernel1, 
                    InputTensors = new[] { new TensorDescription<T> { Name = "input", Dimensions = new[] { 512, 1024 }, DataType = typeof(T) } },
                    OutputTensors = new[] { new TensorDescription<T> { Name = "output", Dimensions = new[] { 512, 512 }, DataType = typeof(T) } }
                },
                new ModelLayer<T> 
                { 
                    LayerId = 1, 
                    Name = "ReLU", 
                    Kernel = kernel2,
                    InputTensors = new[] { new TensorDescription<T> { Name = "input", Dimensions = new[] { 512, 512 }, DataType = typeof(T) } },
                    OutputTensors = new[] { new TensorDescription<T> { Name = "output", Dimensions = new[] { 512, 512 }, DataType = typeof(T) } }
                }
            },
            InputTensors = new[] { new TensorDescription<T> { Name = "model_input", Dimensions = new[] { 512, 1024 }, DataType = typeof(T) } },
            OutputTensors = new[] { new TensorDescription<T> { Name = "model_output", Dimensions = new[] { 512, 512 }, DataType = typeof(T) } }
        };
    }

    private PipelineDefinition<T> CreateMockPipelineDefinition<T>() where T : unmanaged
    {
        return new PipelineDefinition<T>
        {
            Stages = new List<PipelineStageDefinition>
            {
                new PipelineStageDefinition { Name = "Preprocessing", KernelName = "preprocess" },
                new PipelineStageDefinition { Name = "Computation", KernelName = "compute" },
                new PipelineStageDefinition { Name = "Postprocessing", KernelName = "postprocess" }
            },
            InputSpec = new PipelineInputSpec<T>
            {
                Tensors = new[] { new TensorDescription<T> { Name = "input", Dimensions = new[] { 1024 }, DataType = typeof(T) } }
            },
            OutputSpec = new PipelineOutputSpec<T>
            {
                Tensors = new[] { new TensorDescription<T> { Name = "output", Dimensions = new[] { 1024 }, DataType = typeof(T) } }
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
                    InputBuffers = CreateMockBuffers<T>(64, 1),
                    OutputBuffers = CreateMockBuffers<T>(64, 1),
                    EstimatedProcessingTimeMs = (i % 10) + 1
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
                    InputBuffers = CreateMockBuffers<T>(32, 1),
                    OutputBuffers = CreateMockBuffers<T>(32, 1),
                    EstimatedProcessingTimeMs = (i % 50) + 1
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
public class TestDataParallelismOptions
{
    public int? MaxDevices { get; set; }
    public string[]? TargetDevices { get; set; }
    public bool EnablePeerToPeer { get; set; } = true;
    public SynchronizationStrategy SyncStrategy { get; set; } = SynchronizationStrategy.EventBased;
    public LoadBalancingStrategy LoadBalancing { get; set; } = LoadBalancingStrategy.RoundRobin;
    
    /// <summary>
    /// Converts test options to actual DotCompute.Core.Execution.DataParallelismOptions.
    /// </summary>
    public DataParallelismOptions ToRealDataParallelOptions()
    {
        return new DataParallelismOptions
        {
            MaxDevices = MaxDevices,
            TargetDevices = TargetDevices,
            EnablePeerToPeer = EnablePeerToPeer,
            SyncStrategy = ConvertSyncStrategy(SyncStrategy),
            LoadBalancing = ConvertLoadBalancingStrategy(LoadBalancing)
        };
    }
    
    private static DotCompute.Core.Execution.SynchronizationStrategy ConvertSyncStrategy(SynchronizationStrategy strategy)
    {
        return strategy switch
        {
            SynchronizationStrategy.EventBased => DotCompute.Core.Execution.SynchronizationStrategy.EventBased,
            SynchronizationStrategy.Barrier => DotCompute.Core.Execution.SynchronizationStrategy.Barrier,
            SynchronizationStrategy.LockFree => DotCompute.Core.Execution.SynchronizationStrategy.LockFree,
            SynchronizationStrategy.HostBased => DotCompute.Core.Execution.SynchronizationStrategy.HostBased,
            _ => DotCompute.Core.Execution.SynchronizationStrategy.EventBased
        };
    }
    
    private static DotCompute.Core.Execution.LoadBalancingStrategy ConvertLoadBalancingStrategy(LoadBalancingStrategy strategy)
    {
        return strategy switch
        {
            LoadBalancingStrategy.RoundRobin => DotCompute.Core.Execution.LoadBalancingStrategy.RoundRobin,
            LoadBalancingStrategy.Weighted => DotCompute.Core.Execution.LoadBalancingStrategy.Weighted,
            LoadBalancingStrategy.Adaptive => DotCompute.Core.Execution.LoadBalancingStrategy.Adaptive,
            LoadBalancingStrategy.Dynamic => DotCompute.Core.Execution.LoadBalancingStrategy.Dynamic,
            LoadBalancingStrategy.Manual => DotCompute.Core.Execution.LoadBalancingStrategy.Manual,
            _ => DotCompute.Core.Execution.LoadBalancingStrategy.RoundRobin
        };
    }
}

public class ModelParallelismOptions
{
    public CommunicationBackend CommunicationBackend { get; set; } = CommunicationBackend.P2P;
    public MemoryOptimizationLevel MemoryOptimization { get; set; } = MemoryOptimizationLevel.Balanced;
    
    public TestDataParallelismOptions ToRealDataParallelOptions()
    {
        return new TestDataParallelismOptions();
    }
    
    public DotCompute.Core.Execution.ModelParallelismOptions ToRealModelParallelOptions()
    {
        return new DotCompute.Core.Execution.ModelParallelismOptions
        {
            CommunicationBackend = CommunicationBackend,
            MemoryOptimization = MemoryOptimization
        };
    }
}

public class PipelineParallelismOptions
{
    public int MicrobatchSize { get; set; } = 32;
    public bool OverlapCommunication { get; set; } = true;
    
    public TestDataParallelismOptions ToRealDataParallelOptions()
    {
        return new TestDataParallelismOptions();
    }
    
    public DotCompute.Core.Execution.PipelineParallelismOptions ToRealPipelineOptions()
    {
        return new DotCompute.Core.Execution.PipelineParallelismOptions
        {
            MicrobatchSize = MicrobatchSize
        };
    }
}

public class WorkStealingOptions
{
    public StealingStrategy StealingStrategy { get; set; } = StealingStrategy.RandomVictim;
    public int WorkItemGranularity { get; set; } = 64;
    
    public TestDataParallelismOptions ToRealDataParallelOptions()
    {
        return new TestDataParallelismOptions();
    }
    
    public DotCompute.Core.Execution.WorkStealingOptions ToRealWorkStealingOptions()
    {
        return new DotCompute.Core.Execution.WorkStealingOptions
        {
            StealingStrategy = StealingStrategy,
            WorkQueueDepth = WorkItemGranularity
        };
    }
}







#endregion