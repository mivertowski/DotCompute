// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Services;
using DotCompute.Tests;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.InteropServices;
using Xunit;

namespace DotCompute.Runtime.Tests;

/// <summary>
/// Comprehensive unit tests for RuntimeMemoryManager to achieve 90%+ coverage.
/// Tests all public methods, properties, error conditions, and edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Mock)]
[Trait("Category", TestCategories.CI)]
public sealed class RuntimeMemoryManagerTests : IDisposable
{
    private readonly Mock<ILogger<RuntimeMemoryManager>> _mockLogger;
    private readonly RuntimeMemoryManager _memoryManager;

    public RuntimeMemoryManagerTests()
    {
        _mockLogger = new Mock<ILogger<RuntimeMemoryManager>>();
        _memoryManager = new RuntimeMemoryManager(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Constructor_WithValidLogger_ShouldSucceed()
    {
        // Arrange & Act
        using var manager = new RuntimeMemoryManager(_mockLogger.Object);

        // Assert
        manager.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new RuntimeMemoryManager(null!);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("logger");
    }

    #endregion

    #region AllocateAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAsync_WithValidSize_ShouldReturnBuffer()
    {
        // Arrange
        const long sizeInBytes = 1024;

        // Act
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.IsDisposed.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task AllocateAsync_WithInvalidSize_ShouldThrowArgumentOutOfRangeException(long invalidSize)
    {
        // Arrange, Act & Assert
        var act = async () => await _memoryManager.AllocateAsync(invalidSize);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAsync_WithMemoryOptions_ShouldReturnBufferWithOptions()
    {
        // Arrange
        const long sizeInBytes = 1024;
        const MemoryOptions options = MemoryOptions.ReadOnly;

        // Act
        var buffer = await _memoryManager.AllocateAsync(sizeInBytes, options);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sizeInBytes);
        buffer.Options.Should().Be(options);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAsync_MultipleCalls_ShouldReturnDifferentBuffers()
    {
        // Arrange
        const long sizeInBytes = 1024;

        // Act
        var buffer1 = await _memoryManager.AllocateAsync(sizeInBytes);
        var buffer2 = await _memoryManager.AllocateAsync(sizeInBytes);

        // Assert
        buffer1.Should().NotBeNull();
        buffer2.Should().NotBeNull();
        buffer1.Should().NotBeSameAs(buffer2);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _memoryManager.Dispose();

        // Act & Assert
        var act = async () => await _memoryManager.AllocateAsync(1024);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = async () => await _memoryManager.AllocateAsync(1024, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region AllocateAndCopyAsync Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAndCopyAsync_WithValidData_ShouldCreateBufferAndCopyData()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5 };
        var sourceMemory = new ReadOnlyMemory<int>(sourceData);

        // Act
        var buffer = await _memoryManager.AllocateAndCopyAsync(sourceMemory);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(sourceData.Length * sizeof(int));
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAndCopyAsync_WithEmptyData_ShouldCreateEmptyBuffer()
    {
        // Arrange
        var sourceData = Array.Empty<int>();
        var sourceMemory = new ReadOnlyMemory<int>(sourceData);

        // Act
        var buffer = await _memoryManager.AllocateAndCopyAsync(sourceMemory);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(0);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAndCopyAsync_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3 };
        var sourceMemory = new ReadOnlyMemory<int>(sourceData);
        _memoryManager.Dispose();

        // Act & Assert
        var act = async () => await _memoryManager.AllocateAndCopyAsync(sourceMemory);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task AllocateAndCopyAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3 };
        var sourceMemory = new ReadOnlyMemory<int>(sourceData);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = async () => await _memoryManager.AllocateAndCopyAsync(sourceMemory, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region CreateView Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CreateView_WithValidParameters_ShouldReturnView()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);
        const long offset = 100;
        const long length = 200;

        // Act
        var view = _memoryManager.CreateView(buffer, offset, length);

        // Assert
        view.Should().NotBeNull();
        view.SizeInBytes.Should().Be(length);
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CreateView_WithNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => _memoryManager.CreateView(null!, 0, 100);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("buffer");
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(-10, 100)]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task CreateView_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException(long offset, long length)
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);

        // Act & Assert
        var act = () => _memoryManager.CreateView(buffer, offset, length);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("offset");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(0, -100)]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task CreateView_WithInvalidLength_ShouldThrowArgumentOutOfRangeException(long offset, long length)
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);

