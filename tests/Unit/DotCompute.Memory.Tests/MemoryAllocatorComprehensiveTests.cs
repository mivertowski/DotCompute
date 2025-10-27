// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for MemoryAllocator covering all critical scenarios.
/// Part of Phase 1: Memory Module testing to achieve 80% coverage.
/// </summary>
public sealed class MemoryAllocatorComprehensiveTests : IDisposable
{
    private readonly MemoryAllocator _allocator;
    private readonly List<IDisposable> _disposables = [];

    public MemoryAllocatorComprehensiveTests()
    {
        _allocator = new MemoryAllocator();
        _disposables.Add(_allocator);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region AllocateAligned Tests

    [Fact]
    public void AllocateAligned_WithValidParameters_AllocatesMemory()
    {
        // Arrange
        const int length = 1024;
        const int alignment = 64;

        // Act
        using var owner = _allocator.AllocateAligned<float>(length, alignment);

        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().Be(length);
        _allocator.TotalAllocations.Should().Be(1);
        _allocator.TotalAllocatedBytes.Should().Be(length * sizeof(float));
    }

    [Fact]
    public void AllocateAligned_WithDefaultAlignment_UsesDefaultAlignment()
    {
        // Act
        using var owner = _allocator.AllocateAligned<int>(100);

        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().Be(100);
        _allocator.TotalAllocations.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void AllocateAligned_WithInvalidLength_ThrowsArgumentOutOfRangeException(int invalidLength)
    {
        // Act
        Action act = () => _allocator.AllocateAligned<float>(invalidLength, 64);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void AllocateAligned_WithNegativeAlignment_ThrowsArgumentOutOfRangeException(int invalidAlignment)
    {
        // Act
        Action act = () => _allocator.AllocateAligned<float>(100, invalidAlignment);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(15)]
    public void AllocateAligned_WithNonPowerOf2Alignment_ThrowsArgumentException(int invalidAlignment)
    {
        // Act
        Action act = () => _allocator.AllocateAligned<float>(100, invalidAlignment);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*power of 2*");
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    public void AllocateAligned_WithValidPowerOf2Alignments_Succeeds(int alignment)
    {
        // Act
        using var owner = _allocator.AllocateAligned<byte>(alignment, alignment);

        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().Be(alignment);
    }

    [Fact]
    public void AllocateAligned_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _allocator.Dispose();

        // Act
        Action act = () => _allocator.AllocateAligned<int>(100, 16);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region AllocatePinned Tests

    [Fact]
    public void AllocatePinned_WithValidLength_AllocatesPinnedMemory()
    {
        // Arrange
        const int length = 512;

        // Act
        using var owner = _allocator.AllocatePinned<double>(length);

        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().Be(length);
        _allocator.TotalAllocations.Should().Be(1);
        _allocator.TotalAllocatedBytes.Should().Be(length * sizeof(double));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void AllocatePinned_WithInvalidLength_ThrowsArgumentOutOfRangeException(int invalidLength)
    {
        // Act
        Action act = () => _allocator.AllocatePinned<float>(invalidLength);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AllocatePinned_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _allocator.Dispose();

        // Act
        Action act = () => _allocator.AllocatePinned<int>(100);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void AllocatePinned_AllowsMemoryModification()
    {
        // Arrange
        using var owner = _allocator.AllocatePinned<int>(10);

        // Act
        var span = owner.Memory.Span;
        span[0] = 42;
        span[9] = 99;

        // Assert
        owner.Memory.Span[0].Should().Be(42);
        owner.Memory.Span[9].Should().Be(99);
    }

    #endregion

    #region Allocate Tests

    [Fact]
    public void Allocate_WithValidLength_AllocatesMemory()
    {
        // Act
        using var owner = _allocator.Allocate<byte>(256);

        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().Be(256);
        _allocator.TotalAllocations.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Allocate_WithInvalidLength_ThrowsArgumentOutOfRangeException(int invalidLength)
    {
        // Act
        Action act = () => _allocator.Allocate<int>(invalidLength);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Allocate_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _allocator.Dispose();

        // Act
        Action act = () => _allocator.Allocate<float>(100);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region CreateUnifiedBuffer Tests

    [Fact]
    public void CreateUnifiedBuffer_WithValidParameters_CreatesBuffer()
    {
        // Arrange
        var mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);

        // Act
        var buffer = _allocator.CreateUnifiedBuffer<float>(mockMemoryManager, 1024);
        _disposables.Add(buffer);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(1024);
    }

    [Fact]
    public void CreateUnifiedBuffer_WithNullMemoryManager_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _allocator.CreateUnifiedBuffer<int>(null!, 100);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateUnifiedBuffer_WithInvalidLength_ThrowsArgumentOutOfRangeException(int invalidLength)
    {
        // Arrange
        var mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);

        // Act
        Action act = () => _allocator.CreateUnifiedBuffer<float>(mockMemoryManager, invalidLength);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateUnifiedBuffer_WithInitialData_PopulatesBuffer()
    {
        // Arrange
        var mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);
        var data = new float[] { 1.0f, 2.0f, 3.0f };

        // Act
        var buffer = _allocator.CreateUnifiedBuffer<float>(mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(3);
        var span = buffer.AsReadOnlySpan();
        span.ToArray().Should().Equal(data);
    }

    [Fact]
    public void CreateUnifiedBuffer_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);
        _allocator.Dispose();

        // Act
        Action act = () => _allocator.CreateUnifiedBuffer<int>(mockMemoryManager, 100);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_InitialState_ReturnsZeroStatistics()
    {
        // Act
        var stats = _allocator.GetStatistics();

        // Assert
        stats.TotalAllocatedBytes.Should().Be(0);
        stats.TotalAllocations.Should().Be(0);
        stats.TotalDeallocations.Should().Be(0);
        stats.ActiveAllocations.Should().Be(0);
    }

    [Fact]
    public void GetStatistics_AfterAllocations_ReturnsCorrectStatistics()
    {
        // Arrange
        using var owner1 = _allocator.Allocate<int>(100);
        using var owner2 = _allocator.Allocate<float>(200);

        // Act
        var stats = _allocator.GetStatistics();

        // Assert
        stats.TotalAllocations.Should().Be(2);
        stats.TotalAllocatedBytes.Should().Be((100 * sizeof(int)) + (200 * sizeof(float)));
        stats.ActiveAllocations.Should().Be(2);
    }

    [Fact]
    public void GetStatistics_AfterDeallocations_UpdatesCorrectly()
    {
        // Arrange
        var owner1 = _allocator.Allocate<int>(100);
        var owner2 = _allocator.Allocate<float>(200);

        // Act - Dispose first allocation
        owner1.Dispose();
        var stats = _allocator.GetStatistics();

        // Assert
        stats.TotalAllocations.Should().Be(2);
        stats.TotalDeallocations.Should().Be(1);
        stats.ActiveAllocations.Should().Be(1);

        // Cleanup
        owner2.Dispose();
    }

    [Fact]
    public void GetStatistics_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        _allocator.Dispose();

        // Act
        Action act = () => _allocator.GetStatistics();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void TotalAllocatedBytes_DecreasesAfterDeallocation()
    {
        // Arrange
        const int length = 100;
        const long expectedBytes = length * sizeof(int);
        var owner = _allocator.Allocate<int>(length);

        // Act
        var bytesBeforeDispose = _allocator.TotalAllocatedBytes;
        owner.Dispose();
        var bytesAfterDispose = _allocator.TotalAllocatedBytes;

        // Assert
        bytesBeforeDispose.Should().Be(expectedBytes);
        bytesAfterDispose.Should().Be(0);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Act
        _allocator.Dispose();
        Action act = () => _allocator.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_AfterAllocations_AllowsDisposal()
    {
        // Arrange
        using var owner = _allocator.Allocate<int>(100);

        // Act
        Action act = () => _allocator.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public void Allocate_WithMinimumLength_WorksCorrectly()
    {
        // Act
        using var owner = _allocator.Allocate<byte>(1);

        // Assert
        owner.Memory.Length.Should().Be(1);
        _allocator.TotalAllocations.Should().Be(1);
    }

    [Fact]
    public void Allocate_WithLargeLength_AllocatesSuccessfully()
    {
        // Arrange
        const int largeSize = 10_000_000; // 10 million bytes

        // Act
        using var owner = _allocator.Allocate<byte>(largeSize);

        // Assert
        owner.Memory.Length.Should().Be(largeSize);
        _allocator.TotalAllocatedBytes.Should().Be(largeSize);
    }

    [Fact]
    public void MultipleAllocations_TrackStatisticsCorrectly()
    {
        // Arrange & Act
        using var owner1 = _allocator.Allocate<int>(100);
        using var owner2 = _allocator.AllocatePinned<float>(200);
        using var owner3 = _allocator.AllocateAligned<byte>(300, 64);

        // Assert
        _allocator.TotalAllocations.Should().Be(3);
        var expectedBytes = (100 * sizeof(int)) + (200 * sizeof(float)) + (300 * sizeof(byte));
        _allocator.TotalAllocatedBytes.Should().Be(expectedBytes);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentAllocations_UpdateStatisticsSafely()
    {
        // Arrange
        const int concurrentTasks = 10;
        const int allocationsPerTask = 100;

        // Act
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async _ =>
        {
            await Task.Yield();
            for (int i = 0; i < allocationsPerTask; i++)
            {
                using var owner = _allocator.Allocate<int>(10);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        var stats = _allocator.GetStatistics();
        stats.TotalAllocations.Should().Be(concurrentTasks * allocationsPerTask);
        stats.TotalDeallocations.Should().Be(concurrentTasks * allocationsPerTask);
        stats.ActiveAllocations.Should().Be(0);
    }

    #endregion

    #region Type-Specific Tests

    [Fact]
    public void Allocate_WorksWithFloatType()
    {
        // Act
        using var owner = _allocator.Allocate<float>(100);
        owner.Memory.Span[0] = 3.14f;

        // Assert
        owner.Memory.Span[0].Should().BeApproximately(3.14f, 0.0001f);
    }

    [Fact]
    public void Allocate_WorksWithDoubleType()
    {
        // Act
        using var owner = _allocator.Allocate<double>(100);
        owner.Memory.Span[0] = Math.PI;

        // Assert
        owner.Memory.Span[0].Should().BeApproximately(Math.PI, 1e-10);
    }

    [Fact]
    public void Allocate_WorksWithByteType()
    {
        // Act
        using var owner = _allocator.Allocate<byte>(256);
        var span = owner.Memory.Span;
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = (byte)i;
        }

        // Assert
        owner.Memory.Span[0].Should().Be(0);
        owner.Memory.Span[255].Should().Be(255);
    }

    #endregion

    #region Memory Owner Disposal Tests

    [Fact]
    public void AlignedMemoryOwner_Dispose_UpdatesStatistics()
    {
        // Arrange
        var owner = _allocator.AllocateAligned<int>(100, 64);
        var initialDeallocations = _allocator.TotalDeallocations;

        // Act
        owner.Dispose();

        // Assert
        _allocator.TotalDeallocations.Should().Be(initialDeallocations + 1);
    }

    [Fact]
    public void PinnedMemoryOwner_Dispose_UpdatesStatistics()
    {
        // Arrange
        var owner = _allocator.AllocatePinned<float>(100);
        var initialDeallocations = _allocator.TotalDeallocations;

        // Act
        owner.Dispose();

        // Assert
        _allocator.TotalDeallocations.Should().Be(initialDeallocations + 1);
    }

    [Fact]
    public void MemoryOwner_DisposeCalledMultipleTimes_IsSafe()
    {
        // Arrange
        var owner = _allocator.Allocate<int>(100);

        // Act
        owner.Dispose();
        Action act = () => owner.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
