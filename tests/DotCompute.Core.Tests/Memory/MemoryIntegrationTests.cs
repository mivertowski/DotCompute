// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Integration tests for memory system with CPU backend and full system scenarios.
/// </summary>
public class MemoryIntegrationTests : IDisposable
{
    private readonly CpuMemoryManager _memoryManager;
    private readonly CpuAccelerator _accelerator;

    public MemoryIntegrationTests()
    {
        _memoryManager = new CpuMemoryManager();
        _accelerator = new CpuAccelerator();
    }

    [Fact]
    public async Task EndToEndKernelExecution_WithMemoryOperations_WorksCorrectly()
    {
        // Arrange
        const int dataSize = 1024;
        var inputData = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();
        var expectedOutput = inputData.Select(x => x * 2.0f).ToArray();

        // Allocate input and output buffers
        var inputBuffer = await _memoryManager.AllocateAsync(dataSize * sizeof(float));
        var outputBuffer = await _memoryManager.AllocateAsync(dataSize * sizeof(float));

        try
        {
            // Act
            // Copy input data to device
            await inputBuffer.CopyFromHostAsync<float>(inputData);

            // Simulate kernel execution by manually performing the computation
            // (In a real scenario, this would be done by a compiled kernel)
            var tempData = new float[dataSize];
            await inputBuffer.CopyToHostAsync<float>(tempData);
            
            for (int i = 0; i < dataSize; i++)
            {
                tempData[i] *= 2.0f;
            }
            
            await outputBuffer.CopyFromHostAsync<float>(tempData);

            // Copy result back to host
            var resultData = new float[dataSize];
            await outputBuffer.CopyToHostAsync<float>(resultData);

            // Assert
            resultData.Should().BeEquivalentTo(expectedOutput);
        }
        finally
        {
            // Cleanup
            await inputBuffer.DisposeAsync();
            await outputBuffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task MultiBufferOperations_ComplexDataFlow_WorksCorrectly()
    {
        // Arrange
        const int vectorSize = 512;
        var vectorA = Enumerable.Range(0, vectorSize).Select(i => (float)i).ToArray();
        var vectorB = Enumerable.Range(0, vectorSize).Select(i => (float)(i * 2)).ToArray();
        var vectorC = Enumerable.Range(0, vectorSize).Select(i => (float)(i * 3)).ToArray();

        // Allocate buffers
        var bufferA = await _memoryManager.AllocateAsync(vectorSize * sizeof(float));
        var bufferB = await _memoryManager.AllocateAsync(vectorSize * sizeof(float));
        var bufferC = await _memoryManager.AllocateAsync(vectorSize * sizeof(float));
        var resultBuffer = await _memoryManager.AllocateAsync(vectorSize * sizeof(float));

        try
        {
            // Act
            // Copy input vectors to device
            await bufferA.CopyFromHostAsync<float>(vectorA);
            await bufferB.CopyFromHostAsync<float>(vectorB);
            await bufferC.CopyFromHostAsync<float>(vectorC);

            // Simulate complex computation: result = A + B * C
            var tempA = new float[vectorSize];
            var tempB = new float[vectorSize];
            var tempC = new float[vectorSize];
            
            await bufferA.CopyToHostAsync<float>(tempA);
            await bufferB.CopyToHostAsync<float>(tempB);
            await bufferC.CopyToHostAsync<float>(tempC);

            var expectedResult = new float[vectorSize];
            for (int i = 0; i < vectorSize; i++)
            {
                expectedResult[i] = tempA[i] + (tempB[i] * tempC[i]);
            }

            await resultBuffer.CopyFromHostAsync<float>(expectedResult);

            // Copy result back to host
            var actualResult = new float[vectorSize];
            await resultBuffer.CopyToHostAsync<float>(actualResult);

            // Assert
            actualResult.Should().BeEquivalentTo(expectedResult);
        }
        finally
        {
            // Cleanup
            await bufferA.DisposeAsync();
            await bufferB.DisposeAsync();
            await bufferC.DisposeAsync();
            await resultBuffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryViews_InKernelOperations_WorkCorrectly()
    {
        // Arrange
        const int totalSize = 2048;
        const int segmentSize = 512;
        var inputData = Enumerable.Range(0, totalSize).Select(i => (float)i).ToArray();

        var buffer = await _memoryManager.AllocateAsync(totalSize * sizeof(float));

        try
        {
            // Act
            await buffer.CopyFromHostAsync<float>(inputData);

            // Create views for different segments
            var view1 = _memoryManager.CreateView(buffer, 0, segmentSize * sizeof(float));
            var view2 = _memoryManager.CreateView(buffer, segmentSize * sizeof(float), segmentSize * sizeof(float));
            var view3 = _memoryManager.CreateView(buffer, 2 * segmentSize * sizeof(float), segmentSize * sizeof(float));
            var view4 = _memoryManager.CreateView(buffer, 3 * segmentSize * sizeof(float), segmentSize * sizeof(float));

            // Process each segment differently
            var segment1 = new float[segmentSize];
            var segment2 = new float[segmentSize];
            var segment3 = new float[segmentSize];
            var segment4 = new float[segmentSize];

            await view1.CopyToHostAsync<float>(segment1);
            await view2.CopyToHostAsync<float>(segment2);
            await view3.CopyToHostAsync<float>(segment3);
            await view4.CopyToHostAsync<float>(segment4);

            // Apply different transformations
            for (int i = 0; i < segmentSize; i++)
            {
                segment1[i] *= 2.0f;        // Double
                segment2[i] += 100.0f;      // Add constant
                segment3[i] = segment3[i] * segment3[i]; // Square
                segment4[i] = -segment4[i]; // Negate
            }

            // Copy results back
            await view1.CopyFromHostAsync<float>(segment1);
            await view2.CopyFromHostAsync<float>(segment2);
            await view3.CopyFromHostAsync<float>(segment3);
            await view4.CopyFromHostAsync<float>(segment4);

            // Get final result
            var finalResult = new float[totalSize];
            await buffer.CopyToHostAsync<float>(finalResult);

            // Assert
            // Check each segment was processed correctly
            for (int i = 0; i < segmentSize; i++)
            {
                finalResult[i].Should().Be(inputData[i] * 2.0f);
                finalResult[i + segmentSize].Should().Be(inputData[i + segmentSize] + 100.0f);
                finalResult[i + 2 * segmentSize].Should().Be(inputData[i + 2 * segmentSize] * inputData[i + 2 * segmentSize]);
                finalResult[i + 3 * segmentSize].Should().Be(-inputData[i + 3 * segmentSize]);
            }

            // Cleanup views
            await view1.DisposeAsync();
            await view2.DisposeAsync();
            await view3.DisposeAsync();
            await view4.DisposeAsync();
        }
        finally
        {
            // Cleanup
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task MemoryReuse_AcrossMultipleKernels_WorksCorrectly()
    {
        // Arrange
        const int dataSize = 1024;
        var inputData = Enumerable.Range(0, dataSize).Select(i => (float)i).ToArray();

        var buffer = await _memoryManager.AllocateAsync(dataSize * sizeof(float));

        try
        {
            // Act
            await buffer.CopyFromHostAsync<float>(inputData);

            // Simulate multiple kernel executions on same buffer
            for (int kernel = 0; kernel < 5; kernel++)
            {
                var tempData = new float[dataSize];
                await buffer.CopyToHostAsync<float>(tempData);

                // Different operations for each "kernel"
                switch (kernel)
                {
                    case 0: // Add 10
                        for (int i = 0; i < dataSize; i++) tempData[i] += 10.0f;
                        break;
                    case 1: // Multiply by 2
                        for (int i = 0; i < dataSize; i++) tempData[i] *= 2.0f;
                        break;
                    case 2: // Subtract 5
                        for (int i = 0; i < dataSize; i++) tempData[i] -= 5.0f;
                        break;
                    case 3: // Divide by 2
                        for (int i = 0; i < dataSize; i++) tempData[i] /= 2.0f;
                        break;
                    case 4: // Square root
                        for (int i = 0; i < dataSize; i++) tempData[i] = (float)Math.Sqrt(Math.Abs(tempData[i]));
                        break;
                }

                await buffer.CopyFromHostAsync<float>(tempData);
            }

            // Get final result
            var finalResult = new float[dataSize];
            await buffer.CopyToHostAsync<float>(finalResult);

            // Assert
            // Verify the chain of operations was applied correctly
            var expectedResult = new float[dataSize];
            for (int i = 0; i < dataSize; i++)
            {
                float value = (float)i;
                value += 10.0f;     // Kernel 0
                value *= 2.0f;      // Kernel 1
                value -= 5.0f;      // Kernel 2
                value /= 2.0f;      // Kernel 3
                value = (float)Math.Sqrt(Math.Abs(value)); // Kernel 4
                expectedResult[i] = value;
            }

            for (int i = 0; i < dataSize; i++)
            {
                finalResult[i].Should().BeApproximately(expectedResult[i], 0.001f);
            }
        }
        finally
        {
            // Cleanup
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task BufferPooling_HighFrequencyAllocations_OptimizesPerformance()
    {
        // Arrange
        const int iterations = 1000;
        const long bufferSize = 4096;
        var buffers = new IMemoryBuffer[iterations];

        try
        {
            // Act - Rapid allocations
            for (int i = 0; i < iterations; i++)
            {
                buffers[i] = await _memoryManager.AllocateAsync(bufferSize);
            }

            // Use each buffer
            var testData = new byte[bufferSize];
            for (int i = 0; i < iterations; i++)
            {
                await buffers[i].CopyFromHostAsync<byte>(testData);
                await buffers[i].CopyToHostAsync<byte>(testData);
            }

            // Assert
            _memoryManager.TotalAllocatedBytes.Should().Be(iterations * bufferSize);
        }
        finally
        {
            // Cleanup
            for (int i = 0; i < iterations; i++)
            {
                if (buffers[i] != null)
                {
                    await buffers[i].DisposeAsync();
                }
            }
        }
    }

    [Fact]
    public async Task MemoryCoherence_MultipleViews_MaintainsConsistency()
    {
        // Arrange
        const int bufferSize = 1024;
        var initialData = Enumerable.Range(0, bufferSize).Select(i => (byte)i).ToArray();

        var buffer = await _memoryManager.AllocateAsync(bufferSize);
        await buffer.CopyFromHostAsync<byte>(initialData);

        // Create overlapping views
        var view1 = _memoryManager.CreateView(buffer, 0, 512);
        var view2 = _memoryManager.CreateView(buffer, 256, 512);
        var view3 = _memoryManager.CreateView(buffer, 512, 512);

        try
        {
            // Act
            // Modify data through view1
            var modifiedData1 = new byte[512];
            for (int i = 0; i < 512; i++) modifiedData1[i] = (byte)(255 - i);
            await view1.CopyFromHostAsync<byte>(modifiedData1);

            // Read through view2 (should see the overlap)
            var readData2 = new byte[512];
            await view2.CopyToHostAsync<byte>(readData2);

            // Assert
            // First 256 bytes of view2 should match last 256 bytes of view1
            for (int i = 0; i < 256; i++)
            {
                readData2[i].Should().Be(modifiedData1[256 + i]);
            }

            // Cleanup views
            await view1.DisposeAsync();
            await view2.DisposeAsync();
            await view3.DisposeAsync();
        }
        finally
        {
            // Cleanup
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ErrorRecovery_AfterMemoryExhaustion_RecoversGracefully()
    {
        // Arrange
        const long largeBufferSize = 1024 * 1024 * 1024; // 1GB
        var buffers = new List<IMemoryBuffer>();

        try
        {
            // Act - Try to allocate until we hit limits
            while (buffers.Count < 100) // Safety limit
            {
                try
                {
                    var buffer = await _memoryManager.AllocateAsync(largeBufferSize);
                    buffers.Add(buffer);
                }
                catch (OutOfMemoryException)
                {
                    // Expected when we hit memory limits
                    break;
                }
                catch (NotSupportedException)
                {
                    // Expected for very large allocations
                    break;
                }
            }

            // Free some buffers
            for (int i = 0; i < Math.Min(buffers.Count, 5); i++)
            {
                await buffers[i].DisposeAsync();
            }

            // Try to allocate a smaller buffer - should succeed
            var smallBuffer = await _memoryManager.AllocateAsync(1024);
            smallBuffer.Should().NotBeNull();
            await smallBuffer.DisposeAsync();

            // Assert
            // System should remain functional after hitting limits
            _memoryManager.TotalAllocatedBytes.Should().BeGreaterThan(0);
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                try
                {
                    await buffer.DisposeAsync();
                }
                catch
                {
                    // Ignore disposal errors during cleanup
                }
            }
        }
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
        _accelerator?.Dispose();
    }
}