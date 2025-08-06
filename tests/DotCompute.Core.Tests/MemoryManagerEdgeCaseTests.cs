// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests;

/// <summary>
/// Edge case tests for MemoryManager focusing on boundary conditions,
/// race conditions, resource exhaustion, and error recovery.
/// </summary>
public sealed class MemoryManagerEdgeCaseTests : IDisposable
{
    private readonly Mock<ILogger<MemoryManager>> _loggerMock;
    private readonly MemoryManager _memoryManager;
    private bool _disposed;

    public MemoryManagerEdgeCaseTests()
    {
        _loggerMock = new Mock<ILogger<MemoryManager>>();
        _memoryManager = new MemoryManager(_loggerMock.Object);
    }

    #region Boundary Value Tests

    [Fact]
    public async Task AllocateAsync_WithMaxLongSize_ShouldThrowOrSucceedGracefully()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();

        // Act & Assert - Should either succeed or throw appropriate exception
        var act = async () => await _memoryManager.AllocateAsync(accelerator, long.MaxValue);
        
        await act.Should().ThrowAsync<Exception>()
            .Where(ex => ex is ArgumentException or OutOfMemoryException or NotSupportedException,
                "Allocation of maximum size should fail gracefully");
    }

    [Fact]
    public async Task AllocateAsync_WithMinLongSize_ShouldThrowArgumentException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryManager.AllocateAsync(accelerator, long.MinValue));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task AllocateAsync_WithVerySmallSizes_ShouldSucceed(long size)
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();

        // Act
        var buffer = await _memoryManager.AllocateAsync(accelerator, size);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(size, buffer.SizeInBytes);
    }

    [Fact]
    public async Task AllocateAsync_AtMemoryBoundaries_ShouldHandleCorrectly()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var sizes = new long[] 
        { 
            4095,    // Just under 4KB
            4096,    // Exactly 4KB
            4097,    // Just over 4KB
            65535,   // Just under 64KB  
            65536,   // Exactly 64KB
            65537    // Just over 64KB
        };

        // Act & Assert
        foreach (var size in sizes)
        {
            var buffer = await _memoryManager.AllocateAsync(accelerator, size);
            Assert.NotNull(buffer);
            Assert.Equal(size, buffer.SizeInBytes);
        }
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentAllocations_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 20;
        const int allocationsPerThread = 50;
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var exceptions = new ConcurrentBag<Exception>();
        var buffers = new ConcurrentBag<IMemoryBuffer>();

        // Act - Multiple threads allocating concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId => 
            Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < allocationsPerThread; i++)
                    {
                        var size = 1024 + (threadId * 100) + i;
                        var buffer = await _memoryManager.AllocateAsync(accelerator, size);
                        buffers.Add(buffer);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("All concurrent allocations should succeed");
        buffers.Count.Should().Be(threadCount * allocationsPerThread);
        
        var stats = _memoryManager.GetMemoryStatistics();
        stats.TotalAllocations.Should().Be(threadCount * allocationsPerThread);
        stats.ActiveAllocations.Should().Be(threadCount * allocationsPerThread);
    }

    [Fact]
    public async Task ConcurrentAllocationAndFreeing_ShouldMaintainConsistency()
    {
        // Arrange
        const int operationCount = 100;
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var exceptions = new ConcurrentBag<Exception>();
        var allocatedBuffers = new ConcurrentBag<IMemoryBuffer>();

        // Pre-allocate some buffers
        var initialBuffers = new List<IMemoryBuffer>();
        for (int i = 0; i < 50; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(accelerator, 1024);
            initialBuffers.Add(buffer);
        }

        // Act - Mixed allocation and deallocation operations
        var tasks = Enumerable.Range(0, operationCount).Select(i => 
            Task.Run(async () =>
            {
                try
                {
                    if (i % 3 == 0)
                    {
                        // Allocate
                        var buffer = await _memoryManager.AllocateAsync(accelerator, 512 + i);
                        allocatedBuffers.Add(buffer);
                    }
                    else if (i % 3 == 1 && i < initialBuffers.Count)
                    {
                        // Free a pre-allocated buffer
                        await _memoryManager.FreeAsync(initialBuffers[i]);
                    }
                    else if (i % 3 == 2)
                    {
                        // Allocate and immediately free
                        var buffer = await _memoryManager.AllocateAsync(accelerator, 256);
                        await _memoryManager.FreeAsync(buffer);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Mixed operations should not cause race conditions");
        
        var stats = _memoryManager.GetMemoryStatistics();
        stats.TotalAllocations.Should().BeGreaterThan(50); // Initial + new allocations
        stats.TotalDeallocations.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConcurrentTransfers_ShouldNotCorruptData()
    {
        // Arrange
        const int transferCount = 50;
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var exceptions = new ConcurrentBag<Exception>();

        // Create source and destination buffers
        var sourceBuffers = new List<IMemoryBuffer>();
        var destBuffers = new List<IMemoryBuffer>();
        
        for (int i = 0; i < transferCount; i++)
        {
            var source = await _memoryManager.AllocateAsync(accelerator, 1024);
            var dest = await _memoryManager.AllocateAsync(accelerator, 1024);
            
            // Initialize source with test data
            var testData = new byte[1024];
            new Random(i).NextBytes(testData);
            await source.CopyFromHostAsync(testData.AsMemory());
            
            sourceBuffers.Add(source);
            destBuffers.Add(dest);
        }

        // Act - Perform concurrent transfers
        var tasks = Enumerable.Range(0, transferCount).Select(i => 
            Task.Run(async () =>
            {
                try
                {
                    await _memoryManager.TransferAsync(sourceBuffers[i], destBuffers[i]);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("Concurrent transfers should not cause data corruption");

        // Verify data integrity
        for (int i = 0; i < transferCount; i++)
        {
            var sourceData = new byte[1024];
            var destData = new byte[1024];
            
            await sourceBuffers[i].CopyToHostAsync(sourceData.AsMemory());
            await destBuffers[i].CopyToHostAsync(destData.AsMemory());
            
            sourceData.Should().Equal(destData, "Transfer should preserve data integrity");
        }
    }

    #endregion

    #region Memory Pressure Tests

    [Fact]
    public async Task AllocateUnderMemoryPressure_ShouldHandleGracefully()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var allocations = new List<IMemoryBuffer>();
        const int largeAllocationSize = 1024 * 1024; // 1MB
        const int maxAllocations = 50; // Reasonable limit

        try
        {
            // Act - Allocate until we hit memory pressure
            for (int i = 0; i < maxAllocations; i++)
            {
                var buffer = await _memoryManager.AllocateAsync(accelerator, largeAllocationSize);
                allocations.Add(buffer);
            }

            // Should succeed up to reasonable limits
            allocations.Count.Should().BeGreaterThan(0);
        }
        catch (OutOfMemoryException)
        {
            // Expected under memory pressure
            allocations.Count.Should().BeGreaterThan(0, "Should have allocated some memory before failing");
        }
        finally
        {
            // Cleanup
            foreach (var allocation in allocations)
            {
                await _memoryManager.FreeAsync(allocation);
            }
        }
    }

    [Fact]
    public async Task MemoryFragmentation_ShouldBeHandledCorrectly()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var buffers = new List<IMemoryBuffer>();

        // Create fragmentation by allocating various sizes
        var sizes = new[] { 512, 1024, 2048, 4096, 8192 };
        
        // Act - Allocate many buffers of different sizes
        for (int cycle = 0; cycle < 20; cycle++)
        {
            foreach (var size in sizes)
            {
                var buffer = await _memoryManager.AllocateAsync(accelerator, size);
                buffers.Add(buffer);
            }
        }

        // Free every other allocation to create fragmentation
        for (int i = 1; i < buffers.Count; i += 2)
        {
            await _memoryManager.FreeAsync(buffers[i]);
        }

        // Try to allocate again - should handle fragmentation
        var newBuffer = await _memoryManager.AllocateAsync(accelerator, 1024);
        
        // Assert
        newBuffer.Should().NotBeNull("Should handle fragmented memory correctly");
        
        var stats = _memoryManager.GetMemoryStatistics();
        stats.TotalAllocations.Should().BeGreaterThan(buffers.Count);

        // Cleanup remaining buffers
        for (int i = 0; i < buffers.Count; i += 2)
        {
            if (i < buffers.Count)
                await _memoryManager.FreeAsync(buffers[i]);
        }
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public async Task FreeAsync_WithCorruptedBuffer_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var buffer = await _memoryManager.AllocateAsync(accelerator, 1024);
        
        // Simulate buffer corruption by freeing it first
        await _memoryManager.FreeAsync(buffer);

        // Act & Assert - Should throw when trying to free already freed buffer
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _memoryManager.FreeAsync(buffer));
    }

    [Fact]
    public async Task TransferAsync_WithMismatchedBufferSizes_ShouldThrowArgumentException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var sourceBuffer = await _memoryManager.AllocateAsync(accelerator, 1024);
        var destBuffer = await _memoryManager.AllocateAsync(accelerator, 2048);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryManager.TransferAsync(sourceBuffer, destBuffer));
    }

    [Fact]
    public async Task OperationsAfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        await _memoryManager.DisposeAsync();

        // Act & Assert - All operations should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(() => 
            _memoryManager.AllocateAsync(accelerator, 1024));
    }

    [Fact]
    public void GetMemoryStatistics_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _memoryManager.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => 
            _memoryManager.GetMemoryStatistics());
    }

    #endregion

    #region Peak Memory Tracking Tests

    [Fact]
    public async Task PeakMemoryUsage_WithComplexAllocationPattern_ShouldTrackCorrectly()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();

        // Act - Complex allocation pattern
        var buffer1 = await _memoryManager.AllocateAsync(accelerator, 1000);
        var buffer2 = await _memoryManager.AllocateAsync(accelerator, 2000);
        var buffer3 = await _memoryManager.AllocateAsync(accelerator, 3000);
        
        var peakAfterAllocations = _memoryManager.GetMemoryStatistics().PeakMemoryUsage;
        
        await _memoryManager.FreeAsync(buffer2); // Free middle buffer
        
        var buffer4 = await _memoryManager.AllocateAsync(accelerator, 1500);
        var buffer5 = await _memoryManager.AllocateAsync(accelerator, 4000);
        
        var finalPeak = _memoryManager.GetMemoryStatistics().PeakMemoryUsage;
        
        await _memoryManager.FreeAsync(buffer1);
        await _memoryManager.FreeAsync(buffer3);
        
        var buffer6 = await _memoryManager.AllocateAsync(accelerator, 8000);
        var ultimatePeak = _memoryManager.GetMemoryStatistics().PeakMemoryUsage;

        // Assert
        peakAfterAllocations.Should().Be(6000, "Peak after first three allocations");
        finalPeak.Should().BeGreaterOrEqualTo(peakAfterAllocations, "Peak should not decrease");
        ultimatePeak.Should().BeGreaterOrEqualTo(finalPeak, "Ultimate peak should be highest");
    }

    #endregion

    #region Statistics Accuracy Tests

    [Fact]
    public async Task MemoryStatistics_UnderComplexScenario_ShouldRemainAccurate()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        const int iterations = 100;

        // Act - Complex allocation/deallocation pattern
        var activeBuffers = new List<IMemoryBuffer>();
        
        for (int i = 0; i < iterations; i++)
        {
            // Allocate
            var buffer = await _memoryManager.AllocateAsync(accelerator, 1024 + (i * 10));
            activeBuffers.Add(buffer);

            // Periodically free some buffers
            if (i % 10 == 9 && activeBuffers.Count >= 5)
            {
                for (int j = 0; j < 5; j++)
                {
                    await _memoryManager.FreeAsync(activeBuffers[j]);
                }
                activeBuffers.RemoveRange(0, 5);
            }
        }

        var stats = _memoryManager.GetMemoryStatistics();

        // Assert
        stats.TotalAllocations.Should().Be(iterations);
        stats.ActiveAllocations.Should().Be(activeBuffers.Count);
        stats.TotalDeallocations.Should().Be(iterations - activeBuffers.Count);
        stats.TotalBytesAllocated.Should().BeGreaterThan(0);
        stats.PeakMemoryUsage.Should().BeGreaterThan(0);
    }

    #endregion

    private static IAccelerator CreateMockAcceleratorWithMemoryManager()
    {
        var mockMemoryManager = new Mock<IMemoryManager>();
        mockMemoryManager.Setup(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long size, MemoryOptions options, CancellationToken ct) =>
            {
                var buffer = new Mock<IMemoryBuffer>();
                buffer.Setup(b => b.SizeInBytes).Returns(size);
                buffer.Setup(b => b.Options).Returns(options);
                buffer.Setup(b => b.CopyFromHostAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                    .Returns(ValueTask.CompletedTask);
                buffer.Setup(b => b.CopyToHostAsync(It.IsAny<Memory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                    .Returns(ValueTask.CompletedTask);
                buffer.Setup(b => b.DisposeAsync()).Returns(ValueTask.CompletedTask);
                return buffer.Object;
            });

        var mockAccelerator = new Mock<IAccelerator>();
        mockAccelerator.Setup(a => a.Memory).Returns(mockMemoryManager.Object);
        mockAccelerator.Setup(a => a.Info).Returns(new AcceleratorInfo(
            AcceleratorType.CPU, "Test CPU", "1.0", 1024 * 1024 * 1024, 8, 2400, new Version(1, 0), 64, true));

        return mockAccelerator.Object;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _memoryManager?.Dispose();
            _disposed = true;
        }
    }
}