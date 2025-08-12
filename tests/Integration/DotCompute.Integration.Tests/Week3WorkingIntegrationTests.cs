// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Execution;
using DotCompute.Memory;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Week 3 Working Integration Tests - Focused on testable APIs with solid Week 2 foundation.
/// These tests use only the APIs that are confirmed to exist and work correctly.
/// </summary>
public class Week3WorkingIntegrationTests : IntegrationTestBase
{
    public Week3WorkingIntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Core Memory Management Integration

    [Fact]
    public async Task Integration_BasicMemoryOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        const int dataSize = 1024;
        var testData = GenerateTestFloatArray(dataSize, 1.0f, 100.0f);

        // Act - Complete memory lifecycle
        var stopwatch = Stopwatch.StartNew();
        var buffer = await CreateInputBuffer<float>(memoryManager, testData);
        var readData = await ReadBufferAsync<float>(buffer);
        await buffer.DisposeAsync();
        stopwatch.Stop();

        // Assert
        readData.Should().HaveCount(dataSize, "Read data should match input size");
        readData.Should().Equal(testData, "Data should be preserved through lifecycle");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Memory operations should be fast");

        TestOutput.WriteLine($"Memory operations completed in {stopwatch.ElapsedMilliseconds}ms for {dataSize} elements");
    }

    [Theory]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(4096)]
    public async Task Integration_BufferScaling_ShouldScaleReasonably(int bufferSize)
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var testData = GenerateTestFloatArray(bufferSize, 0.0f, 1.0f);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var buffer = await CreateInputBuffer<float>(memoryManager, testData);
        var readData = await ReadBufferAsync<float>(buffer);
        await buffer.DisposeAsync();
        stopwatch.Stop();

        // Assert
        readData.Should().HaveCount(bufferSize, "Buffer size should be preserved");
        var timePerElement = stopwatch.ElapsedTicks / (double)bufferSize;
        timePerElement.Should().BeLessThan(10000, "Time per element should scale reasonably");

        TestOutput.WriteLine($"Buffer size {bufferSize}: {stopwatch.ElapsedMilliseconds}ms ({timePerElement:F2} ticks/element)");
    }

    [Fact]
    public async Task Integration_MultipleBuffers_ShouldManageCorrectly()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        const int bufferCount = 10;
        const int bufferSize = 512;
        var buffers = new System.Collections.Generic.List<IMemoryBuffer>();

        try
        {
            // Act
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < bufferCount; i++)
            {
                var data = GenerateTestFloatArray(bufferSize, i, i + 10);
                var buffer = await CreateInputBuffer<float>(memoryManager, data);
                buffers.Add(buffer);
            }
            
            stopwatch.Stop();

            // Assert
            buffers.Should().HaveCount(bufferCount, "All buffers should be allocated");
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000, "Multiple allocations should be efficient");

            TestOutput.WriteLine($"Managed {bufferCount} buffers of size {bufferSize} in {stopwatch.ElapsedMilliseconds}ms");
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    #endregion

    #region Unified Memory Manager Integration

    [Fact]
    public Task Integration_UnifiedMemoryManager_ShouldCreateCorrectly()
    {
        // Arrange
        var baseMemoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();

        // Act & Assert
        using (var unifiedManager = new UnifiedMemoryManager(baseMemoryManager))
        {
            unifiedManager.Should().NotBeNull("UnifiedMemoryManager should be created successfully");
            unifiedManager.Should().BeAssignableTo<IUnifiedMemoryManager>("Should implement interface");
        }

        TestOutput.WriteLine("UnifiedMemoryManager creation and disposal validated successfully");
        
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Integration_UnifiedMemoryBuffer_BasicOperations()
    {
        // Arrange
        var baseMemoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        using var unifiedManager = new UnifiedMemoryManager(baseMemoryManager);
        const int testSize = 256;

        // Act
        var stopwatch = Stopwatch.StartNew();
        var buffer = await unifiedManager.CreateUnifiedBufferAsync<float>(testSize);
        var testData = GenerateTestFloatArray(testSize, 0.0f, 100.0f);
        await buffer.CopyFromAsync(testData.AsMemory());
        var readData = new float[testSize];
        await buffer.CopyToAsync(readData.AsMemory());
        await buffer.DisposeAsync();
        stopwatch.Stop();

        // Assert
        readData.Length.Should().Be(testSize, "Buffer should have correct size");
        readData.Should().Equal(testData, "Data should be preserved");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Operations should be fast");

        TestOutput.WriteLine($"UnifiedBuffer operations completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    #region Core Execution Types Integration

    [Fact]
    public async Task Integration_CompiledKernelCache_ShouldInitialize()
    {
        // Act
        await using var cache = new CompiledKernelCache();
        var stats = cache.GetStatistics();

        // Assert
        cache.Should().NotBeNull("Cache should be created");
        cache.Count.Should().Be(0, "New cache should be empty");
        cache.IsEmpty.Should().BeTrue("New cache should report empty");
        
        stats.Should().NotBeNull("Statistics should be available");
        stats.TotalKernels.Should().Be(0, "No kernels should be cached initially");
        stats.TotalAccessCount.Should().Be(0, "No accesses initially");

        TestOutput.WriteLine("CompiledKernelCache initialization validated");
    }

    [Fact]
    public async Task Integration_GlobalKernelCacheManager_ShouldWork()
    {
        // Act
        await using var manager = new GlobalKernelCacheManager();
        var cache1 = manager.GetOrCreateCache("test_kernel");
        var cache2 = manager.GetOrCreateCache("test_kernel");
        var cache3 = manager.GetOrCreateCache("other_kernel");
        var stats = manager.GetStatistics();

        // Assert
        cache1.Should().NotBeNull("First cache should be created");
        cache1.Should().BeSameAs(cache2, "Same kernel name should return same cache");
        cache3.Should().NotBeSameAs(cache1, "Different kernel should get different cache");
        
        stats.TotalCaches.Should().Be(2, "Should have 2 distinct caches");
        stats.KernelNames.Should().Contain("test_kernel");
        stats.KernelNames.Should().Contain("other_kernel");

        TestOutput.WriteLine($"GlobalKernelCacheManager: {stats.TotalCaches} caches managed");
    }

    #endregion

    #region Performance and Concurrency Integration

    [Theory]
    [InlineData(20, 3)]
    [InlineData(100, 10)]
    [InlineData(500, 25)]
    public async Task Integration_ConcurrentOperations_ShouldMaintainPerformance(int operationCount, int maxConcurrent)
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        using var semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        var tasks = new System.Collections.Generic.List<Task<bool>>();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < operationCount; i++)
        {
            tasks.Add(ExecuteBufferOperationAsync(semaphore, memoryManager, i));
        }
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        results.Should().OnlyContain(r => r, "All operations should succeed");
        var operationsPerSecond = operationCount / Math.Max(stopwatch.ElapsedMilliseconds / 1000.0, 0.001);
        operationsPerSecond.Should().BeGreaterThan(10, "Should maintain minimum throughput");

        TestOutput.WriteLine($"Completed {operationCount} concurrent operations in {stopwatch.ElapsedMilliseconds}ms ({operationsPerSecond:F2} ops/sec, max concurrent: {maxConcurrent})");
    }

    [Fact]
    public async Task Integration_StressTest_ShouldHandleResourcePressure()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        var allocatedBuffers = new System.Collections.Generic.List<IMemoryBuffer>();
        var successCount = 0;
        const int maxAttempts = 50; // Reduced for faster testing

        try
        {
            // Act - Stress test with bounded resource usage
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    var data = GenerateTestFloatArray(1024, i, i + 1); // 4KB per buffer
                    var buffer = await CreateInputBuffer<float>(memoryManager, data);
                    allocatedBuffers.Add(buffer);
                    successCount++;
                }
                catch (OutOfMemoryException)
                {
                    break; // Expected under pressure
                }
                catch (InvalidOperationException)
                {
                    break; // Also acceptable
                }
            }

            // Assert
            successCount.Should().BeGreaterThan(0, "Should allocate some buffers");

            TestOutput.WriteLine($"Stress test: Successfully allocated {successCount}/{maxAttempts} buffers");
        }
        finally
        {
            // Cleanup
            foreach (var buffer in allocatedBuffers)
            {
                try { await buffer.DisposeAsync(); } catch { /* Ignore cleanup errors */ }
            }
        }
    }

    #endregion

    #region Error Handling and Resilience

    [Fact]
    public async Task Integration_ErrorHandling_InvalidInput()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            CreateInputBuffer<float>(memoryManager, Array.Empty<float>()));

        TestOutput.WriteLine("Invalid input error handling validated");
    }

    [Fact]
    public async Task Integration_CancellationHandling_ShouldWork()
    {
        // Arrange
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        using var cts = new CancellationTokenSource();

        // Act & Assert
        var task = Task.Run(async () =>
        {
            await Task.Delay(50, cts.Token); // Will be cancelled
            var data = GenerateTestFloatArray(1024, 0, 1);
            var buffer = await CreateInputBuffer<float>(memoryManager, data);
            await buffer.DisposeAsync();
        });

        cts.CancelAfter(25); // Cancel before completion

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        TestOutput.WriteLine("Cancellation handling validated");
    }

    #endregion

    #region Real-World Integration Scenarios

    [Fact]
    public async Task Integration_DataProcessingPipeline_ShouldWork()
    {
        // Arrange - Simulate a simple data processing pipeline
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        const int dataSize = 512;
        var inputData = GenerateTestFloatArray(dataSize, -10.0f, 10.0f);

        // Act - Multi-stage processing simulation
        var stopwatch = Stopwatch.StartNew();

        // Stage 1: Load data
        var inputBuffer = await CreateInputBuffer<float>(memoryManager, inputData);
        var loadedData = await ReadBufferAsync<float>(inputBuffer);

        // Stage 2: Process data (square all values)
        var processedData = loadedData.Select(x => x * x).ToArray();
        var processBuffer = await CreateInputBuffer<float>(memoryManager, processedData);
        var processResult = await ReadBufferAsync<float>(processBuffer);

        // Stage 3: Aggregate results
        var sum = processResult.Sum();
        var average = sum / processResult.Length;

        stopwatch.Stop();

        // Assert
        processResult.Should().HaveCount(dataSize, "Processed data should maintain size");
        processResult.Should().OnlyContain(x => x >= 0, "Squared values should be non-negative");
        average.Should().BeGreaterThan(0, "Average of squared values should be positive");

        TestOutput.WriteLine($"Data processing pipeline completed in {stopwatch.ElapsedMilliseconds}ms: {dataSize} elements, avg={average:F2}");

        // Cleanup
        await inputBuffer.DisposeAsync();
        await processBuffer.DisposeAsync();
    }

    [Fact]
    public async Task Integration_MemoryEfficiency_BulkOperations()
    {
        // Arrange - Test bulk memory operations efficiency
        var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
        const int batchSize = 100;
        const int elementCount = 256;
        var totalElements = batchSize * elementCount;

        // Act - Bulk operations
        var stopwatch = Stopwatch.StartNew();
        var processedElements = 0;

        for (int batch = 0; batch < batchSize; batch++)
        {
            var data = GenerateTestFloatArray(elementCount, batch, batch + 1);
            var buffer = await CreateInputBuffer<float>(memoryManager, data);
            var readData = await ReadBufferAsync<float>(buffer);
            processedElements += readData.Length;
            await buffer.DisposeAsync();
        }

        stopwatch.Stop();

        // Assert
        processedElements.Should().Be(totalElements, "All elements should be processed");
        var elementsPerSecond = processedElements / Math.Max(stopwatch.ElapsedMilliseconds / 1000.0, 0.001);
        elementsPerSecond.Should().BeGreaterThan(1000, "Should maintain reasonable throughput");

        TestOutput.WriteLine($"Bulk operations: Processed {processedElements} elements in {stopwatch.ElapsedMilliseconds}ms ({elementsPerSecond:F0} elements/sec)");
    }

    #endregion

    #region Helper Methods

    private static float[] GenerateTestFloatArray(int size, float min = 0.0f, float max = 1.0f)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        return Enumerable.Range(0, size)
                        .Select(_ => (float)(random.NextDouble() * (max - min) + min))
                        .ToArray();
    }

    private async Task<bool> ExecuteBufferOperationAsync(SemaphoreSlim semaphore, IMemoryManager memoryManager, int operationId)
    {
        await semaphore.WaitAsync();
        try
        {
            // Simple buffer operation: allocate, write, read, dispose
            var data = GenerateTestFloatArray(64, operationId, operationId + 1);
            var buffer = await CreateInputBuffer<float>(memoryManager, data);
            var readData = await ReadBufferAsync<float>(buffer);
            await buffer.DisposeAsync();
            return readData.Length == data.Length && Math.Abs(readData[0] - operationId) < 0.1f;
        }
        catch (Exception ex)
        {
            TestOutput.WriteLine($"Operation {operationId} failed: {ex.Message}");
            return false;
        }
        finally
        {
            semaphore.Release();
        }
    }

    #endregion
}