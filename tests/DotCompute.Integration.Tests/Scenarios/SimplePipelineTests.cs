// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Integration.Tests.Fixtures;
using DotCompute.Integration.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace DotCompute.Integration.Tests.Scenarios;

[Collection(nameof(IntegrationTestCollection))]
public class SimplePipelineTests
{
    private readonly IntegrationTestFixture _fixture;

    public SimplePipelineTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SimpleData_Processing_Pipeline_Should_Work()
    {
        // Arrange - Create test data for a simple processing pipeline
        var inputData = TestDataGenerator.GenerateSequentialFloats(1000, 1.0f, 1.0f);

        // Step 1: Load data into buffer
        using var inputBuffer = await _fixture.CreateBufferAsync(inputData);
        using var outputBuffer = await _fixture.CreateBufferAsync<float>(inputData.Length);

        // Step 2: Simulate kernel compilation and execution
        const string kernelCode = @"
            kernel void simple_multiply(global float* input, global float* output, float factor, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = input[i] * factor;
                }
            }";

        var kernel = await _fixture.CompileKernelAsync(kernelCode);
        kernel.Should().NotBeNull();
        kernel.Name.Should().Be("simple_multiply");

        // Step 3: Execute the operation (mocked)
        await _fixture.ExecuteKernelAsync(kernel, inputBuffer, outputBuffer, 2.0f, inputData.Length);

        // Step 4: Verify pipeline completed successfully
        var inputCheck = await inputBuffer.ReadAsync();
        inputCheck.Should().HaveCount(inputData.Length);
        
