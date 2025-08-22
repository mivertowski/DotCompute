using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using NSubstitute;
using Xunit;

namespace DotCompute.Memory.Tests;


public sealed class UnifiedBufferTests : IDisposable
{
    private readonly IMemoryManager _memoryManager;

    public UnifiedBufferTests()
    {
        _memoryManager = Substitute.For<IMemoryManager>();
    }

    public void Dispose()
        // Cleanup if needed

        => GC.SuppressFinalize(this);

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Act
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 1024);

        // Assert
        _ = buffer.Length.Should().Be(1024);
        _ = buffer.SizeInBytes.Should().Be(1024 * sizeof(int));
    }

    [Fact]
    public void Constructor_WithZeroLength_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act = () => new UnifiedBuffer<int>(_memoryManager, 0);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Fact]
    public void Constructor_WithNullMemoryManager_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new UnifiedBuffer<int>(null!, 1024);
        _ = Assert.Throws<ArgumentNullException>(() => act());
    }

    [Fact]
    public void Constructor_WithInitialData_ShouldPopulateBuffer()
    {
        // Arrange
        var data = new[] { 1, 2, 3, 4, 5 };

        // Act
        using var buffer = new UnifiedBuffer<int>(_memoryManager, data);

        // Assert
        _ = buffer.Length.Should().Be(5);
        var span = buffer.AsSpan();
        for (var i = 0; i < data.Length; i++)
        {
            _ = span[i].Should().Be(data[i]);
        }
    }

    [Fact]
    public void AsSpan_ShouldReturnCorrectSpan()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 10);

        // Act
        var span = buffer.AsSpan();

        // Assert
        _ = span.Length.Should().Be(10);
    }

    [Fact]
    public void State_ShouldTrackBufferState()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 10);

        // Assert - Buffer is allocated on host at creation
        _ = buffer.State.Should().Be(BufferState.HostOnly);

        // Act - Write to span
        var span = buffer.AsSpan();
        span[0] = 42;

        // State should change after modification
        // Note: actual state transition depends on implementation
    }

    [Fact]
    public void IsOnHost_ShouldBeTrue_ForNewBuffer()
    {
        // Arrange & Act
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 10);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void IsOnDevice_ShouldBeFalse_ForNewBuffer()
    {
        // Arrange & Act
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 10);

        // Assert
        _ = buffer.IsOnDevice.Should().BeFalse();
    }

    [Fact]
    public async Task CopyToAsync_WithMemory_ShouldCopyData()
    {
        // Arrange
        var sourceData = new[] { 1, 2, 3, 4, 5 };
        using var buffer = new UnifiedBuffer<int>(_memoryManager, sourceData);
        var destination = new int[5];

        // Act
        await buffer.CopyToAsync(destination);

        // Assert
        _ = destination.Should().BeEquivalentTo(sourceData);
    }

    [Fact]
    public async Task CopyToAsync_WithBuffer_ShouldCopyData()
    {
        // Arrange
        var sourceData = new[] { 1, 2, 3, 4, 5 };
        using var source = new UnifiedBuffer<int>(_memoryManager, sourceData);
        using var destination = new UnifiedBuffer<int>(_memoryManager, 5);

        // Act
        await source.CopyToAsync(destination);

        // Assert
        var destSpan = destination.AsSpan();
        for (var i = 0; i < sourceData.Length; i++)
        {
            _ = destSpan[i].Should().Be(sourceData[i]);
        }
    }

    [Theory]
    [InlineData(typeof(byte), 1)]
    [InlineData(typeof(short), 2)]
    [InlineData(typeof(int), 4)]
    [InlineData(typeof(long), 8)]
    [InlineData(typeof(float), 4)]
    [InlineData(typeof(double), 8)]
    [RequiresDynamicCode("This test uses reflection and Activator.CreateInstance")]
    [RequiresUnreferencedCode("This test uses reflection to create generic types")]
    public void SizeInBytes_ForDifferentTypes_ShouldBeCorrect(Type type, int expectedElementSize)
    {
        // Act
        var bufferType = typeof(UnifiedBuffer<>).MakeGenericType(type);
        var buffer = Activator.CreateInstance(bufferType, _memoryManager, 10);
        var sizeInBytes = bufferType.GetProperty("SizeInBytes")!.GetValue(buffer);

        // Assert
        Assert.Equal(10L * expectedElementSize, sizeInBytes);

        // Cleanup
        ((IDisposable)buffer!).Dispose();
    }

    [Fact]
    public void Accelerator_ShouldReturnNull_ForUnifiedBuffer()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 10);

        // Act & Assert
        _ = buffer.Accelerator.Should().BeNull();
    }

    [Fact]
    public void Options_ShouldReturnNone()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 10);

        // Act & Assert
        _ = buffer.Options.Should().Be(Abstractions.MemoryOptions.None);
    }

    [Fact]
    public void IsDirty_ShouldBeFalse_ForNewBuffer()
    {
        // Arrange
        using var buffer = new UnifiedBuffer<int>(_memoryManager, 10);

        // Act & Assert
        _ = buffer.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void MultipleBuffers_ShouldBeIndependent()
    {
        // Arrange
        using var buffer1 = new UnifiedBuffer<int>(_memoryManager, 5);
        using var buffer2 = new UnifiedBuffer<int>(_memoryManager, 5);

        var span1 = buffer1.AsSpan();
        var span2 = buffer2.AsSpan();

        // Act
        for (var i = 0; i < 5; i++)
        {
            span1[i] = i;
            span2[i] = i * 10;
        }

        // Assert
        for (var i = 0; i < 5; i++)
        {
            _ = span1[i].Should().Be(i);
            _ = span2[i].Should().Be(i * 10);
        }
    }
}
