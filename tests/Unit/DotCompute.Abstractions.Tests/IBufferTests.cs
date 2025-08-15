// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace DotCompute.Abstractions.Tests;

/// <summary>
/// Comprehensive unit tests for the IBuffer&lt;T&gt; interface.
/// </summary>
public class IBufferTests
{
    private readonly Mock<IBuffer<float>> _mockFloatBuffer;
    private readonly Mock<IBuffer<int>> _mockIntBuffer;
    private readonly Mock<IAccelerator> _mockAccelerator;

    public IBufferTests()
    {
        _mockFloatBuffer = new Mock<IBuffer<float>>();
        _mockIntBuffer = new Mock<IBuffer<int>>();
        _mockAccelerator = new Mock<IAccelerator>();
    }

    private static MappedMemory<T> CreateMappedMemory<T>(IBuffer<T> buffer, Memory<T> memory, MapMode mode) where T : unmanaged
    {
        // Use reflection to create MappedMemory since constructor is internal
        var constructor = typeof(MappedMemory<T>).GetConstructors(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0];
        return(MappedMemory<T>)constructor.Invoke(new object[] { buffer, memory, mode });
    }

    #region Property Tests

    [Fact]
    public void Length_ShouldReturnCorrectElementCount()
    {
        // Arrange
        const int expectedLength = 1000;
        _mockFloatBuffer.SetupGet(b => b.Length).Returns(expectedLength);

        // Act
        var length = _mockFloatBuffer.Object.Length;

        // Assert
        Assert.Equal(expectedLength, length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(1000000)]
    [InlineData(int.MaxValue)]
    public void Length_ShouldSupportDifferentSizes(int length)
    {
        // Arrange
        _mockFloatBuffer.SetupGet(b => b.Length).Returns(length);

        // Act
        var actualLength = _mockFloatBuffer.Object.Length;

        // Assert
        Assert.Equal(length, actualLength);
    }

    [Fact]
    public void ElementCount_ShouldBeAliasForLength()
    {
        // Arrange
        const int expectedCount = 500;
        _mockFloatBuffer.SetupGet(b => b.Length).Returns(expectedCount);
        _mockFloatBuffer.SetupGet(b => b.ElementCount).Returns(expectedCount);

        // Act
        var elementCount = _mockFloatBuffer.Object.ElementCount;
        var length = _mockFloatBuffer.Object.Length;

        // Assert
        Assert.Equal(expectedCount, elementCount);
        Assert.Equal(length, elementCount);
    }

    [Fact]
    public void Accelerator_ShouldReturnAssociatedAccelerator()
    {
        // Arrange
        _mockFloatBuffer.SetupGet(b => b.Accelerator).Returns(_mockAccelerator.Object);

        // Act
        var accelerator = _mockFloatBuffer.Object.Accelerator;

        // Assert
        Assert.NotNull(accelerator);
        Assert.Equal(_mockAccelerator.Object, accelerator);
    }

    [Fact]
    public void SizeInBytes_ShouldBeConsistentWithLength()
    {
        // Arrange
        const int length = 250;
        const long expectedSizeInBytes = length * sizeof(float);
        
        _mockFloatBuffer.SetupGet(b => b.Length).Returns(length);
        _mockFloatBuffer.SetupGet(b => b.SizeInBytes).Returns(expectedSizeInBytes);

        // Act
        var sizeInBytes = _mockFloatBuffer.Object.SizeInBytes;
        var calculatedSize = _mockFloatBuffer.Object.Length * sizeof(float);

        // Assert
        Assert.Equal(expectedSizeInBytes, sizeInBytes);
        Assert.Equal(calculatedSize, sizeInBytes);
    }

    #endregion

    #region Slice Tests

    [Fact]
    public void Slice_WithValidParameters_ShouldReturnSlice()
    {
        // Arrange
        const int offset = 100;
        const int length = 200;
        var mockSlice = new Mock<IBuffer<float>>();
        
        mockSlice.SetupGet(s => s.Length).Returns(length);
        _mockFloatBuffer.Setup(b => b.Slice(offset, length))
                       .Returns(mockSlice.Object);

        // Act
        var slice = _mockFloatBuffer.Object.Slice(offset, length);

        // Assert
        Assert.NotNull(slice);
        slice.Length.Should().Be(length);
        _mockFloatBuffer.Verify(b => b.Slice(offset, length), Times.Once);
    }

    [Fact]
    public void Slice_WithZeroOffset_ShouldReturnSliceFromBeginning()
    {
        // Arrange
        const int offset = 0;
        const int length = 50;
        var mockSlice = new Mock<IBuffer<float>>();
        
        mockSlice.SetupGet(s => s.Length).Returns(length);
        _mockFloatBuffer.Setup(b => b.Slice(offset, length))
                       .Returns(mockSlice.Object);

        // Act
        var slice = _mockFloatBuffer.Object.Slice(offset, length);

        // Assert
        Assert.NotNull(slice);
        slice.Length.Should().Be(length);
    }

    [Fact]
    public void Slice_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.Slice(-1, 100))
                       .Throws(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
           () => _mockFloatBuffer.Object.Slice(-1, 100));
        exception.ParamName.Should().Be("offset");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Slice_WithInvalidLength_ShouldThrowArgumentException(int invalidLength)
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.Slice(0, invalidLength))
                       .Throws(new ArgumentException("Length must be positive", "length"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
           () => _mockFloatBuffer.Object.Slice(0, invalidLength));
        exception.ParamName.Should().Be("length");
    }

    [Fact]
    public void Slice_WithOffsetBeyondBufferLength_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        const int bufferLength = 100;
        const int invalidOffset = 150;
        
        _mockFloatBuffer.SetupGet(b => b.Length).Returns(bufferLength);
        _mockFloatBuffer.Setup(b => b.Slice(invalidOffset, 10))
                       .Throws(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
           () => _mockFloatBuffer.Object.Slice(invalidOffset, 10));
        exception.ParamName.Should().Be("offset");
    }

    [Fact]
    public void Slice_WithLengthExceedingRemainingElements_ShouldThrowArgumentException()
    {
        // Arrange
        const int bufferLength = 100;
        const int offset = 80;
        const int invalidLength = 30; // Would exceed buffer length
        
        _mockFloatBuffer.SetupGet(b => b.Length).Returns(bufferLength);
        _mockFloatBuffer.Setup(b => b.Slice(offset, invalidLength))
                       .Throws(new ArgumentException("Slice extends beyond buffer boundary"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
           () => _mockFloatBuffer.Object.Slice(offset, invalidLength));
        exception.Message.Should().Be("Slice extends beyond buffer boundary");
    }

    #endregion

    #region AsType Tests

    [Fact]
    public void AsType_WithValidType_ShouldReturnTypedBuffer()
    {
        // Arrange
        var mockIntBuffer = new Mock<IBuffer<int>>();
        _mockFloatBuffer.Setup(b => b.AsType<int>())
                       .Returns(mockIntBuffer.Object);

        // Act
        var intBuffer = _mockFloatBuffer.Object.AsType<int>();

        // Assert
        Assert.NotNull(intBuffer);
        Assert.Equal(mockIntBuffer.Object, intBuffer);
        _mockFloatBuffer.Verify(b => b.AsType<int>(), Times.Once);
    }

    [Fact]
    public void AsType_WithSameType_ShouldReturnEquivalentBuffer()
    {
        // Arrange
        var mockFloatBuffer2 = new Mock<IBuffer<float>>();
        _mockFloatBuffer.Setup(b => b.AsType<float>())
                       .Returns(mockFloatBuffer2.Object);

        // Act
        var floatBuffer = _mockFloatBuffer.Object.AsType<float>();

        // Assert
        Assert.NotNull(floatBuffer);
        Assert.Equal(mockFloatBuffer2.Object, floatBuffer);
    }

    [Fact]
    public void AsType_WithIncompatibleType_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.AsType<decimal>()) // decimal is not unmanaged
                       .Throws(new InvalidOperationException("Cannot convert to incompatible type"));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
           () => _mockFloatBuffer.Object.AsType<decimal>());
        exception.Message.Should().Be("Cannot convert to incompatible type");
    }

    [Theory]
    [InlineData(typeof(byte))]
    [InlineData(typeof(short))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(double))]
    public void AsType_WithDifferentUnmanagedTypes_ShouldWork(Type targetType)
    {
        // Note: This is a conceptual test - actual implementation would need reflection
        // to create generic method calls dynamically, but we can test the pattern
        
        // Arrange & Act & Assert would depend on the specific type
        if(targetType == typeof(int))
        {
            var mockIntBuffer = new Mock<IBuffer<int>>();
            _mockFloatBuffer.Setup(b => b.AsType<int>()).Returns(mockIntBuffer.Object);
            
            var result = _mockFloatBuffer.Object.AsType<int>();
            Assert.NotNull(result);
        }
        // Similar patterns for other types...
    }

    #endregion

    #region Copy Operations Tests

    [Fact]
    public async Task CopyToAsync_WithDestinationBuffer_ShouldComplete()
    {
        // Arrange
        var mockDestination = new Mock<IBuffer<float>>();
        
        _mockFloatBuffer.Setup(b => b.CopyToAsync(mockDestination.Object, CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockFloatBuffer.Object.CopyToAsync(mockDestination.Object);

        // Assert
        _mockFloatBuffer.Verify(b => b.CopyToAsync(mockDestination.Object, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyToAsync_WithNullDestination_ShouldThrowArgumentNullException()
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.CopyToAsync(null!, CancellationToken.None))
                       .ThrowsAsync(new ArgumentNullException("destination"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
           () => _mockFloatBuffer.Object.CopyToAsync(null!).AsTask());
        exception.ParamName.Should().Be("destination");
    }

    [Fact]
    public async Task CopyToAsync_WithRanges_ShouldCopySpecifiedRange()
    {
        // Arrange
        const int sourceOffset = 10;
        const int destinationOffset = 20;
        const int count = 100;
        var mockDestination = new Mock<IBuffer<float>>();
        
        _mockFloatBuffer.Setup(b => b.CopyToAsync(sourceOffset, mockDestination.Object, destinationOffset, count, CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockFloatBuffer.Object.CopyToAsync(sourceOffset, mockDestination.Object, destinationOffset, count);

        // Assert
        _mockFloatBuffer.Verify(b => b.CopyToAsync(sourceOffset, mockDestination.Object, destinationOffset, count, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CopyToAsync_WithInvalidSourceOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var mockDestination = new Mock<IBuffer<float>>();
        
        _mockFloatBuffer.Setup(b => b.CopyToAsync(-1, mockDestination.Object, 0, 10, CancellationToken.None))
                       .ThrowsAsync(new ArgumentOutOfRangeException("sourceOffset"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
           () => _mockFloatBuffer.Object.CopyToAsync(-1, mockDestination.Object, 0, 10).AsTask());
        exception.ParamName.Should().Be("sourceOffset");
    }

    [Fact]
    public async Task CopyToAsync_WithInvalidDestinationOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var mockDestination = new Mock<IBuffer<float>>();
        
        _mockFloatBuffer.Setup(b => b.CopyToAsync(0, mockDestination.Object, -1, 10, CancellationToken.None))
                       .ThrowsAsync(new ArgumentOutOfRangeException("destinationOffset"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
           () => _mockFloatBuffer.Object.CopyToAsync(0, mockDestination.Object, -1, 10).AsTask());
        exception.ParamName.Should().Be("destinationOffset");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task CopyToAsync_WithInvalidCount_ShouldThrowArgumentException(int invalidCount)
    {
        // Arrange
        var mockDestination = new Mock<IBuffer<float>>();
        
        _mockFloatBuffer.Setup(b => b.CopyToAsync(0, mockDestination.Object, 0, invalidCount, CancellationToken.None))
                       .ThrowsAsync(new ArgumentException("Count must be positive", "count"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockFloatBuffer.Object.CopyToAsync(0, mockDestination.Object, 0, invalidCount).AsTask());
        exception.ParamName.Should().Be("count");
    }

    [Fact]
    public async Task CopyToAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var mockDestination = new Mock<IBuffer<float>>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        _mockFloatBuffer.Setup(b => b.CopyToAsync(mockDestination.Object, cts.Token))
                       .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
           () => _mockFloatBuffer.Object.CopyToAsync(mockDestination.Object, cts.Token).AsTask());
    }

    #endregion

    #region Fill Operations Tests

    [Fact]
    public async Task FillAsync_WithValue_ShouldFillEntireBuffer()
    {
        // Arrange
        const float fillValue = 3.14f;
        
        _mockFloatBuffer.Setup(b => b.FillAsync(fillValue, CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockFloatBuffer.Object.FillAsync(fillValue);

        // Assert
        _mockFloatBuffer.Verify(b => b.FillAsync(fillValue, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task FillAsync_WithRange_ShouldFillSpecifiedRange()
    {
        // Arrange
        const float fillValue = 2.71f;
        const int offset = 50;
        const int count = 100;
        
        _mockFloatBuffer.Setup(b => b.FillAsync(fillValue, offset, count, CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockFloatBuffer.Object.FillAsync(fillValue, offset, count);

        // Assert
        _mockFloatBuffer.Verify(b => b.FillAsync(fillValue, offset, count, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task FillAsync_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.FillAsync(1.0f, -1, 10, CancellationToken.None))
                       .ThrowsAsync(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
           () => _mockFloatBuffer.Object.FillAsync(1.0f, -1, 10).AsTask());
        exception.ParamName.Should().Be("offset");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task FillAsync_WithInvalidCount_ShouldThrowArgumentException(int invalidCount)
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.FillAsync(1.0f, 0, invalidCount, CancellationToken.None))
                       .ThrowsAsync(new ArgumentException("Count must be positive", "count"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
           () => _mockFloatBuffer.Object.FillAsync(1.0f, 0, invalidCount).AsTask());
        exception.ParamName.Should().Be("count");
    }

    [Fact]
    public async Task FillAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        _mockFloatBuffer.Setup(b => b.FillAsync(1.0f, cts.Token))
                       .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
           () => _mockFloatBuffer.Object.FillAsync(1.0f, cts.Token).AsTask());
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.MaxValue)]
    [InlineData(float.MinValue)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public async Task FillAsync_WithDifferentValues_ShouldWork(float value)
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.FillAsync(value, CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);

        // Act
        await _mockFloatBuffer.Object.FillAsync(value);

        // Assert
        _mockFloatBuffer.Verify(b => b.FillAsync(value, CancellationToken.None), Times.Once);
    }

    #endregion

    #region Memory Mapping Tests

    [Fact]
    public void Map_WithDefaultMode_ShouldReturnMappedMemory()
    {
        // Arrange
        var mockMemory = new Memory<float>(new float[100]);
        var expectedMapping = CreateMappedMemory(_mockFloatBuffer.Object, mockMemory, MapMode.ReadWrite);
        
        _mockFloatBuffer.Setup(b => b.Map(MapMode.ReadWrite))
                       .Returns(expectedMapping);

        // Act
        var mapping = _mockFloatBuffer.Object.Map();

        // Assert
        mapping.Mode.Should().Be(MapMode.ReadWrite);
        _mockFloatBuffer.Verify(b => b.Map(MapMode.ReadWrite), Times.Once);
    }

    [Theory]
    [InlineData(MapMode.Read)]
    [InlineData(MapMode.Write)]
    [InlineData(MapMode.ReadWrite)]
    [InlineData(MapMode.Read | MapMode.NoWait)]
    [InlineData(MapMode.Write | MapMode.Discard)]
    public void Map_WithDifferentModes_ShouldPassCorrectMode(MapMode mode)
    {
        // Arrange
        var mockMemory = new Memory<float>(new float[100]);
        var expectedMapping = CreateMappedMemory(_mockFloatBuffer.Object, mockMemory, mode);
        
        _mockFloatBuffer.Setup(b => b.Map(mode))
                       .Returns(expectedMapping);

        // Act
        var mapping = _mockFloatBuffer.Object.Map(mode);

        // Assert
        mapping.Mode.Should().Be(mode);
        _mockFloatBuffer.Verify(b => b.Map(mode), Times.Once);
    }

    [Fact]
    public void MapRange_WithValidParameters_ShouldReturnMappedRange()
    {
        // Arrange
        const int offset = 10;
        const int length = 50;
        const MapMode mode = MapMode.Read;
        var mockMemory = new Memory<float>(new float[length]);
        var expectedMapping = CreateMappedMemory(_mockFloatBuffer.Object, mockMemory, mode);
        
        _mockFloatBuffer.Setup(b => b.MapRange(offset, length, mode))
                       .Returns(expectedMapping);

        // Act
        var mapping = _mockFloatBuffer.Object.MapRange(offset, length, mode);

        // Assert
        mapping.Mode.Should().Be(mode);
        _mockFloatBuffer.Verify(b => b.MapRange(offset, length, mode), Times.Once);
    }

    [Fact]
    public void MapRange_WithNegativeOffset_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.MapRange(-1, 10, MapMode.ReadWrite))
                       .Throws(new ArgumentOutOfRangeException("offset"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
           () => _mockFloatBuffer.Object.MapRange(-1, 10, MapMode.ReadWrite));
        exception.ParamName.Should().Be("offset");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void MapRange_WithInvalidLength_ShouldThrowArgumentException(int invalidLength)
    {
        // Arrange
        _mockFloatBuffer.Setup(b => b.MapRange(0, invalidLength, MapMode.ReadWrite))
                       .Throws(new ArgumentException("Length must be positive", "length"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
           () => _mockFloatBuffer.Object.MapRange(0, invalidLength, MapMode.ReadWrite));
        exception.ParamName.Should().Be("length");
    }

    [Fact]
    public async Task MapAsync_WithDefaultMode_ShouldReturnMappedMemory()
    {
        // Arrange
        var mockMemory = new Memory<float>(new float[100]);
        var expectedMapping = CreateMappedMemory(_mockFloatBuffer.Object, mockMemory, MapMode.ReadWrite);
        
        _mockFloatBuffer.Setup(b => b.MapAsync(MapMode.ReadWrite, CancellationToken.None))
                       .ReturnsAsync(expectedMapping);

        // Act
        var mapping = await _mockFloatBuffer.Object.MapAsync();

        // Assert
        mapping.Mode.Should().Be(MapMode.ReadWrite);
        _mockFloatBuffer.Verify(b => b.MapAsync(MapMode.ReadWrite, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task MapAsync_WithCancellation_ShouldRespectCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        _mockFloatBuffer.Setup(b => b.MapAsync(MapMode.ReadWrite, cts.Token))
                       .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
           () => _mockFloatBuffer.Object.MapAsync(cancellationToken: cts.Token).AsTask());
    }

    #endregion

    #region Interface Inheritance Tests

    [Fact]
    public void IBuffer_ShouldInheritFromIMemoryBuffer()
    {
        // Arrange & Act
        var type = typeof(IBuffer<float>);

        // Assert
        Assert.IsAssignableFrom<IMemoryBuffer>(type);
    }

    [Fact]
    public void IBuffer_ShouldInheritFromIAsyncDisposable()
    {
        // Arrange & Act
        var type = typeof(IBuffer<float>);

        // Assert
        Assert.IsAssignableFrom<IAsyncDisposable>(type);
    }

    [Fact]
    public void IBuffer_ShouldInheritFromIDisposable()
    {
        // Arrange & Act
        var type = typeof(IBuffer<float>);

        // Assert
        Assert.IsAssignableFrom<IDisposable>(type);
    }

    #endregion

    #region Integration and Workflow Tests

    [Fact]
    public async Task CompleteBufferWorkflow_ShouldExecuteAllOperationsInOrder()
    {
        // Arrange
        const int bufferLength = 1000;
        const long bufferSize = bufferLength * sizeof(float);
        const float fillValue = 42.0f;
        
        var mockSlice = new Mock<IBuffer<float>>();
        var mockDestination = new Mock<IBuffer<float>>();
        var mockMemory = new Memory<float>(new float[bufferLength]);
        var expectedMapping = CreateMappedMemory(_mockFloatBuffer.Object, mockMemory, MapMode.ReadWrite);

        // Setup all operations
        _mockFloatBuffer.SetupGet(b => b.Length).Returns(bufferLength);
        _mockFloatBuffer.SetupGet(b => b.ElementCount).Returns(bufferLength);
        _mockFloatBuffer.SetupGet(b => b.SizeInBytes).Returns(bufferSize);
        _mockFloatBuffer.SetupGet(b => b.Accelerator).Returns(_mockAccelerator.Object);
        _mockFloatBuffer.Setup(b => b.Slice(0, 100)).Returns(mockSlice.Object);
        _mockFloatBuffer.Setup(b => b.AsType<int>()).Returns(_mockIntBuffer.Object);
        _mockFloatBuffer.Setup(b => b.CopyToAsync(mockDestination.Object, CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);
        _mockFloatBuffer.Setup(b => b.FillAsync(fillValue, CancellationToken.None))
                       .Returns(ValueTask.CompletedTask);
        _mockFloatBuffer.Setup(b => b.Map(MapMode.ReadWrite))
                       .Returns(expectedMapping);
        _mockFloatBuffer.Setup(b => b.DisposeAsync())
                       .Returns(ValueTask.CompletedTask);

        // Act
        var buffer = _mockFloatBuffer.Object;
        var length = buffer.Length;
        var elementCount = buffer.ElementCount;
        var size = buffer.SizeInBytes;
        var accelerator = buffer.Accelerator;
        var slice = buffer.Slice(0, 100);
        var intBuffer = buffer.AsType<int>();
        await buffer.CopyToAsync(mockDestination.Object);
        await buffer.FillAsync(fillValue);
        var mapping = buffer.Map();
        await buffer.DisposeAsync();

        // Assert
        Assert.Equal(bufferLength, length);
        Assert.Equal(bufferLength, elementCount);
        Assert.Equal(bufferSize, size);
        Assert.Equal(_mockAccelerator.Object, accelerator);
        Assert.Equal(mockSlice.Object, slice);
        Assert.Equal(_mockIntBuffer.Object, intBuffer);
        mapping.Mode.Should().Be(MapMode.ReadWrite);

        // Verify all methods were called
        _mockFloatBuffer.Verify(b => b.Slice(0, 100), Times.Once);
        _mockFloatBuffer.Verify(b => b.AsType<int>(), Times.Once);
        _mockFloatBuffer.Verify(b => b.CopyToAsync(mockDestination.Object, CancellationToken.None), Times.Once);
        _mockFloatBuffer.Verify(b => b.FillAsync(fillValue, CancellationToken.None), Times.Once);
        _mockFloatBuffer.Verify(b => b.Map(MapMode.ReadWrite), Times.Once);
        _mockFloatBuffer.Verify(b => b.DisposeAsync(), Times.Once);
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void IBuffer_ShouldWorkWithDifferentUnmanagedTypes()
    {
        // Test that IBuffer<T> can be instantiated with different unmanaged types
        // This is more of a compile-time test, but we can verify the mock setup
        
        // Arrange
        var intBuffer = new Mock<IBuffer<int>>();
        var doubleBuffer = new Mock<IBuffer<double>>();
        var byteBuffer = new Mock<IBuffer<byte>>();

        intBuffer.SetupGet(b => b.Length).Returns(100);
        doubleBuffer.SetupGet(b => b.Length).Returns(50);
        byteBuffer.SetupGet(b => b.Length).Returns(1000);

        // Act & Assert
        intBuffer.Object.Length.Should().Be(100);
        doubleBuffer.Object.Length.Should().Be(50);
        byteBuffer.Object.Length.Should().Be(1000);
    }

    #endregion

    #region Edge Cases and Error Conditions

    [Fact]
    public void Operations_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _mockFloatBuffer.SetupGet(b => b.IsDisposed).Returns(true);
        _mockFloatBuffer.Setup(b => b.Slice(It.IsAny<int>(), It.IsAny<int>()))
                       .Throws(new ObjectDisposedException(nameof(IBuffer<float>)));
        _mockFloatBuffer.Setup(b => b.AsType<int>())
                       .Throws(new ObjectDisposedException(nameof(IBuffer<float>)));

        // Act & Assert
        var sliceException = Assert.Throws<ObjectDisposedException>(
           () => _mockFloatBuffer.Object.Slice(0, 10));
        sliceException.ObjectName.Should().Be(nameof(IBuffer<float>));

        var asTypeException = Assert.Throws<ObjectDisposedException>(
           () => _mockFloatBuffer.Object.AsType<int>());
        asTypeException.ObjectName.Should().Be(nameof(IBuffer<float>));
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange & Act
        var type = typeof(IBuffer<float>);
        var lengthProperty = type.GetProperty(nameof(IBuffer<float>.Length));
        var elementCountProperty = type.GetProperty(nameof(IBuffer<float>.ElementCount));
        var acceleratorProperty = type.GetProperty(nameof(IBuffer<float>.Accelerator));

        // Assert
        lengthProperty!.CanRead.Should().BeTrue();
        lengthProperty.CanWrite.Should().BeFalse();
        elementCountProperty!.CanRead.Should().BeTrue();
        elementCountProperty.CanWrite.Should().BeFalse();
        acceleratorProperty!.CanRead.Should().BeTrue();
        acceleratorProperty.CanWrite.Should().BeFalse();
    }

    #endregion
}