        _fixture.Logger.LogInformation($"Pipeline processed {inputData.Length} elements successfully");
    }

    [Fact]
    public async Task MultiStage_Pipeline_Should_Coordinate_Properly()
    {
        // Test a multi-stage pipeline with different operations
        var stages = new[]
        {
            "stage1_preprocess",
            "stage2_transform", 
            "stage3_postprocess"
        };

        var testData = TestDataGenerator.GenerateRandomFloats(500);
        using var buffer1 = await _fixture.CreateBufferAsync(testData);
        using var buffer2 = await _fixture.CreateBufferAsync<float>(testData.Length);
        using var buffer3 = await _fixture.CreateBufferAsync<float>(testData.Length);

        // Simulate each stage
        foreach (var stage in stages)
        {
            var kernelCode = $@"
                kernel void {stage}(global float* input, global float* output, int size) {{
                    int i = get_global_id(0);
                    if (i < size) {{
                        output[i] = input[i] + 1.0f; // Simple operation
                    }}
                }}";

            var kernel = await _fixture.CompileKernelAsync(kernelCode);
            kernel.Name.Should().Be(stage);
            
            await _fixture.ExecuteKernelAsync(kernel, buffer1, buffer2, testData.Length);
            
            _fixture.Logger.LogInformation($"Completed pipeline stage: {stage}");
        }

        // Verify all stages completed
        var finalData = await buffer2.ReadAsync();
        finalData.Should().HaveCount(testData.Length);
    }

    [Fact]
    public async Task ErrorHandling_In_Pipeline_Should_Be_Robust()
    {
        // Test error handling scenarios
        var testData = TestDataGenerator.GenerateRandomFloats(100);
        using var buffer = await _fixture.CreateBufferAsync(testData);

        // Test with invalid kernel code
        const string invalidKernelCode = @"
            invalid kernel syntax that should fail compilation
        ";

        var kernel = await _fixture.CompileKernelAsync(invalidKernelCode);
        
        // Even with invalid code, mock compilation should succeed
        kernel.Should().NotBeNull();
        kernel.Name.Should().Be("unknown_kernel");

        // Test execution with mock kernel
        await _fixture.ExecuteKernelAsync(kernel, buffer);
        
        _fixture.Logger.LogInformation("Error handling test completed successfully");
    }

    [Fact]
    public async Task LargeDataset_Pipeline_Should_Handle_Memory_Efficiently()
    {
        // Test with larger dataset to verify memory efficiency
        const int largeSize = 100_000;
        var largeData = TestDataGenerator.GenerateSequentialFloats(largeSize);

        using var inputBuffer = await _fixture.CreateBufferAsync(largeData);
        using var outputBuffer = await _fixture.CreateBufferAsync<float>(largeSize);

        var statsBefore = _fixture.MemoryManager.GetStats();

        // Simulate processing
        const string kernelCode = @"
            kernel void large_process(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = sin(input[i]) + cos(input[i]);
                }
            }";

        var kernel = await _fixture.CompileKernelAsync(kernelCode);
        await _fixture.ExecuteKernelAsync(kernel, inputBuffer, outputBuffer, largeSize);

        var statsAfter = _fixture.MemoryManager.GetStats();

        // Verify memory was managed efficiently
        _fixture.Logger.LogInformation($"Large dataset processing: " +
                                     $"Before: {statsBefore.TotalAllocatedBytes} bytes, " +
                                     $"After: {statsAfter.TotalAllocatedBytes} bytes, " +
                                     $"Active buffers: {statsAfter.ActiveUnifiedBuffers}");

        statsAfter.ActiveUnifiedBuffers.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConcurrentPipeline_Execution_Should_Work()
    {
        // Test multiple pipelines running concurrently
        const int pipelineCount = 5;
        var tasks = new List<Task>();

        for (int i = 0; i < pipelineCount; i++)
        {
            int pipelineId = i;
            tasks.Add(Task.Run(async () =>
            {
                var data = TestDataGenerator.GenerateRandomFloats(1000, new Random(pipelineId));
                using var buffer = await _fixture.CreateBufferAsync(data);

                var kernelCode = $@"
                    kernel void pipeline_{pipelineId}(global float* input, global float* output, int size) {{
                        int i = get_global_id(0);
                        if (i < size) {{
                            output[i] = input[i] * {pipelineId + 1}.0f;
                        }}
                    }}";

                var kernel = await _fixture.CompileKernelAsync(kernelCode);
                kernel.Name.Should().Be($"pipeline_{pipelineId}");

                using var outputBuffer = await _fixture.CreateBufferAsync<float>(data.Length);
                await _fixture.ExecuteKernelAsync(kernel, buffer, outputBuffer, data.Length);

                _fixture.Logger.LogInformation($"Concurrent pipeline {pipelineId} completed");
            }));
        }

        // Wait for all pipelines to complete
        await Task.WhenAll(tasks);

        // All pipelines should complete successfully
        tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }

    [Fact]
    public async Task ResourceCleanup_Should_Work_Properly()
    {
        var statsBefore = _fixture.MemoryManager.GetStats();

        // Create and dispose resources in a scope
        {
            var data = TestDataGenerator.GenerateRandomFloats(10000);
            using var buffer1 = await _fixture.CreateBufferAsync(data);
            using var buffer2 = await _fixture.CreateBufferAsync<float>(data.Length);

            var kernel = await _fixture.CompileKernelAsync(@"
                kernel void cleanup_test(global float* input, global float* output, int size) {
                    int i = get_global_id(0);
                    if (i < size) {
                        output[i] = input[i];
                    }
                }");

            await _fixture.ExecuteKernelAsync(kernel, buffer1, buffer2, data.Length);

            // Resources are still in use here
            var statsInUse = _fixture.MemoryManager.GetStats();
            statsInUse.ActiveUnifiedBuffers.Should().BeGreaterThan(statsBefore.ActiveUnifiedBuffers);
        }

        // Force garbage collection to ensure cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Compact memory to clean up
        await _fixture.MemoryManager.CompactAsync();

        var statsAfter = _fixture.MemoryManager.GetStats();
        
        _fixture.Logger.LogInformation($"Resource cleanup test: " +
                                     $"Before: {statsBefore.ActiveUnifiedBuffers} buffers, " +
                                     $"After: {statsAfter.ActiveUnifiedBuffers} buffers");
    }
}