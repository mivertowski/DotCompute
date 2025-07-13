// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using DotCompute.Abstractions;
using DotCompute.Memory;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for MemoryPool functionality including power-of-2 bucket allocation,
/// thread safety, and memory pressure handling.
/// </summary>
public class MemoryPoolTests
{
    private readonly IMemoryManager _memoryManager;
    
    public MemoryPoolTests()
    {
        _memoryManager = Substitute.For<IMemoryManager>();
    }
    
    [Fact]
    public void ConstructorWithValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Assert
        pool.MaxBufferSize.Should().BeGreaterThan(0);
        pool.TotalRetainedBytes.Should().Be(0);
        pool.TotalAllocatedBytes.Should().Be(0);
        pool.AllocationCount.Should().Be(0);
        pool.ReuseCount.Should().Be(0);
        pool.EfficiencyRatio.Should().Be(0.0);
    }
    
    [Fact]
    public void ConstructorWithNullMemoryManager_DoesNotThrow()
    {
        // Arrange & Act
        using var pool = new MemoryPool<float>(null);
        
        // Assert
        pool.Should().NotBeNull();
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(1024)]
    [InlineData(65536)]
    public void RentWithValidSize_ReturnsCorrectBuffer(int size)
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act
        using var owner = pool.Rent(size);
        
        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().BeGreaterOrEqualTo(size);
        pool.AllocationCount.Should().Be(1);
    }
    
    [Fact]
    public void RentWithNegativeSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(-1));
    }
    
    [Fact]
    public void RentWithZeroSize_ReturnsEmptyBuffer()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act
        using var owner = pool.Rent(0);
        
        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().Be(0);
    }
    
    [Fact]
    public void RentPowerOf2BucketAllocation_AllocatesCorrectSize()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act & Assert
        using var owner100 = pool.Rent(100);
        owner100.Memory.Length.Should().Be(128); // Next power of 2 >= 100
        
        using var owner200 = pool.Rent(200);
        owner200.Memory.Length.Should().Be(256); // Next power of 2 >= 200
        
        using var owner1000 = pool.Rent(1000);
        owner1000.Memory.Length.Should().Be(1024); // Next power of 2 >= 1000
    }
    
    [Fact]
    public void RentAndReturnBufferReuse_IncreasesReuseCount()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act
        // First allocation
        using (var owner1 = pool.Rent(1024))
        {
            // Use the buffer
            owner1.Memory.Span[0] = 123.45f;
        } // Dispose returns to pool
        
        // Second allocation - should reuse
        using var owner2 = pool.Rent(1024);
        
        // Assert
        pool.AllocationCount.Should().Be(2);
        pool.ReuseCount.Should().Be(1);
        pool.EfficiencyRatio.Should().Be(0.5); // 1 reuse / 2 allocations
        
        // Buffer should be cleared
        owner2.Memory.Span[0].Should().Be(0.0f);
    }
    
    [Fact]
    public void TryRentWithValidSize_ReturnsTrue()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act
        var success = pool.TryRent(1024, out var owner);
        
        // Assert
        success.Should().BeTrue();
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().BeGreaterOrEqualTo(1024);
        
        owner.Dispose();
    }
    
    [Fact]
    public void TryRentWithNegativeSize_ReturnsFalse()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act
        var success = pool.TryRent(-1, out var owner);
        
        // Assert
        success.Should().BeFalse();
        owner.Should().BeNull();
    }
    
    [Fact]
    public void TryRentFromEmptyPool_ReturnsFalse()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act
        var success = pool.TryRent(1024, out var owner);
        
        // Assert
        success.Should().BeFalse();
        owner.Should().BeNull();
    }
    
    [Fact]
    public void TryRentFromPopulatedPool_ReturnsTrue()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // First, populate the pool
        using (var owner1 = pool.Rent(1024))
        {
            // Use and return
        }
        
        // Act
        var success = pool.TryRent(1024, out var owner);
        
        // Assert
        success.Should().BeTrue();
        owner.Should().NotBeNull();
        pool.ReuseCount.Should().Be(1);
        
        owner.Dispose();
    }
    
    [Fact]
    public void ClearRemovesAllRetainedBuffers()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Populate the pool
        for (int i = 0; i < 5; i++)
        {
            using var owner = pool.Rent(1024);
            // Return to pool
        }
        
        var retainedBefore = pool.TotalRetainedBytes;
        
        // Act
        pool.Clear();
        
        // Assert
        pool.TotalRetainedBytes.Should().Be(0);
        retainedBefore.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void CompactReleasesMemory()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Populate the pool
        for (int i = 0; i < 10; i++)
        {
            using var owner = pool.Rent(1024);
            // Return to pool
        }
        
        var retainedBefore = pool.TotalRetainedBytes;
        
        // Act
        var releasedBytes = pool.Compact();
        
        // Assert
        releasedBytes.Should().BeGreaterThan(0);
        pool.TotalRetainedBytes.Should().BeLessThan(retainedBefore);
    }
    
    [Fact]
    public void HandleMemoryPressureWithLowPressure_NoEffect()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Populate the pool
        for (int i = 0; i < 5; i++)
        {
            using var owner = pool.Rent(1024);
            // Return to pool
        }
        
        var retainedBefore = pool.TotalRetainedBytes;
        
        // Act
        pool.HandleMemoryPressure(0.05); // 5% pressure
        
        // Assert
        pool.TotalRetainedBytes.Should().Be(retainedBefore);
    }
    
    [Fact]
    public void HandleMemoryPressureWithHighPressure_ReleasesMemory()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Populate the pool
        for (int i = 0; i < 10; i++)
        {
            using var owner = pool.Rent(1024);
            // Return to pool
        }
        
        var retainedBefore = pool.TotalRetainedBytes;
        
        // Act
        pool.HandleMemoryPressure(0.9); // 90% pressure
        
        // Assert
        pool.TotalRetainedBytes.Should().BeLessThan(retainedBefore);
    }
    
    [Fact]
    public void HandleMemoryPressureWithInvalidPressure_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.HandleMemoryPressure(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.HandleMemoryPressure(1.1));
    }
    
    [Fact]
    public void GetPerformanceStatsReturnsValidStats()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Populate the pool
        for (int i = 0; i < 5; i++)
        {
            using var owner = pool.Rent(1024);
            // Return to pool
        }
        
        // Act
        var stats = pool.GetPerformanceStats();
        
        // Assert
        stats.TotalAllocatedBytes.Should().BeGreaterThan(0);
        stats.AllocationCount.Should().Be(5);
        stats.ReuseCount.Should().BeGreaterThan(0);
        stats.EfficiencyRatio.Should().BeGreaterThan(0);
        stats.BucketStats.Should().NotBeNull();
        stats.BucketStats.Length.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void ConcurrentRentAndReturnIsThreadSafe()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        const int threadCount = 10;
        const int operationsPerThread = 100;
        
        // Act
        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    using var owner = pool.Rent(1024);
                    // Use the buffer
                    owner.Memory.Span[0] = i * 1.5f;
                }
            });
        }
        
        Task.WaitAll(tasks);
        
        // Assert
        pool.AllocationCount.Should().Be(threadCount * operationsPerThread);
        pool.ReuseCount.Should().BeGreaterThan(0);
        pool.EfficiencyRatio.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public void MultipleSizesUseDifferentBuckets()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        var sizes = new[] { 64, 128, 256, 512, 1024 };
        
        // Act
        var owners = new List<IMemoryOwner<float>>();
        foreach (var size in sizes)
        {
            owners.Add(pool.Rent(size));
        }
        
        // Assert
        var stats = pool.GetPerformanceStats();
        var activeBuckets = stats.BucketStats.Where(b => b.Count > 0 || b.TotalBytes > 0).ToArray();
        activeBuckets.Length.Should().BeGreaterThan(0);
        
        // Cleanup
        foreach (var owner in owners)
        {
            owner.Dispose();
        }
    }
    
    [Fact]
    public void PooledMemoryOwnerDispose_ReturnsToPool()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act
        var owner = pool.Rent(1024);
        owner.Memory.Span[0] = 123.45f;
        owner.Dispose();
        
        // Get another buffer - should be reused and cleared
        using var owner2 = pool.Rent(1024);
        
        // Assert
        pool.ReuseCount.Should().Be(1);
        owner2.Memory.Span[0].Should().Be(0.0f); // Should be cleared
    }
    
    [Fact]
    public void MaxBufferSizeIsReasonable()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act & Assert
        pool.MaxBufferSize.Should().BeGreaterThan(1024);
        pool.MaxBufferSize.Should().BeLessThan(100 * 1024 * 1024); // Less than 100MB
    }
    
    [Fact]
    public void RentExceedsMaxBufferSize_StillWorks()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        var largeSize = pool.MaxBufferSize + 1000;
        
        // Act
        using var owner = pool.Rent(largeSize);
        
        // Assert
        owner.Should().NotBeNull();
        owner.Memory.Length.Should().BeGreaterOrEqualTo(largeSize);
    }
    
    [Fact]
    public void BucketSizeCalculationFollowsPowerOf2()
    {
        // Arrange
        using var pool = new MemoryPool<float>(_memoryManager);
        
        // Act & Assert
        using var owner60 = pool.Rent(60);
        owner60.Memory.Length.Should().Be(64); // 2^6
        
        using var owner100 = pool.Rent(100);
        owner100.Memory.Length.Should().Be(128); // 2^7
        
        using var owner500 = pool.Rent(500);
        owner500.Memory.Length.Should().Be(512); // 2^9
    }
    
    [Fact]
    public void DisposeClearsAllResources()
    {
        // Arrange
        var pool = new MemoryPool<float>(_memoryManager);
        
        // Populate the pool
        for (int i = 0; i < 5; i++)
        {
            using var owner = pool.Rent(1024);
            // Return to pool
        }
        
        var retainedBefore = pool.TotalRetainedBytes;
        
        // Act
        pool.Dispose();
        
        // Assert
        pool.TotalRetainedBytes.Should().Be(0);
        retainedBefore.Should().BeGreaterThan(0);
        
        // Subsequent operations should throw
        Assert.Throws<ObjectDisposedException>(() => pool.Rent(1024));
    }
}