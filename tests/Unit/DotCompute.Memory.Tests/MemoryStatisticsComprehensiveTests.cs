// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Memory;
using FluentAssertions;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for MemoryStatistics covering all critical scenarios.
/// Part of Phase 1: Memory Module testing to achieve 80% coverage.
/// </summary>
public sealed class MemoryStatisticsComprehensiveTests
{
    #region Initial State Tests

    [Fact]
    public void Constructor_InitializesAllPropertiesToZero()
    {
        // Act
        var stats = new MemoryStatistics();

        // Assert
        stats.CurrentlyAllocatedBytes.Should().Be(0);
        stats.PeakAllocatedBytes.Should().Be(0);
        stats.TotalAllocations.Should().Be(0);
        stats.TotalDeallocations.Should().Be(0);
        stats.TotalBytesAllocated.Should().Be(0);
        stats.TotalBytesFreed.Should().Be(0);
        stats.ActiveAllocations.Should().Be(0);
        stats.PoolHits.Should().Be(0);
        stats.PoolMisses.Should().Be(0);
        stats.FailedAllocations.Should().Be(0);
        stats.CopyOperations.Should().Be(0);
    }

    [Fact]
    public void Constructor_InitializesComputedPropertiesToZero()
    {
        // Act
        var stats = new MemoryStatistics();

        // Assert
        stats.AverageAllocationTime.Should().Be(0.0);
        stats.AverageDeallocationTime.Should().Be(0.0);
        stats.AverageCopyTime.Should().Be(0.0);
        stats.PoolHitRate.Should().Be(0.0);
        stats.MemoryEfficiency.Should().Be(0.0);
        stats.AverageAllocationSize.Should().Be(0.0);
    }

    #endregion

    #region RecordAllocation Tests

