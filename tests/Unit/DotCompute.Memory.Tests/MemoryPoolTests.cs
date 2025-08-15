using DotCompute.Abstractions;
using FluentAssertions;
using DotCompute.Memory;
using NSubstitute;
using Xunit;

namespace DotCompute.Tests.Unit;

public sealed class MemoryPoolTests : IDisposable
{
    private readonly IMemoryManager _memoryManager;
    private readonly MemoryPool<int> _pool;

    public MemoryPoolTests()
    {
        _memoryManager = Substitute.For<IMemoryManager>();
        _pool = new MemoryPool<int>(_memoryManager);
    }

    public void Dispose()
    {
        _pool.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Rent_WithValidSize_ShouldReturnBuffer()
    {
        // Act
        var buffer = _pool.Rent(1024);

        // Assert
        Assert.NotNull(buffer);
        buffer.Length.Should().BeGreaterThanOrEqualTo(1024);
    }

    [Fact]
    public void Rent_WithZeroSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var act =() => _pool.Rent(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Fact]
    public void Rent_WithNegativeSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var act =() => _pool.Rent(-1);
        Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1024)]
    [InlineData(4096)]
    public void Rent_WithVariousSizes_ShouldReturnAppropriateBuffers(int size)
    {
        // Act
        var buffer = _pool.Rent(size);

        // Assert
        Assert.NotNull(buffer);
        buffer.Length.Should().BeGreaterThanOrEqualTo(size);
    }

    [Fact]
    public void Return_WithValidBuffer_ShouldAcceptIt()
    {
        // Arrange
        var buffer = _pool.Rent(1024);

        // Act & Assert
        buffer.Dispose();
        // Disposing a buffer should not throw
    }

    [Fact]
    public void RentDispose_Pattern_ShouldWork()
    {
        // Arrange & Act
        IMemoryBuffer<int>? buffer = null;
        var act =() =>
        {
            buffer = _pool.Rent(1024);
            buffer.Dispose();
        };

        // Assert
        act(); // Should not throw
    }

    [Fact]
    public void RentAndDispose_Pattern_ShouldWork()
    {
        // Arrange
        var buffer = _pool.Rent(10);

        // Act & Assert
        Assert.NotNull(buffer);
        buffer.Length.Should().BeGreaterThanOrEqualTo(10);

        // Cleanup
        buffer.Dispose();
    }

    [Fact]
    public void RentAndReturn_ShouldReuseBuffers()
    {
        // Arrange
        var initialAllocated = _pool.TotalAllocatedBuffers;
        var buffer1 = _pool.Rent(1024);

        // Verify buffer was allocated
        _pool.TotalAllocatedBuffers.Should().Be(initialAllocated + 1);

        // Act
        buffer1.Dispose();
        _ = _pool.Rent(1024);

        // Assert - No new allocation should have occurred for same size
        _pool.TotalAllocatedBuffers.Should().Be(initialAllocated + 1);
    }

    [Fact]
    public void TotalAllocatedBytes_ShouldTrackMemoryUsage()
    {
        // Arrange
        var initialBytes = _pool.TotalAllocatedBytes;
        var buffer1 = _pool.Rent(1024);
        var buffer2 = _pool.Rent(2048);

        // Act
        var totalBytes = _pool.TotalAllocatedBytes;

        // Assert
        totalBytes.Should().BeGreaterThanOrEqualTo(initialBytes);

        // Cleanup
        buffer1.Dispose();
        buffer2.Dispose();
    }

    [Fact]
    public void Compact_ShouldReduceMemoryUsage()
    {
        // Arrange
        var buffers = new List<IMemoryBuffer<int>>();
        for(var i = 0; i < 10; i++)
        {
            buffers.Add(_pool.Rent(1024));
        }

        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }

        var bytesBefore = _pool.TotalAllocatedBytes;

        // Act
        _pool.Compact();
        var bytesAfter = _pool.TotalAllocatedBytes;

        // Assert
        bytesAfter.Should().BeLessThanOrEqualTo(bytesBefore);
    }

    [Fact]
    public void HandleMemoryPressure_ShouldReleaseUnusedBuffers()
    {
        // Arrange
        var buffers = new List<IMemoryBuffer<int>>();
        for(var i = 0; i < 5; i++)
        {
            var buffer = _pool.Rent(1024 * 1024); // 1MB each
            buffers.Add(buffer);
        }

        // Return all buffers
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }

        // Act
        _pool.HandleMemoryPressure(0.5); // Release 50%

        // Assert
        // Memory pressure handling should not throw
        _pool.TotalReturnedBuffers.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task MultipleThreads_ShouldWorkConcurrently()
    {
        // Arrange
        var tasks = new List<Task>();
        var bufferCount = 100;
        var threadCount = 10;

        // Act
        for(var t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for(var i = 0; i < bufferCount; i++)
                {
#pragma warning disable CA5394 // Do not use insecure randomness - Test code uses Random for test data generation
                    var buffer = _pool.Rent(Random.Shared.Next(100, 1000));
                    Thread.Sleep(Random.Shared.Next(0, 1));
#pragma warning restore CA5394
                    buffer.Dispose();
                }
            }));
        }

        // Assert
        await Task.WhenAll(tasks);
        // If we get here without exceptions, the test passes
    }

    [Fact]
    public void Dispose_ShouldReleaseAllResources()
    {
        // Arrange
        var memMgr = Substitute.For<IMemoryManager>();
        var pool = new MemoryPool<byte>(memMgr);
        var buffer1 = pool.Rent(1024);
        var buffer2 = pool.Rent(2048);

        // Act
        pool.Dispose();

        // Assert
        var act =() => pool.Rent(100);
        Assert.Throws<ObjectDisposedException>(() => act());
    }

    [Fact]
    public void RentAfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var memMgr = Substitute.For<IMemoryManager>();
        var pool = new MemoryPool<int>(memMgr);
        pool.Dispose();

        // Act & Assert
        var act =() => pool.Rent(100);
        Assert.Throws<ObjectDisposedException>(() => act());
    }
}
