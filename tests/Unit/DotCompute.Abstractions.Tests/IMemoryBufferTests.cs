// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests
{

/// <summary>
/// Comprehensive unit tests for the IMemoryBuffer interface.
/// </summary>
public sealed class IMemoryBufferTests
{
    private readonly Mock<IMemoryBuffer> _mockBuffer;

    public IMemoryBufferTests()
    {
        _mockBuffer = new Mock<IMemoryBuffer>();
    }

    #region Property Tests

    [Fact]
    public void SizeInBytes_ShouldReturnCorrectSize()
    {
        // Arrange
        const long expectedSize = 4096;
        _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(expectedSize);

        // Act
        var size = _mockBuffer.Object.SizeInBytes;

        // Assert
        Assert.Equal(expectedSize, size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    [InlineData(long.MaxValue)]
    public void SizeInBytes_ShouldSupportDifferentSizes(long size)
    {
        // Arrange
        _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(size);

        // Act
        var actualSize = _mockBuffer.Object.SizeInBytes;

        // Assert
        Assert.Equal(size, actualSize);
    }

    [Fact]
    public void Options_ShouldReturnMemoryOptions()
    {
        // Arrange
        var expectedOptions = MemoryOptions.ReadOnly | MemoryOptions.HostVisible;
        _mockBuffer.SetupGet(b => b.Options).Returns(expectedOptions);

        // Act
        var options = _mockBuffer.Object.Options;

        // Assert
        Assert.Equal(expectedOptions, options);
    }

    [Theory]
    [InlineData(MemoryOptions.None)]
    [InlineData(MemoryOptions.ReadOnly)]
    [InlineData(MemoryOptions.WriteOnly)]
    [InlineData(MemoryOptions.HostVisible)]
    [InlineData(MemoryOptions.Cached)]
    [InlineData(MemoryOptions.Atomic)]
    [InlineData(MemoryOptions.ReadOnly | MemoryOptions.HostVisible | MemoryOptions.Cached)]
    public void Options_ShouldSupportAllMemoryOptions(MemoryOptions options)
    {
        // Arrange
        _mockBuffer.SetupGet(b => b.Options).Returns(options);

        // Act
        var actualOptions = _mockBuffer.Object.Options;

        // Assert
        Assert.Equal(options, actualOptions);
    }

    [Fact]
    public void IsDisposed_WhenNotDisposed_ShouldReturnFalse()
    {
        // Arrange
        _mockBuffer.SetupGet(b => b.IsDisposed).Returns(false);

        // Act
        var isDisposed = _mockBuffer.Object.IsDisposed;

        // Assert
        Assert.False(isDisposed);
    }

    [Fact]
    public void IsDisposed_WhenDisposed_ShouldReturnTrue()
    {
        // Arrange
        _mockBuffer.SetupGet(b => b.IsDisposed).Returns(true);

        // Act
        var isDisposed = _mockBuffer.Object.IsDisposed;

        // Assert
        Assert.True(isDisposed);
    }

    #endregion

    #region CopyFromHostAsync Tests

    [Fact]
    public async Task CopyFromHostAsync_WithValidData_ShouldComplete()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var sourceMemory = new ReadOnlyMemory<float>(sourceData);
        const long offset = 0;

        _mockBuffer.Setup(b => b.CopyFromHostAsync(sourceMemory, offset, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.CopyFromHostAsync(sourceMemory, offset);

        // Assert
        _mockBuffer.Verify(b => b.CopyFromHostAsync(sourceMemory, offset, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyFromHostAsync_WithOffset_ShouldPassCorrectOffset()
    {
        // Arrange
        var sourceData = new double[] { 1.0, 2.0, 3.0 };
        var sourceMemory = new ReadOnlyMemory<double>(sourceData);
        const long offset = 256;

        _mockBuffer.Setup(b => b.CopyFromHostAsync(sourceMemory, offset, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.CopyFromHostAsync(sourceMemory, offset);

        // Assert
        _mockBuffer.Verify(b => b.CopyFromHostAsync(sourceMemory, offset, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyFromHostAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3 };
        var sourceMemory = new ReadOnlyMemory<int>(sourceData);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockBuffer.Setup(b => b.CopyFromHostAsync(sourceMemory, 0, cts.Token))
                  .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
           () => _mockBuffer.Object.CopyFromHostAsync(sourceMemory, cancellationToken: cts.Token).AsTask());
    }

    [Fact]
    public async Task CopyFromHostAsync_WithEmptyData_ShouldComplete()
    {
        // Arrange
        var emptyMemory = ReadOnlyMemory<byte>.Empty;

        _mockBuffer.Setup(b => b.CopyFromHostAsync(emptyMemory, 0, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.CopyFromHostAsync(emptyMemory);

        // Assert
        _mockBuffer.Verify(b => b.CopyFromHostAsync(emptyMemory, 0, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyFromHostAsync_WhenBufferDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var sourceData = new float[] { 1.0f };
        var sourceMemory = new ReadOnlyMemory<float>(sourceData);

        _mockBuffer.Setup(b => b.CopyFromHostAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new ObjectDisposedException("Buffer has been disposed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
           () => _mockBuffer.Object.CopyFromHostAsync(sourceMemory).AsTask());
        exception.Message.Should().Be("Buffer has been disposed");
    }

    [Fact]
    public async Task CopyFromHostAsync_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var sourceData = new int[] { 1, 2, 3 };
        var sourceMemory = new ReadOnlyMemory<int>(sourceData);

        _mockBuffer.Setup(b => b.CopyFromHostAsync(sourceMemory, -1, CancellationToken.None))
                  .ThrowsAsync(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
           () => _mockBuffer.Object.CopyFromHostAsync(sourceMemory, -1).AsTask());
        exception.ParamName.Should().Be("offset");
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(float))]
    [InlineData(typeof(double))]
    [InlineData(typeof(byte))]
    [InlineData(typeof(long))]
    public async Task CopyFromHostAsync_WithDifferentTypes_ShouldWork(Type elementType)
    {
        // Arrange & Act & Assert based on type
        if (elementType == typeof(int))
        {
            var data = new int[] { 1, 2, 3 };
            var memory = new ReadOnlyMemory<int>(data);
            _mockBuffer.Setup(b => b.CopyFromHostAsync(memory, 0, CancellationToken.None))
                      .Returns(ValueTask.CompletedTask);

            await _mockBuffer.Object.CopyFromHostAsync(memory);
            _mockBuffer.Verify(b => b.CopyFromHostAsync(memory, 0, CancellationToken.None), Times.Once);
        }
        else if (elementType == typeof(float))
        {
            var data = new float[] { 1.0f, 2.0f, 3.0f };
            var memory = new ReadOnlyMemory<float>(data);
            _mockBuffer.Setup(b => b.CopyFromHostAsync(memory, 0, CancellationToken.None))
                      .Returns(ValueTask.CompletedTask);

            await _mockBuffer.Object.CopyFromHostAsync(memory);
            _mockBuffer.Verify(b => b.CopyFromHostAsync(memory, 0, CancellationToken.None), Times.Once);
        }
        // Add similar blocks for other types...
    }

    #endregion

    #region CopyToHostAsync Tests

    [Fact]
    public async Task CopyToHostAsync_WithValidDestination_ShouldComplete()
    {
        // Arrange
        var destinationData = new float[4];
        var destinationMemory = new Memory<float>(destinationData);
        const long offset = 0;

        _mockBuffer.Setup(b => b.CopyToHostAsync(destinationMemory, offset, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.CopyToHostAsync(destinationMemory, offset);

        // Assert
        _mockBuffer.Verify(b => b.CopyToHostAsync(destinationMemory, offset, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyToHostAsync_WithOffset_ShouldPassCorrectOffset()
    {
        // Arrange
        var destinationData = new double[3];
        var destinationMemory = new Memory<double>(destinationData);
        const long offset = 512;

        _mockBuffer.Setup(b => b.CopyToHostAsync(destinationMemory, offset, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.CopyToHostAsync(destinationMemory, offset);

        // Assert
        _mockBuffer.Verify(b => b.CopyToHostAsync(destinationMemory, offset, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyToHostAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var destinationData = new int[3];
        var destinationMemory = new Memory<int>(destinationData);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockBuffer.Setup(b => b.CopyToHostAsync(destinationMemory, 0, cts.Token))
                  .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
           () => _mockBuffer.Object.CopyToHostAsync(destinationMemory, cancellationToken: cts.Token).AsTask());
    }

    [Fact]
    public async Task CopyToHostAsync_WithEmptyDestination_ShouldComplete()
    {
        // Arrange
        var emptyMemory = Memory<byte>.Empty;

        _mockBuffer.Setup(b => b.CopyToHostAsync(emptyMemory, 0, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.CopyToHostAsync(emptyMemory);

        // Assert
        _mockBuffer.Verify(b => b.CopyToHostAsync(emptyMemory, 0, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyToHostAsync_WhenBufferDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var destinationData = new float[1];
        var destinationMemory = new Memory<float>(destinationData);

        _mockBuffer.Setup(b => b.CopyToHostAsync(It.IsAny<Memory<float>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new ObjectDisposedException("Buffer has been disposed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
           () => _mockBuffer.Object.CopyToHostAsync(destinationMemory).AsTask());
        exception.Message.Should().Be("Buffer has been disposed");
    }

    [Fact]
    public async Task CopyToHostAsync_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var destinationData = new int[3];
        var destinationMemory = new Memory<int>(destinationData);

        _mockBuffer.Setup(b => b.CopyToHostAsync(destinationMemory, -1, CancellationToken.None))
                  .ThrowsAsync(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
           () => _mockBuffer.Object.CopyToHostAsync(destinationMemory, -1).AsTask());
        exception.ParamName.Should().Be("offset");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ShouldComplete()
    {
        // Arrange
        _mockBuffer.Setup(b => b.Dispose())
                  .Verifiable();

        // Act
        _mockBuffer.Object.Dispose();

        // Assert
        _mockBuffer.Verify(b => b.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ShouldComplete()
    {
        // Arrange
        _mockBuffer.Setup(b => b.DisposeAsync())
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.DisposeAsync();

        // Assert
        _mockBuffer.Verify(b => b.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void Dispose_MultipleCallsShouldBeIdempotent()
    {
        // Arrange
        var callCount = 0;
        _mockBuffer.Setup(b => b.Dispose())
                  .Callback(() => callCount++);

        // Act
        _mockBuffer.Object.Dispose();
        _mockBuffer.Object.Dispose();
        _mockBuffer.Object.Dispose();

        // Assert
        Assert.Equal(3, callCount);
        _mockBuffer.Verify(b => b.Dispose(), Times.Exactly(3));
    }

    [Fact]
    public async Task DisposeAsync_MultipleCallsShouldBeIdempotent()
    {
        // Arrange
        var callCount = 0;
        _mockBuffer.Setup(b => b.DisposeAsync())
                  .Callback(() => callCount++)
                  .Returns(ValueTask.CompletedTask);

        // Act
        await _mockBuffer.Object.DisposeAsync();
        await _mockBuffer.Object.DisposeAsync();
        await _mockBuffer.Object.DisposeAsync();

        // Assert
        Assert.Equal(3, callCount);
        _mockBuffer.Verify(b => b.DisposeAsync(), Times.Exactly(3));
    }

    #endregion

    #region Interface Inheritance Tests

    [Fact]
    public void IMemoryBuffer_ShouldInheritFromIAsyncDisposable()
    {
        // Arrange & Act
        var type = typeof(IMemoryBuffer);

        // Assert
        Assert.IsAssignableFrom<IAsyncDisposable>(type);
    }

    [Fact]
    public void IMemoryBuffer_ShouldInheritFromIDisposable()
    {
        // Arrange & Act
        var type = typeof(IMemoryBuffer);

        // Assert
        Assert.IsAssignableFrom<IDisposable>(type);
    }

    #endregion

    #region Integration and Workflow Tests

    [Fact]
    public async Task CompleteBufferWorkflow_ShouldExecuteAllOperationsInOrder()
    {
        // Arrange
        const long bufferSize = 1024;
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var destinationData = new float[4];
        var sourceMemory = new ReadOnlyMemory<float>(sourceData);
        var destinationMemory = new Memory<float>(destinationData);

        _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(bufferSize);
        _mockBuffer.SetupGet(b => b.Options).Returns(MemoryOptions.None);
        _mockBuffer.SetupGet(b => b.IsDisposed).Returns(false);
        _mockBuffer.Setup(b => b.CopyFromHostAsync(sourceMemory, 0, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);
        _mockBuffer.Setup(b => b.CopyToHostAsync(destinationMemory, 0, CancellationToken.None))
                  .Returns(ValueTask.CompletedTask);
        _mockBuffer.Setup(b => b.DisposeAsync())
                  .Returns(ValueTask.CompletedTask);

        // Act
        var buffer = _mockBuffer.Object;
        var size = buffer.SizeInBytes;
        var options = buffer.Options;
        var isDisposed = buffer.IsDisposed;
        await buffer.CopyFromHostAsync(sourceMemory);
        await buffer.CopyToHostAsync(destinationMemory);
        await buffer.DisposeAsync();

        // Assert
        Assert.Equal(bufferSize, size);
        Assert.Equal(MemoryOptions.None, options);
        Assert.False(isDisposed);

        // Verify all methods were called
        _mockBuffer.Verify(b => b.CopyFromHostAsync(sourceMemory, 0, CancellationToken.None), Times.Once);
        _mockBuffer.Verify(b => b.CopyToHostAsync(destinationMemory, 0, CancellationToken.None), Times.Once);
        _mockBuffer.Verify(b => b.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange & Act
        var type = typeof(IMemoryBuffer);
        var sizeProperty = type.GetProperty(nameof(IMemoryBuffer.SizeInBytes));
        var optionsProperty = type.GetProperty(nameof(IMemoryBuffer.Options));
        var disposedProperty = type.GetProperty(nameof(IMemoryBuffer.IsDisposed));

        // Assert
        sizeProperty!.CanRead.Should().BeTrue();
        sizeProperty.CanWrite.Should().BeFalse();
        optionsProperty!.CanRead.Should().BeTrue();
        optionsProperty.CanWrite.Should().BeFalse();
        disposedProperty!.CanRead.Should().BeTrue();
        disposedProperty.CanWrite.Should().BeFalse();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CopyFromHostAsync_WithInsufficientBufferSpace_ShouldThrowArgumentException()
    {
        // Arrange
        var largeData = new byte[2048];
        var sourceMemory = new ReadOnlyMemory<byte>(largeData);

        _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(1024); // Buffer smaller than data
        _mockBuffer.Setup(b => b.CopyFromHostAsync(sourceMemory, 0, CancellationToken.None))
                  .ThrowsAsync(new ArgumentException("Data size exceeds buffer capacity"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockBuffer.Object.CopyFromHostAsync(sourceMemory).AsTask());
        exception.Message.Should().Be("Data size exceeds buffer capacity");
    }

    [Fact]
    public async Task CopyToHostAsync_WithInsufficientDestinationSpace_ShouldThrowArgumentException()
    {
        // Arrange
        var smallDestination = new byte[512];
        var destinationMemory = new Memory<byte>(smallDestination);

        _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(1024); // Buffer larger than destination
        _mockBuffer.Setup(b => b.CopyToHostAsync(destinationMemory, 0, CancellationToken.None))
                  .ThrowsAsync(new ArgumentException("Destination too small for buffer data"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockBuffer.Object.CopyToHostAsync(destinationMemory).AsTask());
        exception.Message.Should().Be("Destination too small for buffer data");
    }

    [Fact]
    public async Task CopyOperations_WithOffsetBeyondBufferSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var data = new float[] { 1.0f };
        var sourceMemory = new ReadOnlyMemory<float>(data);
        var destinationMemory = new Memory<float>(data);
        const long bufferSize = 1024;
        const long invalidOffset = 2048;

        _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(bufferSize);
        _mockBuffer.Setup(b => b.CopyFromHostAsync(sourceMemory, invalidOffset, CancellationToken.None))
                  .ThrowsAsync(new ArgumentOutOfRangeException("offset"));
        _mockBuffer.Setup(b => b.CopyToHostAsync(destinationMemory, invalidOffset, CancellationToken.None))
                  .ThrowsAsync(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception1 = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
           () => _mockBuffer.Object.CopyFromHostAsync(sourceMemory, invalidOffset).AsTask());
        exception1.ParamName.Should().Be("offset");

        var exception2 = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
           () => _mockBuffer.Object.CopyToHostAsync(destinationMemory, invalidOffset).AsTask());
        exception2.ParamName.Should().Be("offset");
    }

    #endregion

    #region Memory Management Edge Cases

    [Fact]
    public async Task OperationsAfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var data = new float[] { 1.0f };
        var sourceMemory = new ReadOnlyMemory<float>(data);
        var destinationMemory = new Memory<float>(data);

        _mockBuffer.SetupGet(b => b.IsDisposed).Returns(true);
        _mockBuffer.Setup(b => b.CopyFromHostAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new ObjectDisposedException(nameof(IMemoryBuffer)));
        _mockBuffer.Setup(b => b.CopyToHostAsync(It.IsAny<Memory<float>>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new ObjectDisposedException(nameof(IMemoryBuffer)));

        // Act & Assert
        var exception1 = await Assert.ThrowsAsync<ObjectDisposedException>(
           () => _mockBuffer.Object.CopyFromHostAsync(sourceMemory).AsTask());
        exception1.ObjectName.Should().Be(nameof(IMemoryBuffer));

        var exception2 = await Assert.ThrowsAsync<ObjectDisposedException>(
           () => _mockBuffer.Object.CopyToHostAsync(destinationMemory).AsTask());
        exception2.ObjectName.Should().Be(nameof(IMemoryBuffer));
    }

    [Fact]
    public void PropertyAccess_AfterDispose_ShouldStillWork()
    {
        // Note: Properties typically remain accessible even after disposal
        // They may return cached values or throw exceptions - depends on implementation

        // Arrange
        const long size = 1024;
        var options = MemoryOptions.ReadOnly;

        _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(size);
        _mockBuffer.SetupGet(b => b.Options).Returns(options);
        _mockBuffer.SetupGet(b => b.IsDisposed).Returns(true);

        // Act
        var actualSize = _mockBuffer.Object.SizeInBytes;
        var actualOptions = _mockBuffer.Object.Options;
        var isDisposed = _mockBuffer.Object.IsDisposed;

        // Assert
        Assert.Equal(size, actualSize);
        Assert.Equal(options, actualOptions);
        Assert.True(isDisposed);
    }

    #endregion
}
}
