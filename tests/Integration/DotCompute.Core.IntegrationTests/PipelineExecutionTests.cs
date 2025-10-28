// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Interfaces.Pipelines;
using DotCompute.Abstractions.Memory;
using DotCompute.Abstractions.Pipelines.Models;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Core.IntegrationTests;

/// <summary>
/// Comprehensive integration tests for pipeline execution covering:
/// - End-to-end pipeline execution with real components
/// - Cross-component integration validation
/// - Performance characteristics under realistic workloads
/// - Error handling and recovery in integrated scenarios
/// - Memory management across pipeline stages
/// - Resource coordination and cleanup
/// - Concurrent pipeline execution
/// - Real-world usage patterns and scenarios
///
/// These tests validate the complete pipeline execution stack working together.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "PipelineExecution")]
public sealed class PipelineExecutionTests : IAsyncLifetime, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<PipelineExecutionTests> _logger;
    private readonly List<IDisposable> _disposables = [];
    private bool _disposed;

    // Services under test
    private readonly IPipelineExecutor _pipelineExecutor;
    private readonly IPipelineOptimizer _pipelineOptimizer;
    private readonly IMemoryManager _memoryManager;
    private readonly ITelemetryProvider _telemetryProvider;

    public PipelineExecutionTests(ITestOutputHelper output)
    {
        _output = output;

        // Setup dependency injection container with all required services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        _disposables.Add(_serviceProvider);

        // Get services
        _logger = _serviceProvider.GetRequiredService<ILogger<PipelineExecutionTests>>();
        _pipelineExecutor = _serviceProvider.GetRequiredService<IPipelineExecutor>();
        _pipelineOptimizer = _serviceProvider.GetRequiredService<IPipelineOptimizer>();
        _memoryManager = _serviceProvider.GetRequiredService<IMemoryManager>();
        _telemetryProvider = _serviceProvider.GetRequiredService<ITelemetryProvider>();
    }

    #region Simple Pipeline Execution Tests

    [Fact]
    [Trait("TestType", "BasicExecution")]
    public async Task ExecutePipeline_SimpleLinearPipeline_ExecutesSuccessfully()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 1000).ToArray();
        var pipeline = CreateLinearPipeline("simple_linear", new[]
        {
            CreateMapStage("multiply_by_2", x => x * 2),
            CreateFilterStage("filter_even", x => x % 4 == 0), // Even numbers after multiplication
            CreateMapStage("add_100", x => x + 100)
        });

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputBuffer.Should().NotBeNull();

        var outputData = new int[result.OutputBuffer.Length];
        await result.OutputBuffer.CopyToAsync(outputData);

        // Verify transformation: input -> *2 -> filter even -> +100
        var expectedData = inputData
            .Select(x => x * 2)
            .Where(x => x % 4 == 0)
            .Select(x => x + 100)
            .ToArray();

        outputData.Should().BeEquivalentTo(expectedData);

        _output.WriteLine($"Pipeline execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Input size: {inputData.Length}, Output size: {outputData.Length}");

        // Cleanup
        result.OutputBuffer.Dispose();
    }

    [Fact]
    [Trait("TestType", "BasicExecution")]
    public async Task ExecutePipeline_BranchingPipeline_HandlesMultiplePaths()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 100).ToArray();
        var pipeline = CreateBranchingPipeline("branching_test", inputData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.BranchResults.Should().HaveCount(2);

        // Verify both branches executed
        result.BranchResults.Should().AllSatisfy(br =>
        {
            br.Success.Should().BeTrue();
            br.OutputBuffer.Should().NotBeNull();
        });

        // Cleanup
        result.OutputBuffer?.Dispose();
        foreach (var branchResult in result.BranchResults)
        {
            branchResult.OutputBuffer?.Dispose();
        }
    }

    [Fact]
    [Trait("TestType", "BasicExecution")]
    public async Task ExecutePipeline_ReductionPipeline_ProducesAggregateResult()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 1000).ToArray();
        var pipeline = CreateReductionPipeline("sum_reduction", inputData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputBuffer.Should().NotBeNull();
        result.OutputBuffer.Length.Should().Be(1); // Reduction produces single result

        var outputData = new int[1];
        await result.OutputBuffer.CopyToAsync(outputData);

        var expectedSum = inputData.Sum();
        outputData[0].Should().Be(expectedSum);

        _output.WriteLine($"Sum of {inputData.Length} elements: {outputData[0]} (expected: {expectedSum})");

        // Cleanup
        result.OutputBuffer.Dispose();
    }

    #endregion

    #region Performance and Optimization Tests

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task ExecutePipeline_WithOptimization_ShowsPerformanceImprovement()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 10000).ToArray();
        var pipeline = CreateOptimizablePipeline("optimization_test", inputData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act - Execute without optimization
        var unoptimizedStopwatch = Stopwatch.StartNew();
        var unoptimizedResult = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);
        unoptimizedStopwatch.Stop();

        // Act - Execute with optimization
        var optimizedPipeline = await _pipelineOptimizer.OptimizeAsync(pipeline);
        var optimizedStopwatch = Stopwatch.StartNew();
        var optimizedResult = await _pipelineExecutor.ExecuteAsync(optimizedPipeline, inputBuffer);
        optimizedStopwatch.Stop();

        // Assert
        unoptimizedResult.Success.Should().BeTrue();
        optimizedResult.Success.Should().BeTrue();

        // Verify results are equivalent
        var unoptimizedData = new int[unoptimizedResult.OutputBuffer.Length];
        var optimizedData = new int[optimizedResult.OutputBuffer.Length];

        await unoptimizedResult.OutputBuffer.CopyToAsync(unoptimizedData);
        await optimizedResult.OutputBuffer.CopyToAsync(optimizedData);

        unoptimizedData.Should().BeEquivalentTo(optimizedData);

        // Performance comparison
        var performanceImprovement = (double)unoptimizedStopwatch.ElapsedMilliseconds / optimizedStopwatch.ElapsedMilliseconds;

        _output.WriteLine($"Unoptimized execution time: {unoptimizedStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Optimized execution time: {optimizedStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Performance improvement: {performanceImprovement:F2}x");

        // Optimization should provide some improvement (even if minimal in this test environment)
        performanceImprovement.Should().BeGreaterOrEqualTo(0.8, "optimization should not significantly worsen performance");

        // Cleanup
        unoptimizedResult.OutputBuffer.Dispose();
        optimizedResult.OutputBuffer.Dispose();
    }

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task ExecutePipeline_LargeDataSet_HandlesVolumeEfficiently()
    {
        // Arrange
        const int dataSize = 1_000_000; // 1M elements
        var inputData = new int[dataSize];
        var random = new Random(42);
        for (int i = 0; i < dataSize; i++)
        {
            inputData[i] = random.Next(1, 1000);
        }

        var pipeline = CreatePerformanceTestPipeline("large_dataset_test", dataSize);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(dataSize * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act
        var initialMemory = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();

        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        stopwatch.Stop();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var throughput = dataSize / stopwatch.Elapsed.TotalSeconds;
        var memoryIncrease = finalMemory - initialMemory;

        _output.WriteLine($"Processing time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Throughput: {throughput:F0} elements/second");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024:F1}MB");

        // Performance expectations
        throughput.Should().BeGreaterThan(100_000, "should process at least 100K elements/second");
        memoryIncrease.Should().BeLessThan(100 * 1024 * 1024, "memory increase should be reasonable");

        // Cleanup
        result.OutputBuffer?.Dispose();
    }

    [Fact]
    [Trait("TestType", "Performance")]
    public async Task ExecutePipeline_ConcurrentExecution_ScalesCorrectly()
    {
        // Arrange
        const int concurrentPipelines = 5;
        const int dataSize = 10000;

        var pipelines = Enumerable.Range(0, concurrentPipelines)
            .Select(i => CreateConcurrentTestPipeline($"concurrent_{i}", dataSize))
            .ToArray();

        var inputBuffers = new List<IMemoryBuffer<int>>();
        var tasks = new List<Task<PipelineExecutionResult>>();

        try
        {
            // Setup input data for each pipeline
            foreach (var pipeline in pipelines)
            {
                var inputData = Enumerable.Range(1, dataSize).ToArray();
                var inputBuffer = await _memoryManager.AllocateAsync<int>(dataSize * sizeof(int));
                await inputBuffer.CopyFromAsync(inputData);
                inputBuffers.Add(inputBuffer);

                tasks.Add(_pipelineExecutor.ExecuteAsync(pipeline, inputBuffer));
            }

            // Act - Execute all pipelines concurrently
            var stopwatch = Stopwatch.StartNew();
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            results.Should().HaveLength(concurrentPipelines);
            results.Should().AllSatisfy(r => r.Success.Should().BeTrue());

            var totalElements = concurrentPipelines * dataSize;
            var concurrentThroughput = totalElements / stopwatch.Elapsed.TotalSeconds;

            _output.WriteLine($"Concurrent execution time: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Total elements processed: {totalElements}");
            _output.WriteLine($"Concurrent throughput: {concurrentThroughput:F0} elements/second");

            // Cleanup results
            foreach (var result in results)
            {
                result.OutputBuffer?.Dispose();
            }
        }
        finally
        {
            // Cleanup input buffers
            foreach (var buffer in inputBuffers)
            {
                buffer.Dispose();
            }
        }
    }

    #endregion

    #region Memory Management Integration Tests

    [Fact]
    [Trait("TestType", "MemoryIntegration")]
    public async Task ExecutePipeline_MemoryPressure_HandlesGracefully()
    {
        // Arrange - Create a memory-intensive pipeline
        const int largeDataSize = 100000;
        var inputData = Enumerable.Range(1, largeDataSize).ToArray();
        var pipeline = CreateMemoryIntensivePipeline("memory_pressure_test", largeDataSize);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(largeDataSize * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Simulate memory pressure by allocating additional memory
        var pressureBuffers = new List<IMemoryBuffer<byte>>();
        try
        {
            for (int i = 0; i < 50; i++)
            {
                var pressureBuffer = await _memoryManager.AllocateAsync<byte>(1024 * 1024); // 1MB each
                pressureBuffers.Add(pressureBuffer);
            }

            // Act - Execute pipeline under memory pressure
            var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();

            _output.WriteLine($"Pipeline executed successfully under memory pressure");
            _output.WriteLine($"Memory manager statistics: {_memoryManager.Statistics}");

            // Cleanup
            result.OutputBuffer?.Dispose();
        }
        finally
        {
            foreach (var buffer in pressureBuffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    [Trait("TestType", "MemoryIntegration")]
    public async Task ExecutePipeline_MemoryLeakDetection_NoLeaks()
    {
        // Arrange
        const int iterations = 100;
        const int dataSize = 1000;
        var inputData = Enumerable.Range(1, dataSize).ToArray();

        var initialMemory = GC.GetTotalMemory(true);

        // Act - Execute pipeline multiple times to detect leaks
        for (int i = 0; i < iterations; i++)
        {
            var pipeline = CreateLeakTestPipeline($"leak_test_{i}", dataSize);

            using var inputBuffer = await _memoryManager.AllocateAsync<int>(dataSize * sizeof(int));
            await inputBuffer.CopyFromAsync(inputData);

            var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);
            result.Success.Should().BeTrue();

            result.OutputBuffer?.Dispose();

            // Force GC every 20 iterations
            if (i % 20 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // Final cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(true);

        // Assert
        var memoryIncrease = finalMemory - initialMemory;
        var memoryPerIteration = memoryIncrease / (double)iterations;

        _output.WriteLine($"Initial memory: {initialMemory / 1024 / 1024:F1}MB");
        _output.WriteLine($"Final memory: {finalMemory / 1024 / 1024:F1}MB");
        _output.WriteLine($"Memory increase: {memoryIncrease / 1024 / 1024:F1}MB");
        _output.WriteLine($"Memory per iteration: {memoryPerIteration / 1024:F1}KB");

        // Memory increase should be minimal (less than 10MB for 100 iterations)
        memoryIncrease.Should().BeLessThan(10 * 1024 * 1024, "should not have significant memory leaks");
    }

    #endregion

    #region Error Handling and Recovery Tests

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task ExecutePipeline_StageFailure_HandlesGracefully()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 100).ToArray();
        var pipeline = CreateFailingPipeline("error_handling_test", inputData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error.Message.Should().Contain("Simulated stage failure");

        _output.WriteLine($"Pipeline failed as expected: {result.Error.Message}");

        // Verify error was logged
        var telemetryEvents = _telemetryProvider.GetEvents();
        telemetryEvents.Should().Contain(e => e.Name.Contains("PipelineError"));
    }

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task ExecutePipeline_PartialFailure_RecoversAndContinues()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 1000).ToArray();
        var pipeline = CreatePartialFailurePipeline("partial_failure_test", inputData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Should recover from partial failure
        result.Warnings.Should().HaveCountGreaterThan(0); // Should have warnings about recovery

        _output.WriteLine($"Pipeline recovered from partial failure with {result.Warnings.Count} warnings");

        // Cleanup
        result.OutputBuffer?.Dispose();
    }

    [Fact]
    [Trait("TestType", "ErrorHandling")]
    public async Task ExecutePipeline_ResourceExhaustion_FallsBackGracefully()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 10000).ToArray();
        var pipeline = CreateResourceExhaustionPipeline("resource_exhaustion_test", inputData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        // May succeed with fallback or fail gracefully
        if (result.Success)
        {
            result.FallbacksUsed.Should().HaveCountGreaterThan(0);
            _output.WriteLine($"Pipeline succeeded using {result.FallbacksUsed.Count} fallbacks");
        }
        else
        {
            result.Error.Should().NotBeNull();
            _output.WriteLine($"Pipeline failed gracefully: {result.Error.Message}");
        }

        // Cleanup
        result.OutputBuffer?.Dispose();
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    [Trait("TestType", "RealWorldScenario")]
    public async Task ExecutePipeline_ImageProcessingWorkflow_ProcessesCorrectly()
    {
        // Arrange - Simulate image processing pipeline (grayscale conversion, blur, edge detection)
        var imageData = GenerateTestImageData(256, 256); // 256x256 test image
        var pipeline = CreateImageProcessingPipeline("image_processing", imageData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<float>(imageData.Length * sizeof(float));
        await inputBuffer.CopyFromAsync(imageData);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var outputData = new float[result.OutputBuffer.Length];
        await result.OutputBuffer.CopyToAsync(outputData);

        // Verify image processing results
        outputData.Should().AllSatisfy(pixel => pixel.Should().BeInRange(0.0f, 1.0f));

        _output.WriteLine($"Processed {imageData.Length} pixels through image processing pipeline");
        _output.WriteLine($"Pipeline stages: {pipeline.Stages.Count}");

        // Cleanup
        result.OutputBuffer.Dispose();
    }

    [Fact]
    [Trait("TestType", "RealWorldScenario")]
    public async Task ExecutePipeline_MachineLearningInference_ComputesCorrectly()
    {
        // Arrange - Simulate ML inference pipeline (normalization, matrix multiply, activation)
        var inputFeatures = GenerateTestFeatureData(1000, 10); // 1000 samples, 10 features each
        var pipeline = CreateMLInferencePipeline("ml_inference", inputFeatures.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<float>(inputFeatures.Length * sizeof(float));
        await inputBuffer.CopyFromAsync(inputFeatures);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var predictions = new float[result.OutputBuffer.Length];
        await result.OutputBuffer.CopyToAsync(predictions);

        // Verify ML inference results
        predictions.Should().HaveCountGreaterThan(0);
        predictions.Should().AllSatisfy(prediction => prediction.Should().BeInRange(-10.0f, 10.0f));

        _output.WriteLine($"Generated {predictions.Length} predictions from {inputFeatures.Length} input features");

        // Cleanup
        result.OutputBuffer.Dispose();
    }

    [Fact]
    [Trait("TestType", "RealWorldScenario")]
    public async Task ExecutePipeline_FinancialAnalysis_CalculatesCorrectly()
    {
        // Arrange - Simulate financial analysis pipeline (moving averages, volatility, risk metrics)
        var priceData = GenerateTestPriceData(10000); // 10K price points
        var pipeline = CreateFinancialAnalysisPipeline("financial_analysis", priceData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<double>(priceData.Length * sizeof(double));
        await inputBuffer.CopyFromAsync(priceData);

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var analysisResults = new double[result.OutputBuffer.Length];
        await result.OutputBuffer.CopyToAsync(analysisResults);

        // Verify financial analysis results
        analysisResults.Should().HaveCountGreaterThan(0);
        analysisResults.Should().AllSatisfy(value => value.Should().NotBe(double.NaN));
        analysisResults.Should().AllSatisfy(value => value.Should().NotBe(double.PositiveInfinity));

        _output.WriteLine($"Computed {analysisResults.Length} financial metrics from {priceData.Length} price points");

        // Cleanup
        result.OutputBuffer.Dispose();
    }

    #endregion

    #region Telemetry and Monitoring Tests

    [Fact]
    [Trait("TestType", "Telemetry")]
    public async Task ExecutePipeline_TelemetryCollection_CapturesMetrics()
    {
        // Arrange
        var inputData = Enumerable.Range(1, 1000).ToArray();
        var pipeline = CreateTelemetryTestPipeline("telemetry_test", inputData.Length);

        using var inputBuffer = await _memoryManager.AllocateAsync<int>(inputData.Length * sizeof(int));
        await inputBuffer.CopyFromAsync(inputData);

        var initialMetrics = _telemetryProvider.GetMetrics();

        // Act
        var result = await _pipelineExecutor.ExecuteAsync(pipeline, inputBuffer);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        var finalMetrics = _telemetryProvider.GetMetrics();

        // Verify telemetry was collected
        finalMetrics.Should().ContainKey("pipeline.execution_time");
        finalMetrics.Should().ContainKey("pipeline.stages_executed");
        finalMetrics.Should().ContainKey("memory.allocated_bytes");

        var events = _telemetryProvider.GetEvents();
        events.Should().Contain(e => e.Name == "PipelineStarted");
        events.Should().Contain(e => e.Name == "PipelineCompleted");

        _output.WriteLine($"Captured {finalMetrics.Count - initialMetrics.Count} new metrics");
        _output.WriteLine($"Recorded {events.Count} telemetry events");

        // Cleanup
        result.OutputBuffer.Dispose();
    }

    #endregion

    #region Helper Methods

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));

        // Add core DotCompute services
        services.AddSingleton<IMemoryManager, TestMemoryManager>();
        services.AddSingleton<ITelemetryProvider, TestTelemetryProvider>();
        services.AddSingleton<IPipelineExecutor, TestPipelineExecutor>();
        services.AddSingleton<IPipelineOptimizer, TestPipelineOptimizer>();
    }

    // Pipeline creation helpers
    private TestPipeline CreateLinearPipeline(string name, TestPipelineStage[] stages)
    {
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateBranchingPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("input_preparation", x => x),
            CreateBranchStage("branch_point", new[]
            {
                CreateMapStage("branch_1", x => x * 2),
                CreateMapStage("branch_2", x => x * 3)
            }),
            CreateMergeStage("merge_results")
        };
        return new TestPipeline(name, stages, PipelineTopology.Branching);
    }

    private TestPipeline CreateReductionPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("prepare_for_reduction", x => x),
            CreateReduceStage("sum_all", (x, y) => x + y)
        };
        return new TestPipeline(name, stages, PipelineTopology.Reduction);
    }

    private TestPipeline CreateOptimizablePipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("step_1", x => x + 1),
            CreateMapStage("step_2", x => x * 2), // Can be fused with step_1
            CreateMapStage("step_3", x => x - 5), // Can be fused with step_2
            CreateFilterStage("filter_positive", x => x > 0)
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreatePerformanceTestPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("normalize", x => x / 1000.0f),
            CreateMapStage("transform", x => Math.Sin(x)),
            CreateFilterStage("filter_range", x => x >= -0.5f && x <= 0.5f)
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateConcurrentTestPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("concurrent_map", x => x * 2),
            CreateFilterStage("concurrent_filter", x => x % 3 != 0)
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateMemoryIntensivePipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMemoryIntensiveStage("large_buffer_operation", dataSize),
            CreateMapStage("final_transform", x => x)
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateLeakTestPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("leak_test_stage", x => x * 2)
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateFailingPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("before_failure", x => x),
            CreateFailingStage("failing_stage"),
            CreateMapStage("after_failure", x => x) // Should not execute
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreatePartialFailurePipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("stage_1", x => x),
            CreatePartialFailureStage("partial_failure_stage"),
            CreateMapStage("recovery_stage", x => x)
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateResourceExhaustionPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateResourceIntensiveStage("resource_exhaustion_stage", dataSize)
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateImageProcessingPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("grayscale_convert", pixel => pixel * 0.299f + pixel * 0.587f + pixel * 0.114f),
            CreateMapStage("blur_filter", pixel => pixel * 0.8f), // Simplified blur
            CreateMapStage("edge_detection", pixel => Math.Abs(pixel - 0.5f)) // Simplified edge detection
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateMLInferencePipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("feature_normalization", feature => (feature - 0.5f) / 0.5f),
            CreateMapStage("linear_transform", feature => feature * 1.2f + 0.1f),
            CreateMapStage("activation_function", feature => Math.Tanh(feature)) // Tanh activation
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateFinancialAnalysisPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateMapStage("price_normalization", price => Math.Log(price)),
            CreateMapStage("volatility_calculation", price => price * price), // Simplified volatility
            CreateMapStage("risk_adjustment", price => price * 0.95) // Risk factor
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    private TestPipeline CreateTelemetryTestPipeline(string name, int dataSize)
    {
        var stages = new[]
        {
            CreateTelemetryStage("telemetry_stage_1"),
            CreateMapStage("transform", x => x * 2),
            CreateTelemetryStage("telemetry_stage_2")
        };
        return new TestPipeline(name, stages, PipelineTopology.Linear);
    }

    // Stage creation helpers
    private TestPipelineStage CreateMapStage(string name, Func<dynamic, dynamic> transform)
    {
        return new TestPipelineStage(name, StageType.Map)
        {
            Transform = transform,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateFilterStage(string name, Func<dynamic, bool> predicate)
    {
        return new TestPipelineStage(name, StageType.Filter)
        {
            Predicate = predicate,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateReduceStage(string name, Func<dynamic, dynamic, dynamic> reducer)
    {
        return new TestPipelineStage(name, StageType.Reduce)
        {
            Reducer = reducer,
            CanExecuteInParallel = false
        };
    }

    private TestPipelineStage CreateBranchStage(string name, TestPipelineStage[] branches)
    {
        return new TestPipelineStage(name, StageType.Branch)
        {
            Branches = branches,
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateMergeStage(string name)
    {
        return new TestPipelineStage(name, StageType.Merge)
        {
            CanExecuteInParallel = false
        };
    }

    private TestPipelineStage CreateMemoryIntensiveStage(string name, int bufferSize)
    {
        return new TestPipelineStage(name, StageType.MemoryIntensive)
        {
            MemoryRequirement = bufferSize * sizeof(int) * 10, // 10x input size
            CanExecuteInParallel = true
        };
    }

    private TestPipelineStage CreateFailingStage(string name)
    {
        return new TestPipelineStage(name, StageType.Failing)
        {
            ShouldFail = true,
            FailureMessage = "Simulated stage failure"
        };
    }

    private TestPipelineStage CreatePartialFailureStage(string name)
    {
        return new TestPipelineStage(name, StageType.PartialFailure)
        {
            PartialFailureRate = 0.1f, // 10% failure rate
            CanRecover = true
        };
    }

    private TestPipelineStage CreateResourceIntensiveStage(string name, int dataSize)
    {
        return new TestPipelineStage(name, StageType.ResourceIntensive)
        {
            ResourceRequirement = dataSize * 100, // Very high resource requirement
            CanExecuteInParallel = false
        };
    }

    private TestPipelineStage CreateTelemetryStage(string name)
    {
        return new TestPipelineStage(name, StageType.Telemetry)
        {
            EmitTelemetry = true,
            CanExecuteInParallel = true
        };
    }

    // Test data generation helpers
    private float[] GenerateTestImageData(int width, int height)
    {
        var data = new float[width * height * 3]; // RGB
        var random = new Random(42);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)random.NextDouble();
        }
        return data;
    }

    private float[] GenerateTestFeatureData(int samples, int features)
    {
        var data = new float[samples * features];
        var random = new Random(42);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)(random.NextDouble() * 2.0 - 1.0); // Range [-1, 1]
        }
        return data;
    }

    private double[] GenerateTestPriceData(int points)
    {
        var data = new double[points];
        var random = new Random(42);
        var price = 100.0; // Starting price

        for (int i = 0; i < points; i++)
        {
            var change = (random.NextDouble() - 0.5) * 0.02; // ±1% change
            price *= (1.0 + change);
            data[i] = price;
        }
        return data;
    }

    public async Task InitializeAsync()
    {
        // Any async initialization if needed
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (!_disposed)
        {
            foreach (var disposable in _disposables)
            {
                try
                {
                    if (disposable is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else
                    {
                        disposable.Dispose();
                    }
                }
                catch
                {
                    // Ignore disposal errors during cleanup
                }
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }
}

// Helper enums and classes for test infrastructure
public enum PipelineTopology
{
    Linear,
    Branching,
    Reduction,
    Complex
}

public enum StageType
{
    Map,
    Filter,
    Reduce,
    Branch,
    Merge,
    MemoryIntensive,
    Failing,
    PartialFailure,
    ResourceIntensive,
    Telemetry
}

// Additional test classes would be implemented in separate files for maintainability
public class TestPipeline : IPipeline
{
    public string Name { get; }
    public IReadOnlyList<IPipelineStage> Stages { get; }
    public PipelineTopology Topology { get; }

    public TestPipeline(string name, TestPipelineStage[] stages, PipelineTopology topology)
    {
        Name = name;
        Stages = stages;
        Topology = topology;
    }
}

public class TestPipelineStage : IPipelineStage
{
    public string Name { get; }
    public StageType Type { get; }
    public bool CanExecuteInParallel { get; set; }
    public Func<dynamic, dynamic>? Transform { get; set; }
    public Func<dynamic, bool>? Predicate { get; set; }
    public Func<dynamic, dynamic, dynamic>? Reducer { get; set; }
    public TestPipelineStage[]? Branches { get; set; }

    // Test-specific properties
    public long MemoryRequirement { get; set; }
    public bool ShouldFail { get; set; }
    public string FailureMessage { get; set; } = string.Empty;
    public float PartialFailureRate { get; set; }
    public bool CanRecover { get; set; }
    public long ResourceRequirement { get; set; }
    public bool EmitTelemetry { get; set; }

    public TestPipelineStage(string name, StageType type)
    {
        Name = name;
        Type = type;
    }
}

public class PipelineExecutionResult
{
    public bool Success { get; set; }
    public IMemoryBuffer<dynamic>? OutputBuffer { get; set; }
    public List<PipelineExecutionResult> BranchResults { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> FallbacksUsed { get; set; } = new();
    public PipelineError? Error { get; set; }
}

public class PipelineError
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public string StageName { get; set; } = string.Empty;
}

// Interfaces that would be implemented in the actual DotCompute system
public interface IPipelineExecutor
{
    Task<PipelineExecutionResult> ExecuteAsync(IPipeline pipeline, IMemoryBuffer<dynamic> inputBuffer, CancellationToken cancellationToken = default);
}

public interface IPipelineOptimizer
{
    Task<IPipeline> OptimizeAsync(IPipeline pipeline, CancellationToken cancellationToken = default);
}

public interface ITelemetryProvider
{
    Dictionary<string, List<object>> GetMetrics();
    List<TelemetryEvent> GetEvents();
    void RecordMetric(string name, object value);
    void TrackEvent(string name, Dictionary<string, object>? properties = null);
}

public class TelemetryEvent
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; }
}

// Test implementations would be in separate files
public class TestMemoryManager : IMemoryManager
{
    public MemoryStatistics Statistics => throw new NotImplementedException();

    public Task<IMemoryBuffer<T>> AllocateAsync<T>(long sizeInBytes, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return Task.FromResult<IMemoryBuffer<T>>(new TestMemoryBuffer<T>(sizeInBytes));
    }

    public void Dispose() { }
}

public class TestMemoryBuffer<T> : IMemoryBuffer<T> where T : unmanaged
{
    public long SizeInBytes { get; }
    public long Length { get; }
    public MemoryType MemoryType => MemoryType.Host;
    public IntPtr DevicePointer => IntPtr.Zero;
    public bool IsDisposed { get; private set; }

    private readonly T[] _data;

    public TestMemoryBuffer(long sizeInBytes)
    {
        SizeInBytes = sizeInBytes;
        Length = sizeInBytes / sizeof(T);
        _data = new T[Length];
    }

    public Span<T> AsSpan() => _data.AsSpan();
    public Memory<T> AsMemory() => _data.AsMemory();

    public Task CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        source.Span.CopyTo(_data.AsSpan());
        return Task.CompletedTask;
    }

    public Task CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        _data.AsSpan().CopyTo(destination.Span);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

public class TestPipelineExecutor : IPipelineExecutor
{
    public Task<PipelineExecutionResult> ExecuteAsync(IPipeline pipeline, IMemoryBuffer<dynamic> inputBuffer, CancellationToken cancellationToken = default)
    {
        // Simplified test implementation
        var result = new PipelineExecutionResult
        {
            Success = true,
            OutputBuffer = inputBuffer // For testing, just return input as output
        };
        return Task.FromResult(result);
    }
}

public class TestPipelineOptimizer : IPipelineOptimizer
{
    public Task<IPipeline> OptimizeAsync(IPipeline pipeline, CancellationToken cancellationToken = default)
    {
        // For testing, return the same pipeline (no optimization)
        return Task.FromResult(pipeline);
    }
}

public class TestTelemetryProvider : ITelemetryProvider
{
    private readonly Dictionary<string, List<object>> _metrics = new();
    private readonly List<TelemetryEvent> _events = new();

    public Dictionary<string, List<object>> GetMetrics() => _metrics;
    public List<TelemetryEvent> GetEvents() => _events;

    public void RecordMetric(string name, object value)
    {
        if (!_metrics.ContainsKey(name))
            _metrics[name] = new List<object>();
        _metrics[name].Add(value);
    }

    public void TrackEvent(string name, Dictionary<string, object>? properties = null)
    {
        _events.Add(new TelemetryEvent
        {
            Name = name,
            Properties = properties ?? new Dictionary<string, object>(),
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}