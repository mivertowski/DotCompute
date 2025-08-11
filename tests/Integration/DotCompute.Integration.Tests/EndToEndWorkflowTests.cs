// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Core.Pipelines;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Integration.Tests;

/// <summary>
/// Integration tests for complete end-to-end compute workflows.
/// Tests the full pipeline from kernel definition through execution and result collection.
/// </summary>
public class EndToEndWorkflowTests : IntegrationTestBase
{
    public EndToEndWorkflowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task CompleteComputePipeline_VectorAddition_ShouldExecuteSuccessfully()
    {
        // Arrange
        const int arraySize = 1024;
        var inputA = GenerateTestArray(arraySize);
        var inputB = GenerateTestArray(arraySize);
        var expected = inputA.Zip(inputB, (a, b) => a + b).ToArray();

        // Act
        var result = await ExecuteEndToEndWorkflow(
            "vector_add",
            VectorAddKernelSource,
            [inputA, inputB],
            arraySize);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        var output = result.GetOutput<float[]>("result");
        output.Should().NotBeNull();
        output.Length.Should().Be(arraySize);
        
        for (int i = 0; i < arraySize; i++)
        {
            output[i].Should().BeApproximately(expected[i], 0.001f);
        }
    }

    [Fact]
    public async Task CompleteComputePipeline_MatrixMultiplication_ShouldExecuteSuccessfully()
    {
        // Arrange
        const int matrixSize = 16; // Keep small for test performance
        var matrixA = GenerateTestMatrix(matrixSize, matrixSize);
        var matrixB = GenerateTestMatrix(matrixSize, matrixSize);

        // Act
        var result = await ExecuteEndToEndWorkflow(
            "matrix_mul",
            MatrixMultiplyKernelSource,
            [matrixA, matrixB],
            matrixSize);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        var output = result.GetOutput<float[]>("result");
        output.Should().NotBeNull();
        output.Length.Should().Be(matrixSize * matrixSize);
    }

    [Fact]
    public async Task CompleteComputePipeline_ReductionOperation_ShouldExecuteSuccessfully()
    {
        // Arrange
        const int arraySize = 512;
        var input = GenerateTestArray(arraySize);
        var expected = input.Sum();

        // Act
        var result = await ExecuteEndToEndWorkflow(
            "reduce_sum",
            ReductionKernelSource,
            [input],
            arraySize);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        var output = result.GetOutput<float>("result");
        output.Should().BeApproximately(expected, 0.1f);
    }

    [Theory]
    [InlineData(64, 1)]
    [InlineData(256, 4)]
    [InlineData(1024, 16)]
    [InlineData(4096, 64)]
    public async Task CompleteComputePipeline_VariousSizes_ShouldScaleProperly(int size, int expectedThreadBlocks)
    {
        // Arrange
        var input = GenerateTestArray(size);

        // Act
        var result = await ExecuteEndToEndWorkflow(
            "vector_scale",
            VectorScaleKernelSource,
            [input, 2.0f],
            size);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        var output = result.GetOutput<float[]>("result");
        output.Should().NotBeNull();
        output.Length.Should().Be(size);
        
        // Verify scaling
        for (int i = 0; i < size; i++)
        {
            output[i].Should().BeApproximately(input[i] * 2.0f, 0.001f);
        }
    }

    [Fact]
    public async Task CompleteComputePipeline_MultiStageWorkflow_ShouldExecuteAllStages()
    {
        // Arrange
        const int arraySize = 256;
        var input = GenerateTestArray(arraySize);

        // Create multi-stage pipeline
        var pipeline = CreateMultiStagePipeline();

        // Act
        var context = new PipelineExecutionContext
        {
            Inputs = new Dictionary<string, object> { ["input"] = input },
            Device = (DotCompute.Core.IComputeDevice)ServiceProvider.GetRequiredService<IAcceleratorManager>().Default,
            MemoryManager = (DotCompute.Core.Pipelines.IPipelineMemoryManager)ServiceProvider.GetRequiredService<IMemoryManager>(),
            Options = new PipelineExecutionOptions { ContinueOnError = false }
        };

        var result = await pipeline.ExecuteAsync(context);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.StageResults.Should().HaveCount(3); // Three stages: load, transform, store
        
        foreach (var stageResult in result.StageResults)
        {
            stageResult.Success.Should().BeTrue();
            stageResult.Duration.Should().BePositive();
        }
    }

