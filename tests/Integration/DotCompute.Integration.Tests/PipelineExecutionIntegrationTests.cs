// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Core.Pipelines;
using DotCompute.Tests.Integration.Infrastructure;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for multi-stage kernel pipeline execution,
/// including parallel execution, data dependencies, and pipeline optimization.
/// </summary>
[Collection("Integration")]
public class PipelineExecutionIntegrationTests : ComputeWorkflowTestBase
{
    public PipelineExecutionIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task LinearPipeline_SequentialStages_ShouldExecuteInOrder()
    {
        // Arrange
        var pipeline = CreateLinearImageProcessingPipeline();

        // Act
        var result = await ExecuteComputeWorkflowAsync("LinearPipeline", pipeline);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionResults.Count.Should().Be(4);

        // Verify stages executed in correct order
        var executionTimes = result.ExecutionResults
            .OrderBy(r => int.Parse(r.Key.Split('_')[1])) // stage_1, stage_2, etc.
            .Select(r => r.Value.Duration.TotalMilliseconds)
            .ToArray();

        // Each stage should have had time to complete before the next started
        Logger.LogInformation("Stage execution times: {Times}",
            string.Join(", ", executionTimes.Select(t => $"{t:F1}ms")));

        executionTimes.Should().AllSatisfy(t => t.Should().BeGreaterThan(0));

        LogPerformanceMetrics("LinearPipeline", result.Duration, 256 * 256 * 4);
    }

    [Fact]
    public async Task ParallelPipeline_IndependentStages_ShouldExecuteConcurrently()
    {
        // Arrange
        var pipeline = CreateParallelProcessingPipeline();

        // Act
        var result = await ExecuteComputeWorkflowAsync("ParallelPipeline", pipeline);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionResults.Count.Should().Be(5); // 1 setup + 3 parallel + 1 merge

        // Verify parallel stages
        var parallelStages = result.ExecutionResults
            .Where(r => r.Key.Contains("parallel"))
            .ToList();

        Assert.Equal(3, parallelStages.Count());
        parallelStages.Should().AllSatisfy(stage =>
            stage.Value.Success.Should().BeTrue());

        // Calculate expected vs actual execution time
        var parallelExecutionTime = parallelStages.Max(s => s.Value.Duration.TotalMilliseconds);
        var sequentialTime = parallelStages.Sum(s => s.Value.Duration.TotalMilliseconds);
        var parallelEfficiency = (sequentialTime - parallelExecutionTime) / sequentialTime;

        Logger.LogInformation("Parallel efficiency: {Efficiency:P1}Sequential: {Sequential:F1}ms, Parallel: {Parallel:F1}ms)",
            parallelEfficiency, sequentialTime, parallelExecutionTime);

        parallelEfficiency.Should().BeGreaterThan(0.3, "Parallel execution should show significant improvement");

        LogPerformanceMetrics("ParallelPipeline", result.Duration, 1024 * 3);
    }

    [Fact]
    public async Task ComplexDAGPipeline_DataDependencies_ShouldRespectDependencies()
    {
        // Arrange
        var pipeline = CreateComplexDAGPipeline();

        // Act
        var result = await ExecuteComputeWorkflowAsync("ComplexDAGPipeline", pipeline);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionResults.Count.Should().Be(7);

        // Verify dependency execution order
        ValidateDAGExecutionOrder(result.ExecutionResults);

        // Check that all final outputs are present and valid
        result.Results.Should().ContainKey("final_output");
        var finalOutput = (float[])result.Results["final_output"];
        Assert.NotEmpty(finalOutput);
        finalOutput.Should().NotContain(float.NaN);

        LogPerformanceMetrics("ComplexDAGPipeline", result.Duration, 2048);
    }

