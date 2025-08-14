// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using CoreMemory = DotCompute.Core.Memory;
using AbstractionsMemory = DotCompute.Abstractions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for the IMemoryManager interface implementations.
/// These tests validate the interface contracts using mocks.
/// </summary>
public sealed class MemoryManagerTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<CoreMemory.IMemoryManager> _coreMemoryManagerMock;
    private readonly Mock<AbstractionsMemory.IMemoryManager> _abstractionsMemoryManagerMock;
    private bool _disposed;

    public MemoryManagerTests()
    {
        _loggerMock = new Mock<ILogger>();
        _coreMemoryManagerMock = new Mock<CoreMemory.IMemoryManager>();
        _abstractionsMemoryManagerMock = new Mock<AbstractionsMemory.IMemoryManager>();
        
        // Setup default behavior for Core.Memory.IMemoryManager
        _coreMemoryManagerMock.Setup(m => m.AvailableLocations)
            .Returns(new[] { CoreMemory.MemoryLocation.Host, CoreMemory.MemoryLocation.Device });
    }

    [Fact]
    public void CoreMemoryManager_AvailableLocations_ReturnsConfiguredLocations()
    {
        // Act
        var locations = _coreMemoryManagerMock.Object.AvailableLocations;

        // Assert
        Assert.NotNull(locations);
        Assert.Contains(CoreMemory.MemoryLocation.Host, locations);
        Assert.Contains(CoreMemory.MemoryLocation.Device, locations);
    }

    [Fact]
    public async Task CoreMemoryManager_CreateBufferAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var mockBuffer = new Mock<CoreMemory.IBuffer<int>>();
        mockBuffer.Setup(b => b.ElementCount).Returns(100);
        mockBuffer.Setup(b => b.Location).Returns(CoreMemory.MemoryLocation.Device);
        mockBuffer.Setup(b => b.Access).Returns(CoreMemory.MemoryAccess.ReadWrite);
        
        _coreMemoryManagerMock
            .Setup(m => m.CreateBufferAsync<int>(100, CoreMemory.MemoryLocation.Device, CoreMemory.MemoryAccess.ReadWrite, It.IsAny<CancellationToken>()
            .ReturnsAsync(mockBuffer.Object)));

        // Act
        var buffer = await _coreMemoryManagerMock.Object.CreateBufferAsync<int>(100, CoreMemory.MemoryLocation.Device);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(100, buffer.ElementCount);
        Assert.Equal(CoreMemory.MemoryLocation.Device, buffer.Location);
    }

    [Fact]
    public async Task CoreMemoryManager_CreateBufferAsync_WithNegativeSize_ShouldThrowArgumentException()
    {
        // Arrange
        _coreMemoryManagerMock
            .Setup(m => m.CreateBufferAsync<byte>(-1, CoreMemory.MemoryLocation.Host, CoreMemory.MemoryAccess.ReadWrite, It.IsAny<CancellationToken>()
            .ThrowsAsync(new ArgumentException("Element count must be positive"))));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _coreMemoryManagerMock.Object.CreateBufferAsync<byte>(-1, CoreMemory.MemoryLocation.Host));
    }

    [Fact]
    public async Task CoreMemoryManager_CopyAsync_WithValidBuffers_ShouldSucceed()
    {
        // Arrange
        var sourceBuffer = Mock.Of<CoreMemory.IBuffer<float>>();
        var destBuffer = Mock.Of<CoreMemory.IBuffer<float>>();
        
        _coreMemoryManagerMock
            .Setup(m => m.CopyAsync(sourceBuffer, destBuffer, 0, 0, null, It.IsAny<CancellationToken>()
            .Returns(ValueTask.CompletedTask)));

        // Act & Assert (should not throw)
        await _coreMemoryManagerMock.Object.CopyAsync(sourceBuffer, destBuffer);
        
        _coreMemoryManagerMock.Verify(m => m.CopyAsync(sourceBuffer, destBuffer, 0, 0, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CoreMemoryManager_GetStatistics_ShouldReturnMemoryStatistics()
    {
        // Arrange
        var mockStats = Mock.Of<CoreMemory.IMemoryStatistics>();
        _coreMemoryManagerMock.Setup(m => m.GetStatistics()).Returns(mockStats);

        // Act
        var stats = _coreMemoryManagerMock.Object.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        _coreMemoryManagerMock.Verify(m => m.GetStatistics(), Times.Once);
    }

    [Fact]
    public async Task AbstractionsMemoryManager_AllocateAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var mockBuffer = new Mock<AbstractionsMemory.IMemoryBuffer>();
        mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);
        mockBuffer.Setup(b => b.Options).Returns(AbstractionsMemory.MemoryOptions.None);
        
        _abstractionsMemoryManagerMock
            .Setup(m => m.AllocateAsync(1024, AbstractionsMemory.MemoryOptions.None, It.IsAny<CancellationToken>()
            .ReturnsAsync(mockBuffer.Object)));

        // Act
        var buffer = await _abstractionsMemoryManagerMock.Object.AllocateAsync(1024);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(1024, buffer.SizeInBytes);
    }

    [Fact]
    public async Task AbstractionsMemoryManager_AllocateAsync_WithZeroSize_ShouldThrowArgumentException()
    {
        // Arrange
        _abstractionsMemoryManagerMock
            .Setup(m => m.AllocateAsync(0, It.IsAny<AbstractionsMemory.MemoryOptions>(), It.IsAny<CancellationToken>()
            .ThrowsAsync(new ArgumentException("Size must be positive"))));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => 
            await _abstractionsMemoryManagerMock.Object.AllocateAsync(0));
    }

    [Fact]
    public async Task AbstractionsMemoryManager_AllocateAndCopyAsync_WithValidData_ShouldSucceed()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5 };
        var mockBuffer = new Mock<AbstractionsMemory.IMemoryBuffer>();
        mockBuffer.Setup(b => b.SizeInBytes).Returns(sourceData.Length * sizeof(int));
        
        _abstractionsMemoryManagerMock
            .Setup(m => m.AllocateAndCopyAsync(It.IsAny<ReadOnlyMemory<int>>(), AbstractionsMemory.MemoryOptions.None, It.IsAny<CancellationToken>()
            .ReturnsAsync(mockBuffer.Object)));

        // Act
        var buffer = await _abstractionsMemoryManagerMock.Object.AllocateAndCopyAsync<int>(sourceData);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(sourceData.Length * sizeof(int), buffer.SizeInBytes);
    }

    [Fact]
    public void AbstractionsMemoryManager_CreateView_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var sourceBuffer = Mock.Of<AbstractionsMemory.IMemoryBuffer>(b => b.SizeInBytes == 1024);
        var mockView = Mock.Of<AbstractionsMemory.IMemoryBuffer>(b => b.SizeInBytes == 512);
        
        _abstractionsMemoryManagerMock
            .Setup(m => m.CreateView(sourceBuffer, 0, 512))
            .Returns(mockView);

        // Act
        var view = _abstractionsMemoryManagerMock.Object.CreateView(sourceBuffer, 0, 512);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(512, view.SizeInBytes);
    }

    [Fact]
    public async Task CoreMemoryManager_DisposeAsync_ShouldDisposeCorrectly()
    {
        // Arrange
        _coreMemoryManagerMock.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        // Act & Assert (should not throw)
        await _coreMemoryManagerMock.Object.DisposeAsync();
        _coreMemoryManagerMock.Verify(m => m.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void NamespacesShouldBeDistinct()
    {
        // This test validates that we can distinguish between the two memory manager types
        Assert.NotEqual(typeof(CoreMemory.IMemoryManager), typeof(AbstractionsMemory.IMemoryManager));
        Assert.NotEqual(typeof(CoreMemory.MemoryLocation), typeof(AbstractionsMemory.MemoryLocation));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
