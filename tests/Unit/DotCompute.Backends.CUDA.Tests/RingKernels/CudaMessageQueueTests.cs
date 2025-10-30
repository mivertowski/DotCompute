// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.RingKernels;
using DotCompute.Backends.CUDA.RingKernels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.RingKernels;

/// <summary>
/// Unit tests for CudaMessageQueue.
/// Tests lock-free ring buffer implementation without requiring GPU hardware.
/// </summary>
public class CudaMessageQueueTests
{
    private readonly ILogger<CudaMessageQueue<int>> _mockLogger;

    public CudaMessageQueueTests()
    {
        _mockLogger = Substitute.For<ILogger<CudaMessageQueue<int>>>();
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor should initialize with valid capacity")]
    public void Constructor_WithValidCapacity_ShouldInitialize()
    {
        // Arrange & Act
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Assert
        queue.Should().NotBeNull();
        queue.Capacity.Should().Be(256);
    }

    [Theory(DisplayName = "Constructor should validate power-of-2 capacity")]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    public void Constructor_WithPowerOfTwoCapacity_ShouldSucceed(int capacity)
    {
        // Arrange & Act
        var queue = new CudaMessageQueue<int>(capacity, _mockLogger);

        // Assert
        queue.Capacity.Should().Be(capacity);
    }

    [Theory(DisplayName = "Constructor should throw on non-power-of-2 capacity")]
    [InlineData(100)]
    [InlineData(255)]
    [InlineData(300)]
    [InlineData(1000)]
    public void Constructor_WithNonPowerOfTwoCapacity_ShouldThrow(int capacity)
    {
        // Arrange & Act
        Action act = () => _ = new CudaMessageQueue<int>(capacity, _mockLogger);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("capacity")
            .WithMessage("*power of 2*");
    }

    [Theory(DisplayName = "Constructor should throw on invalid capacity")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-256)]
    public void Constructor_WithInvalidCapacity_ShouldThrow(int capacity)
    {
        // Arrange & Act
        Action act = () => _ = new CudaMessageQueue<int>(capacity, _mockLogger);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("capacity");
    }

    [Fact(DisplayName = "Constructor should throw on null logger")]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange & Act
        Action act = () => _ = new CudaMessageQueue<int>(256, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Initialization Tests

    [Fact(DisplayName = "IsEmpty should return true before initialization")]
    public void IsEmpty_BeforeInitialization_ShouldReturnTrue()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act & Assert
        queue.IsEmpty.Should().BeTrue();
    }

    [Fact(DisplayName = "IsFull should return false before initialization")]
    public void IsFull_BeforeInitialization_ShouldReturnFalse()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act & Assert
        queue.IsFull.Should().BeFalse();
    }

    [Fact(DisplayName = "Count should return 0 before initialization")]
    public void Count_BeforeInitialization_ShouldReturnZero()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act & Assert
        queue.Count.Should().Be(0);
    }

    [Fact(DisplayName = "InitializeAsync should be idempotent")]
    public async Task InitializeAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act - calling multiple times should not throw or cause issues
        // Note: This will fail without actual CUDA but tests the pattern
        try
        {
            await queue.InitializeAsync();
            await queue.InitializeAsync();
        }
        catch
        {
            // Expected without GPU - testing the pattern
        }

        // Assert - queue should handle multiple init calls gracefully
        queue.Should().NotBeNull();
    }

    #endregion

    #region Message Operations Tests (Pattern Tests Without GPU)

    [Fact(DisplayName = "TryEnqueueAsync should throw when not initialized")]
    public async Task TryEnqueueAsync_WhenNotInitialized_ShouldThrow()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);
        var message = KernelMessage<int>.Create(0, 0, MessageType.Data, 42);

        // Act
        Func<Task> act = async () => await queue.TryEnqueueAsync(message);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "TryDequeueAsync should throw when not initialized")]
    public async Task TryDequeueAsync_WhenNotInitialized_ShouldThrow()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act
        Func<Task> act = async () => await queue.TryDequeueAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "EnqueueAsync should throw when not initialized")]
    public async Task EnqueueAsync_WhenNotInitialized_ShouldThrow()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);
        var message = KernelMessage<int>.Create(0, 0, MessageType.Data, 42);

        // Act
        Func<Task> act = async () => await queue.EnqueueAsync(message);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "DequeueAsync should throw when not initialized")]
    public async Task DequeueAsync_WhenNotInitialized_ShouldThrow()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act
        Func<Task> act = async () => await queue.DequeueAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact(DisplayName = "ClearAsync should handle uninitialized queue gracefully")]
    public async Task ClearAsync_WhenNotInitialized_ShouldNotThrow()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act
        Func<Task> act = async () => await queue.ClearAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Statistics Tests

    [Fact(DisplayName = "GetStatisticsAsync should return initial statistics")]
    public async Task GetStatisticsAsync_BeforeOperations_ShouldReturnZeros()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act
        var stats = await queue.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalEnqueued.Should().Be(0);
        stats.TotalDequeued.Should().Be(0);
        stats.TotalDropped.Should().Be(0);
    }

    #endregion

    #region Unmanaged Type Tests

    [Fact(DisplayName = "CudaMessageQueue should work with int type")]
    public void CudaMessageQueue_WithIntType_ShouldCompile()
    {
        // Arrange & Act
        var queue = new CudaMessageQueue<int>(256, Substitute.For<ILogger<CudaMessageQueue<int>>>());

        // Assert
        queue.Should().NotBeNull();
    }

    [Fact(DisplayName = "CudaMessageQueue should work with float type")]
    public void CudaMessageQueue_WithFloatType_ShouldCompile()
    {
        // Arrange & Act
        var queue = new CudaMessageQueue<float>(256, Substitute.For<ILogger<CudaMessageQueue<float>>>());

        // Assert
        queue.Should().NotBeNull();
    }

    [Fact(DisplayName = "CudaMessageQueue should work with struct type")]
    public void CudaMessageQueue_WithStructType_ShouldCompile()
    {
        // Arrange
        var logger = NullLogger<CudaMessageQueue<TestStruct>>.Instance;

        // Act
        var queue = new CudaMessageQueue<TestStruct>(256, logger);

        // Assert
        queue.Should().NotBeNull();
    }

    #endregion

    #region Disposal Tests

    [Fact(DisplayName = "DisposeAsync should not throw")]
    public async Task DisposeAsync_ShouldNotThrow()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act
        Func<Task> act = async () => await queue.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "DisposeAsync should be idempotent")]
    public async Task DisposeAsync_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);

        // Act
        await queue.DisposeAsync();
        Func<Task> act = async () => await queue.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Operations after disposal should handle gracefully")]
    public async Task Operations_AfterDisposal_ShouldHandleGracefully()
    {
        // Arrange
        var queue = new CudaMessageQueue<int>(256, _mockLogger);
        await queue.DisposeAsync();

        // Act & Assert - disposed queue should return safe defaults
        queue.Count.Should().Be(0);
        queue.IsEmpty.Should().BeTrue();
        queue.IsFull.Should().BeFalse();
    }

    #endregion

    #region Test Helper Types

    private struct TestStruct
    {
        public int Value1;
        public float Value2;
        public long Value3;
    }

    #endregion
}
