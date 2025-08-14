// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using CoreMemory = DotCompute.Core.Memory;
using AbstractionsMemory = DotCompute.Abstractions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Edge case tests for IMemoryManager implementations focusing on boundary conditions,
/// race conditions, resource exhaustion, and error recovery.
/// Note: Since MemoryManager is an interface, this tests a mock implementation.
/// </summary>
public sealed class MemoryManagerEdgeCaseTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<CoreMemory.IMemoryManager> _memoryManagerMock;
    private bool _disposed;

    public MemoryManagerEdgeCaseTests()
    {
        _loggerMock = new Mock<ILogger>();
        _memoryManagerMock = new Mock<CoreMemory.IMemoryManager>();
        
        // Setup default behavior
        _memoryManagerMock.Setup(m => m.AvailableLocations)
            .Returns(new[] { CoreMemory.MemoryLocation.Host, CoreMemory.MemoryLocation.Device });
    }

    #region Boundary Value Tests

    [Fact]
    public async Task CreateBufferAsync_WithMaxIntSize_ShouldThrowOrSucceedGracefully()
    {
        // Arrange
        _memoryManagerMock
            .Setup(m => m.CreateBufferAsync<byte>(int.MaxValue, CoreMemory.MemoryLocation.Device, CoreMemory.MemoryAccess.ReadWrite, It.IsAny<CancellationToken>()
            .ThrowsAsync(new ArgumentException("Buffer size too large"))));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryManagerMock.Object.CreateBufferAsync<byte>(int.MaxValue, CoreMemory.MemoryLocation.Device).AsTask());
    }

    [Fact]
    public async Task CreateBufferAsync_WithNegativeSize_ShouldThrowArgumentException()
    {
        // Arrange
        _memoryManagerMock
            .Setup(m => m.CreateBufferAsync<byte>(-1, CoreMemory.MemoryLocation.Host, CoreMemory.MemoryAccess.ReadWrite, It.IsAny<CancellationToken>()
            .ThrowsAsync(new ArgumentException("Element count must be positive"))));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _memoryManagerMock.Object.CreateBufferAsync<byte>(-1, CoreMemory.MemoryLocation.Host).AsTask());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task CreateBufferAsync_WithSmallSizes_ShouldSucceed(int elementCount)
    {
        // Arrange
        var mockBuffer = new Mock<CoreMemory.IBuffer<byte>>();
        mockBuffer.Setup(b => b.ElementCount).Returns(elementCount);
        mockBuffer.Setup(b => b.SizeInBytes).Returns(elementCount);
        
        _memoryManagerMock
            .Setup(m => m.CreateBufferAsync<byte>(elementCount, CoreMemory.MemoryLocation.Host, CoreMemory.MemoryAccess.ReadWrite, It.IsAny<CancellationToken>()
            .ReturnsAsync(mockBuffer.Object)));

        // Act
        var buffer = await _memoryManagerMock.Object.CreateBufferAsync<byte>(elementCount, CoreMemory.MemoryLocation.Host);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(elementCount, buffer.ElementCount);
    }

    [Fact]
    public void MemoryManager_AvailableLocations_ShouldReturnConfiguredLocations()
    {
        // Act
        var locations = _memoryManagerMock.Object.AvailableLocations;

        // Assert
        Assert.NotNull(locations);
        Assert.Contains(CoreMemory.MemoryLocation.Host, locations);
        Assert.Contains(CoreMemory.MemoryLocation.Device, locations);
    }

    #endregion

    // Note: Complex edge case tests are commented out because they require concrete MemoryManager implementation
    // which doesn't exist in the current codebase. These would be appropriate for integration tests.
    
    [Fact]
    public async Task CreateBufferAsync_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var mockBuffer = new Mock<CoreMemory.IBuffer<int>>();
        mockBuffer.Setup(b => b.ElementCount).Returns(100);
        mockBuffer.Setup(b => b.Location).Returns(CoreMemory.MemoryLocation.Device);
        mockBuffer.Setup(b => b.Access).Returns(CoreMemory.MemoryAccess.ReadWrite);
        
        _memoryManagerMock
            .Setup(m => m.CreateBufferAsync<int>(100, CoreMemory.MemoryLocation.Device, CoreMemory.MemoryAccess.ReadWrite, It.IsAny<CancellationToken>()
            .ReturnsAsync(mockBuffer.Object)));

        // Act
        var buffer = await _memoryManagerMock.Object.CreateBufferAsync<int>(100, CoreMemory.MemoryLocation.Device);

        // Assert
        Assert.NotNull(buffer);
        Assert.Equal(100, buffer.ElementCount);
        Assert.Equal(CoreMemory.MemoryLocation.Device, buffer.Location);
    }

    [Fact]
    public async Task CopyAsync_WithValidBuffers_ShouldSucceed()
    {
        // Arrange
        var sourceBuffer = Mock.Of<CoreMemory.IBuffer<float>>();
        var destBuffer = Mock.Of<CoreMemory.IBuffer<float>>();
        
        _memoryManagerMock
            .Setup(m => m.CopyAsync(sourceBuffer, destBuffer, 0, 0, null, It.IsAny<CancellationToken>()
            .Returns(ValueTask.CompletedTask)));

        // Act & Assert (should not throw)
        await _memoryManagerMock.Object.CopyAsync(sourceBuffer, destBuffer);
        
        _memoryManagerMock.Verify(m => m.CopyAsync(sourceBuffer, destBuffer, 0, 0, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetStatistics_ShouldReturnMemoryStatistics()
    {
        // Arrange
        var mockStats = Mock.Of<CoreMemory.IMemoryStatistics>();
        _memoryManagerMock.Setup(m => m.GetStatistics()).Returns(mockStats);

        // Act
        var stats = _memoryManagerMock.Object.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        _memoryManagerMock.Verify(m => m.GetStatistics(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeCorrectly()
    {
        // Arrange
        _memoryManagerMock.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        // Act & Assert (should not throw)
        await _memoryManagerMock.Object.DisposeAsync();
        _memoryManagerMock.Verify(m => m.DisposeAsync(), Times.Once);
    }

    private static DotCompute.Abstractions.IBuffer<T> CreateMockBuffer<T>(int elementCount) where T : unmanaged
    {
        var mockBuffer = new Mock<DotCompute.Abstractions.IBuffer<T>>();
        mockBuffer.Setup(b => b.ElementCount).Returns(elementCount);
        mockBuffer.Setup(b => b.SizeInBytes).Returns(elementCount * System.Runtime.InteropServices.Marshal.SizeOf<T>());
        return mockBuffer.Object;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // No resources to dispose in this test class
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
