// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for the UnifiedMemoryManager class.
/// </summary>
public sealed class UnifiedMemoryManagerTests : IDisposable
{
    private readonly Mock<ILogger<UnifiedMemoryManager>> _loggerMock;
    private readonly Mock<IMemoryManager> _deviceMemoryManagerMock;
    private readonly UnifiedMemoryManager _unifiedMemoryManager;
    private bool _disposed;

    public UnifiedMemoryManagerTests()
    {
        _loggerMock = new Mock<ILogger<UnifiedMemoryManager>>();
        _deviceMemoryManagerMock = new Mock<IMemoryManager>();
        _unifiedMemoryManager = new UnifiedMemoryManager(_loggerMock.Object, _deviceMemoryManagerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedMemoryManager(null!, _deviceMemoryManagerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullDeviceMemoryManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedMemoryManager(_loggerMock.Object, null!));
    }

    [Fact]
    public async Task AllocateUnifiedAsync_CreatesUnifiedBuffer()
    {
        // Arrange
        const long size = 1024;
        var mockDeviceBuffer = Mock.Of<IMemoryBuffer>(b => b.SizeInBytes == size);
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeviceBuffer);

        // Act
        var buffer = await _unifiedMemoryManager.AllocateUnifiedAsync(size);

        // Assert
        Assert.NotNull(buffer);
        Assert.IsType<UnifiedBuffer>(buffer);
        Assert.Equal(size, buffer.SizeInBytes);
    }

