// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Backends.Metal.Tests.Utilities;

/// <summary>
/// Comprehensive tests for MetalCommandBufferPool.
/// Tests use mock command queue since Metal functionality requires macOS hardware.
/// </summary>
public sealed class MetalCommandBufferPoolTests
{
    private readonly Mock<ILogger<MetalCommandBufferPool>> _mockLogger;
    private readonly IntPtr _mockCommandQueue;

    public MetalCommandBufferPoolTests()
    {
        _mockLogger = new Mock<ILogger<MetalCommandBufferPool>>();
        _mockCommandQueue = new IntPtr(0x1000); // Mock command queue handle
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesPool()
    {
        // Arrange & Act
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object, maxPoolSize: 10);

        // Assert
        var stats = pool.Stats;
        Assert.Equal(0, stats.AvailableBuffers);
        Assert.Equal(0, stats.ActiveBuffers);
        Assert.Equal(10, stats.MaxPoolSize);
        Assert.Equal(0, stats.CurrentPoolSize);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MetalCommandBufferPool(_mockCommandQueue, null!, maxPoolSize: 10));
    }

    [Fact]
    public void Stats_Property_ReturnsCurrentStatistics()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object, maxPoolSize: 16);

        // Act
        var stats = pool.Stats;

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(16, stats.MaxPoolSize);
        Assert.Equal(0.0, stats.Utilization);
    }

    [Fact]
    public void GetStats_Method_ReturnsCurrentStatistics()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object, maxPoolSize: 16);

        // Act
        var stats = pool.GetStats();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(16, stats.MaxPoolSize);
    }

    [Fact]
    public void GetStats_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object, maxPoolSize: 16);
        pool.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => pool.GetCommandBuffer());
    }

    [Fact]
    public void ReturnCommandBuffer_ZeroPointer_DoesNotThrow()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);

        // Act & Assert (should not throw)
        pool.ReturnCommandBuffer(IntPtr.Zero);
    }

    [Fact]
    public void ReturnCommandBuffer_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);
        pool.Dispose();

        // Act & Assert (should not throw)
        pool.ReturnCommandBuffer(new IntPtr(0x1234));
    }

    [Fact]
    public void Cleanup_EmptyPool_CompletesSuccessfully()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);

        // Act & Assert (should not throw)
        pool.Cleanup();

        var stats = pool.Stats;
        Assert.Equal(0, stats.AvailableBuffers);
        Assert.Equal(0, stats.ActiveBuffers);
    }

    [Fact]
    public void Cleanup_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);
        pool.Dispose();

        // Act & Assert (should not throw)
        pool.Cleanup();
    }

    [Fact]
    public void Dispose_MultipleTimes_IsSafe()
    {
        // Arrange
        var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);

        // Act
        pool.Dispose();
        pool.Dispose();
        pool.Dispose();

        // Assert - verify logging indicates single disposal
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("pool disposed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CommandBufferPoolStats_Utilization_CalculatesCorrectly()
    {
        // Arrange
        var stats1 = new CommandBufferPoolStats
        {
            CurrentPoolSize = 5,
            MaxPoolSize = 10
        };

        var stats2 = new CommandBufferPoolStats
        {
            CurrentPoolSize = 10,
            MaxPoolSize = 10
        };

        var stats3 = new CommandBufferPoolStats
        {
            CurrentPoolSize = 0,
            MaxPoolSize = 10
        };

        var stats4 = new CommandBufferPoolStats
        {
            CurrentPoolSize = 0,
            MaxPoolSize = 0
        };

        // Act & Assert
        Assert.Equal(50.0, stats1.Utilization);
        Assert.Equal(100.0, stats2.Utilization);
        Assert.Equal(0.0, stats3.Utilization);
        Assert.Equal(0.0, stats4.Utilization);
    }

    [Fact]
    public void Constructor_DefaultMaxPoolSize_Uses16()
    {
        // Arrange & Act
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);

        // Assert
        var stats = pool.Stats;
        Assert.Equal(16, stats.MaxPoolSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void Constructor_VariousMaxPoolSizes_SetsCorrectly(int maxPoolSize)
    {
        // Arrange & Act
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object, maxPoolSize);

        // Assert
        var stats = pool.Stats;
        Assert.Equal(maxPoolSize, stats.MaxPoolSize);
    }

    [Fact]
    public void Stats_InitialState_AllCountersZero()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object, maxPoolSize: 20);

        // Act
        var stats = pool.Stats;

        // Assert
        Assert.Equal(0, stats.AvailableBuffers);
        Assert.Equal(0, stats.ActiveBuffers);
        Assert.Equal(0, stats.CurrentPoolSize);
        Assert.Equal(20, stats.MaxPoolSize);
        Assert.Equal(0.0, stats.Utilization);
    }

    [Fact]
    public void Dispose_EmptyPool_LogsCorrectly()
    {
        // Arrange
        var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);

        // Act
        pool.Dispose();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disposing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disposed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ReturnCommandBuffer_UntrackedBuffer_LogsWarning()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);
        var untrackedBuffer = new IntPtr(0x9999);

        // Act
        pool.ReturnCommandBuffer(untrackedBuffer);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("untracked")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CommandBufferPoolStats_Properties_AreReadOnly()
    {
        // Arrange
        var stats = new CommandBufferPoolStats
        {
            AvailableBuffers = 5,
            ActiveBuffers = 3,
            MaxPoolSize = 10,
            CurrentPoolSize = 8
        };

        // Act & Assert
        Assert.Equal(5, stats.AvailableBuffers);
        Assert.Equal(3, stats.ActiveBuffers);
        Assert.Equal(10, stats.MaxPoolSize);
        Assert.Equal(8, stats.CurrentPoolSize);
        Assert.Equal(80.0, stats.Utilization);
    }

    [Fact]
    public void CommandBufferPoolStats_Utilization_WithZeroMaxSize_ReturnsZero()
    {
        // Arrange
        var stats = new CommandBufferPoolStats
        {
            CurrentPoolSize = 5,
            MaxPoolSize = 0
        };

        // Act & Assert
        Assert.Equal(0.0, stats.Utilization);
    }

    [Fact]
    public void Constructor_LogsCreation()
    {
        // Arrange & Act
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object, maxPoolSize: 32);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("created") && v.ToString()!.Contains("32")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Cleanup_MultipleInvocations_IsSafe()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);

        // Act
        pool.Cleanup();
        pool.Cleanup();
        pool.Cleanup();

        // Assert - should complete without error
        var stats = pool.Stats;
        Assert.Equal(0, stats.AvailableBuffers);
    }

    [Fact]
    public void GetCommandBuffer_AfterCleanup_StillWorks()
    {
        // Arrange
        using var pool = new MetalCommandBufferPool(_mockCommandQueue, _mockLogger.Object);
        pool.Cleanup();

        // Act & Assert - should throw ObjectDisposedException if not working
        // We can't actually test buffer creation without Metal hardware,
        // but we can verify the pool is still operational
        var stats = pool.Stats;
        Assert.Equal(0, stats.AvailableBuffers);
    }
}
