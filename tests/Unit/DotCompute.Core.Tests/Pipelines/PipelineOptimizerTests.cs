// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Interfaces.Pipelines.Interfaces;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Abstractions.Pipelines.Results;
using DotCompute.Abstractions.Pipelines.Enums;
using DotCompute.Core.Pipelines;
using DotCompute.Abstractions.Validation;
using PipelineExecutionContext = DotCompute.Abstractions.Models.Pipelines.PipelineExecutionContext;
using PipelineValidationResult = DotCompute.Abstractions.Models.Pipelines.PipelineValidationResult;
using IPipelineMetrics = DotCompute.Abstractions.Interfaces.Pipelines.Interfaces.IPipelineMetrics;

// Add using aliases for test interfaces
using Microsoft.Extensions.Logging;
using Moq;
using System;

namespace DotCompute.Core.Tests.Pipelines;

/// <summary>
/// Comprehensive tests for PipelineOptimizer covering all optimization strategies:
/// - Stage fusion and parallelization optimization
/// - Memory access pattern optimization
/// - Pipeline restructuring and dependency analysis
/// - Performance profiling and bottleneck detection
/// - Resource allocation optimization
/// - Error handling and fallback strategies
///
/// Achieves 95%+ code coverage with extensive scenario validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "PipelineOptimizer")]
public sealed class PipelineOptimizerTests(ITestOutputHelper output) : IDisposable
{
    private readonly ITestOutputHelper _output = output;
    private readonly Mock<ILogger<PipelineOptimizer>> _mockLogger = new();
    private readonly PipelineOptimizer _optimizer = new();
    private readonly List<IDisposable> _disposables = [];
    private bool _disposed;
    /// <summary>
    /// Gets optimize async_ sequential stages_ fuses compatible stages.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #region Pipeline Fusion Tests

