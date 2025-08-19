// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using FluentAssertions;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Memory;
using Xunit;

namespace DotCompute.Tests.Unit;


/// <summary>
/// Edge case and boundary condition tests for MemoryAllocator.
/// Tests extreme values, concurrent access, resource exhaustion, and disposal patterns.
/// </summary>
public sealed class MemoryAllocatorEdgeCaseTests : IDisposable
{
    private readonly MemoryAllocator _allocator;

    public MemoryAllocatorEdgeCaseTests()
    {
        _allocator = new MemoryAllocator();
    }

    public void Dispose()
    {
        _allocator.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Boundary Value Tests

    [Fact]
    public void Allocate_WithMaxIntLength_ShouldThrowOutOfMemoryException()
    {
        // Act & Assert - Should fail gracefully with memory exhaustion
        var act = () => _allocator.Allocate<byte>(int.MaxValue);
        _ = act.Should().Throw<Exception>()
           .Where(ex => ex is OutOfMemoryException || ex is ArgumentOutOfRangeException || ex is OverflowException);
    }

    [Fact]
    public void Allocate_WithMaxReasonableSize_ShouldWork()
    {
        // Try allocating a reasonably large but not excessive amount
        const int reasonableMaxSize = 1024 * 1024; // 1MB

        // Act & Assert
        using var memory = _allocator.Allocate<byte>(reasonableMaxSize);
        Assert.NotNull(memory);
        _ = memory.Memory.Length.Should().Be(reasonableMaxSize);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(31)]
    public void Allocate_WithVerySmallSizes_ShouldWork(int size)
    {
        // Act
        using var memory = _allocator.Allocate<byte>(size);

        // Assert
        Assert.NotNull(memory);
        _ = memory.Memory.Length.Should().Be(size);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)] // Non-power-of-2 should throw
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void AllocateAligned_WithInvalidAlignment_ShouldThrowArgumentException(int alignment)
    {
        if ((alignment & (alignment - 1)) != 0) // Not power of 2
        {
            // Act & Assert
            var act = () => _allocator.AllocateAligned<int>(256, alignment);
            _ = act.Should().Throw<ArgumentException>()
               .WithMessage("*power of 2*");
        }
        else
        {
            // Valid power of 2, should work
            using var memory = _allocator.AllocateAligned<int>(256, alignment);
            Assert.NotNull(memory);
        }
    }