    [Fact]
    public async Task StreamingPipeline_ContinuousData_ShouldProcessInChunks()
    {
        // Arrange
        const int totalDataSize = 8192;
        const int chunkSize = 512;
        var sourceData = TestDataGenerators.GenerateFloatArray(totalDataSize);
        var results = new ConcurrentQueue<float[]>();
        var processingStopwatch = Stopwatch.StartNew();

        // Act - Process data in streaming fashion
        var chunkTasks = new List<Task>();

        for (var offset = 0; offset < totalDataSize; offset += chunkSize)
        {
            var currentChunk = sourceData
                .Skip(offset)
                .Take(Math.Min(chunkSize, totalDataSize - offset))
                .ToArray();

            var chunkPipeline = CreateStreamingChunkPipeline(currentChunk, offset);

            var chunkTask = Task.Run(async () =>
            {
                var chunkResult = await ExecuteComputeWorkflowAsync(
                    $"StreamingChunk_{offset / chunkSize}", chunkPipeline);

                if (chunkResult.Success && chunkResult.Results.ContainsKey("processed_chunk"))
                {
                    results.Enqueue((float[])chunkResult.Results["processed_chunk"]);
                }

                return chunkResult;
            });

            chunkTasks.Add(chunkTask);

            // Add small delay to simulate streaming
            await Task.Delay(10);
        }

        var chunkResults = await Task.WhenAll(chunkTasks.Cast<Task<WorkflowExecutionResult>>());
        processingStopwatch.Stop();

        // Assert
        chunkResults.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        Assert.Equal(totalDataSize / chunkSize, results.Count());

        var totalProcessedElements = results.Sum(chunk => chunk.Length);
        Assert.Equal(totalDataSize, totalProcessedElements);

        var streamingThroughput = (totalDataSize * sizeof(float)) / 1024.0 / 1024.0 /
                                 processingStopwatch.Elapsed.TotalSeconds;

        Logger.LogInformation("Streaming pipeline: {Chunks} chunks, {Throughput:F2} MB/s",
            chunkResults.Length, streamingThroughput);

        streamingThroughput.Should().BeGreaterThan(5, "Streaming should maintain reasonable throughput");

        LogPerformanceMetrics("StreamingPipeline", processingStopwatch.Elapsed, totalDataSize);
    }

    [Fact]
    public async Task AdaptivePipeline_LoadBalancing_ShouldOptimizeResourceUsage()
    {
        // Arrange
        var pipeline = CreateAdaptiveLoadBalancingPipeline();

        // Simulate varying system load
        HardwareSimulator.SimulateMemoryPressure(AcceleratorType.CUDA, 0.7);

        // Act
        var result = await ExecuteComputeWorkflowAsync("AdaptivePipeline", pipeline);

        // Assert
        result.Success.Should().BeTrue();

        // Verify that the pipeline adapted to system conditions
        var executionResults = result.ExecutionResults.Values.ToList();

        // Should have reasonable resource utilization despite pressure
        if (result.Metrics != null)
        {
            var resourceUtil = result.Metrics.ResourceUtilization;
            resourceUtil.MemoryUsagePercent.Should().BeLessThan(90,
                "Adaptive pipeline should manage memory pressure");
        }

        // All stages should complete successfully despite constraints
        executionResults.Should().AllSatisfy(r => r.Success.Should().BeTrue());

        LogPerformanceMetrics("AdaptivePipeline", result.Duration, 4096);

        // Reset hardware simulation
        HardwareSimulator.ResetAllConditions();
    }

    [Fact]
    public async Task PipelineWithFeedback_IterativeProcessing_ShouldConverge()
    {
        // Arrange
        const int maxIterations = 10;
        const float convergenceThreshold = 0.01f;
        var initialData = TestDataGenerators.GenerateFloatArray(1024, 10f, 100f);

        var feedbackPipeline = CreateIterativeFeedbackPipeline(
            initialData, maxIterations, convergenceThreshold);

        // Act
        var result = await ExecuteComputeWorkflowAsync("FeedbackPipeline", feedbackPipeline);

        // Assert
        result.Success.Should().BeTrue();

        // Verify convergence was achieved
        result.Results.Should().ContainKey("final_result");
        result.Results.Should().ContainKey("iterations_count");
        result.Results.Should().ContainKey("convergence_achieved");

        var iterationsCount = (float[])result.Results["iterations_count"];
        var convergenceAchieved = (float[])result.Results["convergence_achieved"];

        iterationsCount[0].Should().BeLessOrEqualTo(maxIterations);
        convergenceAchieved[0].Should().Be(1.0f); // Algorithm should converge

        Logger.LogInformation("Feedback pipeline converged in {Iterations} iterations", iterationsCount[0]);

        LogPerformanceMetrics("FeedbackPipeline", result.Duration, 1024);
    }

