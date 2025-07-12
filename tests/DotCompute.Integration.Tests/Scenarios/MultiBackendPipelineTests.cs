// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Compute;
using DotCompute.Core.Memory;
using DotCompute.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DotCompute.Integration.Tests.Scenarios;

[Collection(nameof(IntegrationTestCollection))]
public class MultiBackendPipelineTests
{
    private readonly IntegrationTestFixture _fixture;

    public MultiBackendPipelineTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CPU_to_CUDA_Pipeline_Should_Transfer_Data_Correctly()
    {
        // Skip if CUDA not available
        if (!_fixture.IsCudaAvailable)
        {
            _fixture.Logger.LogInformation("CUDA not available, skipping CUDA pipeline test");
            return;
        }

        // Arrange
        var inputData = Enumerable.Range(1, 1000).Select(x => (float)x).ToArray();
        
        // Step 1: Process on CPU (preprocessing)
        const string cpuKernel = @"
            kernel void preprocess(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = input[i] * 2.0f; // Simple preprocessing
                }
            }";

        // Step 2: Process on CUDA (main computation)
        const string cudaKernel = @"
            kernel void compute(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = sqrt(input[i]) + 1.0f; // Main computation
                }
            }";

        // Act
        // Create initial buffer on CPU
        using var inputBuffer = await _fixture.CreateBufferAsync(inputData, MemoryLocation.Host);
        using var intermediateBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(inputData.Length, MemoryLocation.Host);
        using var outputBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(inputData.Length, MemoryLocation.Device);

        // Execute CPU preprocessing
        var cpuKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(cpuKernel);
        await _fixture.ComputeEngine.ExecuteAsync(cpuKernelCompiled, 
            new object[] { inputBuffer, intermediateBuffer, inputData.Length }, 
            ComputeBackendType.CPU);

        // Transfer to GPU memory
        await _fixture.MemoryManager.CopyAsync(intermediateBuffer, outputBuffer);

        // Execute CUDA computation
        var cudaKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(cudaKernel);
        using var finalBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(inputData.Length, MemoryLocation.Device);
        
        await _fixture.ComputeEngine.ExecuteAsync(cudaKernelCompiled, 
            new object[] { outputBuffer, finalBuffer, inputData.Length }, 
            ComputeBackendType.CUDA);

        // Transfer back to CPU for verification
        using var resultBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(inputData.Length, MemoryLocation.Host);
        await _fixture.MemoryManager.CopyAsync(finalBuffer, resultBuffer);

        var result = await resultBuffer.ReadAsync();

        // Assert
        result.Should().HaveCount(inputData.Length);
        for (int i = 0; i < inputData.Length; i++)
        {
            var expected = MathF.Sqrt(inputData[i] * 2.0f) + 1.0f;
            result[i].Should().BeApproximately(expected, 0.001f, 
                $"Element at index {i} should be processed correctly through CPU->CUDA pipeline");
        }
    }

    [Fact]
    public async Task Multi_Backend_Performance_Comparison_Should_Show_Consistent_Results()
    {
        // Arrange
        var size = 100000;
        var inputData = Enumerable.Range(1, size).Select(x => (float)x / 1000.0f).ToArray();
        
        const string kernel = @"
            kernel void complex_computation(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    float x = input[i];
                    // Complex computation that benefits from parallelization
                    output[i] = sin(x) * cos(x) + sqrt(x * x + 1.0f) - log(x + 1.0f);
                }
            }";

        // Act - Execute on CPU
        using var cpuInputBuffer = await _fixture.CreateBufferAsync(inputData, MemoryLocation.Host);
        using var cpuOutputBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(size, MemoryLocation.Host);

        var kernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(kernel);
        
        var cpuStartTime = DateTime.UtcNow;
        await _fixture.ComputeEngine.ExecuteAsync(kernelCompiled, 
            new object[] { cpuInputBuffer, cpuOutputBuffer, size }, 
            ComputeBackendType.CPU);
        var cpuEndTime = DateTime.UtcNow;
        var cpuResult = await cpuOutputBuffer.ReadAsync();

        float[]? cudaResult = null;
        TimeSpan cudaExecutionTime = TimeSpan.Zero;

        // Execute on CUDA if available
        if (_fixture.IsCudaAvailable)
        {
            using var cudaInputBuffer = await _fixture.CreateBufferAsync(inputData, MemoryLocation.Device);
            using var cudaOutputBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(size, MemoryLocation.Device);
            using var cudaResultBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(size, MemoryLocation.Host);

            var cudaStartTime = DateTime.UtcNow;
            await _fixture.ComputeEngine.ExecuteAsync(kernelCompiled, 
                new object[] { cudaInputBuffer, cudaOutputBuffer, size }, 
                ComputeBackendType.CUDA);
            await _fixture.MemoryManager.CopyAsync(cudaOutputBuffer, cudaResultBuffer);
            var cudaEndTime = DateTime.UtcNow;
            
            cudaExecutionTime = cudaEndTime - cudaStartTime;
            cudaResult = await cudaResultBuffer.ReadAsync();
        }

        // Assert
        var cpuExecutionTime = cpuEndTime - cpuStartTime;
        _fixture.Logger.LogInformation($"CPU execution time: {cpuExecutionTime.TotalMilliseconds}ms");
        
        if (cudaResult != null)
        {
            _fixture.Logger.LogInformation($"CUDA execution time: {cudaExecutionTime.TotalMilliseconds}ms");
            
            // Results should be numerically equivalent
            cudaResult.Should().HaveCount(cpuResult.Length);
            for (int i = 0; i < Math.Min(1000, cpuResult.Length); i++) // Sample first 1000 elements
            {
                cudaResult[i].Should().BeApproximately(cpuResult[i], 0.01f, 
                    $"CUDA and CPU results should match at index {i}");
            }
        }

        cpuResult.Should().NotBeNull();
        cpuResult.Should().HaveCount(size);
    }

    [Fact]
    public async Task Memory_Transfer_Patterns_Should_Be_Optimized()
    {
        if (!_fixture.IsCudaAvailable)
        {
            _fixture.Logger.LogInformation("CUDA not available, skipping memory transfer test");
            return;
        }

        // Test different memory transfer patterns for optimization
        var sizes = new[] { 1000, 10000, 100000 };
        var results = new List<(int Size, TimeSpan DirectTransfer, TimeSpan PinnedTransfer)>();

        foreach (var size in sizes)
        {
            var data = Enumerable.Range(1, size).Select(x => (float)x).ToArray();

            // Direct transfer (Host -> Device)
            var directStart = DateTime.UtcNow;
            using var hostBuffer = await _fixture.CreateBufferAsync(data, MemoryLocation.Host);
            using var deviceBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(size, MemoryLocation.Device);
            await _fixture.MemoryManager.CopyAsync(hostBuffer, deviceBuffer);
            var directEnd = DateTime.UtcNow;

            // Pinned memory transfer (if supported)
            var pinnedStart = DateTime.UtcNow;
            using var pinnedBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(size, MemoryLocation.HostPinned);
            await pinnedBuffer.WriteAsync(data);
            using var deviceBuffer2 = await _fixture.MemoryManager.CreateBufferAsync<float>(size, MemoryLocation.Device);
            await _fixture.MemoryManager.CopyAsync(pinnedBuffer, deviceBuffer2);
            var pinnedEnd = DateTime.UtcNow;

            results.Add((size, directEnd - directStart, pinnedEnd - pinnedStart));
        }

        // Assert that memory transfers complete successfully
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r =>
        {
            r.DirectTransfer.Should().BePositive();
            r.PinnedTransfer.Should().BePositive();
        });

        // Log performance characteristics
        foreach (var (size, direct, pinned) in results)
        {
            _fixture.Logger.LogInformation(
                $"Size: {size}, Direct: {direct.TotalMilliseconds:F2}ms, Pinned: {pinned.TotalMilliseconds:F2}ms");
        }
    }
}