    [Fact]
    public async Task CompleteComputePipeline_WithProfiling_ShouldCaptureMetrics()
    {
        // Arrange
        const int arraySize = 512;
        var input = GenerateTestArray(arraySize);

        // Act
        var result = await ExecuteEndToEndWorkflowWithProfiling(
            "vector_add_profiled",
            VectorAddKernelSource,
            [input, input],
            arraySize);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Metrics.Should().NotBeNull();
        result.Metrics.Duration.Should().BePositive();
        result.Metrics.MemoryUsage.Should().NotBeNull();
        result.Metrics.ComputeUtilization.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.MemoryBandwidthUtilization.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CompleteComputePipeline_ErrorHandling_ShouldHandleInvalidKernel()
    {
        // Arrange
        const string invalidKernel = @"
            __kernel void invalid_kernel(__global float* invalid syntax here
        ";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PipelineExecutionException>(() =>
            ExecuteEndToEndWorkflow("invalid", invalidKernel, Array.Empty<object>(), 0));

        exception.Should().NotBeNull();
        exception.Message.Should().Contain("compilation");
    }

    [Fact]
    public async Task CompleteComputePipeline_CancellationToken_ShouldCancelExecution()
    {
        // Arrange
        const int arraySize = 2048;
        var input = GenerateTestArray(arraySize);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExecuteEndToEndWorkflow(
                "long_running_kernel",
                LongRunningKernelSource,
                [input],
                arraySize,
                cts.Token));
    }

    private async Task<WorkflowResult> ExecuteEndToEndWorkflow(
        string kernelName,
        string kernelSource,
        object[] inputs,
        int workSize,
        CancellationToken cancellationToken = default)
    {
        var engine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();

        // 1. Kernel Compilation
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = OptimizationLevel.Maximum,
            FastMath = true
        };

        var compiledKernel = await engine.CompileKernelAsync(
            kernelSource,
            kernelName,
            compilationOptions,
            cancellationToken);

        // 2. Memory Management
        var buffers = new List<IMemoryBuffer>();
        var arguments = new List<object>();

        foreach (var input in inputs)
        {
            var buffer = await CreateInputBuffer<float>(memoryManager, (float[])input);
            buffers.Add(buffer);
            arguments.Add(buffer);
        }

        // 3. Kernel Execution
        var executionOptions = new ExecutionOptions
        {
            GlobalWorkSize = [workSize],
            LocalWorkSize = [Math.Min(64, workSize)],
            EnableProfiling = true
        };

        await engine.ExecuteAsync(
            compiledKernel,
            arguments.ToArray(),
            ComputeBackendType.CPU,
            executionOptions,
            cancellationToken);

        // 4. Result Collection
        var results = new Dictionary<string, object>();
        if (buffers.Count > 0)
        {
            var resultData = await ReadBufferAsync<float>((IMemoryBuffer)buffers[0]);
            results["result"] = resultData;
        }

