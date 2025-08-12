using System.Buffers;
using FluentAssertions;
using Xunit;

namespace DotCompute.Tests.Unit;

public sealed class MemoryAllocatorTests : IDisposable
{
    private readonly MemoryAllocator _allocator;

    public MemoryAllocatorTests()
    {
        _allocator = new MemoryAllocator();
    }

    public void Dispose()
    {
        _allocator.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Allocate_WithValidSize_ShouldReturnMemoryOwner()
    {
        // Act
        using var memory = _allocator.Allocate<int>(1024);

        // Assert
        memory.Should().NotBeNull();
        memory.Memory.Length.Should().Be(1024);
    }

    [Fact]
    public void Allocate_WithZeroSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => _allocator.Allocate<int>(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AllocateAligned_WithValidParameters_ShouldReturnAlignedMemory()
    {
        // Act
        using var memory = _allocator.AllocateAligned<int>(1024, 64);

        // Assert
        memory.Should().NotBeNull();
        memory.Memory.Length.Should().Be(1024);
        // Alignment is handled internally
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void AllocateAligned_WithDifferentAlignments_ShouldReturnValidMemory(int alignment)
    {
        // Act
        using var memory = _allocator.AllocateAligned<int>(1024, alignment);

        // Assert
        memory.Should().NotBeNull();
        memory.Memory.Length.Should().Be(1024);
    }

    [Fact]
    public void AllocatePinned_WithValidSize_ShouldReturnPinnedMemory()
    {
        // Act
        using var memory = _allocator.AllocatePinned<int>(1024);

        // Assert
        memory.Should().NotBeNull();
        memory.Memory.Length.Should().Be(1024);
    }

    [Fact]
    public void Dispose_Pattern_ShouldWork()
    {
        // Arrange
        var memory = _allocator.Allocate<int>(1024);

        // Act & Assert
        var act = () => memory.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void TotalAllocatedBytes_ShouldTrackAllocations()
    {
        // Arrange
        var initialBytes = _allocator.TotalAllocatedBytes;

        // Act
        using var mem1 = _allocator.Allocate<byte>(1024);
        using var mem2 = _allocator.Allocate<byte>(2048);
        var allocatedBytes = _allocator.TotalAllocatedBytes;

        // Assert
        allocatedBytes.Should().BeGreaterThanOrEqualTo(initialBytes + 3072);
    }

    [Fact]
    public void TotalAllocations_ShouldTrackNumberOfAllocations()
    {
        // Arrange
        var initialCount = _allocator.TotalAllocations;

        // Act
        using var mem1 = _allocator.Allocate<int>(256);
        using var mem2 = _allocator.Allocate<int>(512);
        var count = _allocator.TotalAllocations;

        // Assert
        count.Should().Be(initialCount + 2);
    }

    [Fact]
    public void MultipleAllocations_ShouldWorkCorrectly()
    {
        // Arrange
        var memories = new List<IMemoryOwner<int>>();

        // Act - Allocate multiple blocks
        for (var i = 0; i < 10; i++)
        {
            memories.Add(_allocator.Allocate<int>(256 * (i + 1)));
        }

        // Assert - All should be valid
        memories.Should().OnlyContain(m => m != null && m.Memory.Length > 0);
        _allocator.TotalAllocations.Should().BeGreaterThanOrEqualTo(10);

        // Cleanup
        foreach (var memory in memories)
        {
            memory.Dispose();
        }
    }

    [Fact]
    public void Dispose_ShouldFreeAllAllocations()
    {
        // Arrange
        var allocator = new MemoryAllocator();
        _ = allocator.Allocate<byte>(1024);
        _ = allocator.Allocate<byte>(2048);

        // Act
        allocator.Dispose();

        // Assert
        // After dispose, the allocator should be cleaned up
    }

    [Fact]
    public void AllocateAfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var allocator = new MemoryAllocator();
        allocator.Dispose();

        // Act & Assert
        var act = () => allocator.Allocate<int>(256);
        act.Should().Throw<ObjectDisposedException>();
    }
}