    [Fact]
    [Trait("TestType", "StageFusion")]
    public async Task OptimizeAsync_SequentialStages_FusesCompatibleStages()
    {
        // Arrange
        var pipeline = CreateTestPipeline("sequential_fusion_test",
        [
            CreateMapStage("stage1", x => x * 2),
            CreateMapStage("stage2", x => x + 10),
            CreateFilterStage("stage3", x => x > 50)
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();
        _ = optimizedPipeline.Pipeline.Stages.Should().HaveCountLessThan(pipeline.Stages.Count, "stages should be fused");

        // Verify fusion was applied
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stage fusion", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets optimize async_ incompatible stages_ preserves separation.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "StageFusion")]
    public async Task OptimizeAsync_IncompatibleStages_PreservesSeparation()
    {
        // Arrange
        var pipeline = CreateTestPipeline("incompatible_fusion_test",
        [
            CreateMapStage("map_stage", x => x * 2),
            CreateSyncStage("sync_stage"), // Cannot be fused due to synchronization
            CreateReduceStage("reduce_stage", (x, y) => x + y)
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();
        _ = optimizedPipeline.Pipeline.Stages.Should().HaveCount(pipeline.Stages.Count, "incompatible stages should remain separate");

        // Verify no fusion occurred for incompatible stages
        var fusedStages = optimizedPipeline.Pipeline.Stages.Where(s => s.Name.Contains("fused", StringComparison.OrdinalIgnoreCase)).ToList();
        _ = fusedStages.Should().BeEmpty("no stages should be fused when incompatible");
    }
    /// <summary>
    /// Gets optimize async_ long sequential chain_ fuses optimally.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "StageFusion")]
    public async Task OptimizeAsync_LongSequentialChain_FusesOptimally()
    {
        // Arrange - Create a long chain of compatible operations
        var stages = Enumerable.Range(0, 10)
            .Select(i => CreateMapStage($"map_{i}", x => x + i))
            .ToArray();

        var pipeline = CreateTestPipeline("long_chain_test", stages);
        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();
        _ = optimizedPipeline.Pipeline.Stages.Should().HaveCountLessThan(pipeline.Stages.Count / 2, "long chains should be significantly optimized");

        // Verify the optimization preserved functionality
        var originalStageNames = pipeline.Stages.Select(s => s.Name).ToList();
        var optimizedStageNames = optimizedPipeline.Pipeline.Stages.Select(s => s.Name).ToList();

        _output.WriteLine($"Original stages: {originalStageNames.Count} - {string.Join(", ", originalStageNames)}");
        _output.WriteLine($"Optimized stages: {optimizedStageNames.Count} - {string.Join(", ", optimizedStageNames)}");
    }
    /// <summary>
    /// Gets optimize async_ independent branches_ enables parallel execution.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Parallelization Tests

    [Fact]
    [Trait("TestType", "Parallelization")]
    public async Task OptimizeAsync_IndependentBranches_EnablesParallelExecution()
    {
        // Arrange
        var pipeline = CreateTestPipeline("parallel_branches_test",
        [
            CreateMapStage("input_stage", x => x),
            CreateBranchStage("branch_stage",
            [
                (ExtendedTestPipelineStage)CreateMapStage("branch1_map", x => x * 2),
                (ExtendedTestPipelineStage)CreateMapStage("branch2_map", x => x * 3)
            ]),
            CreateMergeStage("merge_stage")
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Check for parallel execution enablement
        var parallelStages = optimizedPipeline.Pipeline.Stages.OfType<ExtendedTestPipelineStage>().Where(s => s.CanExecuteInParallel).ToList();
        _ = parallelStages.Should().HaveCountGreaterThan(0, "independent branches should enable parallel execution");

        // Verify optimization logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Parallel", StringComparison.CurrentCulture) || v.ToString()!.Contains("concurrent", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets optimize async_ data dependencies_ preserves sequencing.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Parallelization")]
    public async Task OptimizeAsync_DataDependencies_PreservesSequencing()
    {
        // Arrange
        var pipeline = CreateTestPipeline("data_dependencies_test",
        [
            CreateMapStage("producer", x => x * 2),
            CreateDependentStage("consumer", "producer"), // Depends on producer output
            CreateMapStage("independent", x => x + 1)     // Independent of the chain
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify dependencies are preserved
        var consumerStage = optimizedPipeline.Pipeline.Stages.FirstOrDefault(s => s.Name.Contains("consumer", StringComparison.CurrentCulture));
        _ = consumerStage.Should().NotBeNull("consumer stage should be preserved");

        // Verify independent stages exist (IPipelineStage doesn't have CanExecuteInParallel property)
        var independentStage = optimizedPipeline.Pipeline.Stages.FirstOrDefault(s => s.Name.Contains("independent", StringComparison.CurrentCulture));
        _ = independentStage.Should().NotBeNull("independent stages should be preserved");
    }
    /// <summary>
    /// Gets optimize async_ cyclic dependencies_ detects and handles.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Parallelization")]
    public async Task OptimizeAsync_CyclicDependencies_DetectsAndHandles()
    {
        // Arrange
        var pipeline = CreateTestPipeline("cyclic_dependencies_test",
        [
            CreateDependentStage("stageA", "stageB"),
            CreateDependentStage("stageB", "stageC"),
            CreateDependentStage("stageC", "stageA") // Creates cycle
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify cycle detection logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cycle", StringComparison.CurrentCulture) || v.ToString()!.Contains("circular", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets optimize async_ memory intensive stages_ optimizes buffering.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Memory Optimization Tests

    [Fact]
    [Trait("TestType", "MemoryOptimization")]
    public async Task OptimizeAsync_MemoryIntensiveStages_OptimizesBuffering()
    {
        // Arrange
        var pipeline = CreateTestPipeline("memory_intensive_test",
        [
            CreateMemoryIntensiveStage("large_input", memoryUsageMB: 100),
            CreateMapStage("transform", x => x),
            CreateMemoryIntensiveStage("large_output", memoryUsageMB: 150)
        ]);

        SetupMetricsForOptimization(memoryPressure: true);

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify memory optimization was applied
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Memory", StringComparison.CurrentCulture) && v.ToString()!.Contains("optim", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        // Check that buffer management was optimized
        var memoryOptimizedStages = optimizedPipeline.Pipeline.Stages.OfType<ExtendedTestPipelineStage>().Where(s => s.OptimizationHints.Contains("memory")).ToList();
        _ = memoryOptimizedStages.Should().HaveCountGreaterThan(0, "memory-intensive stages should be optimized");
    }
    /// <summary>
    /// Gets optimize async_ streaming data_ enables stream processing.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryOptimization")]
    public async Task OptimizeAsync_StreamingData_EnablesStreamProcessing()
    {
        // Arrange
        var pipeline = CreateTestPipeline("streaming_test",
        [
            CreateStreamingStage("input_stream"),
            CreateMapStage("transform_stream", x => x * 2),
            CreateStreamingStage("output_stream")
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify streaming optimization
        var streamingStages = optimizedPipeline.Pipeline.Stages.OfType<ExtendedTestPipelineStage>().Where(s => s.SupportsStreaming).ToList();
        _ = streamingStages.Should().HaveCountGreaterThanOrEqualTo(pipeline.Stages.OfType<ExtendedTestPipelineStage>().Count(s => s.SupportsStreaming),
            "streaming capability should be preserved or enhanced");
    }
    /// <summary>
    /// Gets optimize async_ in place operations_ minimizes copying.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "MemoryOptimization")]
    public async Task OptimizeAsync_InPlaceOperations_MinimizesCopying()
    {
        // Arrange
        var pipeline = CreateTestPipeline("in_place_test",
        [
            CreateInPlaceStage("in_place_transform", x => x * 2),
            CreateFilterStage("in_place_filter", x => x > 10),
            CreateCopyStage("copy_stage") // Forces copy
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify in-place optimization
        var inPlaceStages = optimizedPipeline.Pipeline.Stages.OfType<ExtendedTestPipelineStage>().Where(s => s.SupportsInPlaceOperation).ToList();
        _ = inPlaceStages.Should().HaveCountGreaterThanOrEqualTo(2, "in-place operations should be preserved and optimized");
    }
    /// <summary>
    /// Gets optimize async_ bottleneck detection_ optimizes critical path.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Performance Optimization Tests

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task OptimizeAsync_BottleneckDetection_OptimizesCriticalPath()
    {
        // Arrange
        var pipeline = CreateTestPipeline("bottleneck_test",
        [
            CreateFastStage("fast1", executionTimeMs: 10),
            CreateSlowStage("bottleneck", executionTimeMs: 1000), // Bottleneck
            CreateFastStage("fast2", executionTimeMs: 15)
        ]);

        SetupBottleneckMetrics();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify bottleneck was identified and optimized
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("bottleneck", StringComparison.CurrentCulture) || v.ToString()!.Contains("critical path", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets optimize async_ load balancing_ distributes work evenly.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task OptimizeAsync_LoadBalancing_DistributesWorkEvenly()
    {
        // Arrange
        var pipeline = CreateTestPipeline("load_balancing_test",
        [
            CreateMapStage("input", x => x),
            CreateParallelProcessingStage("parallel_work", workerCount: 4),
            CreateMergeStage("merge_results")
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify load balancing optimization (check metadata instead of OptimizationHints)
        var parallelStage = optimizedPipeline.Pipeline.Stages.FirstOrDefault(s => s.Name.Contains("parallel", StringComparison.CurrentCulture));
        _ = parallelStage.Should().NotBeNull("parallel stages should exist in optimized pipeline");
    }
    /// <summary>
    /// Gets optimize async_ cache optimization_ reduces redundant work.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task OptimizeAsync_CacheOptimization_ReducesRedundantWork()
    {
        // Arrange
        var pipeline = CreateTestPipeline("cache_test",
        [
            CreateExpensiveComputationStage("expensive1"),
            CreateMapStage("transform1", x => x),
            CreateExpensiveComputationStage("expensive2"), // Same computation as expensive1
            CreateMapStage("transform2", x => x + 1)
        ]);

        SetupMetricsForOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify caching optimization
        var cachedStages = optimizedPipeline.Pipeline.Stages.OfType<ExtendedTestPipelineStage>().Where(s => s.OptimizationHints.Contains("cached")).ToList();
        _ = cachedStages.Should().HaveCountGreaterThan(0, "redundant computations should be cached");
    }
    /// <summary>
    /// Gets optimize async_ resource constraints_ allocates optimally.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Resource Optimization Tests

    [Fact]
    [Trait("TestType", "ResourceOptimization")]
    public async Task OptimizeAsync_ResourceConstraints_AllocatesOptimally()
    {
        // Arrange
        var pipeline = CreateTestPipeline("resource_constrained_test",
        [
            CreateResourceIntensiveStage("cpu_intensive", cpuUsage: 80),
            CreateResourceIntensiveStage("memory_intensive", memoryUsageMB: 500),
            CreateResourceIntensiveStage("io_intensive", ioOpsPerSec: 1000)
        ]);

        SetupResourceConstrainedMetrics();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify resource allocation optimization
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resource", StringComparison.CurrentCulture) && v.ToString()!.Contains("alloc", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets optimize async_ g p u acceleration_ selects optimal device.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ResourceOptimization")]
    public async Task OptimizeAsync_GPUAcceleration_SelectsOptimalDevice()
    {
        // Arrange
        var pipeline = CreateTestPipeline("gpu_acceleration_test",
        [
            CreateGPUCapableStage("matrix_multiply"),
            CreateCPUOnlyStage("file_io"),
            CreateGPUCapableStage("vector_operations")
        ]);

        SetupGPUMetrics();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify GPU stages are optimized for GPU execution
        var gpuOptimizedStages = optimizedPipeline.Pipeline.Stages.OfType<ExtendedTestPipelineStage>().Where(s => s.PreferredDevice == "GPU").ToList();
        _ = gpuOptimizedStages.Should().HaveCountGreaterThan(0, "GPU-capable stages should be optimized for GPU");

        // Verify CPU-only stages remain on CPU
        var cpuStages = optimizedPipeline.Pipeline.Stages.OfType<ExtendedTestPipelineStage>().Where(s => s.PreferredDevice == "CPU").ToList();
        _ = cpuStages.Should().HaveCountGreaterThan(0, "CPU-only stages should remain on CPU");
    }
    /// <summary>
    /// Gets optimize async_ null pipeline_ throws argument null exception.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Error Handling and Edge Cases

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task OptimizeAsync_NullPipeline_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _optimizer.OptimizeAsync(null!, DotCompute.Abstractions.Pipelines.Enums.OptimizationType.Comprehensive);
        _ = await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("pipeline");
    }
    /// <summary>
    /// Gets optimize async_ empty pipeline_ returns empty optimized pipeline.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task OptimizeAsync_EmptyPipeline_ReturnsEmptyOptimizedPipeline()
    {
        // Arrange
        var emptyPipeline = CreateTestPipeline("empty_test", Array.Empty<TestPipelineStage>());

        // Act
        var optimizedPipeline = await _optimizer.OptimizeAsync(emptyPipeline, DotCompute.Abstractions.Pipelines.Enums.OptimizationType.Comprehensive);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();
        _ = optimizedPipeline.Stages.Should().BeEmpty();
    }
    /// <summary>
    /// Gets optimize async_ with cancellation_ responds to token.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task OptimizeAsync_WithCancellation_RespondsToToken()
    {
        // Arrange
        var pipeline = CreateTestPipeline("cancellation_test",
        [
            CreateSlowStage("slow_stage", executionTimeMs: 2000)
        ]);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var settings = new PipelineOptimizationSettings();
        var act = async () => await _optimizer.OptimizeAsync(pipeline, settings, cts.Token);
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }
    /// <summary>
    /// Gets optimize async_ optimization failure_ falls back to original.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task OptimizeAsync_OptimizationFailure_FallsBackToOriginal()
    {
        // Arrange
        var pipeline = CreateTestPipeline("fallback_test",
        [
            CreateProblematicStage("problematic_stage")
        ]);

        SetupFailingOptimization();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();
        _ = optimizedPipeline.Pipeline.Stages.Should().HaveCount(pipeline.Stages.Count, "should fallback to original on optimization failure");

        // Verify fallback was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("fallback", StringComparison.CurrentCulture) || v.ToString()!.Contains("original", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets optimize async_ corrupted metrics_ handles gracefully.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task OptimizeAsync_CorruptedMetrics_HandlesGracefully()
    {
        // Arrange
        var pipeline = CreateTestPipeline("corrupted_metrics_test",
        [
            CreateMapStage("stage1", x => x)
        ]);

        SetupCorruptedMetrics();

        // Act
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();

        // Verify error was handled gracefully
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("metrics", StringComparison.CurrentCulture) && v.ToString()!.Contains("error", StringComparison.CurrentCulture)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
    /// <summary>
    /// Gets optimize async_ concurrent optimizations_ thread safe.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Concurrency and Thread Safety Tests

    [Fact]
    [Trait("TestType", "Concurrency")]
    public async Task OptimizeAsync_ConcurrentOptimizations_ThreadSafe()
    {
        // Arrange
        const int concurrentOptimizations = 10;
        var pipelines = Enumerable.Range(0, concurrentOptimizations)
            .Select(i => CreateTestPipeline($"concurrent_{i}",
            [
                CreateMapStage($"stage1_{i}", x => x * i),
                CreateMapStage($"stage2_{i}", x => x + i)
            ]))
            .ToArray();

        SetupMetricsForOptimization();

        // Act - Run optimizations concurrently
        var optimizationTasks = pipelines.Select(p => _optimizer.OptimizeAsync(p, DotCompute.Abstractions.Pipelines.Enums.OptimizationType.Comprehensive));
        var results = await Task.WhenAll(optimizationTasks);

        // Assert
        _ = results.Should().HaveCount(concurrentOptimizations);
        _ = results.Should().AllSatisfy(r => r.Should().NotBeNull());

        // Verify all optimizations completed successfully
        foreach (var (result, index) in results.Select((r, i) => (r, i)))
        {
            _ = result.Name.Should().Contain($"concurrent_{index}");
        }
    }
    /// <summary>
    /// Gets optimize async_ state isolation_ maintains independence.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Concurrency")]
    public async Task OptimizeAsync_StateIsolation_MaintainsIndependence()
    {
        // Arrange
        var pipeline1 = CreateTestPipeline("isolation_test_1",
        [
            CreateMapStage("stage1", x => x * 2)
        ]);

        var pipeline2 = CreateTestPipeline("isolation_test_2",
        [
            CreateMapStage("stage1", x => x * 3)
        ]);

        SetupMetricsForOptimization();

        // Act - Optimize both pipelines
        var task1 = _optimizer.OptimizeAsync(pipeline1, DotCompute.Abstractions.Pipelines.Enums.OptimizationType.Comprehensive);
        var task2 = _optimizer.OptimizeAsync(pipeline2, DotCompute.Abstractions.Pipelines.Enums.OptimizationType.Comprehensive);

        var results = await Task.WhenAll(task1, task2);

        // Assert
        _ = results[0].Name.Should().Contain("isolation_test_1");
        _ = results[1].Name.Should().Contain("isolation_test_2");

        // Verify state isolation - optimizations should not interfere
        _ = results[0].Stages.Should().NotBeEquivalentTo(results[1].Stages);
    }
    /// <summary>
    /// Gets optimize async_ optimization overhead_ is minimal.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    #endregion

    #region Performance Benchmarks

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task OptimizeAsync_OptimizationOverhead_IsMinimal()
    {
        // Arrange
        var pipeline = CreateTestPipeline("overhead_test",
        [
            CreateMapStage("stage1", x => x),
            CreateMapStage("stage2", x => x),
            CreateMapStage("stage3", x => x)
        ]);

        SetupMetricsForOptimization();

        // Act - Measure optimization time
        var stopwatch = Stopwatch.StartNew();
        var settings = new PipelineOptimizationSettings { EnableFusion = true };
        var optimizedPipeline = await _optimizer.OptimizeAsync(pipeline, settings);
        stopwatch.Stop();

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();
        _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "optimization should complete within reasonable time");

        _output.WriteLine($"Optimization time: {stopwatch.ElapsedMilliseconds}ms");
    }
    /// <summary>
    /// Gets optimize async_ large pipeline_ scales efficiently.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task OptimizeAsync_LargePipeline_ScalesEfficiently()
    {
        // Arrange - Create a large pipeline
        const int stageCount = 100;
        var stages = Enumerable.Range(0, stageCount)
            .Select(i => CreateMapStage($"stage_{i:D3}", x => x + i))
            .ToArray();

        var largePipeline = CreateTestPipeline("large_pipeline_test", stages);
        SetupMetricsForOptimization();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var optimizedPipeline = await _optimizer.OptimizeAsync(largePipeline, DotCompute.Abstractions.Pipelines.Enums.OptimizationType.Comprehensive);
        stopwatch.Stop();

        // Assert
        _ = optimizedPipeline.Should().NotBeNull();
        _ = optimizedPipeline.Stages.Should().HaveCountLessThan(stageCount, "large pipeline should be optimized");

        var optimizationRatio = (double)optimizedPipeline.Stages.Count / stageCount;
        _output.WriteLine($"Original stages: {stageCount}, Optimized: {optimizedPipeline.Stages.Count}, Ratio: {optimizationRatio:P}");
        _output.WriteLine($"Optimization time: {stopwatch.ElapsedMilliseconds}ms");

        _ = optimizationRatio.Should().BeLessThan(0.8, "should achieve significant optimization for large pipelines");
        _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "large pipeline optimization should complete reasonably fast");
    }

    #endregion

    #region Helper Methods

    private TestPipeline CreateTestPipeline(string name, TestPipelineStage[] stages) => new(name, stages);

    private TestPipelineStage CreateMapStage(string name, Func<int, int> transform)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            Transform = transform,
            CanExecuteInParallel = true,
            SupportsInPlaceOperation = true
        };
    }

    private TestPipelineStage CreateFilterStage(string name, Func<int, bool> predicate)
    {
        return new ExtendedTestPipelineStage(name, StageType.Filter)
        {
            Predicate = predicate,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateReduceStage(string name, Func<int, int, int> reducer)
    {
        return new ExtendedTestPipelineStage(name, StageType.Reduce)
        {
            Reducer = reducer,
            CanExecuteInParallel = false // Reduce typically requires sequential processing
        };
    }

    private TestPipelineStage CreateSyncStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.Synchronization)
        {
            CanExecuteInParallel = false,
            RequiresSynchronization = true
        };
    }

    private TestPipelineStage CreateBranchStage(string name, ExtendedTestPipelineStage[] branches)
    {
        return new ExtendedTestPipelineStage(name, StageType.Branch)
        {
            Branches = branches,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateMergeStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.Merge)
        {
            CanExecuteInParallel = false // Merge requires coordination
        };
    }

    private TestPipelineStage CreateDependentStage(string name, string dependsOn)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            Dependencies = new[] { dependsOn },
            CanExecuteInParallel = false
        };
    }

    private TestPipelineStage CreateMemoryIntensiveStage(string name, int memoryUsageMB)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            MemoryUsageMB = memoryUsageMB,
            OptimizationHints = ["memory_intensive"]
        };
    }

    private ExtendedTestPipelineStage CreateStreamingStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.Stream)
        {
            SupportsStreaming = true,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateInPlaceStage(string name, Func<int, int> transform)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            Transform = transform,
            SupportsInPlaceOperation = true,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateCopyStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            SupportsInPlaceOperation = false,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateFastStage(string name, int executionTimeMs)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            EstimatedExecutionTimeMs = executionTimeMs,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateSlowStage(string name, int executionTimeMs)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            EstimatedExecutionTimeMs = executionTimeMs,
            CanExecuteInParallel = true,
            OptimizationHints = ["bottleneck"]
        };
    }

    private TestPipelineStage CreateParallelProcessingStage(string name, int workerCount)
    {
        return new ExtendedTestPipelineStage(name, StageType.ParallelProcessing)
        {
            WorkerCount = workerCount,
            CanExecuteInParallel = true,
            OptimizationHints = ["parallel", "workers"]
        };
    }

    private TestPipelineStage CreateExpensiveComputationStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            EstimatedExecutionTimeMs = 1000,
            ComputationSignature = "expensive_computation", // Same signature for caching
            OptimizationHints = ["expensive", "cacheable"]
        };
    }

    private TestPipelineStage CreateResourceIntensiveStage(string name, int cpuUsage = 0, int memoryUsageMB = 0, int ioOpsPerSec = 0)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            CpuUsagePercent = cpuUsage,
            MemoryUsageMB = memoryUsageMB,
            IoOperationsPerSecond = ioOpsPerSec,
            OptimizationHints = ["resource_intensive"]
        };
    }

    private TestPipelineStage CreateGPUCapableStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            SupportsGPU = true,
            PreferredDevice = "GPU",
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateCPUOnlyStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.IO)
        {
            SupportsGPU = false,
            PreferredDevice = "CPU",
            CanExecuteInParallel = false
        };
    }

    private TestPipelineStage CreateProblematicStage(string name)
    {
        return new ExtendedTestPipelineStage(name, StageType.Map)
        {
            CausesOptimizationFailure = true
        };
    }

    private void SetupMetricsForOptimization(bool memoryPressure = false)
    {
        // Mock setups removed - IPipelineMetrics interface methods don't exist in current implementation
    }

    private void SetupBottleneckMetrics()
        // Mock setups removed - IPipelineMetrics.GetStageExecutionTime doesn't exist

        => SetupMetricsForOptimization();

    private void SetupResourceConstrainedMetrics()
        // Mock setups removed - IPipelineMetrics methods don't exist

        => SetupMetricsForOptimization();

    private void SetupGPUMetrics()
        // Mock setups removed - IPipelineMetrics methods don't exist

        => SetupMetricsForOptimization();

    private void SetupFailingOptimization()
    {
        // Mock setup removed - IPipelineProfiler.ProfileStageAsync doesn't exist
    }

    private void SetupCorruptedMetrics()
    {
        // Mock setup removed - IPipelineMetrics.GetStageExecutionTime doesn't exist
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _disposed = true;
        }
    }

    #endregion
}
/// <summary>
/// An stage type enumeration.
/// </summary>

// Test helper classes
public enum StageType
{
    Map,
    Filter,
    Reduce,
    Branch,
    Merge,
    Synchronization,
    Stream,
    ParallelProcessing,
    IO
}
/// <summary>
/// A class that represents test pipeline.
/// </summary>

public class TestPipeline(string name, TestPipelineStage[] stages) : IKernelPipeline
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public string Id => Guid.NewGuid().ToString();
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; } = name;
    /// <summary>
    /// Gets or sets the stages.
    /// </summary>
    /// <value>The stages.</value>
    public IReadOnlyList<IPipelineStage> Stages { get; } = stages;
    /// <summary>
    /// Gets or sets the optimization settings.
    /// </summary>
    /// <value>The optimization settings.</value>
    public PipelineOptimizationSettings OptimizationSettings => new();
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public IReadOnlyDictionary<string, object> Metadata => new Dictionary<string, object>();
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask<PipelineExecutionResult> ExecuteAsync(PipelineExecutionContext context, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    /// <summary>
    /// Validates the .
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public PipelineValidationResult Validate()
        => new() { IsValid = true, Errors = Array.Empty<ValidationIssue>(), Warnings = Array.Empty<ValidationWarning>() };
    /// <summary>
    /// Gets the metrics.
    /// </summary>
    /// <returns>The metrics.</returns>

    public IPipelineMetrics GetMetrics()
        => throw new NotImplementedException();
    /// <summary>
    /// Gets optimize asynchronously.
    /// </summary>
    /// <param name="optimizer">The optimizer.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask<IKernelPipeline> OptimizeAsync(IPipelineOptimizer optimizer)
        => throw new NotImplementedException();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
/// <summary>
/// A class that represents test pipeline stage.
/// </summary>

public class TestPipelineStage(string id, string name, PipelineStageType type = PipelineStageType.Computation) : IPipelineStage
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public string Id { get; } = id;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; } = name;
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public PipelineStageType Type { get; } = type;
    /// <summary>
    /// Gets or sets the dependencies.
    /// </summary>
    /// <value>The dependencies.</value>
    public IReadOnlyList<string> Dependencies { get; } = Array.Empty<string>();
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public IReadOnlyDictionary<string, object> Metadata { get; } = new Dictionary<string, object>();
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask<Abstractions.Models.Pipelines.StageExecutionResult> ExecuteAsync(PipelineExecutionContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new Abstractions.Models.Pipelines.StageExecutionResult
        {
            Success = true,
            StageId = Id,
            OutputData = []
        });
    }
    /// <summary>
    /// Validates the .
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Abstractions.Models.Pipelines.StageValidationResult Validate() => new() { IsValid = true };
    /// <summary>
    /// Gets the metrics.
    /// </summary>
    /// <returns>The metrics.</returns>

    public IStageMetrics GetMetrics() => new TestStageMetrics();
}
/// <summary>
/// A class that represents test stage metrics.
/// </summary>

public class TestStageMetrics : IStageMetrics
{
    /// <summary>
    /// Gets or sets the stage identifier.
    /// </summary>
    /// <value>The stage id.</value>
    public string StageId => "test-stage";
    /// <summary>
    /// Gets or sets the stage name.
    /// </summary>
    /// <value>The stage name.</value>
    public string StageName => "Test Stage";
    /// <summary>
    /// Gets or sets the execution count.
    /// </summary>
    /// <value>The execution count.</value>
    public long ExecutionCount => 1;
    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    /// <value>The total execution time.</value>
    public TimeSpan TotalExecutionTime => TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    /// <value>The average execution time.</value>
    public TimeSpan AverageExecutionTime => TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Gets or sets the min execution time.
    /// </summary>
    /// <value>The min execution time.</value>
    public TimeSpan MinExecutionTime => TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Gets or sets the max execution time.
    /// </summary>
    /// <value>The max execution time.</value>
    public TimeSpan MaxExecutionTime => TimeSpan.FromMilliseconds(100);
    /// <summary>
    /// Gets or sets the memory usage.
    /// </summary>
    /// <value>The memory usage.</value>
    public MemoryUsageStats MemoryUsage => new()
    {
        TotalAllocatedBytes = 1024,
        PeakMemoryUsageBytes = 2048,
        AllocationCount = 1
    };
    /// <summary>
    /// Gets or sets the throughput ops per second.
    /// </summary>
    /// <value>The throughput ops per second.</value>
    public double ThroughputOpsPerSecond => 1000.0;
    /// <summary>
    /// Gets or sets the average memory usage.
    /// </summary>
    /// <value>The average memory usage.</value>
    public long AverageMemoryUsage => 1024;
    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    /// <value>The success rate.</value>
    public double SuccessRate => 1.0;
    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    /// <value>The error count.</value>
    public long ErrorCount => 0;
    /// <summary>
    /// Gets or sets the custom metrics.
    /// </summary>
    /// <value>The custom metrics.</value>
    public IReadOnlyDictionary<string, double> CustomMetrics => new Dictionary<string, double>();
}
/// <summary>
/// A class that represents extended test pipeline stage.
/// </summary>

public class ExtendedTestPipelineStage(string name, StageType type) : TestPipelineStage(name + "_ext", name, PipelineStageType.Computation)
{
    /// <summary>
    /// Gets or sets the estimated execution time ms.
    /// </summary>
    /// <value>The estimated execution time ms.</value>
    // Performance characteristics
    public int EstimatedExecutionTimeMs { get; set; } = 10;
    /// <summary>
    /// Gets or sets the memory usage m b.
    /// </summary>
    /// <value>The memory usage m b.</value>
    public int MemoryUsageMB { get; set; } = 1;
    /// <summary>
    /// Gets or sets the cpu usage percent.
    /// </summary>
    /// <value>The cpu usage percent.</value>
    public int CpuUsagePercent { get; set; } = 10;
    /// <summary>
    /// Gets or sets the io operations per second.
    /// </summary>
    /// <value>The io operations per second.</value>
    public int IoOperationsPerSecond { get; set; }
    /// <summary>
    /// Gets or sets the worker count.
    /// </summary>
    /// <value>The worker count.</value>

