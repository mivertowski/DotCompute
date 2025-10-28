// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: TypedMemoryBufferWrapper class does not exist in Runtime module yet
// TODO: Uncomment when TypedMemoryBufferWrapper is implemented in DotCompute.Runtime.Services.Buffers
/*
using DotCompute.Abstractions;
using DotCompute.Runtime.Services.Buffers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Buffers;

/// <summary>
/// Tests for TypedMemoryBufferWrapper
/// </summary>
public sealed class TypedMemoryBufferWrapperTests : IDisposable
{
    private IUnifiedMemoryBuffer? _mockBuffer;
    private TypedMemoryBufferWrapper<int>? _wrapper;

    public void Dispose()
    {
        _wrapper?.Dispose();
    }

    [Fact]
    public void Constructor_WithValidSize_CreatesWrapper()
    {
        // Arrange
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();

        // Act
        _wrapper = new TypedMemoryBufferWrapper<int>(_mockBuffer, 100);

        // Assert
        _wrapper.Should().NotBeNull();
        _wrapper.Length.Should().Be(100);
    }

    [Fact]
    public void Constructor_WithNullBuffer_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new TypedMemoryBufferWrapper<int>(null!, 100);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithZeroSize_CreatesWrapper()
    {
        // Arrange
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();

        // Act
        _wrapper = new TypedMemoryBufferWrapper<int>(_mockBuffer, 0);

        // Assert - zero size is valid
        _wrapper.Length.Should().Be(0);
    }

    [Fact]
    public void Length_ReturnsCorrectValue()
    {
        // Arrange
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();
        _wrapper = new TypedMemoryBufferWrapper<int>(_mockBuffer, 100);

        // Act
        var length = _wrapper.Length;

        // Assert
        length.Should().Be(100);
    }

    [Fact]
    public void SizeInBytes_CalculatesCorrectly()
    {
        // Arrange
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();
        _wrapper = new TypedMemoryBufferWrapper<int>(_mockBuffer, 10);

        // Act
        var size = _wrapper.SizeInBytes;

        // Assert
        size.Should().Be(10 * sizeof(int));
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();
        _wrapper = new TypedMemoryBufferWrapper<int>(_mockBuffer, 10);

        // Act
        _wrapper.Dispose();

        // Assert - should not throw
        _wrapper.Dispose();
    }

    [Fact]
    public void GetSpan_ReturnsValidSpan()
    {
        // Arrange
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();
        _wrapper = new TypedMemoryBufferWrapper<int>(_mockBuffer, 10);

        // Act
        var span = _wrapper.GetSpan();

        // Assert
        span.Length.Should().Be(10);
    }

    [Fact]
    public void GetMemory_ReturnsValidMemory()
    {
        // Arrange
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();
        _wrapper = new TypedMemoryBufferWrapper<int>(_mockBuffer, 10);

        // Act
        var memory = _wrapper.GetMemory();

        // Assert
        memory.Length.Should().Be(10);
    }
}
*/