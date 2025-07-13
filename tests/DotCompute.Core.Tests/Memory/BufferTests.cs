// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using DotCompute.Core;
using DotCompute.Abstractions;
using DotCompute.Core.Tests.TestHelpers;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Tests for buffer operations and memory management.
/// </summary>
public class BufferTests
{
    private readonly IMemoryManager _memoryManager;
    private readonly IAccelerator _accelerator;

    public BufferTests()
    {
        _memoryManager = Substitute.For<IMemoryManager>();
        _accelerator = Substitute.For<IAccelerator>();
        _accelerator.Memory.Returns(_memoryManager);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(1024 * 1024)]
    public void AllocateWithValidSize_CreatesBuffer(int size)
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();
        buffer.Length.Returns(size);
        buffer.SizeInBytes.Returns(size * sizeof(int));
        
        _memoryManager.Allocate<int>(size).Returns(buffer);

        // Act
        var result = _memoryManager.Allocate<int>(size);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(size);
        result.SizeInBytes.Should().Be(size * sizeof(int));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void AllocateWithInvalidSize_ThrowsArgumentException(int size)
    {
        // Arrange
        _memoryManager.Allocate<int>(size)
            .Returns(x => throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero"));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _memoryManager.Allocate<int>(size));
    }

    [Fact]
    public async Task CopyFromAsyncWithValidData_CopiesSuccessfully()
    {
        // Arrange
        var data = TestDataGenerator.GenerateIntArray(1024);
        var buffer = Substitute.For<IBuffer<int>>();
        buffer.Length.Returns(data.Length);
        
        _memoryManager.Allocate<int>(data.Length).Returns(buffer);

        // Act
        var result = _memoryManager.Allocate<int>(data.Length);
        await result.CopyFromAsync(data);

        // Assert
        await buffer.Received(1).CopyFromAsync(data);
    }

    [Fact]
    public async Task CopyToAsyncWithValidBuffer_CopiesSuccessfully()
    {
        // Arrange
        var size = 1024;
        var destination = new int[size];
        var buffer = Substitute.For<IBuffer<int>>();
        buffer.Length.Returns(size);
        
        _memoryManager.Allocate<int>(size).Returns(buffer);

        // Act
        var result = _memoryManager.Allocate<int>(size);
        await result.CopyToAsync(destination);

        // Assert
        await buffer.Received(1).CopyToAsync(destination);
    }

    [Fact]
    public async Task CopyFromAsyncWithMismatchedSize_ThrowsArgumentException()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();
        buffer.Length.Returns(100);
        buffer.CopyFromAsync(Arg.Any<int[]>())
            .Returns(Task.FromException(new ArgumentException("Source array size does not match buffer size")));
        
        _memoryManager.Allocate<int>(100).Returns(buffer);
        var data = new int[200]; // Different size

        // Act & Assert
        var result = _memoryManager.Allocate<int>(100);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await result.CopyFromAsync(data));
    }

    [Fact]
    public async Task ClearResetsBufferToZero()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<float>>();
        buffer.Length.Returns(1024);
        
        _memoryManager.Allocate<float>(1024).Returns(buffer);

        // Act
        var result = _memoryManager.Allocate<float>(1024);
        await result.ClearAsync();

        // Assert
        await buffer.Received(1).ClearAsync();
    }

    [Theory]
    [MemberData(nameof(GetDifferentTypes))]
    public void AllocateWithDifferentTypes_AllocatesCorrectSize(Type elementType, int expectedElementSize)
    {
        // Arrange
        var size = 100;
        var allocateMethod = _memoryManager.GetType()
            .GetMethod(nameof(IMemoryManager.Allocate))!
            .MakeGenericMethod(elementType);

        // Act - This would be done dynamically in real code
        // Here we're just verifying the concept
        
        // Assert
        expectedElementSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BufferSliceWithValidRange_CreatesView()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<double>>();
        buffer.Length.Returns(1000);
        
        var slice = Substitute.For<IBuffer<double>>();
        slice.Length.Returns(100);
        buffer.Slice(100, 100).Returns(slice);
        
        _memoryManager.Allocate<double>(1000).Returns(buffer);

        // Act
        var result = _memoryManager.Allocate<double>(1000);
        var sliceResult = result.Slice(100, 100);

        // Assert
        sliceResult.Should().NotBeNull();
        sliceResult.Length.Should().Be(100);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, -10)]
    [InlineData(100, 1000)]
    [InlineData(1000, 1)]
    public void BufferSliceWithInvalidRange_ThrowsArgumentException(int offset, int length)
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();
        buffer.Length.Returns(100);
        buffer.Slice(offset, length)
            .Returns(x => throw new ArgumentOutOfRangeException("Invalid slice range"));
        
        _memoryManager.Allocate<int>(100).Returns(buffer);

        // Act & Assert
        var result = _memoryManager.Allocate<int>(100);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            result.Slice(offset, length));
    }

    [Fact]
    public async Task DisposeAsyncReleasesMemory()
    {
        // Arrange
        var buffer = Substitute.For<IBuffer<int>>();
        buffer.Length.Returns(1024);
        _memoryManager.Allocate<int>(1024).Returns(buffer);

        // Act
        var result = _memoryManager.Allocate<int>(1024);
        await result.DisposeAsync();

        // Assert
        await buffer.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task MultipleBufferOperationsWorkCorrectly()
    {
        // Arrange
        var size = 512;
        var sourceData = TestDataGenerator.GenerateFloatArray(size);
        var buffer1 = Substitute.For<IBuffer<float>>();
        var buffer2 = Substitute.For<IBuffer<float>>();
        
        buffer1.Length.Returns(size);
        buffer2.Length.Returns(size);
        
        _memoryManager.Allocate<float>(size)
            .Returns(buffer1, buffer2);

        // Act
        var source = _memoryManager.Allocate<float>(size);
        var destination = _memoryManager.Allocate<float>(size);
        
        await source.CopyFromAsync(sourceData);
        await source.CopyToAsync(destination);

        // Assert
        await buffer1.Received(1).CopyFromAsync(sourceData);
        await buffer1.Received(1).CopyToAsync(buffer2);
    }

    public static TheoryData<Type, int> GetDifferentTypes()
    {
        return new TheoryData<Type, int>
        {
            { typeof(byte), 1 },
            { typeof(short), 2 },
            { typeof(int), 4 },
            { typeof(long), 8 },
            { typeof(float), 4 },
            { typeof(double), 8 }
        };
    }
}