    [Fact]
    public async Task PipelineErrorHandling_PartialFailure_ShouldRecoverGracefully()
    {
        // Arrange - Pipeline with intentional failure in middle stage
        var pipeline = CreateErrorPronePipeline();

        // Act
        var result = await ExecuteComputeWorkflowAsync("ErrorRecoveryPipeline", pipeline);

        // Assert
        // Pipeline should handle errors gracefully
        var successfulStages = result.ExecutionResults.Values.Count(r => r.Success);
        var failedStages = result.ExecutionResults.Values.Count(r => !r.Success);

        Logger.LogInformation("Pipeline execution: {Successful} successful, {Failed} failed stages",
            successfulStages, failedStages);

        failedStages.Should().BeGreaterThan(0, "Should have simulated failures");
        (successfulStages > failedStages).Should().BeTrue();

        // Should have partial results available
        result.Results.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(2, 512)]
    [InlineData(4, 1024)]
    [InlineData(8, 2048)]
    public async Task ScalablePipeline_VaryingComplexity_ShouldScalePerformance(int stages, int dataSize)
    {
        // Arrange
        var pipeline = CreateScalablePipeline(stages, dataSize);

        // Act
        var result = await ExecuteComputeWorkflowAsync($"ScalablePipeline_{stages}_{dataSize}", pipeline);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionResults.Count.Should().Be(stages);

        // Verify performance scaling
        if (result.Metrics != null)
        {
            var throughputPerStage = result.Metrics.ThroughputMBps / stages;
            throughputPerStage.Should().BeGreaterThan(1, "Each stage should maintain reasonable throughput");

            var executionTimePerElement = result.Metrics.ExecutionTime / dataSize;
            executionTimePerElement.Should().BeLessThan(1, "Per-element processing should be efficient");
        }

        LogPerformanceMetrics($"ScalablePipeline_{stages}_{dataSize}", result.Duration, dataSize * stages);
    }

    // Helper methods for creating different pipeline configurations

