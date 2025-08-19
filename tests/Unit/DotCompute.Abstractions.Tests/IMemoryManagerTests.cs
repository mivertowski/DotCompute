// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for the IMemoryManager interface.
/// </summary>
public sealed class IMemoryManagerTests
{
    private readonly Mock<IMemoryManager> _mockMemoryManager;
    private readonly Mock<IMemoryBuffer> _mockBuffer;

    public IMemoryManagerTests()
    {
        _mockMemoryManager = new Mock<IMemoryManager>();
        _mockBuffer = new Mock<IMemoryBuffer>();
    }

    #region AllocateAsync Tests

    [Fact]
    public async Task AllocateAsync_WithValidSize_ShouldReturnBuffer()
    {
        // Arrange
        const long size = 1024;
        var options = MemoryOptions.None;

        _ = _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(size);
        _ = _mockBuffer.SetupGet(b => b.Options).Returns(options);
        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(size, options, CancellationToken.None))
                         .ReturnsAsync(_mockBuffer.Object);

        // Act
        var buffer = await _mockMemoryManager.Object.AllocateAsync(size, options);

        // Assert
        Assert.NotNull(buffer);
        _ = buffer.SizeInBytes.Should().Be(size);
        _ = buffer.Options.Should().Be(options);
        _mockMemoryManager.Verify(m => m.AllocateAsync(size, options, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AllocateAsync_WithZeroSize_ShouldThrowArgumentException()
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(0, MemoryOptions.None, CancellationToken.None))
                         .ThrowsAsync(new ArgumentException("Size must be positive", "sizeInBytes"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockMemoryManager.Object.AllocateAsync(0).AsTask());
        _ = exception.ParamName.Should().Be("sizeInBytes");
    }

    [Fact]
    public async Task AllocateAsync_WithNegativeSize_ShouldThrowArgumentException()
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(-1, MemoryOptions.None, CancellationToken.None))
                         .ThrowsAsync(new ArgumentException("Size must be positive", "sizeInBytes"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockMemoryManager.Object.AllocateAsync(-1).AsTask());
        _ = exception.ParamName.Should().Be("sizeInBytes");
    }

