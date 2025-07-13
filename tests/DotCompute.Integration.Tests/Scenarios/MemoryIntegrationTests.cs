// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Integration.Tests.Fixtures;
using DotCompute.Integration.Tests.Helpers;
using DotCompute.Memory;
using FluentAssertions;
using Xunit;

namespace DotCompute.Integration.Tests.Scenarios;

[Collection(nameof(IntegrationTestCollection))]
public class MemoryIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public MemoryIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UnifiedBufferShould_Handle_Basic_Operations()
    {
        // Arrange
        var testData = TestDataGenerator.GenerateRandomFloats(1000);

        // Act - Create buffer and write data
        using var buffer = await _fixture.CreateBufferAsync(testData);

        // Verify buffer was created
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(testData.Length);

        // Read data back
        var readData = await buffer.ReadAsync();

        // Assert
        readData.Should().HaveCount(testData.Length);
        for (int i = 0; i < testData.Length; i++)
        {
            readData[i].Should().BeApproximately(testData[i], 0.001f);
        }
    }

    [Fact]
    public async Task MemoryManager_Should_Handle_Multiple_Buffer_Types()
    {
        // Test different data types
        var floatData = TestDataGenerator.GenerateRandomFloats(100);
        var intData = TestDataGenerator.GenerateRandomInts(100);
        
        using var floatBuffer = await _fixture.CreateBufferAsync(floatData);
        using var intBuffer = await _fixture.CreateBufferAsync(intData);

        // Verify both buffers work correctly
        var readFloats = await floatBuffer.ReadAsync();
        var readInts = await intBuffer.ReadAsync();

        readFloats.Should().HaveCount(floatData.Length);
        readInts.Should().HaveCount(intData.Length);

        for (int i = 0; i < floatData.Length; i++)
        {
            readFloats[i].Should().BeApproximately(floatData[i], 0.001f);
        }

        for (int i = 0; i < intData.Length; i++)
        {
            readInts[i].Should().Be(intData[i]);
        }
    }

    [Fact]
    public async Task MemoryManager_Should_Handle_Large_Allocations()
    {
        // Test with larger data sets to verify memory management
        const int largeSize = 1_000_000; // 1M elements
        var largeData = TestDataGenerator.GenerateSequentialFloats(largeSize);

        using var buffer = await _fixture.CreateBufferAsync(largeData);
        
        // Verify basic properties
        buffer.Length.Should().Be(largeSize);
        
        // Sample verification (don't read all data to avoid memory issues)
        var sampleIndices = new[] { 0, largeSize / 4, largeSize / 2, 3 * largeSize / 4, largeSize - 1 };
        var partialData = new float[sampleIndices.Length];
        
        // Read specific elements
        var allData = await buffer.ReadAsync();
        for (int i = 0; i < sampleIndices.Length; i++)
        {
            allData[sampleIndices[i]].Should().BeApproximately(largeData[sampleIndices[i]], 0.001f);
        }
    }

    [Fact]
    public async Task MemoryManager_Should_Provide_Statistics()
    {
        // Arrange - Create several buffers
        var buffers = new List<UnifiedBuffer<float>>();
        try
        {
            for (int i = 0; i < 10; i++)
            {
                var data = TestDataGenerator.GenerateRandomFloats(100 * (i + 1));
                var buffer = await _fixture.CreateBufferAsync(data);
                buffers.Add(buffer);
            }

            // Act - Get memory statistics
            var stats = _fixture.MemoryManager.GetStats();

            // Assert
            stats.ActiveUnifiedBuffers.Should().BeGreaterThan(0);
            stats.TotalAllocatedBytes.Should().BeGreaterThan(0);
            
            _fixture.Logger.LogInformation($"Memory Stats: {stats.ActiveUnifiedBuffers} buffers, " +
                                         $"{stats.TotalAllocatedBytes} bytes allocated");
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    public async Task MemoryManager_Should_Handle_Concurrent_Operations()
    {
        // Test concurrent buffer creation and operations
        const int concurrentCount = 10;
        var tasks = new List<Task>();

        for (int i = 0; i < concurrentCount; i++)
        {
            int index = i; // Capture for closure
            tasks.Add(Task.Run(async () =>
            {
                var data = TestDataGenerator.GenerateRandomFloats(1000, new Random(index));
                using var buffer = await _fixture.CreateBufferAsync(data);
                
                // Perform some operations
                var readData = await buffer.ReadAsync();
                readData.Should().HaveCount(data.Length);
                
                // Verify data integrity
                for (int j = 0; j < Math.Min(100, data.Length); j++)
                {
                    readData[j].Should().BeApproximately(data[j], 0.001f);
                }
            }));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        
        // All operations should complete without exceptions
        tasks.Should().AllSatisfy(t => t.IsCompletedSuccessfully.Should().BeTrue());
    }

    [Fact]
    public async Task MemoryManager_Should_Handle_Memory_Pressure()
    {
        // Create initial buffers
        var initialBuffers = new List<UnifiedBuffer<float>>();
        try
        {
            for (int i = 0; i < 5; i++)
            {
                var data = TestDataGenerator.GenerateRandomFloats(10000);
                var buffer = await _fixture.CreateBufferAsync(data);
                initialBuffers.Add(buffer);
            }

            var statsBefore = _fixture.MemoryManager.GetStats();

            // Simulate memory pressure
            await _fixture.MemoryManager.HandleMemoryPressureAsync(0.8); // 80% pressure

            var statsAfter = _fixture.MemoryManager.GetStats();

            // Memory pressure handling should succeed
            _fixture.Logger.LogInformation($"Memory pressure handling: " +
                                         $"Before: {statsBefore.TotalAllocatedBytes} bytes, " +
                                         $"After: {statsAfter.TotalAllocatedBytes} bytes");
        }
        finally
        {
            foreach (var buffer in initialBuffers)
            {
                buffer.Dispose();
            }
        }
    }

    [Fact]
    public async Task MemoryManager_Should_Support_Compaction()
    {
        // Create and dispose several buffers to create fragmentation
        for (int i = 0; i < 10; i++)
        {
            var data = TestDataGenerator.GenerateRandomFloats(1000);
            using var buffer = await _fixture.CreateBufferAsync(data);
            
            // Use the buffer briefly
            await buffer.ReadAsync();
        }

        var statsBefore = _fixture.MemoryManager.GetStats();

        // Compact memory
        var bytesReleased = await _fixture.MemoryManager.CompactAsync();

        var statsAfter = _fixture.MemoryManager.GetStats();

        // Compaction should succeed
        bytesReleased.Should().BeGreaterOrEqualTo(0);
        
        _fixture.Logger.LogInformation($"Compaction released {bytesReleased} bytes. " +
                                     $"Allocated before: {statsBefore.TotalAllocatedBytes}, " +
                                     $"after: {statsAfter.TotalAllocatedBytes}");
    }

    [Fact]
    public async Task MemoryPools_Should_Be_Accessible()
    {
        // Test that memory pools can be accessed for different types
        var floatPool = _fixture.MemoryManager.GetPool<float>();
        var intPool = _fixture.MemoryManager.GetPool<int>();

        floatPool.Should().NotBeNull();
        intPool.Should().NotBeNull();
        floatPool.Should().NotBeSameAs(intPool);
    }

    [Fact]
    public async Task BenchmarkShould_Run_Successfully()
    {
        // Run memory benchmarks
        var benchmarkResults = await _fixture.MemoryManager.RunBenchmarksAsync();

        benchmarkResults.Should().NotBeNull();
        
        _fixture.Logger.LogInformation($"Benchmark results: " +
                                     $"Sequential Read: {benchmarkResults.SequentialReadThroughput:F2} GB/s, " +
                                     $"Random Access: {benchmarkResults.RandomAccessLatency:F2} ns");
    }
}