    [Fact]
    public void RecordAllocation_WithValidParameters_UpdatesCounters()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1024, 0.5, fromPool: false);

        // Assert
        stats.TotalAllocations.Should().Be(1);
        stats.TotalBytesAllocated.Should().Be(1024);
        stats.CurrentlyAllocatedBytes.Should().Be(1024);
        stats.PeakAllocatedBytes.Should().Be(1024);
        stats.PoolMisses.Should().Be(1);
        stats.PoolHits.Should().Be(0);
    }

    [Fact]
    public void RecordAllocation_WithPoolHit_IncrementsPoolHits()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(512, 0.1, fromPool: true);

        // Assert
        stats.PoolHits.Should().Be(1);
        stats.PoolMisses.Should().Be(0);
    }

    [Fact]
    public void RecordAllocation_WithPoolMiss_IncrementsPoolMisses()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(512, 0.1, fromPool: false);

        // Assert
        stats.PoolHits.Should().Be(0);
        stats.PoolMisses.Should().Be(1);
    }

    [Fact]
    public void RecordAllocation_MultipleAllocations_UpdatesPeakCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1000, 0.5, false);
        stats.RecordAllocation(2000, 0.5, false); // Peak should be 3000
        stats.RecordDeallocation(1500, 0.5);      // Current becomes 1500
        stats.RecordAllocation(500, 0.5, false);  // Current becomes 2000, but peak stays 3000

        // Assert
        stats.PeakAllocatedBytes.Should().Be(3000);
        stats.CurrentlyAllocatedBytes.Should().Be(2000);
    }

    [Fact]
    public void RecordAllocation_WithNegativeBytes_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act & Assert
        try
        {
            stats.RecordAllocation(-100, 0.5, false);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void RecordAllocation_WithNegativeTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act & Assert
        try
        {
            stats.RecordAllocation(100, -0.5, false);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void RecordAllocation_TracksAllocationTime()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(100, 1.5, false);
        stats.RecordAllocation(200, 2.5, false);

        // Assert
        stats.AverageAllocationTime.Should().Be(2.0); // (1.5 + 2.5) / 2
    }

    #endregion

    #region RecordDeallocation Tests

    [Fact]
    public void RecordDeallocation_WithValidParameters_UpdatesCounters()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1024, 0.5, false);

        // Act
        stats.RecordDeallocation(512, 0.3);

        // Assert
        stats.TotalDeallocations.Should().Be(1);
        stats.TotalBytesFreed.Should().Be(512);
        stats.CurrentlyAllocatedBytes.Should().Be(512); // 1024 - 512
    }

    [Fact]
    public void RecordDeallocation_WithoutTime_DoesNotUpdateAverageTime()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1024, 1.0, false);

        // Act
        stats.RecordDeallocation(512); // No time specified

        // Assert
        stats.AverageDeallocationTime.Should().Be(0.0);
    }

    [Fact]
    public void RecordDeallocation_WithTime_UpdatesAverageTime()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(2000, 1.0, false);

        // Act
        stats.RecordDeallocation(500, 1.5);
        stats.RecordDeallocation(500, 2.5);

        // Assert
        stats.AverageDeallocationTime.Should().Be(2.0); // (1.5 + 2.5) / 2
    }

    [Fact]
    public void RecordDeallocation_WithNegativeBytes_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act & Assert
        try
        {
            stats.RecordDeallocation(-100, 0.5);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void RecordDeallocation_WithNegativeTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act & Assert
        try
        {
            stats.RecordDeallocation(100, -0.5);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    #endregion

    #region RecordFailedAllocation Tests

    [Fact]
    public void RecordFailedAllocation_IncrementsCounter()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordFailedAllocation(2048);

        // Assert
        stats.FailedAllocations.Should().Be(1);
    }

    [Fact]
    public void RecordFailedAllocation_MultipleFailures_IncrementCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordFailedAllocation(1024);
        stats.RecordFailedAllocation(2048);
        stats.RecordFailedAllocation(4096);

        // Assert
        stats.FailedAllocations.Should().Be(3);
    }

    [Fact]
    public void RecordFailedAllocation_WithNegativeBytes_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act & Assert
        try
        {
            stats.RecordFailedAllocation(-100);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    #endregion

    #region RecordCopyOperation Tests

    [Fact]
    public void RecordCopyOperation_UpdatesCounters()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordCopyOperation(1024, 2.5, isHostToDevice: true);

        // Assert
        stats.CopyOperations.Should().Be(1);
        stats.AverageCopyTime.Should().Be(2.5);
    }

    [Fact]
    public void RecordCopyOperation_MultipleOperations_CalculatesAverageCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordCopyOperation(1024, 1.0, true);
        stats.RecordCopyOperation(2048, 3.0, false);
        stats.RecordCopyOperation(512, 2.0, true);

        // Assert
        stats.CopyOperations.Should().Be(3);
        stats.AverageCopyTime.Should().Be(2.0); // (1.0 + 3.0 + 2.0) / 3
    }

    [Fact]
    public void RecordCopyOperation_WithNegativeBytes_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act & Assert
        try
        {
            stats.RecordCopyOperation(-100, 1.0, true);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    [Fact]
    public void RecordCopyOperation_WithNegativeTime_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act & Assert
        try
        {
            stats.RecordCopyOperation(100, -1.0, true);
            Assert.Fail("Expected ArgumentOutOfRangeException was not thrown");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected exception
        }
    }

    #endregion

    #region Computed Properties Tests

    [Fact]
    public void ActiveAllocations_ReturnsCorrectCount()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 0.5, false);
        stats.RecordAllocation(2000, 0.5, false);
        stats.RecordAllocation(3000, 0.5, false);
        stats.RecordDeallocation(1000, 0.5);

        // Assert
        stats.ActiveAllocations.Should().Be(2); // 3 allocations - 1 deallocation
    }

    [Fact]
    public void PoolHitRate_WithNoOperations_ReturnsZero()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Assert
        stats.PoolHitRate.Should().Be(0.0);
    }

    [Fact]
    public void PoolHitRate_CalculatesCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(100, 0.1, fromPool: true);  // Hit
        stats.RecordAllocation(200, 0.1, fromPool: true);  // Hit
        stats.RecordAllocation(300, 0.1, fromPool: false); // Miss
        stats.RecordAllocation(400, 0.1, fromPool: true);  // Hit

        // Assert
        stats.PoolHitRate.Should().Be(0.75); // 3 hits out of 4 total
    }

    [Fact]
    public void MemoryEfficiency_WithNoAllocations_ReturnsZero()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Assert
        stats.MemoryEfficiency.Should().Be(0.0);
    }

    [Fact]
    public void MemoryEfficiency_CalculatesCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1000, 0.5, false);
        stats.RecordAllocation(2000, 0.5, false);
        stats.RecordDeallocation(1500, 0.5);

        // Assert
        // Total allocated: 3000, Total freed: 1500, Efficiency: 0.5
        stats.MemoryEfficiency.Should().Be(0.5);
    }

    [Fact]
    public void MemoryEfficiency_WhenAllMemoryFreed_ReturnsOne()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1000, 0.5, false);
        stats.RecordAllocation(2000, 0.5, false);
        stats.RecordDeallocation(3000, 0.5);

        // Assert
        stats.MemoryEfficiency.Should().Be(1.0);
    }

    [Fact]
    public void AverageAllocationSize_WithNoAllocations_ReturnsZero()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Assert
        stats.AverageAllocationSize.Should().Be(0.0);
    }

    [Fact]
    public void AverageAllocationSize_CalculatesCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1000, 0.5, false);
        stats.RecordAllocation(2000, 0.5, false);
        stats.RecordAllocation(3000, 0.5, false);

        // Assert
        stats.AverageAllocationSize.Should().Be(2000.0); // (1000 + 2000 + 3000) / 3
    }

    [Fact]
    public void AverageAllocationTime_WithNoDeallocations_HandlesDivisionByZero()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Assert
        stats.AverageDeallocationTime.Should().Be(0.0);
    }

    [Fact]
    public void AverageCopyTime_WithNoCopies_HandlesDivisionByZero()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Assert
        stats.AverageCopyTime.Should().Be(0.0);
    }

    #endregion

    #region CreateSnapshot Tests

    [Fact]
    public void CreateSnapshot_CapturesCurrentState()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 1.0, true);
        stats.RecordAllocation(2000, 2.0, false);
        stats.RecordDeallocation(500, 0.5);

        // Act
        var snapshot = stats.CreateSnapshot();

        // Assert
        snapshot.TotalAllocations.Should().Be(2);
        snapshot.TotalBytesAllocated.Should().Be(3000);
        snapshot.TotalDeallocations.Should().Be(1);
        snapshot.TotalBytesFreed.Should().Be(500);
        snapshot.CurrentlyAllocatedBytes.Should().Be(2500);
        snapshot.PoolHits.Should().Be(1);
        snapshot.PoolMisses.Should().Be(1);
    }

    [Fact]
    public void CreateSnapshot_IsIndependentOfOriginal()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 1.0, false);

        // Act
        var snapshot = stats.CreateSnapshot();
        stats.RecordAllocation(2000, 1.0, false); // Modify original

        // Assert
        snapshot.TotalAllocations.Should().Be(1); // Snapshot should not change
        stats.TotalAllocations.Should().Be(2);    // Original should change
    }

    [Fact]
    public void CreateSnapshot_PreservesComputedProperties()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 2.0, true);
        stats.RecordAllocation(2000, 4.0, false);

        // Act
        var snapshot = stats.CreateSnapshot();

        // Assert
        snapshot.AverageAllocationTime.Should().Be(3.0);
        snapshot.PoolHitRate.Should().Be(0.5);
        snapshot.AverageAllocationSize.Should().Be(1500.0);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllCounters()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 1.0, true);
        stats.RecordAllocation(2000, 2.0, false);
        stats.RecordDeallocation(500, 0.5);
        stats.RecordCopyOperation(1024, 1.5, true);
        stats.RecordFailedAllocation(4096);

        // Act
        stats.Reset();

        // Assert
        stats.TotalAllocations.Should().Be(0);
        stats.TotalDeallocations.Should().Be(0);
        stats.TotalBytesAllocated.Should().Be(0);
        stats.TotalBytesFreed.Should().Be(0);
        stats.CurrentlyAllocatedBytes.Should().Be(0);
        stats.PeakAllocatedBytes.Should().Be(0);
        stats.PoolHits.Should().Be(0);
        stats.PoolMisses.Should().Be(0);
        stats.FailedAllocations.Should().Be(0);
        stats.CopyOperations.Should().Be(0);
        stats.AverageAllocationTime.Should().Be(0.0);
        stats.AverageDeallocationTime.Should().Be(0.0);
        stats.AverageCopyTime.Should().Be(0.0);
    }

    [Fact]
    public void Reset_AllowsNewRecordsAfterReset()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 1.0, false);
        stats.Reset();

        // Act
        stats.RecordAllocation(2000, 2.0, true);

        // Assert
        stats.TotalAllocations.Should().Be(1);
        stats.TotalBytesAllocated.Should().Be(2000);
        stats.PoolHits.Should().Be(1);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 1.0, true);
        stats.RecordAllocation(500, 0.5, false);

        // Act
        var result = stats.ToString();

        // Assert
        result.Should().Contain("MemoryStatistics");
        result.Should().Contain("CurrentlyAllocated:");
        result.Should().Contain("1,500");
        result.Should().Contain("TotalAllocations:");
        result.Should().Contain("2");
    }

    [Fact]
    public void ToString_WithEmptyStats_DoesNotThrow()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        var result = stats.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("MemoryStatistics");
    }

    [Fact]
    public void ToDetailedString_ReturnsComprehensiveInformation()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000, 1.5, true);
        stats.RecordDeallocation(500, 0.5);
        stats.RecordCopyOperation(2048, 2.0, true);

        // Act
        var result = stats.ToDetailedString();

        // Assert
        result.Should().Contain("Detailed Memory Statistics");
        result.Should().Contain("Memory Usage:");
        result.Should().Contain("Allocation Statistics:");
        result.Should().Contain("Pool Statistics:");
        result.Should().Contain("Performance Statistics:");
        result.Should().Contain("Currently Allocated:");
        result.Should().Contain("Average Allocation Time:");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentAllocations_UpdateCountersSafely()
    {
        // Arrange
        var stats = new MemoryStatistics();
        const int concurrentTasks = 10;
        const int allocationsPerTask = 100;

        // Act
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async _ =>
        {
            await Task.Yield();
            for (int i = 0; i < allocationsPerTask; i++)
            {
                stats.RecordAllocation(100, 0.1, i % 2 == 0);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        stats.TotalAllocations.Should().Be(concurrentTasks * allocationsPerTask);
        stats.TotalBytesAllocated.Should().Be(concurrentTasks * allocationsPerTask * 100);
        stats.PoolHits.Should().Be(concurrentTasks * allocationsPerTask / 2);
        stats.PoolMisses.Should().Be(concurrentTasks * allocationsPerTask / 2);
    }

    [Fact]
    public async Task ConcurrentDeallocations_UpdateCountersSafely()
    {
        // Arrange
        var stats = new MemoryStatistics();
        const int concurrentTasks = 10;
        const int deallocationsPerTask = 100;

        // Pre-allocate to have something to deallocate
        for (int i = 0; i < concurrentTasks * deallocationsPerTask; i++)
        {
            stats.RecordAllocation(100, 0.1, false);
        }

        // Act
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async _ =>
        {
            await Task.Yield();
            for (int i = 0; i < deallocationsPerTask; i++)
            {
                stats.RecordDeallocation(100, 0.1);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        stats.TotalDeallocations.Should().Be(concurrentTasks * deallocationsPerTask);
        stats.TotalBytesFreed.Should().Be(concurrentTasks * deallocationsPerTask * 100);
    }

    [Fact]
    public async Task ConcurrentMixedOperations_MaintainConsistency()
    {
        // Arrange
        var stats = new MemoryStatistics();
        const int operationsPerTask = 50;

        // Act
        var tasks = new[]
        {
            Task.Run(() =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    stats.RecordAllocation(100, 0.1, true);
                }
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    stats.RecordDeallocation(50, 0.1);
                }
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    stats.RecordCopyOperation(200, 0.2, true);
                }
            }),
            Task.Run(() =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    stats.RecordFailedAllocation(1024);
                }
            })
        };

        await Task.WhenAll(tasks);

        // Assert
        stats.TotalAllocations.Should().Be(operationsPerTask);
        stats.TotalDeallocations.Should().Be(operationsPerTask);
        stats.CopyOperations.Should().Be(operationsPerTask);
        stats.FailedAllocations.Should().Be(operationsPerTask);
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public void RecordAllocation_WithMaxLongValue_DoesNotOverflow()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(long.MaxValue / 2, 1.0, false);
        stats.RecordAllocation(long.MaxValue / 2, 1.0, false);

        // Assert - Should not throw overflow exception
        stats.TotalBytesAllocated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RecordAllocation_WithZeroBytes_IsAllowed()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(0, 0.1, false);

        // Assert
        stats.TotalAllocations.Should().Be(1);
        stats.TotalBytesAllocated.Should().Be(0);
    }

    [Fact]
    public void RecordAllocation_WithZeroTime_IsAllowed()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(100, 0.0, false);

        // Assert
        stats.TotalAllocations.Should().Be(1);
        stats.AverageAllocationTime.Should().Be(0.0);
    }

    [Fact]
    public void RecordDeallocation_ExceedingAllocated_DoesNotCrash()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(100, 0.1, false);

        // Act - Deallocate more than allocated
        stats.RecordDeallocation(200, 0.1);

        // Assert - Should handle gracefully (negative current is mathematically valid)
        stats.TotalBytesFreed.Should().Be(200);
        stats.CurrentlyAllocatedBytes.Should().Be(-100);
    }

    [Fact]
    public void PeakAllocatedBytes_NeverDecreases()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1000, 0.1, false); // Peak = 1000
        stats.RecordAllocation(2000, 0.1, false); // Peak = 3000
        stats.RecordDeallocation(2500, 0.1);      // Current = 500, Peak still 3000
        stats.RecordAllocation(100, 0.1, false);  // Current = 600, Peak still 3000

        // Assert
        stats.PeakAllocatedBytes.Should().Be(3000);
        stats.CurrentlyAllocatedBytes.Should().Be(600);
    }

    #endregion

    #region RecordBufferCreation Tests

    [Fact]
    public void RecordBufferCreation_StaticMethod_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        MemoryStatistics.RecordBufferCreation(1024);
    }

    [Fact]
    public void RecordBufferCreation_WithLargeValue_DoesNotThrow()
    {
        // Act & Assert
        MemoryStatistics.RecordBufferCreation(long.MaxValue);
    }

    #endregion
}
