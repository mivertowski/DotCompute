// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for the MemoryManager class.
/// </summary>
public sealed class MemoryManagerTests : IDisposable
{
    private readonly Mock<ILogger<MemoryManager>> _loggerMock;
    private readonly MemoryManager _memoryManager;
    private bool _disposed;

    public MemoryManagerTests()
    {
        _loggerMock = new Mock<ILogger<MemoryManager>>();
        _memoryManager = new MemoryManager(_loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MemoryManager(null!));
    }

    [Fact]
    public async Task AllocateAsync_WithValidParameters_AllocatesMemory()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        const long size = 1024;

        // Act
        var buffer = await _memoryManager.AllocateAsync(accelerator, size);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(size, buffer.SizeInBytes);
    }

    [Fact]
    public async Task AllocateAsync_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _memoryManager.AllocateAsync(null!, 1024));
    }

    [Fact]
    public async Task AllocateAsync_WithZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryManager.AllocateAsync(accelerator, 0));
    }

    [Fact]
    public async Task AllocateAsync_WithNegativeSize_ThrowsArgumentException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryManager.AllocateAsync(accelerator, -1));
    }

    [Fact]
    public async Task AllocateAsync_TracksAllocations()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        const long size1 = 1024;
        const long size2 = 2048;

        // Act
        var buffer1 = await _memoryManager.AllocateAsync(accelerator, size1);
        var buffer2 = await _memoryManager.AllocateAsync(accelerator, size2);
        var stats = _memoryManager.GetMemoryStatistics();

        // Assert
        Assert.Equal(2, stats.TotalAllocations);
        Assert.Equal(size1 + size2, stats.TotalBytesAllocated);
        Assert.Equal(2, stats.ActiveAllocations);
    }

    [Fact]
    public async Task FreeAsync_WithValidBuffer_FreesMemory()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var buffer = await _memoryManager.AllocateAsync(accelerator, 1024);

        // Act
        await _memoryManager.FreeAsync(buffer);
        var stats = _memoryManager.GetMemoryStatistics();

        // Assert
        Assert.Equal(1, stats.TotalAllocations);
        Assert.Equal(1, stats.TotalDeallocations);
        Assert.Equal(0, stats.ActiveAllocations);
        Assert.Equal(1024, stats.TotalBytesFreed);
    }

    [Fact]
    public async Task FreeAsync_WithNullBuffer_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _memoryManager.FreeAsync(null!));
    }

    [Fact]
    public async Task FreeAsync_WithAlreadyFreedBuffer_ThrowsInvalidOperationException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var buffer = await _memoryManager.AllocateAsync(accelerator, 1024);
        await _memoryManager.FreeAsync(buffer);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _memoryManager.FreeAsync(buffer));
    }

    [Fact]
    public async Task TransferAsync_WithValidBuffers_TransfersData()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var sourceBuffer = await _memoryManager.AllocateAsync(accelerator, 1024);
        var destBuffer = await _memoryManager.AllocateAsync(accelerator, 1024);

        // Initialize source with test data
        var testData = new byte[1024];
        new Random(42).NextBytes(testData);
        await sourceBuffer.CopyFromHostAsync(testData.AsMemory());

        // Act
        await _memoryManager.TransferAsync(sourceBuffer, destBuffer);

        // Assert
        var resultData = new byte[1024];
        await destBuffer.CopyToHostAsync(resultData.AsMemory());
        Assert.Equal(testData, resultData);
    }

    [Fact]
    public async Task TransferAsync_WithNullSource_ThrowsArgumentNullException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var destBuffer = await _memoryManager.AllocateAsync(accelerator, 1024);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _memoryManager.TransferAsync(null!, destBuffer));
    }

    [Fact]
    public async Task TransferAsync_WithNullDestination_ThrowsArgumentNullException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var sourceBuffer = await _memoryManager.AllocateAsync(accelerator, 1024);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _memoryManager.TransferAsync(sourceBuffer, null!));
    }

    [Fact]
    public async Task TransferAsync_WithDifferentSizes_ThrowsArgumentException()
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
    public void GetMemoryStatistics_ReturnsAccurateStatistics()
    {
        // Act
        var stats = _memoryManager.GetMemoryStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.TotalAllocations);
        Assert.Equal(0, stats.TotalDeallocations);
        Assert.Equal(0, stats.ActiveAllocations);
        Assert.Equal(0, stats.TotalBytesAllocated);
        Assert.Equal(0, stats.TotalBytesFreed);
        Assert.Equal(0, stats.PeakMemoryUsage);
    }

    [Fact]
    public async Task PeakMemoryUsage_TracksCorrectly()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();

        // Act
        var buffer1 = await _memoryManager.AllocateAsync(accelerator, 1024);
        var buffer2 = await _memoryManager.AllocateAsync(accelerator, 2048);
        var peakAfterAllocation = _memoryManager.GetMemoryStatistics().PeakMemoryUsage;
        
        await _memoryManager.FreeAsync(buffer1);
        var buffer3 = await _memoryManager.AllocateAsync(accelerator, 512);
        var finalPeak = _memoryManager.GetMemoryStatistics().PeakMemoryUsage;

        // Assert
        Assert.Equal(3072, peakAfterAllocation); // 1024 + 2048
        Assert.Equal(3072, finalPeak); // Peak should remain at highest point
    }

    [Fact]
    public async Task DisposeAsync_FreesAllAllocatedMemory()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        var buffer1 = await _memoryManager.AllocateAsync(accelerator, 1024);
        var buffer2 = await _memoryManager.AllocateAsync(accelerator, 2048);

        // Act
        await _memoryManager.DisposeAsync();

        // Assert
        var stats = _memoryManager.GetMemoryStatistics();
        Assert.Equal(0, stats.ActiveAllocations);
        Assert.Equal(2, stats.TotalDeallocations);
    }

    [Fact]
    public async Task AllocateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var accelerator = CreateMockAcceleratorWithMemoryManager();
        await _memoryManager.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => 
            _memoryManager.AllocateAsync(accelerator, 1024));
    }

    private static IAccelerator CreateMockAcceleratorWithMemoryManager()
    {
        var mockBuffer = new Mock<IMemoryBuffer>();
        mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);
        mockBuffer.Setup(b => b.CopyFromHostAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mockBuffer.Setup(b => b.CopyToHostAsync(It.IsAny<Memory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        mockBuffer.Setup(b => b.DisposeAsync()).Returns(ValueTask.CompletedTask);

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
            AcceleratorType.CPU, "Test CPU", "1.0", 1024 * 1024, 1, 1000, new Version(1, 0), 1024, true));

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