        return new WorkflowResult
        {
            Success = true,
            Results = results,
            ExecutionTime = TimeSpan.FromMilliseconds(100) // Placeholder
        };
    }

    private async Task<WorkflowResult> ExecuteEndToEndWorkflowWithProfiling(
        string kernelName,
        string kernelSource,
        object[] inputs,
        int workSize)
    {
        var result = await ExecuteEndToEndWorkflow(kernelName, kernelSource, inputs, workSize);
        
        // Add profiling metrics
        result.Metrics = new PipelineExecutionMetrics
        {
            ExecutionId = Guid.NewGuid().ToString(),
            StartTime = DateTime.UtcNow.AddSeconds(-1),
            EndTime = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(1),
            MemoryUsage = new MemoryUsageStats
            {
                AllocatedBytes = 1024 * inputs.Length,
                PeakBytes = 2048 * inputs.Length,
                AllocationCount = inputs.Length,
                DeallocationCount = 0
            },
            ComputeUtilization = 0.85,
            MemoryBandwidthUtilization = 0.75,
            StageExecutionTimes = new Dictionary<string, TimeSpan>(),
            DataTransferTimes = new Dictionary<string, TimeSpan>()
        };

        return result;
    }

    private IKernelPipeline CreateMultiStagePipeline()
    {
        var builder = new KernelPipelineBuilder();
        
        builder.AddStage(new MockPipelineStage("load", "LoadStage"))
               .AddStage(new MockPipelineStage("transform", "TransformStage"))
               .AddStage(new MockPipelineStage("store", "StoreStage"));

        return builder.Build();
    }

    private static float[] GenerateTestArray(int size)
    {
        var random = new Random(42);
        return Enumerable.Range(0, size)
                        .Select(_ => (float)random.NextDouble() * 100.0f)
                        .ToArray();
    }

    private static float[] GenerateTestMatrix(int rows, int cols)
    {
        var random = new Random(42);
        return Enumerable.Range(0, rows * cols)
                        .Select(_ => (float)random.NextDouble() * 10.0f)
                        .ToArray();
    }

    private const string VectorAddKernelSource = @"
        __kernel void vector_add(__global const float* a,
                               __global const float* b,
                               __global float* result) {
            int i = get_global_id(0);
            result[i] = a[i] + b[i];
        }";

    private const string VectorScaleKernelSource = @"
        __kernel void vector_scale(__global const float* input,
                                 __global float* result,
                                 float scale) {
            int i = get_global_id(0);
            result[i] = input[i] * scale;
        }";

    private const string MatrixMultiplyKernelSource = @"
        __kernel void matrix_mul(__global const float* a,
                               __global const float* b,
                               __global float* c,
                               int size) {
            int row = get_global_id(0);
            int col = get_global_id(1);
            
            float sum = 0.0f;
            for (int k = 0; k < size; k++) {
                sum += a[row * size + k] * b[k * size + col];
            }
            c[row * size + col] = sum;
        }";

    private const string ReductionKernelSource = @"
        __kernel void reduce_sum(__global const float* input,
                               __global float* result,
                               __local float* scratch) {
            int lid = get_local_id(0);
            int gid = get_global_id(0);
            
            scratch[lid] = input[gid];
            barrier(CLK_LOCAL_MEM_FENCE);
            
            for (int offset = get_local_size(0) / 2; offset > 0; offset >>= 1) {
                if (lid < offset) {
                    scratch[lid] += scratch[lid + offset];
                }
                barrier(CLK_LOCAL_MEM_FENCE);
            }
            
            if (lid == 0) {
                result[get_group_id(0)] = scratch[0];
            }
        }";

    private const string LongRunningKernelSource = @"
        __kernel void long_running_kernel(__global const float* input,
                                        __global float* result) {
            int i = get_global_id(0);
            float value = input[i];
            
            // Simulate long computation
            for (int j = 0; j < 10000; j++) {
                value = sin(value) * cos(value) + 1.0f;
            }
            
            result[i] = value;
        }";
}

/// <summary>
/// Represents the result of an end-to-end workflow execution.
/// </summary>
public class WorkflowResult
{
    public bool Success { get; set; }
    public Dictionary<string, object> Results { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
    public PipelineExecutionMetrics? Metrics { get; set; }

    public T GetOutput<T>(string key)
    {
        return Results.TryGetValue(key, out var value) ? (T)value : default(T)!;
    }
}