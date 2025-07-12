using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Backends.CPU.Accelerators;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Stress tests for memory system including leak detection, high-frequency operations, and thread safety.
/// </summary>
public class MemoryStressTests : IDisposable
{
    private readonly CpuMemoryManager _memoryManager;

    public MemoryStressTests()
    {
        _memoryManager = new CpuMemoryManager();
    }

    [Fact]
    public async Task MemoryLeakDetection_ManyAllocationsAndDeallocations_NoMemoryLeaks()
    {
        // Arrange
        const int iterations = 1000;
        const long bufferSize = 1024 * 1024; // 1MB buffers
        var initialMemory = _memoryManager.TotalAllocatedBytes;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(bufferSize);
            
            // Use the buffer briefly
            var data = new byte[1024];
            await buffer.CopyFromHostAsync<byte>(data);
            await buffer.CopyToHostAsync<byte>(data);
            
            // Dispose immediately
            await buffer.DisposeAsync();
        }

        // Assert
        _memoryManager.TotalAllocatedBytes.Should().Be(initialMemory);
    }

    [Fact]
    public async Task HighFrequencyAllocations_RapidAllocateAndDispose_PerformsWell()
    {
        // Arrange
        const int iterations = 5000;
        const long bufferSize = 4096;
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(bufferSize);
            await buffer.DisposeAsync();
        }

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
        _memoryManager.TotalAllocatedBytes.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentAllocations_MultipleThreads_ThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int allocationsPerThread = 100;
        const long bufferSize = 1024;
        var tasks = new Task[threadCount];
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    var buffers = new List<IMemoryBuffer>();
                    
                    // Allocate buffers
                    for (int j = 0; j < allocationsPerThread; j++)
                    {
                        var buffer = await _memoryManager.AllocateAsync(bufferSize);
                        buffers.Add(buffer);
                    }
                    
                    // Use buffers
                    var data = new byte[bufferSize];
                    foreach (var buffer in buffers)
                    {
                        await buffer.CopyFromHostAsync<byte>(data);
                        await buffer.CopyToHostAsync<byte>(data);
                    }
                    
                    // Dispose buffers
                    foreach (var buffer in buffers)
                    {
                        await buffer.DisposeAsync();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty();
        _memoryManager.TotalAllocatedBytes.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentBufferOperations_MultipleThreadsOnSameBuffer_ThreadSafe()
    {
        // Arrange
        const int threadCount = 5;
        const int operationsPerThread = 200;
        const long bufferSize = 1024 * 1024;
        
        var buffer = await _memoryManager.AllocateAsync(bufferSize);
        var tasks = new Task[threadCount];
        var exceptions = new List<Exception>();

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    var threadData = Enumerable.Range(0, 1024)
                        .Select(j => (byte)((threadId * 1000 + j) % 256))
                        .ToArray();
                    
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        var offset = (threadId * 1024 * 100 + j * 1024) % (bufferSize - 1024);
                        
                        // Copy data to buffer
                        await buffer.CopyFromHostAsync<byte>(threadData, offset);
                        
                        // Copy data back
                        var resultData = new byte[1024];
                        await buffer.CopyToHostAsync<byte>(resultData, offset);
                        
                        // Verify data integrity
                        resultData.Should().BeEquivalentTo(threadData);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty();

        // Cleanup
        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task MemoryPressure_LargeNumberOfActiveBuffers_HandlesGracefully()
    {
        // Arrange
        const int bufferCount = 1000;
        const long bufferSize = 1024 * 1024; // 1MB each = 1GB total
        var buffers = new List<IMemoryBuffer>();

        try
        {
            // Act
            for (int i = 0; i < bufferCount; i++)
            {
                var buffer = await _memoryManager.AllocateAsync(bufferSize);
                buffers.Add(buffer);
            }

            // Assert
            buffers.Should().HaveCount(bufferCount);
            _memoryManager.TotalAllocatedBytes.Should().Be(bufferCount * bufferSize);

            // Verify all buffers are usable
            var testData = new byte[1024];
            foreach (var buffer in buffers.Take(10)) // Test first 10 to avoid excessive time
            {
                await buffer.CopyFromHostAsync<byte>(testData);
                await buffer.CopyToHostAsync<byte>(testData);
            }
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

    [Fact]
    public async Task FragmentationTest_MixedSizeAllocations_HandlesFragmentation()
    {
        // Arrange
        var buffers = new List<IMemoryBuffer>();
        var random = new Random(42); // Fixed seed for reproducibility
        
        var sizes = new long[] { 1024, 2048, 4096, 8192, 16384, 32768 };
        const int totalAllocations = 500;

        try
        {
            // Act - Allocate buffers of various sizes
            for (int i = 0; i < totalAllocations; i++)
            {
                var size = sizes[random.Next(sizes.Length)];
                var buffer = await _memoryManager.AllocateAsync(size);
                buffers.Add(buffer);
            }

            // Dispose every other buffer to create fragmentation
            for (int i = 1; i < buffers.Count; i += 2)
            {
                await buffers[i].DisposeAsync();
                buffers[i] = null;
            }

            // Allocate more buffers to test fragmentation handling
            for (int i = 0; i < 100; i++)
            {
                var size = sizes[random.Next(sizes.Length)];
                var buffer = await _memoryManager.AllocateAsync(size);
                buffers.Add(buffer);
            }

            // Assert - All operations should succeed
            var activeBuffers = buffers.Where(b => b != null).ToList();
            activeBuffers.Should().NotBeEmpty();
            
            // Test that active buffers still work
            var testData = new byte[1024];
            foreach (var buffer in activeBuffers.Take(10))
            {
                await buffer.CopyFromHostAsync<byte>(testData);
                await buffer.CopyToHostAsync<byte>(testData);
            }
        }
        finally
        {
            // Cleanup
            foreach (var buffer in buffers.Where(b => b != null))
            {
                await buffer.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ViewOperations_ManyViewsOnSameBuffer_PerformsWell()
    {
        // Arrange
        const long bufferSize = 1024 * 1024;
        const int viewCount = 1000;
        const int viewSize = 1024;
        
        var buffer = await _memoryManager.AllocateAsync(bufferSize);
        var views = new List<IMemoryBuffer>();

        try
        {
            // Act - Create many views
            for (int i = 0; i < viewCount; i++)
            {
                var offset = (i * viewSize) % (bufferSize - viewSize);
                var view = _memoryManager.CreateView(buffer, offset, viewSize);
                views.Add(view);
            }

            // Assert
            views.Should().HaveCount(viewCount);
            views.Should().AllSatisfy(v => v.SizeInBytes.Should().Be(viewSize));

            // Test view operations
            var testData = new byte[viewSize];
            foreach (var view in views.Take(10))
            {
                await view.CopyFromHostAsync<byte>(testData);
                await view.CopyToHostAsync<byte>(testData);
            }
        }
        finally
        {
            // Cleanup
            foreach (var view in views)
            {
                await view.DisposeAsync();
            }
            await buffer.DisposeAsync();
        }
    }

    [Fact]
    public async Task LongRunningOperations_ExtendedMemoryUsage_RemainsStable()
    {
        // Arrange
        const int iterations = 100;
        const int buffersPerIteration = 50;
        const long bufferSize = 1024 * 1024;
        var memorySnapshots = new List<long>();

        // Act
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            var buffers = new List<IMemoryBuffer>();
            
            // Allocate buffers
            for (int i = 0; i < buffersPerIteration; i++)
            {
                var buffer = await _memoryManager.AllocateAsync(bufferSize);
                buffers.Add(buffer);
            }

            // Use buffers
            var data = new byte[1024];
            foreach (var buffer in buffers)
            {
                await buffer.CopyFromHostAsync<byte>(data);
                await buffer.CopyToHostAsync<byte>(data);
            }

            // Dispose buffers
            foreach (var buffer in buffers)
            {
                await buffer.DisposeAsync();
            }

            // Take memory snapshot
            memorySnapshots.Add(_memoryManager.TotalAllocatedBytes);
        }

        // Assert
        memorySnapshots.Should().AllSatisfy(snapshot => snapshot.Should().Be(0));
    }

    [Fact]
    public async Task RapidViewCreationAndDisposal_PerformanceTest()
    {
        // Arrange
        const long bufferSize = 1024 * 1024;
        const int viewOperations = 10000;
        const int viewSize = 1024;
        
        var buffer = await _memoryManager.AllocateAsync(bufferSize);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Act
            for (int i = 0; i < viewOperations; i++)
            {
                var offset = (i * viewSize) % (bufferSize - viewSize);
                var view = _memoryManager.CreateView(buffer, offset, viewSize);
                await view.DisposeAsync();
            }

            stopwatch.Stop();

            // Assert
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
        }
        finally
        {
            // Cleanup
            await buffer.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}