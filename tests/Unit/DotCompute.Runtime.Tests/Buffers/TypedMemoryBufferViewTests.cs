// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: TypedMemoryBufferView class does not exist in Runtime module yet
// TODO: Uncomment when TypedMemoryBufferView is implemented in DotCompute.Runtime.Memory
/*
using DotCompute.Runtime.Memory;
using FluentAssertions;
using Xunit;

namespace DotCompute.Runtime.Tests.Buffers;

/// <summary>
/// Tests for TypedMemoryBufferView
/// </summary>
public sealed class TypedMemoryBufferViewTests : IDisposable
{
    private TypedMemoryBufferWrapper<int>? _buffer;
    private TypedMemoryBufferView<int>? _view;

    public void Dispose()
    {
        _view?.Dispose();
        _buffer?.Dispose();
    }

    [Fact]
    public void Constructor_WithValidBuffer_CreatesView()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);

        // Act
        _view = new TypedMemoryBufferView<int>(_buffer, 0, 50);

        // Assert
        _view.Should().NotBeNull();
        _view.Length.Should().Be(50);
    }

    [Fact]
    public void Constructor_WithNullBuffer_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new TypedMemoryBufferView<int>(null!, 0, 10);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithInvalidOffset_ThrowsArgumentException()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(10);

        // Act
        var action = () => new TypedMemoryBufferView<int>(_buffer, 20, 5);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithInvalidLength_ThrowsArgumentException()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(10);

        // Act
        var action = () => new TypedMemoryBufferView<int>(_buffer, 5, 10);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Indexer_Get_ReturnsCorrectValue()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);
        _buffer[10] = 42;
        _view = new TypedMemoryBufferView<int>(_buffer, 5, 20);

        // Act
        var value = _view[5]; // Offset 5 in view = index 10 in buffer

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void Indexer_Set_SetsCorrectValue()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);
        _view = new TypedMemoryBufferView<int>(_buffer, 5, 20);

        // Act
        _view[5] = 123;

        // Assert
        _buffer[10].Should().Be(123);
    }

    [Fact]
    public void GetSpan_ReturnsViewedSpan()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);
        _view = new TypedMemoryBufferView<int>(_buffer, 10, 30);

        // Act
        var span = _view.GetSpan();

        // Assert
        span.Length.Should().Be(30);
    }

    [Fact]
    public void Slice_CreatesSubView()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);
        _view = new TypedMemoryBufferView<int>(_buffer, 0, 50);

        // Act
        var subView = _view.Slice(10, 20);

        // Assert
        subView.Length.Should().Be(20);
    }

    [Fact]
    public void Slice_WithInvalidParameters_ThrowsArgumentException()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);
        _view = new TypedMemoryBufferView<int>(_buffer, 0, 50);

        // Act
        var action = () => _view.Slice(40, 20);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CopyTo_CopiesViewedData()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);
        for (int i = 0; i < 100; i++)
            _buffer[i] = i;
        _view = new TypedMemoryBufferView<int>(_buffer, 10, 5);
        var destination = new int[5];

        // Act
        _view.CopyTo(destination);

        // Assert
        destination.Should().Equal(10, 11, 12, 13, 14);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        _buffer = new TypedMemoryBufferWrapper<int>(100);
        _view = new TypedMemoryBufferView<int>(_buffer, 0, 50);

        // Act
        _view.Dispose();

        // Assert - should not throw
        _view.Dispose();
    }
}
*/