        // Act & Assert
        var act = () => _memoryManager.CreateView(buffer, offset, length);
        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("length");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task CreateView_WithViewExceedingBufferBounds_ShouldThrowArgumentException()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);
        const long offset = 900;
        const long length = 200; // offset + length = 1100 > 1024

        // Act & Assert
        var act = () => _memoryManager.CreateView(buffer, offset, length);
        act.Should().Throw<ArgumentException>()
           .Which.Message.Should().Contain("View extends beyond buffer boundaries");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void CreateView_WithNonRuntimeBuffer_ShouldThrowArgumentException()
    {
        // Arrange
        var mockBuffer = new Mock<IMemoryBuffer>();
        mockBuffer.Setup(b => b.SizeInBytes).Returns(1024);

        // Act & Assert
        var act = () => _memoryManager.CreateView(mockBuffer.Object, 0, 100);
        act.Should().Throw<ArgumentException>()
           .Which.ParamName.Should().Be("buffer");
    }

    #endregion

    #region Allocate<T> Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task Allocate_Generic_WithValidCount_ShouldReturnBuffer()
    {
        // Arrange
        const int count = 100;

        // Act
        var buffer = await _memoryManager.Allocate<int>(count);

        // Assert
        buffer.Should().NotBeNull();
        buffer.SizeInBytes.Should().Be(count * sizeof(int));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task Allocate_Generic_WithInvalidCount_ShouldThrowArgumentOutOfRangeException(int invalidCount)
    {
        // Arrange, Act & Assert
        var act = async () => await _memoryManager.Allocate<int>(invalidCount);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task Allocate_Generic_WithDifferentTypes_ShouldCalculateCorrectSize()
    {
        // Arrange & Act
        var intBuffer = await _memoryManager.Allocate<int>(10);
        var floatBuffer = await _memoryManager.Allocate<float>(10);
        var byteBuffer = await _memoryManager.Allocate<byte>(10);

        // Assert
        intBuffer.SizeInBytes.Should().Be(10 * sizeof(int));
        floatBuffer.SizeInBytes.Should().Be(10 * sizeof(float));
        byteBuffer.SizeInBytes.Should().Be(10 * sizeof(byte));
    }

    #endregion

    #region CopyToDevice Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CopyToDevice_WithValidData_ShouldSucceed()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = await _memoryManager.AllocateAsync(data.Length * sizeof(int));

        // Act & Assert
        var act = () => _memoryManager.CopyToDevice<int>(buffer, data.AsSpan());
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CopyToDevice_WithNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var data = new int[] { 1, 2, 3 };

        // Act & Assert
        var act = () => _memoryManager.CopyToDevice<int>(null!, data.AsSpan());
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("buffer");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void CopyToDevice_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var data = new int[] { 1, 2, 3 };
        var mockBuffer = new Mock<IMemoryBuffer>();
        _memoryManager.Dispose();

        // Act & Assert
        var act = () => _memoryManager.CopyToDevice(mockBuffer.Object, data.AsSpan());
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region CopyFromDevice Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task CopyFromDevice_WithValidBuffer_ShouldSucceed()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3, 4, 5 };
        var buffer = await _memoryManager.AllocateAndCopyAsync<int>(sourceData);
        var destination = new int[sourceData.Length];

        // Act & Assert
        var act = () => _memoryManager.CopyFromDevice(destination.AsSpan(), buffer);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void CopyFromDevice_WithNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var destination = new int[5];

        // Act & Assert
        var act = () => _memoryManager.CopyFromDevice(destination.AsSpan(), null!);
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("buffer");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void CopyFromDevice_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var destination = new int[5];
        var mockBuffer = new Mock<IMemoryBuffer>();
        _memoryManager.Dispose();

        // Act & Assert
        var act = () => _memoryManager.CopyFromDevice(destination.AsSpan(), mockBuffer.Object);
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Free Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task Free_WithValidBuffer_ShouldFreeBuffer()
    {
        // Arrange
        var buffer = await _memoryManager.AllocateAsync(1024);

        // Act & Assert
        var act = () => _memoryManager.Free(buffer);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Free_WithNullBuffer_ShouldNotThrow()
    {
        // Arrange, Act & Assert
        var act = () => _memoryManager.Free(null!);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Free_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockBuffer = new Mock<IMemoryBuffer>();
        _memoryManager.Dispose();

        // Act & Assert
        var act = () => _memoryManager.Free(mockBuffer.Object);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Free_WithNonRuntimeBuffer_ShouldDisposeBuffer()
    {
        // Arrange
        var mockBuffer = new Mock<IMemoryBuffer>();
        bool disposed = false;
        mockBuffer.Setup(b => b.Dispose()).Callback(() => disposed = true);

        // Act
        _memoryManager.Free(mockBuffer.Object);

        // Assert
        disposed.Should().BeTrue();
    }

    #endregion

    #region Dispose Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Dispose_ShouldDisposeAllBuffers()
    {
        // Arrange
        using var manager = new RuntimeMemoryManager(_mockLogger.Object);

        // Act & Assert
        var act = () => manager.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        using var manager = new RuntimeMemoryManager(_mockLogger.Object);

        // Act & Assert
        var act = () =>
        {
            manager.Dispose();
            manager.Dispose();
            manager.Dispose();
        };
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public async Task Dispose_WithActiveBuffers_ShouldDisposeAllBuffers()
    {
        // Arrange
        using var manager = new RuntimeMemoryManager(_mockLogger.Object);
        var buffer1 = await manager.AllocateAsync(1024);
        var buffer2 = await manager.AllocateAsync(2048);

        // Act
        manager.Dispose();

        // Assert
        buffer1.IsDisposed.Should().BeTrue();
        buffer2.IsDisposed.Should().BeTrue();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Performance)]
    public async Task AllocateAsync_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        const int taskCount = 10;
        const long bufferSize = 1024;

        // Act
        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => _memoryManager.AllocateAsync(bufferSize))
            .ToArray();

        var buffers = await Task.WhenAll(tasks);

        // Assert
        buffers.Should().HaveCount(taskCount);
        buffers.Should().OnlyContain(b => b.SizeInBytes == bufferSize);
        buffers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Performance)]
    public async Task Free_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        const int bufferCount = 10;
        var buffers = new List<IMemoryBuffer>();

        for (int i = 0; i < bufferCount; i++)
        {
            buffers.Add(await _memoryManager.AllocateAsync(1024));
        }

        // Act
        var tasks = buffers.Select(buffer => Task.Run(() => _memoryManager.Free(buffer)));
        await Task.WhenAll(tasks);

        // Assert - Should not throw and all buffers should be disposed
        buffers.Should().OnlyContain(b => b.IsDisposed);
    }

    #endregion

    #region Memory Pressure Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Performance)]
    public async Task AllocateAsync_LargeAllocation_ShouldSucceed()
    {
        // Arrange
        const long largeSize = 100 * 1024 * 1024; // 100MB

        // Act & Assert
        var act = () => _memoryManager.AllocateAsync(largeSize);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.EdgeCase)]
    public async Task AllocateAsync_MaxIntSize_ShouldSucceed()
    {
        // Arrange
        const long maxSize = int.MaxValue;

        // Act & Assert
        var act = () => _memoryManager.AllocateAsync(maxSize);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Integration Tests

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Integration)]
    public async Task EndToEnd_AllocateCopyAndFree_ShouldWorkCorrectly()
    {
        // Arrange
        var sourceData = Enumerable.Range(1, 1000).ToArray();
        var destinationData = new int[sourceData.Length];

        // Act
        var buffer = await _memoryManager.AllocateAndCopyAsync<int>(sourceData);
        _memoryManager.CopyFromDevice(destinationData.AsSpan(), buffer);
        _memoryManager.Free(buffer);

        // Assert
        destinationData.Should().Equal(sourceData);
        buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("Category", TestCategories.Integration)]
    public async Task ViewOperations_ShouldWorkWithParentBuffer()
    {
        // Arrange
        var sourceData = Enumerable.Range(1, 100).ToArray();
        var parentBuffer = await _memoryManager.AllocateAndCopyAsync<int>(sourceData);
        
        // Act
        var view = _memoryManager.CreateView(parentBuffer, 10 * sizeof(int), 20 * sizeof(int));
        var viewData = new int[20];
        _memoryManager.CopyFromDevice(viewData.AsSpan(), view);

        // Assert
        view.SizeInBytes.Should().Be(20 * sizeof(int));
        viewData.Should().Equal(sourceData.Skip(10).Take(20));
    }

    #endregion

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}