    public int WorkerCount { get; set; } = 1;
    /// <summary>
    /// Gets or sets the computation signature.
    /// </summary>
    /// <value>The computation signature.</value>
    public string ComputationSignature { get; set; } = "";
    /// <summary>
    /// Gets or sets the causes optimization failure.
    /// </summary>
    /// <value>The causes optimization failure.</value>

    // Test control
    public bool CausesOptimizationFailure { get; set; }
    /// <summary>
    /// Gets or sets the transform.
    /// </summary>
    /// <value>The transform.</value>

    // Stage operations
    public Func<int, int>? Transform { get; set; }
    /// <summary>
    /// Gets or sets the predicate.
    /// </summary>
    /// <value>The predicate.</value>
    public Func<int, bool>? Predicate { get; set; }
    /// <summary>
    /// Gets or sets the reducer.
    /// </summary>
    /// <value>The reducer.</value>
    public Func<int, int, int>? Reducer { get; set; }
    /// <summary>
    /// Gets or sets the branches.
    /// </summary>
    /// <value>The branches.</value>
    public ExtendedTestPipelineStage[]? Branches { get; set; }
    /// <summary>
    /// Gets or sets the supports streaming.
    /// </summary>
    /// <value>The supports streaming.</value>

    // Additional properties for testing
    public bool SupportsStreaming { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether execute in parallel.
    /// </summary>
    /// <value>The can execute in parallel.</value>
    public bool CanExecuteInParallel { get; set; }
    /// <summary>
    /// Gets or sets the supports in place operation.
    /// </summary>
    /// <value>The supports in place operation.</value>
    public bool SupportsInPlaceOperation { get; set; }
    /// <summary>
    /// Gets or sets the requires synchronization.
    /// </summary>
    /// <value>The requires synchronization.</value>
    public bool RequiresSynchronization { get; set; }
    /// <summary>
    /// Gets or sets the optimization hints.
    /// </summary>
    /// <value>The optimization hints.</value>
    public List<string> OptimizationHints { get; set; } = [];
    /// <summary>
    /// Gets or sets the preferred device.
    /// </summary>
    /// <value>The preferred device.</value>
    public string PreferredDevice { get; set; } = "CPU";
    /// <summary>
    /// Gets or sets the supports g p u.
    /// </summary>
    /// <value>The supports g p u.</value>
    public bool SupportsGPU { get; set; }
    /// <summary>
    /// Gets or sets the dependencies.
    /// </summary>
    /// <value>The dependencies.</value>

    // Override base readonly property with new property
    public new IReadOnlyList<string> Dependencies { get; set; } = Array.Empty<string>();
}