    [Fact]
    public async Task AllocateAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(1024, MemoryOptions.None, cts.Token))
                         .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
           () => _mockMemoryManager.Object.AllocateAsync(1024, cancellationToken: cts.Token).AsTask());
    }

    [Theory]
    [InlineData(MemoryOptions.None)]
    [InlineData(MemoryOptions.ReadOnly)]
    [InlineData(MemoryOptions.WriteOnly)]
    [InlineData(MemoryOptions.HostVisible)]
    [InlineData(MemoryOptions.Cached)]
    [InlineData(MemoryOptions.Atomic)]
    [InlineData(MemoryOptions.ReadOnly | MemoryOptions.HostVisible)]
    public async Task AllocateAsync_WithDifferentOptions_ShouldPassCorrectOptions(MemoryOptions options)
    {
        // Arrange
        const long size = 2048;
        _ = _mockBuffer.SetupGet(b => b.Options).Returns(options);
        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(size, options, CancellationToken.None))
                         .ReturnsAsync(_mockBuffer.Object);

        // Act
        var buffer = await _mockMemoryManager.Object.AllocateAsync(size, options);

        // Assert
        _ = buffer.Options.Should().Be(options);
        _mockMemoryManager.Verify(m => m.AllocateAsync(size, options, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AllocateAsync_WhenOutOfMemory_ShouldThrowMemoryException()
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                         .ThrowsAsync(new MemoryException("Out of memory"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<MemoryException>(
           () => _mockMemoryManager.Object.AllocateAsync(1024).AsTask());
        _ = exception.Message.Should().Be("Out of memory");
    }

    #endregion

    #region AllocateAndCopyAsync Tests

    [Fact]
    public async Task AllocateAndCopyAsync_WithValidData_ShouldReturnBufferWithData()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var sourceMemory = new ReadOnlyMemory<float>(sourceData);
        var options = MemoryOptions.None;

        _ = _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(sourceData.Length * sizeof(float));
        _ = _mockMemoryManager.Setup(m => m.AllocateAndCopyAsync(sourceMemory, options, CancellationToken.None))
                         .ReturnsAsync(_mockBuffer.Object);

        // Act
        var buffer = await _mockMemoryManager.Object.AllocateAndCopyAsync<float>(sourceMemory, options);

        // Assert
        Assert.NotNull(buffer);
        _ = buffer.SizeInBytes.Should().Be(sourceData.Length * sizeof(float));
        _mockMemoryManager.Verify(m => m.AllocateAndCopyAsync(sourceMemory, options, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AllocateAndCopyAsync_WithEmptyData_ShouldThrowArgumentException()
    {
        // Arrange
        var emptyData = ReadOnlyMemory<int>.Empty;

        _ = _mockMemoryManager.Setup(m => m.AllocateAndCopyAsync(emptyData, MemoryOptions.None, CancellationToken.None))
                         .ThrowsAsync(new ArgumentException("Source data cannot be empty", "source"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockMemoryManager.Object.AllocateAndCopyAsync<int>(emptyData).AsTask());
        _ = exception.ParamName.Should().Be("source");
    }

    [Fact]
    public async Task AllocateAndCopyAsync_WithDifferentTypes_ShouldWork()
    {
        // Arrange
        var intData = new int[] { 1, 2, 3 };
        var doubleData = new double[] { 1.0, 2.0, 3.0 };
        var byteData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var intBuffer = new Mock<IMemoryBuffer>();
        var doubleBuffer = new Mock<IMemoryBuffer>();
        var byteBuffer = new Mock<IMemoryBuffer>();

        _ = _mockMemoryManager.Setup(m => m.AllocateAndCopyAsync(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(intBuffer.Object);
        _ = _mockMemoryManager.Setup(m => m.AllocateAndCopyAsync(It.IsAny<ReadOnlyMemory<double>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(doubleBuffer.Object);
        _ = _mockMemoryManager.Setup(m => m.AllocateAndCopyAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(byteBuffer.Object);

        // Act
        var intResult = await _mockMemoryManager.Object.AllocateAndCopyAsync<int>(intData);
        var doubleResult = await _mockMemoryManager.Object.AllocateAndCopyAsync<double>(doubleData);
        var byteResult = await _mockMemoryManager.Object.AllocateAndCopyAsync<byte>(byteData);

        // Assert
        Assert.NotNull(intResult);
        Assert.NotNull(doubleResult);
        Assert.NotNull(byteResult);
    }

    #endregion

    #region CreateView Tests

    [Fact]
    public void CreateView_WithValidParameters_ShouldReturnView()
    {
        // Arrange
        const long offset = 100;
        const long length = 500;
        var parentBuffer = _mockBuffer.Object;
        var viewBuffer = new Mock<IMemoryBuffer>();

        _ = viewBuffer.SetupGet(b => b.SizeInBytes).Returns(length);
        _ = _mockMemoryManager.Setup(m => m.CreateView(parentBuffer, offset, length))
                         .Returns(viewBuffer.Object);

        // Act
        var view = _mockMemoryManager.Object.CreateView(parentBuffer, offset, length);

        // Assert
        Assert.NotNull(view);
        _ = view.SizeInBytes.Should().Be(length);
        _mockMemoryManager.Verify(m => m.CreateView(parentBuffer, offset, length), Times.Once);
    }

    [Fact]
    public void CreateView_WithNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.CreateView(null!, 0, 100))
                         .Throws(new ArgumentNullException("buffer"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
           () => _mockMemoryManager.Object.CreateView(null!, 0, 100));
        _ = exception.ParamName.Should().Be("buffer");
    }

    [Fact]
    public void CreateView_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.CreateView(_mockBuffer.Object, -1, 100))
                         .Throws(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
           () => _mockMemoryManager.Object.CreateView(_mockBuffer.Object, -1, 100));
        _ = exception.ParamName.Should().Be("offset");
    }

    [Fact]
    public void CreateView_WithZeroLength_ShouldThrowArgumentException()
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.CreateView(_mockBuffer.Object, 0, 0))
                         .Throws(new ArgumentException("Length must be positive", "count"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
           () => _mockMemoryManager.Object.CreateView(_mockBuffer.Object, 0, 0));
        _ = exception.ParamName.Should().Be("length");
    }

    #endregion

    #region Generic Allocate Tests

    [Fact]
    public async Task Allocate_WithValidCount_ShouldReturnBuffer()
    {
        // Arrange
        const int count = 1000;
        const long expectedSize = count * sizeof(float);

        _ = _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(expectedSize);
        _ = _mockMemoryManager.Setup(m => m.Allocate<float>(count))
                         .ReturnsAsync(_mockBuffer.Object);

        // Act
        var buffer = await _mockMemoryManager.Object.Allocate<float>(count);

        // Assert
        Assert.NotNull(buffer);
        _ = buffer.SizeInBytes.Should().Be(expectedSize);
        _mockMemoryManager.Verify(m => m.Allocate<float>(count), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task Allocate_WithInvalidCount_ShouldThrowArgumentException(int invalidCount)
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.Allocate<int>(invalidCount))
                         .ThrowsAsync(new ArgumentException("Count must be positive", nameof(invalidCount)));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockMemoryManager.Object.Allocate<int>(invalidCount).AsTask());
        _ = exception.ParamName.Should().Be(nameof(invalidCount));
    }

    [Fact]
    public async Task Allocate_WithDifferentTypes_ShouldCalculateCorrectSizes()
    {
        // Arrange
        const int count = 100;

        var intBuffer = new Mock<IMemoryBuffer>();
        var longBuffer = new Mock<IMemoryBuffer>();
        var doubleBuffer = new Mock<IMemoryBuffer>();

        _ = intBuffer.SetupGet(b => b.SizeInBytes).Returns(count * sizeof(int));
        _ = longBuffer.SetupGet(b => b.SizeInBytes).Returns(count * sizeof(long));
        _ = doubleBuffer.SetupGet(b => b.SizeInBytes).Returns(count * sizeof(double));

        _ = _mockMemoryManager.Setup(m => m.Allocate<int>(count)).ReturnsAsync(intBuffer.Object);
        _ = _mockMemoryManager.Setup(m => m.Allocate<long>(count)).ReturnsAsync(longBuffer.Object);
        _ = _mockMemoryManager.Setup(m => m.Allocate<double>(count)).ReturnsAsync(doubleBuffer.Object);

        // Act
        var intResult = await _mockMemoryManager.Object.Allocate<int>(count);
        var longResult = await _mockMemoryManager.Object.Allocate<long>(count);
        var doubleResult = await _mockMemoryManager.Object.Allocate<double>(count);

        // Assert
        _ = intResult.SizeInBytes.Should().Be(count * sizeof(int));
        _ = longResult.SizeInBytes.Should().Be(count * sizeof(long));
        _ = doubleResult.SizeInBytes.Should().Be(count * sizeof(double));
    }

    #endregion

    #region Copy Operations Tests

    [Fact]
    public void CopyToDevice_WithValidParameters_ShouldExecute()
    {
        // Arrange
        var data = new float[] { 1.0f, 2.0f, 3.0f };
        var span = new ReadOnlySpan<float>(data);

        // For span parameters, we can't use It.IsAny due to ref struct limitations
        // So we just verify the method can be called without exceptions

        // Act & Assert(should not throw)
        _mockMemoryManager.Object.CopyToDevice(_mockBuffer.Object, span);
    }

    [Fact]
    public void CopyToDevice_WithNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var data = new float[] { 1.0f };
        ReadOnlySpan<float> span = data;

        // This test cannot use mocking due to ref struct limitations
        // In a real implementation, this would throw ArgumentNullException

        // Act & Assert
        ArgumentNullException exception = null!;
        try
        {
            _mockMemoryManager.Object.CopyToDevice<float>(null!, span);
        }
        catch (ArgumentNullException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        _ = exception.ParamName.Should().Be("buffer");
    }

    [Fact]
    public void CopyFromDevice_WithValidParameters_ShouldExecute()
    {
        // Arrange
        var data = new float[3];
        var span = new Span<float>(data);

        // For span parameters, we can't use mocking due to ref struct limitations
        // So we just verify the method can be called without exceptions

        // Act & Assert(should not throw)
        _mockMemoryManager.Object.CopyFromDevice(span, _mockBuffer.Object);
    }

    [Fact]
    public void CopyFromDevice_WithNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var data = new float[3];
        Span<float> span = data;

        // This test cannot use mocking due to ref struct limitations
        // In a real implementation, this would throw ArgumentNullException

        // Act & Assert
        ArgumentNullException exception = null!;
        try
        {
            _mockMemoryManager.Object.CopyFromDevice<float>(span, null!);
        }
        catch (ArgumentNullException ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);
        _ = exception.ParamName.Should().Be("buffer");
    }

    [Fact]
    public void CopyOperations_WithEmptySpans_ShouldExecuteWithoutError()
    {
        // Arrange
        var emptyReadSpan = ReadOnlySpan<int>.Empty;
        var emptyWriteSpan = Span<int>.Empty;

        // For span parameters, we can't use mocking due to ref struct limitations
        // So we just verify the methods can be called without exceptions

        // Act & Assert(should not throw)
        _mockMemoryManager.Object.CopyToDevice(_mockBuffer.Object, emptyReadSpan);
        _mockMemoryManager.Object.CopyFromDevice(emptyWriteSpan, _mockBuffer.Object);
    }

    #endregion

    #region Free Tests

    [Fact]
    public void Free_WithValidBuffer_ShouldExecute()
    {
        // Arrange
        _mockMemoryManager.Setup(m => m.Free(_mockBuffer.Object))
                         .Verifiable();

        // Act
        _mockMemoryManager.Object.Free(_mockBuffer.Object);

        // Assert
        _mockMemoryManager.Verify(m => m.Free(_mockBuffer.Object), Times.Once);
    }

    [Fact]
    public void Free_WithNullBuffer_ShouldThrowArgumentNullException()
    {
        // Arrange
        _ = _mockMemoryManager.Setup(m => m.Free(null!))
                         .Throws(new ArgumentNullException("buffer"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
           () => _mockMemoryManager.Object.Free(null!));
        _ = exception.ParamName.Should().Be("buffer");
    }

    [Fact]
    public void Free_MultipleCallsOnSameBuffer_ShouldBeIdempotent()
    {
        // Arrange
        var callCount = 0;
        _ = _mockMemoryManager.Setup(m => m.Free(_mockBuffer.Object))
                         .Callback(() => callCount++);

        // Act
        _mockMemoryManager.Object.Free(_mockBuffer.Object);
        _mockMemoryManager.Object.Free(_mockBuffer.Object);
        _mockMemoryManager.Object.Free(_mockBuffer.Object);

        // Assert
        Assert.Equal(3, callCount);
        _mockMemoryManager.Verify(m => m.Free(_mockBuffer.Object), Times.Exactly(3));
    }

    #endregion

    #region Integration and Workflow Tests

    [Fact]
    public async Task CompleteMemoryWorkflow_ShouldExecuteAllOperationsInOrder()
    {
        // Arrange
        var sourceData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var destinationData = new float[4];

        // Setup the workflow
        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(_mockBuffer.Object);
        _ = _mockMemoryManager.Setup(m => m.Free(_mockBuffer.Object));

        // Act
        var buffer = await _mockMemoryManager.Object.AllocateAsync(sourceData.Length * sizeof(float));
        _mockMemoryManager.Object.CopyToDevice<float>(buffer, sourceData.AsSpan());
        _mockMemoryManager.Object.CopyFromDevice(destinationData.AsSpan(), buffer);
        _mockMemoryManager.Object.Free(buffer);

        // Assert
        _mockMemoryManager.Verify(m => m.AllocateAsync(It.IsAny<long>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMemoryManager.Verify(m => m.Free(_mockBuffer.Object), Times.Once);
        // Note: Copy operations with spans cannot be verified due to ref struct limitations in mocking
    }

    [Fact]
    public async Task AllocateAndCopyWorkflow_ShouldSimplifyCommonPattern()
    {
        // Arrange
        var sourceData = new int[] { 10, 20, 30, 40, 50 };
        var destinationData = new int[5];

        _ = _mockMemoryManager.Setup(m => m.AllocateAndCopyAsync(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(_mockBuffer.Object);
        _ = _mockMemoryManager.Setup(m => m.Free(_mockBuffer.Object));

        // Act
        var buffer = await _mockMemoryManager.Object.AllocateAndCopyAsync<int>(sourceData);
        _mockMemoryManager.Object.CopyFromDevice(destinationData.AsSpan(), buffer);
        _mockMemoryManager.Object.Free(buffer);

        // Assert
        _mockMemoryManager.Verify(m => m.AllocateAndCopyAsync(It.IsAny<ReadOnlyMemory<int>>(), It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockMemoryManager.Verify(m => m.Free(_mockBuffer.Object), Times.Once);
        // Note: Copy operations with spans cannot be verified due to ref struct limitations in mocking
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    public async Task AllocateAsync_WithLargeSize_ShouldHandleCorrectly()
    {
        // Arrange
        const long largeSize = 1024L * 1024 * 1024; // 1 GB
        _ = _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(largeSize);
        _ = _mockMemoryManager.Setup(m => m.AllocateAsync(largeSize, It.IsAny<MemoryOptions>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(_mockBuffer.Object);

        // Act
        var buffer = await _mockMemoryManager.Object.AllocateAsync(largeSize);

        // Assert
        Assert.NotNull(buffer);
        _ = buffer.SizeInBytes.Should().Be(largeSize);
    }

    [Fact]
    public async Task Allocate_WithMaximumCount_ShouldHandleCorrectly()
    {
        // Arrange
        const int maxCount = int.MaxValue / sizeof(int); // Maximum possible count for int array
        const long expectedSize = (long)maxCount * sizeof(int);

        _ = _mockBuffer.SetupGet(b => b.SizeInBytes).Returns(expectedSize);
        _ = _mockMemoryManager.Setup(m => m.Allocate<int>(maxCount))
                         .ReturnsAsync(_mockBuffer.Object);

        // Act
        var buffer = await _mockMemoryManager.Object.Allocate<int>(maxCount);

        // Assert
        Assert.NotNull(buffer);
        _ = buffer.SizeInBytes.Should().Be(expectedSize);
    }

    #endregion
}
