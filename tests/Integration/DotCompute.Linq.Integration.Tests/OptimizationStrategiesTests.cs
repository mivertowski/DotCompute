using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Optimization;
using DotCompute.Memory;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Linq.Integration.Tests;

/// <summary>
/// Integration tests for optimization strategies including adaptive optimization,
/// kernel fusion, memory optimization, and parallelization strategies.
/// </summary>
public class OptimizationStrategiesTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IOptimizationEngine _optimizationEngine;
    private readonly IAdaptiveOptimizer _adaptiveOptimizer;
    private readonly IKernelFusionOptimizer _fusionOptimizer;
    private readonly IMemoryOptimizer _memoryOptimizer;
    private readonly Mock<IAccelerator> _mockAccelerator;
    private readonly Mock<IPerformanceProfiler> _mockProfiler;

    public OptimizationStrategiesTests()
    {
        var services = new ServiceCollection();
        
        // Setup mocks
        _mockAccelerator = new Mock<IAccelerator>();
        _mockProfiler = new Mock<IPerformanceProfiler>();
        
        // Configure services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton<IOptimizationEngine, OptimizationEngine>();
        services.AddSingleton<IAdaptiveOptimizer, AdaptiveOptimizer>();
        services.AddSingleton<IKernelFusionOptimizer, KernelFusionOptimizer>();
        services.AddSingleton<IMemoryOptimizer, MemoryOptimizer>();
        services.AddSingleton<IParallelizationOptimizer, ParallelizationOptimizer>();
        services.AddSingleton<IWorkloadAnalyzer, WorkloadAnalyzer>();
        services.AddSingleton(_mockAccelerator.Object);
        services.AddSingleton(_mockProfiler.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _optimizationEngine = _serviceProvider.GetRequiredService<IOptimizationEngine>();
        _adaptiveOptimizer = _serviceProvider.GetRequiredService<IAdaptiveOptimizer>();
        _fusionOptimizer = _serviceProvider.GetRequiredService<IKernelFusionOptimizer>();
        _memoryOptimizer = _serviceProvider.GetRequiredService<IMemoryOptimizer>();
        
        // Setup mock behavior
        _mockProfiler.Setup(x => x.ProfileExecution(It.IsAny<Func<Task>>()))
            .ReturnsAsync(new PerformanceProfile
            {
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                MemoryUsage = 1024 * 1024,
                ThroughputPerSecond = 1000,
                CpuUtilization = 0.5,
                GpuUtilization = 0.8
            });
    }

    [Fact]
    public async Task AdaptiveOptimization_ShouldLearnFromExecutionHistory()
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            DataSize = 1000000,
            ComputeIntensity = ComputeIntensity.High,
            MemoryPattern = MemoryAccessPattern.Sequential,
            ParallelismDegree = 8
        };

        var initialStrategy = OptimizationStrategy.Conservative;

        // Act - First optimization (no history)
        var result1 = await _adaptiveOptimizer.OptimizeAsync(workload, initialStrategy);

        // Simulate execution and feedback
        await _adaptiveOptimizer.RecordPerformanceAsync(result1.StrategyId, new PerformanceMetrics
        {
            ExecutionTime = TimeSpan.FromMilliseconds(200),
            ThroughputPerSecond = 5000,
            MemoryEfficiency = 0.6,
            Success = true
        });

        // Act - Second optimization (with history)
        var result2 = await _adaptiveOptimizer.OptimizeAsync(workload, initialStrategy);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result2.Strategy.Should().NotBe(result1.Strategy); // Should adapt based on performance
        result2.Confidence.Should().BeGreaterThan(result1.Confidence);
    }

    [Fact]
    public async Task KernelFusion_ShouldCombineCompatibleOperations()
    {
        // Arrange
        var operations = new[]
        {
            new ComputeOperation
            {
                Type = OperationType.Map,
                Expression = "x => x * 2",
                InputType = typeof(float[]),
                OutputType = typeof(float[])
            },
            new ComputeOperation
            {
                Type = OperationType.Map,
                Expression = "x => x + 1",
                InputType = typeof(float[]),
                OutputType = typeof(float[])
            },
            new ComputeOperation
            {
                Type = OperationType.Filter,
                Expression = "x => x > 0",
                InputType = typeof(float[]),
                OutputType = typeof(float[])
            }
        };

        var fusionOptions = new KernelFusionOptions
        {
            MaxOperationsPerKernel = 3,
            AllowedFusionPatterns = new[] { "Map+Map", "Map+Filter" },
            OptimizeMemoryAccess = true
        };

        // Act
        var result = await _fusionOptimizer.FuseOperationsAsync(operations, fusionOptions);

        // Assert
        result.Should().NotBeNull();
        result.FusedKernels.Should().HaveCountLessThan(operations.Length);
        result.FusedKernels.Should().Contain(k => k.OperationCount > 1);
        result.MemoryAccessOptimizations.Should().NotBeEmpty();
        result.EstimatedSpeedup.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public async Task MemoryOptimization_ShouldMinimizeAllocations()
    {
        // Arrange
        var memoryProfile = new MemoryProfile
        {
            TotalAllocations = 10,
            PeakMemoryUsage = 100 * 1024 * 1024, // 100MB
            AllocationPattern = new[]
            {
                new AllocationInfo { Size = 10 * 1024 * 1024, Lifetime = TimeSpan.FromMilliseconds(100) },
                new AllocationInfo { Size = 20 * 1024 * 1024, Lifetime = TimeSpan.FromMilliseconds(200) },
                new AllocationInfo { Size = 30 * 1024 * 1024, Lifetime = TimeSpan.FromMilliseconds(150) }
            }
        };

        var optimizationOptions = new MemoryOptimizationOptions
        {
            EnableBufferReuse = true,
            EnableInPlaceOperations = true,
            TargetMemoryReduction = 0.5, // 50% reduction target
            AllowTemporaryBuffers = true
        };

        // Act
        var result = await _memoryOptimizer.OptimizeAsync(memoryProfile, optimizationOptions);

        // Assert
        result.Should().NotBeNull();
        result.OptimizedAllocations.Should().HaveCountLessThan(memoryProfile.TotalAllocations);
        result.EstimatedMemoryReduction.Should().BeGreaterThan(0.3); // At least 30% reduction
        result.OptimizationTechniques.Should().Contain("BufferReuse");
        result.OptimizationTechniques.Should().Contain("InPlaceOperations");
    }

    [Fact]
    public async Task ParallelizationOptimization_ShouldOptimizeThreadingStrategy()
    {
        // Arrange
        var workload = new ParallelWorkload
        {
            DataSize = 1000000,
            ComputationComplexity = ComputationComplexity.Medium,
            Dependencies = new DependencyGraph(),
            ResourceRequirements = new ResourceRequirements
            {
                CpuIntensive = true,
                MemoryIntensive = false,
                IoIntensive = false
            }
        };

        var systemInfo = new SystemInfo
        {
            CpuCores = Environment.ProcessorCount,
            AvailableMemory = 8L * 1024 * 1024 * 1024, // 8GB
            GpuAvailable = true,
            NumaNodes = 1
        };

        var optimizer = _serviceProvider.GetRequiredService<IParallelizationOptimizer>();

        // Act
        var result = await optimizer.OptimizeAsync(workload, systemInfo);

        // Assert
        result.Should().NotBeNull();
        result.RecommendedThreadCount.Should().BeGreaterThan(1);
        result.RecommendedThreadCount.Should().BeLessOrEqualTo(Environment.ProcessorCount * 2);
        result.PartitioningStrategy.Should().NotBe(PartitioningStrategy.None);
        result.LoadBalancingStrategy.Should().NotBe(LoadBalancingStrategy.None);
    }

    [Fact]
    public async Task WorkloadAnalysis_ShouldCharacterizeComputePatterns()
    {
        // Arrange
        var workloads = new[]
        {
            new ComputeWorkload { DataSize = 1000, Operations = GenerateOperations(OperationType.Map, 5) },
            new ComputeWorkload { DataSize = 10000, Operations = GenerateOperations(OperationType.Reduce, 3) },
            new ComputeWorkload { DataSize = 100000, Operations = GenerateOperations(OperationType.Filter, 2) }
        };

        var analyzer = _serviceProvider.GetRequiredService<IWorkloadAnalyzer>();

        // Act
        var analyses = new List<WorkloadAnalysis>();
        foreach (var workload in workloads)
        {
            var analysis = await analyzer.AnalyzeAsync(workload);
            analyses.Add(analysis);
        }

        // Assert
        analyses.Should().HaveCount(3);
        analyses.Should().OnlyContain(a => a.ComputeIntensity != ComputeIntensity.Unknown);
        analyses.Should().OnlyContain(a => a.MemoryPattern != MemoryAccessPattern.Unknown);
        analyses.Should().OnlyContain(a => a.ParallelismPotential > 0);
    }

    [Fact]
    public async Task OptimizationPipeline_ShouldApplyMultipleOptimizations()
    {
        // Arrange
        var pipeline = new OptimizationPipeline(new[]
        {
            _fusionOptimizer,
            _memoryOptimizer,
            _adaptiveOptimizer
        });

        var computeGraph = new ComputeGraph
        {
            Operations = GenerateOperations(OperationType.Map, 5)
                .Concat(GenerateOperations(OperationType.Filter, 2))
                .Concat(GenerateOperations(OperationType.Reduce, 1))
                .ToArray()
        };

        var pipelineOptions = new OptimizationPipelineOptions
        {
            EnableKernelFusion = true,
            EnableMemoryOptimization = true,
            EnableAdaptiveOptimization = true,
            MaxOptimizationPasses = 3
        };

        // Act
        var result = await pipeline.OptimizeAsync(computeGraph, pipelineOptions);

        // Assert
        result.Should().NotBeNull();
        result.OptimizationPasses.Should().BeGreaterThan(0);
        result.OptimizationPasses.Should().BeLessOrEqualTo(3);
        result.TotalOptimizations.Should().BeGreaterThan(0);
        result.EstimatedPerformanceGain.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public async Task BackendSelection_ShouldChooseOptimalBackend()
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            DataSize = 1000000,
            ComputeIntensity = ComputeIntensity.High,
            MemoryPattern = MemoryAccessPattern.Random,
            ParallelismDegree = 1000
        };

        var availableBackends = new[]
        {
            new BackendInfo
            {
                Type = ComputeBackend.CPU,
                Capabilities = new[] { "SIMD", "MultiCore" },
                Performance = new PerformanceCharacteristics
                {
                    ThroughputPerSecond = 1000,
                    Latency = TimeSpan.FromMilliseconds(10),
                    MemoryBandwidth = 50 * 1024 * 1024 * 1024L // 50 GB/s
                }
            },
            new BackendInfo
            {
                Type = ComputeBackend.GPU,
                Capabilities = new[] { "CUDA", "TensorCores", "HighParallelism" },
                Performance = new PerformanceCharacteristics
                {
                    ThroughputPerSecond = 10000,
                    Latency = TimeSpan.FromMilliseconds(50),
                    MemoryBandwidth = 500 * 1024 * 1024 * 1024L // 500 GB/s
                }
            }
        };

        var selector = _serviceProvider.GetRequiredService<IBackendSelector>();

        // Act
        var result = await selector.SelectOptimalBackendAsync(workload, availableBackends);

        // Assert
        result.Should().NotBeNull();
        result.SelectedBackend.Should().NotBe(ComputeBackend.Unknown);
        result.Confidence.Should().BeGreaterThan(0.5);
        result.Reasoning.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OptimizationCaching_ShouldReuseOptimizationResults()
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            DataSize = 50000,
            ComputeIntensity = ComputeIntensity.Medium,
            MemoryPattern = MemoryAccessPattern.Sequential
        };

        var options = new OptimizationOptions
        {
            Strategy = OptimizationStrategy.Balanced,
            EnableCaching = true,
            CacheTtl = TimeSpan.FromMinutes(10)
        };

        // Act - First optimization
        var start1 = DateTime.UtcNow;
        var result1 = await _optimizationEngine.OptimizeAsync(workload, options);
        var duration1 = DateTime.UtcNow - start1;

        // Act - Second optimization with same workload
        var start2 = DateTime.UtcNow;
        var result2 = await _optimizationEngine.OptimizeAsync(workload, options);
        var duration2 = DateTime.UtcNow - start2;

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result2.FromCache.Should().BeTrue();
        duration2.Should().BeLessThan(duration1);
        result1.OptimizationId.Should().Be(result2.OptimizationId);
    }

    [Fact]
    public async Task DynamicOptimization_ShouldAdaptToRuntimeConditions()
    {
        // Arrange
        var optimizer = _serviceProvider.GetRequiredService<IDynamicOptimizer>();
        var runtimeConditions = new RuntimeConditions
        {
            CpuLoad = 0.8,
            MemoryPressure = 0.3,
            GpuUtilization = 0.2,
            NetworkLatency = TimeSpan.FromMilliseconds(10)
        };

        var workload = new WorkloadCharacteristics
        {
            DataSize = 100000,
            ComputeIntensity = ComputeIntensity.Medium
        };

        // Act
        var optimizationPlan = await optimizer.CreateDynamicPlanAsync(workload, runtimeConditions);
        
        // Simulate runtime condition changes
        runtimeConditions.CpuLoad = 0.3;
        runtimeConditions.GpuUtilization = 0.9;
        
        var adaptedPlan = await optimizer.AdaptPlanAsync(optimizationPlan, runtimeConditions);

        // Assert
        optimizationPlan.Should().NotBeNull();
        adaptedPlan.Should().NotBeNull();
        adaptedPlan.Strategy.Should().NotBe(optimizationPlan.Strategy);
        adaptedPlan.TargetBackend.Should().Be(ComputeBackend.GPU); // Should switch to GPU
    }

    [Theory]
    [InlineData(OptimizationStrategy.Conservative, 1)]
    [InlineData(OptimizationStrategy.Balanced, 2)]
    [InlineData(OptimizationStrategy.Aggressive, 5)]
    public async Task OptimizationStrategies_ShouldApplyDifferentLevelsOfOptimization(
        OptimizationStrategy strategy, int minOptimizations)
    {
        // Arrange
        var workload = new WorkloadCharacteristics
        {
            DataSize = 10000,
            ComputeIntensity = ComputeIntensity.Medium,
            MemoryPattern = MemoryAccessPattern.Sequential
        };

        var options = new OptimizationOptions
        {
            Strategy = strategy,
            MaxOptimizationTime = TimeSpan.FromSeconds(10)
        };

        // Act
        var result = await _optimizationEngine.OptimizeAsync(workload, options);

        // Assert
        result.Should().NotBeNull();
        result.AppliedOptimizations.Should().HaveCountGreaterOrEqualTo(minOptimizations);
        result.Strategy.Should().Be(strategy);
    }

    [Fact]
    public async Task ConcurrentOptimization_ShouldHandleMultipleRequests()
    {
        // Arrange
        var workloads = Enumerable.Range(0, 5)
            .Select(i => new WorkloadCharacteristics
            {
                DataSize = 1000 * (i + 1),
                ComputeIntensity = (ComputeIntensity)(i % 3 + 1)
            })
            .ToArray();

        var options = new OptimizationOptions
        {
            Strategy = OptimizationStrategy.Balanced,
            EnableCaching = true
        };

        // Act
        var tasks = workloads
            .Select(workload => _optimizationEngine.OptimizeAsync(workload, options))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r != null);
        results.Should().OnlyContain(r => r.Success);
        
        // Verify unique optimization IDs (no race conditions)
        var uniqueIds = results.Select(r => r.OptimizationId).Distinct().ToArray();
        uniqueIds.Should().HaveCount(5);
    }

    private IEnumerable<ComputeOperation> GenerateOperations(OperationType type, int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new ComputeOperation
            {
                Type = type,
                Expression = $"operation_{type}_{i}",
                InputType = typeof(float[]),
                OutputType = typeof(float[])
            });
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}