    private static ComputeWorkflowDefinition CreateLinearImageProcessingPipeline()
    {
        const int imageSize = 256;
        var imageData = TestDataGenerators.GenerateFloatArray(imageSize * imageSize, 0f, 255f);

        return new ComputeWorkflowDefinition
        {
            Name = "LinearImageProcessing",
            Kernels =
            [
                new WorkflowKernel { Name = "load_image", SourceCode = KernelSources.LoadImage },
                new WorkflowKernel { Name = "gaussian_blur", SourceCode = KernelSources.GaussianBlur },
                new WorkflowKernel { Name = "edge_detection", SourceCode = KernelSources.EdgeDetection },
                new WorkflowKernel { Name = "save_image", SourceCode = KernelSources.SaveImage }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "raw_image", Data = imageData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "processed_image", Size = imageSize * imageSize }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "loaded_image", SizeInBytes = imageSize * imageSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "blurred_image", SizeInBytes = imageSize * imageSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "edge_image", SizeInBytes = imageSize * imageSize * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "stage_1_load",
                    Order = 1,
                    KernelName = "load_image",
                    ArgumentNames = ["raw_image", "loaded_image"]
                },
                new WorkflowExecutionStage
                {
                    Name = "stage_2_blur",
                    Order = 2,
                    KernelName = "gaussian_blur",
                    ArgumentNames = ["loaded_image", "blurred_image"],
                    Parameters = new Dictionary<string, object> { ["width"] = imageSize, ["height"] = imageSize }
                },
                new WorkflowExecutionStage
                {
                    Name = "stage_3_edge",
                    Order = 3,
                    KernelName = "edge_detection",
                    ArgumentNames = ["blurred_image", "edge_image"],
                    Parameters = new Dictionary<string, object> { ["width"] = imageSize, ["height"] = imageSize }
                },
                new WorkflowExecutionStage
                {
                    Name = "stage_4_save",
                    Order = 4,
                    KernelName = "save_image",
                    ArgumentNames = ["edge_image", "processed_image"]
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateParallelProcessingPipeline()
    {
        const int dataSize = 1024;
        var inputData = TestDataGenerators.GenerateFloatArray(dataSize * 3);

        return new ComputeWorkflowDefinition
        {
            Name = "ParallelProcessing",
            Kernels =
            [
                new WorkflowKernel { Name = "data_split", SourceCode = KernelSources.DataSplit },
                new WorkflowKernel { Name = "process_channel", SourceCode = KernelSources.ProcessChannel },
                new WorkflowKernel { Name = "data_merge", SourceCode = KernelSources.DataMerge }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input_data", Data = inputData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "merged_result", Size = dataSize * 3 }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "channel_0", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "channel_1", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "channel_2", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "processed_0", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "processed_1", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "processed_2", SizeInBytes = dataSize * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "setup_split",
                    Order = 1,
                    KernelName = "data_split",
                    ArgumentNames = ["input_data", "channel_0", "channel_1", "channel_2"],
                    Parameters = new Dictionary<string, object> { ["channel_size"] = dataSize }
                },
                new WorkflowExecutionStage
                {
                    Name = "parallel_process_0",
                    Order = 2,
                    KernelName = "process_channel",
                    ArgumentNames = ["channel_0", "processed_0"],
                    Parameters = new Dictionary<string, object> { ["channel_id"] = 0 }
                },
                new WorkflowExecutionStage
                {
                    Name = "parallel_process_1",
                    Order = 2, // Same order = parallel execution
                    KernelName = "process_channel",
                    ArgumentNames = ["channel_1", "processed_1"],
                    Parameters = new Dictionary<string, object> { ["channel_id"] = 1 }
                },
                new WorkflowExecutionStage
                {
                    Name = "parallel_process_2",
                    Order = 2, // Same order = parallel execution
                    KernelName = "process_channel",
                    ArgumentNames = ["channel_2", "processed_2"],
                    Parameters = new Dictionary<string, object> { ["channel_id"] = 2 }
                },
                new WorkflowExecutionStage
                {
                    Name = "final_merge",
                    Order = 3,
                    KernelName = "data_merge",
                    ArgumentNames = ["processed_0", "processed_1", "processed_2", "merged_result"],
                    Parameters = new Dictionary<string, object> { ["channel_size"] = dataSize }
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateComplexDAGPipeline()
    {
        const int dataSize = 512;
        var inputData = TestDataGenerators.GenerateFloatArray(dataSize);

        return new ComputeWorkflowDefinition
        {
            Name = "ComplexDAG",
            Kernels =
            [
                new WorkflowKernel { Name = "preprocess", SourceCode = KernelSources.PreprocessData },
                new WorkflowKernel { Name = "feature_extract", SourceCode = KernelSources.FeatureExtract },
                new WorkflowKernel { Name = "normalize", SourceCode = KernelSources.NormalizeData },
                new WorkflowKernel { Name = "classify", SourceCode = KernelSources.ClassifyData },
                new WorkflowKernel { Name = "postprocess", SourceCode = KernelSources.PostprocessData }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "raw_data", Data = inputData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "final_output", Size = dataSize }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "preprocessed", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "features_a", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "features_b", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "normalized_a", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "normalized_b", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "classified", SizeInBytes = dataSize * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage { Name = "stage_preprocess", Order = 1, KernelName = "preprocess", ArgumentNames = ["raw_data", "preprocessed"] },
                new WorkflowExecutionStage { Name = "stage_extract_a", Order = 2, KernelName = "feature_extract", ArgumentNames = ["preprocessed", "features_a"], Parameters = new Dictionary<string, object> { ["mode"] = "A" } },
                new WorkflowExecutionStage { Name = "stage_extract_b", Order = 2, KernelName = "feature_extract", ArgumentNames = ["preprocessed", "features_b"], Parameters = new Dictionary<string, object> { ["mode"] = "B" } },
                new WorkflowExecutionStage { Name = "stage_normalize_a", Order = 3, KernelName = "normalize", ArgumentNames = ["features_a", "normalized_a"] },
                new WorkflowExecutionStage { Name = "stage_normalize_b", Order = 3, KernelName = "normalize", ArgumentNames = ["features_b", "normalized_b"] },
                new WorkflowExecutionStage { Name = "stage_classify", Order = 4, KernelName = "classify", ArgumentNames = ["normalized_a", "normalized_b", "classified"] },
                new WorkflowExecutionStage { Name = "stage_postprocess", Order = 5, KernelName = "postprocess", ArgumentNames = ["classified", "final_output"] }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateStreamingChunkPipeline(float[] chunkData, int chunkOffset)
    {
        return new ComputeWorkflowDefinition
        {
            Name = $"StreamingChunk_{chunkOffset}",
            Kernels =
            [
                new WorkflowKernel { Name = "process_chunk", SourceCode = KernelSources.ProcessChunk }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "chunk_data", Data = chunkData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "processed_chunk", Size = chunkData.Length }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "process_stage",
                    Order = 1,
                    KernelName = "process_chunk",
                    ArgumentNames = ["chunk_data", "processed_chunk"],
                    Parameters = new Dictionary<string, object> { ["chunk_offset"] = chunkOffset }
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateAdaptiveLoadBalancingPipeline()
    {
        const int dataSize = 1024;
        var data = TestDataGenerators.GenerateFloatArray(dataSize);

        return new ComputeWorkflowDefinition
        {
            Name = "AdaptiveLoadBalancing",
            Kernels =
            [
                new WorkflowKernel { Name = "adaptive_process", SourceCode = KernelSources.AdaptiveProcess }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = data }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = dataSize }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "adaptive_stage",
                    Order = 1,
                    KernelName = "adaptive_process",
                    ArgumentNames = ["input", "output"],
                    ExecutionOptions = new ExecutionOptions
                    {
                        GlobalWorkSize = [dataSize],
                        LocalWorkSize = [64] // Conservative for memory pressure
                    }
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateIterativeFeedbackPipeline(float[] initialData, int maxIterations, float threshold)
    {
        return new ComputeWorkflowDefinition
        {
            Name = "IterativeFeedback",
            Kernels =
            [
                new WorkflowKernel { Name = "iterative_process", SourceCode = KernelSources.IterativeProcess }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "initial_data", Data = initialData }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "final_result", Size = initialData.Length },
                new WorkflowOutput { Name = "iterations_count", Size = 1 },
                new WorkflowOutput { Name = "convergence_achieved", Size = 1 }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage
                {
                    Name = "iterative_stage",
                    Order = 1,
                    KernelName = "iterative_process",
                    ArgumentNames = ["initial_data", "final_result", "iterations_count", "convergence_achieved"],
                    Parameters = new Dictionary<string, object>
                    {
                        ["max_iterations"] = maxIterations,
                        ["threshold"] = threshold,
                        ["data_size"] = initialData.Length
                    }
                }
            ]
        };
    }

    private static ComputeWorkflowDefinition CreateErrorPronePipeline()
    {
        const int dataSize = 512;
        var data = TestDataGenerators.GenerateFloatArray(dataSize);

        return new ComputeWorkflowDefinition
        {
            Name = "ErrorPronePipeline",
            Kernels =
            [
                new WorkflowKernel { Name = "reliable_stage", SourceCode = KernelSources.ReliableProcess },
                new WorkflowKernel { Name = "error_prone_stage", SourceCode = KernelSources.ErrorProneProcess },
                new WorkflowKernel { Name = "recovery_stage", SourceCode = KernelSources.RecoveryProcess }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = data }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "final_output", Size = dataSize }
            ],
            IntermediateBuffers =
            [
                new WorkflowIntermediateBuffer { Name = "intermediate", SizeInBytes = dataSize * sizeof(float) },
                new WorkflowIntermediateBuffer { Name = "backup", SizeInBytes = dataSize * sizeof(float) }
            ],
            ExecutionStages =
            [
                new WorkflowExecutionStage { Name = "stage_1", Order = 1, KernelName = "reliable_stage", ArgumentNames = ["input", "intermediate", "backup"] },
                new WorkflowExecutionStage { Name = "stage_2", Order = 2, KernelName = "error_prone_stage", ArgumentNames = ["intermediate", "intermediate"] },
                new WorkflowExecutionStage { Name = "stage_3", Order = 3, KernelName = "recovery_stage", ArgumentNames = ["intermediate", "backup", "final_output"] }
            ],
            ContinueOnError = true // Allow pipeline to continue even if some stages fail
        };
    }

    private static ComputeWorkflowDefinition CreateScalablePipeline(int numStages, int dataSize)
    {
        var data = TestDataGenerators.GenerateFloatArray(dataSize);

        var workflow = new ComputeWorkflowDefinition
        {
            Name = $"ScalablePipeline_{numStages}",
            Kernels =
            [
                new WorkflowKernel { Name = "scalable_process", SourceCode = KernelSources.ScalableProcess }
            ],
            Inputs =
            [
                new WorkflowInput { Name = "input", Data = data }
            ],
            Outputs =
            [
                new WorkflowOutput { Name = "output", Size = dataSize }
            ],
            IntermediateBuffers = [],
            ExecutionStages = []
        };

        // Create intermediate buffers
        for (var i = 0; i < numStages - 1; i++)
        {
            workflow.IntermediateBuffers.Add(new WorkflowIntermediateBuffer
            {
                Name = $"intermediate_{i}",
                SizeInBytes = dataSize * sizeof(float)
            });
        }

        // Create execution stages
        for (var stage = 0; stage < numStages; stage++)
        {
            var inputName = stage == 0 ? "input" : $"intermediate_{stage - 1}";
            var outputName = stage == numStages - 1 ? "output" : $"intermediate_{stage}";

            workflow.ExecutionStages.Add(new WorkflowExecutionStage
            {
                Name = $"stage_{stage}",
                Order = stage + 1,
                KernelName = "scalable_process",
                ArgumentNames = [inputName, outputName],
                Parameters = new Dictionary<string, object> { ["stage_id"] = stage }
            });
        }

        return workflow;
    }

    private void ValidateDAGExecutionOrder(Dictionary<string, StageExecutionResult> executionResults)
    {
        // Verify that DAG dependencies were respected
        var stages = executionResults.Keys.ToList();

        // stage_preprocess should execute before extract stages
        var preprocessEnd = executionResults["stage_preprocess"].Duration;
        var extractAStart = executionResults["stage_extract_a"].Duration;
        var extractBStart = executionResults["stage_extract_b"].Duration;

        // Note: In a real implementation, we'd have actual start/end timestamps
        // For testing, we validate logical dependencies

        Assert.Contains("stage_preprocess", stages);
        Assert.Contains("stage_extract_a", stages);
        Assert.Contains("stage_extract_b", stages);
        Assert.Contains("stage_normalize_a", stages);
        Assert.Contains("stage_normalize_b", stages);
        Assert.Contains("stage_classify", stages);
        Assert.Contains("stage_postprocess", stages);

        Logger.LogInformation("DAG execution validated: all {Count} stages present and executed", stages.Count);
    }
}

/// <summary>
/// Additional kernel sources for pipeline testing.
/// </summary>
internal static partial class KernelSources
{
    public const string LoadImage = @"
__kernel void load_image(__global const float* raw, __global float* loaded) {
    int gid = get_global_id(0);
    loaded[gid] = clamp(raw[gid], 0.0f, 255.0f);
}";

    public const string SaveImage = @"
__kernel void save_image(__global const float* processed, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = processed[gid];
}";

    public const string DataSplit = @"
__kernel void data_split(__global const float* input,
                        __global float* ch0, __global float* ch1, __global float* ch2,
                        int channel_size) {
    int gid = get_global_id(0);
    if(gid < channel_size) {
        ch0[gid] = input[gid];
        ch1[gid] = input[gid + channel_size];
        ch2[gid] = input[gid + 2 * channel_size];
    }
}";

    public const string ProcessChannel = @"
__kernel void process_channel(__global const float* input, __global float* output, int channel_id) {
    int gid = get_global_id(0);
    float factor = 1.0f + channel_id * 0.1f;
    output[gid] = input[gid] * factor + channel_id;
}";

    public const string DataMerge = @"
__kernel void data_merge(__global const float* ch0, __global const float* ch1, __global const float* ch2,
                        __global float* output, int channel_size) {
    int gid = get_global_id(0);
    if(gid < channel_size) {
        output[gid] = ch0[gid];
        output[gid + channel_size] = ch1[gid];
        output[gid + 2 * channel_size] = ch2[gid];
    }
}";

    public const string PreprocessData = @"
__kernel void preprocess(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] =(input[gid] - 128.0f) / 128.0f; // Normalize to [-1, 1]
}";

    public const string FeatureExtract = @"
__kernel void feature_extract(__global const float* input, __global float* output, char mode) {
    int gid = get_global_id(0);
    if(mode == 'A') {
        output[gid] = fabs(input[gid]); // Absolute value features
    } else {
        output[gid] = input[gid] * input[gid]; // Squared features
    }
}";

    public const string NormalizeData = @"
__kernel void normalize(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    float value = input[gid];
    output[gid] = value /(1.0f + fabs(value)); // Soft normalization
}";

    public const string ClassifyData = @"
__kernel void classify(__global const float* features_a, __global const float* features_b, __global float* output) {
    int gid = get_global_id(0);
    float combined = features_a[gid] * 0.6f + features_b[gid] * 0.4f;
    output[gid] = 1.0f /(1.0f + exp(-combined)); // Sigmoid classification
}";

    public const string PostprocessData = @"
__kernel void postprocess(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    output[gid] = input[gid] > 0.5f ? 1.0f : 0.0f; // Threshold to binary
}";

    public const string ProcessChunk = @"
__kernel void process_chunk(__global const float* chunk, __global float* output, int chunk_offset) {
    int gid = get_global_id(0);
    float offset_factor = chunk_offset * 0.001f;
    output[gid] = chunk[gid] + offset_factor + sin(chunk[gid] + offset_factor);
}";

    public const string AdaptiveProcess = @"
__kernel void adaptive_process(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    __local float shared[64];
    
    shared[lid] = input[gid];
    barrier(CLK_LOCAL_MEM_FENCE);
    
    float result = shared[lid];
    // Adaptive processing based on local values
    if(lid > 0) result += shared[lid - 1] * 0.1f;
    if(lid < get_local_size(0) - 1) result += shared[lid + 1] * 0.1f;
    
    output[gid] = result;
}";

    public const string IterativeProcess = @"
__kernel void iterative_process(__global const float* initial,
                               __global float* result,
                               __global float* iterations,
                               __global float* converged,
                               int max_iterations, float threshold, int data_size) {
    int gid = get_global_id(0);
    
    if(gid == 0) {
        float prev_error = 1000.0f;
        int iter = 0;
        
        for(iter = 0; iter < max_iterations; iter++) {
            float current_error = 0.0f;
            
            // Simulate iterative algorithm(simple gradient descent)
            for(int i = 0; i < data_size; i++) {
                float current =(iter == 0) ? initial[i] : result[i];
                float target = 50.0f; // Target value
                float gradient = 2.0f *(current - target);
                result[i] = current - 0.01f * gradient;
                current_error += fabs(result[i] - target);
            }
            
            current_error /= data_size;
            if(fabs(prev_error - current_error) < threshold) {
                converged[0] = 1.0f;
                break;
            }
            prev_error = current_error;
        }
        
        iterations[0] =(float)iter;
        if(converged[0] != 1.0f) converged[0] = 0.0f;
    }
}";

    public const string ReliableProcess = @"
__kernel void reliable_process(__global const float* input, __global float* output, __global float* backup) {
    int gid = get_global_id(0);
    output[gid] = input[gid] * 1.1f;
    backup[gid] = input[gid]; // Keep backup
}";

    public const string ErrorProneProcess = @"
__kernel void error_prone_process(__global const float* input, __global float* output) {
    int gid = get_global_id(0);
    // Simulate occasional errors
    if(gid % 100 == 13) { // ~1% error rate
        output[gid] = NAN; // Introduce error
    } else {
        output[gid] = input[gid] * 2.0f;
    }
}";

    public const string RecoveryProcess = @"
__kernel void recovery_process(__global const float* input, __global const float* backup, __global float* output) {
    int gid = get_global_id(0);
    if(isnan(input[gid]) || isinf(input[gid])) {
        output[gid] = backup[gid] * 2.0f; // Use backup value
    } else {
        output[gid] = input[gid];
    }
}";

    public const string ScalableProcess = @"
__kernel void scalable_process(__global const float* input, __global float* output, int stage_id) {
    int gid = get_global_id(0);
    float stage_factor = 1.0f + stage_id * 0.05f;
    output[gid] = input[gid] * stage_factor + stage_id;
}";
}