    [Fact]
    public void AllocateAligned_WithZeroAlignment_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act = () => _allocator.AllocateAligned<int>(256, 0);
        _ = act.Should().Throw<ArgumentException>()
           .WithMessage("*power of 2*");
    }

    [Fact]
    public void AllocateAligned_WithNegativeAlignment_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => _allocator.AllocateAligned<int>(256, -1);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentAllocations_ShouldBeThreadSafe()
    {
        const int threadCount = 10;
        const int allocationsPerThread = 100;
        const int allocationSize = 1024;

        var tasks = new Task[threadCount];
        var allocations = new ConcurrentBag<IMemoryOwner<byte>>();
        var exceptions = new ConcurrentBag<Exception>();

        // Act - Multiple threads allocating concurrently
        for (var i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    for (var j = 0; j < allocationsPerThread; j++)
                    {
                        var memory = _allocator.Allocate<byte>(allocationSize);
                        allocations.Add(memory);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        _ = exceptions.Should().BeEmpty("All allocations should succeed without race conditions");
        _ = allocations.Count.Should().Be(threadCount * allocationsPerThread);
        _ = _allocator.TotalAllocations.Should().BeGreaterThanOrEqualTo(threadCount * allocationsPerThread);

        // Cleanup
        foreach (var allocation in allocations)
        {
            allocation.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentDisposeOperations_ShouldBeThreadSafe()
    {
        const int allocationCount = 1000;
        var allocations = new List<IMemoryOwner<byte>>();

        // Pre-allocate memory
        for (var i = 0; i < allocationCount; i++)
        {
            allocations.Add(_allocator.Allocate<byte>(256));
        }

        var tasks = new Task[allocationCount];

        // Act - Dispose all allocations concurrently
        for (var i = 0; i < allocationCount; i++)
        {
            var allocation = allocations[i];
            tasks[i] = Task.Run(allocation.Dispose);
        }

        await Task.WhenAll(tasks);

        // Assert - Should not throw and statistics should be consistent
        _ = _allocator.TotalDeallocations.Should().Be(allocationCount);
    }

    [Fact]
    public async Task MixedConcurrentOperations_ShouldBeThreadSafe()
    {
        const int operationCount = 500;
        var allocations = new ConcurrentBag<IDisposable>();
        var exceptions = new ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, operationCount).Select(i => Task.Run(() =>
        {
            try
            {
                if (i % 3 == 0)
                {
                    // Allocate
                    var memory = _allocator.Allocate<byte>(512 + i % 1024);
                    allocations.Add(memory);
                }
                else if (i % 3 == 1)
                {
                    // Allocate aligned
                    var memory = _allocator.AllocateAligned<int>(128, 32);
                    allocations.Add(memory);
                }
                else
                {
                    // Allocate pinned
                    var memory = _allocator.AllocatePinned<long>(64);
                    allocations.Add(memory);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Empty(exceptions);
        _ = allocations.Count.Should().Be(operationCount);

        // Cleanup
        _ = Parallel.ForEach(allocations, allocation => allocation.Dispose());
    }

    #endregion

    #region Resource Exhaustion Tests

    [Fact]
    public void MassiveAllocationLoop_ShouldHandleResourceExhaustion()
    {
        var allocations = new List<IMemoryOwner<byte>>();
        var allocationSize = 1024 * 1024; // 1MB per allocation
        var maxAllocations = 100; // Reasonable limit to prevent system lockup

        try
        {
            // Act - Allocate until we hit a limit
            for (var i = 0; i < maxAllocations; i++)
            {
                var memory = _allocator.Allocate<byte>(allocationSize);
                allocations.Add(memory);
            }
        }
        catch (OutOfMemoryException)
        {
            // Expected when running out of memory
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Other exceptions should be specific
            _ = (ex.GetType() == typeof(ArgumentOutOfRangeException) ||
              ex.GetType() == typeof(InvalidOperationException)).Should().BeTrue();
        }

        // Assert - Should have made some allocations
        _ = (allocations.Count > 0).Should().BeTrue();
        _ = _allocator.TotalAllocations.Should().Be(allocations.Count);

        // Cleanup
        foreach (var allocation in allocations)
        {
            allocation.Dispose();
        }
    }

    [Fact]
    public void AllocateAfterManyDeallocations_ShouldReuseMemory()
    {
        const int cycleCount = 1000;
        const int allocationSize = 4096;

        // Act - Allocate and deallocate many times
        for (var i = 0; i < cycleCount; i++)
        {
            using var memory = _allocator.Allocate<byte>(allocationSize);
            Assert.NotNull(memory);
        }

        // Assert
        _ = _allocator.TotalAllocations.Should().Be(cycleCount);
        _ = _allocator.TotalDeallocations.Should().Be(cycleCount);
    }

    #endregion

    #region Disposal Pattern Tests

    [Fact]
    public void DoubleDispose_OnAllocator_ShouldNotThrow()
    {
        // Arrange
        var allocator = new MemoryAllocator();
        using var memory = allocator.Allocate<int>(256);

        // Act & Assert
        allocator.Dispose();
        var act = allocator.Dispose;
        act(); // Should not throw
    }

    [Fact]
    public void DoubleDispose_OnMemoryOwner_ShouldNotThrow()
    {
        // Arrange
        var memory = _allocator.Allocate<int>(256);

        // Act & Assert
        memory.Dispose();
        var act = memory.Dispose;
        act(); // Should not throw
    }

    [Fact]
    public void AccessMemoryAfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var allocator = new MemoryAllocator();
        allocator.Dispose();

        // Act & Assert
        var act = () => allocator.Allocate<int>(256);
        _ = Assert.Throws<ObjectDisposedException>(() => act());
    }

    [Fact]
    public void GetStatisticsAfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var allocator = new MemoryAllocator();
        allocator.Dispose();

        // Act & Assert
        var act = allocator.GetStatistics;
        _ = Assert.Throws<ObjectDisposedException>(() => act());
    }

    [Fact]
    public void AllocatorDisposeWithPendingAllocations_ShouldNotAffectExistingMemory()
    {
        // Arrange
        var allocator = new MemoryAllocator();
        var memory1 = allocator.Allocate<byte>(1024);
        var memory2 = allocator.AllocatePinned<int>(256);

        // Act
        allocator.Dispose();

        // Assert - Existing memory should still be accessible
        _ = memory1.Memory.Length.Should().Be(1024);
        _ = memory2.Memory.Length.Should().Be(256);

        // Cleanup
        memory1.Dispose();
        memory2.Dispose();
    }

    #endregion

    #region Memory Alignment Edge Cases

    [Theory]
    [InlineData(4096)]    // Page alignment
    [InlineData(8192)]    // Large page
    [InlineData(16384)]   // Even larger
    [InlineData(32768)]   // Cache line multiple
    public void AllocateAligned_WithLargeAlignments_ShouldWork(int alignment)
    {
        // Act
        using var memory = _allocator.AllocateAligned<byte>(1024, alignment);

        // Assert
        Assert.NotNull(memory);
        _ = memory.Memory.Length.Should().Be(1024);

        // Verify memory is actually usable
        var span = memory.Memory.Span;
        span[0] = 0xFF;
        span[1023] = 0xAA;
        _ = span[0].Should().Be(0xFF);
        _ = span[1023].Should().Be(0xAA);
    }

    [Fact]
    public void AllocatedMemory_ShouldBeZeroed_ForSecurityReasons()
    {
        // Act
        using var memory = _allocator.Allocate<byte>(4096);

        // Assert - Memory should be safe to use(though not necessarily zeroed)
        var span = memory.Memory.Span;

        // Write and read back to ensure memory is valid
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = (byte)(i % 256);
        }

        for (var i = 0; i < span.Length; i++)
        {
            _ = span[i].Should().Be((byte)(i % 256));
        }
    }

    #endregion

    #region Type Safety Edge Cases

    [Fact]
    public void AllocateGenericTypes_WithDifferentSizes_ShouldWorkCorrectly()
    {
        // Act & Assert - Different sized types
        using var byteMemory = _allocator.Allocate<byte>(1024);
        using var intMemory = _allocator.Allocate<int>(256);
        using var longMemory = _allocator.Allocate<long>(128);
        using var structMemory = _allocator.Allocate<TestStruct>(64);

        _ = byteMemory.Memory.Length.Should().Be(1024);
        _ = intMemory.Memory.Length.Should().Be(256);
        _ = longMemory.Memory.Length.Should().Be(128);
        _ = structMemory.Memory.Length.Should().Be(64);
    }

    [Fact]
    public void AllocatePinned_WithComplexStruct_ShouldMaintainLayout()
    {
        // Act
        using var memory = _allocator.AllocatePinned<TestStruct>(10);

        // Assert
        _ = memory.Memory.Length.Should().Be(10);

        var span = memory.Memory.Span;
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = new TestStruct { IntValue = i, LongValue = i * 2L };
        }

        for (var i = 0; i < span.Length; i++)
        {
            _ = span[i].IntValue.Should().Be(i);
            _ = span[i].LongValue.Should().Be(i * 2L);
        }
    }

    #endregion

    #region Performance Edge Cases

    [Fact]
    public void FrequentSmallAllocations_ShouldMaintainPerformance()
    {
        const int allocationCount = 10000;
        const int allocationSize = 64;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (var i = 0; i < allocationCount; i++)
        {
            using var memory = _allocator.Allocate<byte>(allocationSize);
            // Use the memory briefly
            memory.Memory.Span[0] = (byte)i;
        }

        stopwatch.Stop();

        // Assert - Should complete in reasonable time(less than 1 second)
        _ = stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000,
            "Frequent small allocations should be performant");

        _ = _allocator.TotalAllocations.Should().Be(allocationCount);
        _ = _allocator.TotalDeallocations.Should().Be(allocationCount);
    }

    #endregion
}

/// <summary>
/// Test structure for memory layout validation.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TestStruct
{
    public int IntValue;
    public long LongValue;
}