    [Fact]
    public async Task AllocateUnifiedAsync_WithOptions_PassesOptionsToDeviceManager()
    {
        // Arrange
        const long size = 1024;
        var options = MemoryOptions.DeviceLocal | MemoryOptions.HostVisible;
        var mockDeviceBuffer = Mock.Of<IMemoryBuffer>();
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(size, options, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeviceBuffer);

        // Act
        await _unifiedMemoryManager.AllocateUnifiedAsync(size, options);

        // Assert
        _deviceMemoryManagerMock.Verify(m => 
            m.AllocateAsync(size, options, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AllocateUnifiedAsync_WithZeroSize_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _unifiedMemoryManager.AllocateUnifiedAsync(0));
    }

    [Fact]
    public async Task AllocateUnifiedAsync_WithNegativeSize_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _unifiedMemoryManager.AllocateUnifiedAsync(-1));
    }

    [Fact]
    public async Task SynchronizeAsync_WithHostToDevice_SynchronizesData()
    {
        // Arrange
        const long size = 1024;
        var testData = new byte[size];
        new Random(42).NextBytes(testData);
        
        var mockDeviceBuffer = new Mock<IMemoryBuffer>();
        mockDeviceBuffer.Setup(b => b.SizeInBytes).Returns(size);
        mockDeviceBuffer.Setup(b => b.CopyFromHostAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeviceBuffer.Object);

        var unifiedBuffer = await _unifiedMemoryManager.AllocateUnifiedAsync(size);
        
        // Write data to host memory
        await unifiedBuffer.CopyFromHostAsync(testData.AsMemory());

        // Act
        await _unifiedMemoryManager.SynchronizeAsync(unifiedBuffer, SynchronizationDirection.HostToDevice);

        // Assert
        mockDeviceBuffer.Verify(b => 
            b.CopyFromHostAsync(It.IsAny<ReadOnlyMemory<byte>>(), 0, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SynchronizeAsync_WithDeviceToHost_SynchronizesData()
    {
        // Arrange
        const long size = 1024;
        var mockDeviceBuffer = new Mock<IMemoryBuffer>();
        mockDeviceBuffer.Setup(b => b.SizeInBytes).Returns(size);
        mockDeviceBuffer.Setup(b => b.CopyToHostAsync(It.IsAny<Memory<byte>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeviceBuffer.Object);

        var unifiedBuffer = await _unifiedMemoryManager.AllocateUnifiedAsync(size);

        // Act
        await _unifiedMemoryManager.SynchronizeAsync(unifiedBuffer, SynchronizationDirection.DeviceToHost);

        // Assert
        mockDeviceBuffer.Verify(b => 
            b.CopyToHostAsync(It.IsAny<Memory<byte>>(), 0, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SynchronizeAsync_WithNullBuffer_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _unifiedMemoryManager.SynchronizeAsync(null!, SynchronizationDirection.HostToDevice));
    }

    [Fact]
    public async Task SynchronizeAsync_WithNonUnifiedBuffer_ThrowsArgumentException()
    {
        // Arrange
        var regularBuffer = Mock.Of<IMemoryBuffer>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _unifiedMemoryManager.SynchronizeAsync(regularBuffer, SynchronizationDirection.HostToDevice));
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsAccurateStatistics()
    {
        // Arrange
        const long size1 = 1024;
        const long size2 = 2048;
        
        var mockBuffer1 = Mock.Of<IMemoryBuffer>(b => b.SizeInBytes == size1);
        var mockBuffer2 = Mock.Of<IMemoryBuffer>(b => b.SizeInBytes == size2);
        
        _deviceMemoryManagerMock
            .SetupSequence(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBuffer1)
            .ReturnsAsync(mockBuffer2);

        // Act
        var buffer1 = await _unifiedMemoryManager.AllocateUnifiedAsync(size1);
        var buffer2 = await _unifiedMemoryManager.AllocateUnifiedAsync(size2);
        
        await _unifiedMemoryManager.SynchronizeAsync(buffer1, SynchronizationDirection.HostToDevice);
        await _unifiedMemoryManager.SynchronizeAsync(buffer2, SynchronizationDirection.DeviceToHost);
        
        var stats = await _unifiedMemoryManager.GetStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.TotalAllocations);
        Assert.Equal(size1 + size2, stats.TotalBytesAllocated);
        Assert.Equal(2, stats.ActiveBuffers);
        Assert.Equal(1, stats.HostToDeviceSynchronizations);
        Assert.Equal(1, stats.DeviceToHostSynchronizations);
    }

    [Fact]
    public async Task SetAutoSynchronization_EnablesAutomaticSync()
    {
        // Arrange
        const long size = 1024;
        var mockDeviceBuffer = Mock.Of<IMemoryBuffer>(b => b.SizeInBytes == size);
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeviceBuffer);

        // Act
        _unifiedMemoryManager.SetAutoSynchronization(true);
        var buffer = await _unifiedMemoryManager.AllocateUnifiedAsync(size);
        var isAutoSyncEnabled = _unifiedMemoryManager.IsAutoSynchronizationEnabled;

        // Assert
        Assert.True(isAutoSyncEnabled);
    }

    [Fact]
    public async Task DisposeAsync_DisposesAllBuffers()
    {
        // Arrange
        var mockBuffer = new Mock<IMemoryBuffer>();
        mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);
        mockBuffer.Setup(b => b.DisposeAsync()).Returns(ValueTask.CompletedTask);
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBuffer.Object);

        var unifiedBuffer = await _unifiedMemoryManager.AllocateUnifiedAsync(1024);

        // Act
        await _unifiedMemoryManager.DisposeAsync();

        // Assert
        mockBuffer.Verify(b => b.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task AllocateUnifiedAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        await _unifiedMemoryManager.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => 
            _unifiedMemoryManager.AllocateUnifiedAsync(1024));
    }

    [Fact]
    public async Task PinMemoryAsync_PinsBufferInHost()
    {
        // Arrange
        const long size = 1024;
        var mockDeviceBuffer = Mock.Of<IMemoryBuffer>(b => b.SizeInBytes == size);
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeviceBuffer);

        var unifiedBuffer = await _unifiedMemoryManager.AllocateUnifiedAsync(size);

        // Act
        var pinnedHandle = await _unifiedMemoryManager.PinMemoryAsync(unifiedBuffer);

        // Assert
        Assert.NotNull(pinnedHandle);
        Assert.True(pinnedHandle.IsPinned);
    }

    [Fact]
    public async Task UnpinMemoryAsync_UnpinsBuffer()
    {
        // Arrange
        const long size = 1024;
        var mockDeviceBuffer = Mock.Of<IMemoryBuffer>(b => b.SizeInBytes == size);
        
        _deviceMemoryManagerMock
            .Setup(m => m.AllocateAsync(size, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDeviceBuffer);

        var unifiedBuffer = await _unifiedMemoryManager.AllocateUnifiedAsync(size);
        var pinnedHandle = await _unifiedMemoryManager.PinMemoryAsync(unifiedBuffer);

        // Act
        await _unifiedMemoryManager.UnpinMemoryAsync(pinnedHandle);

        // Assert
        Assert.False(pinnedHandle.IsPinned);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _unifiedMemoryManager?.Dispose();
            _disposed = true;
        }
    }
}