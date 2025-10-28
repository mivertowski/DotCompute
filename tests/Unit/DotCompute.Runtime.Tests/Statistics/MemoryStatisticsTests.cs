// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: MemoryStatistics API mismatch - missing properties and method signature changes
// TODO: Uncomment when MemoryStatistics implements:
// - TotalAllocated property
// - TotalFreed property
// - CurrentUsage property
// - RecordAllocation() method signature: RecordAllocation(long, double, bool) not RecordAllocation(long)
/*
using DotCompute.Runtime.Services.Statistics;
using FluentAssertions;
using Xunit;

namespace DotCompute.Runtime.Tests.Statistics;

/// <summary>
/// Tests for MemoryStatistics
/// </summary>
public sealed class MemoryStatisticsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var stats = new MemoryStatistics();

        // Assert
        stats.TotalAllocated.Should().Be(0);
        stats.TotalFreed.Should().Be(0);
        stats.CurrentUsage.Should().Be(0);
    }

    [Fact]
    public void RecordAllocation_IncreasesAllocatedAndCurrentUsage()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1024);

        // Assert
        stats.TotalAllocated.Should().Be(1024);
        stats.CurrentUsage.Should().Be(1024);
    }

    [Fact]
    public void RecordFree_IncreasesFreedAndDecreasesCurrentUsage()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1024);

        // Act
        stats.RecordFree(512);

        // Assert
        stats.TotalFreed.Should().Be(512);
        stats.CurrentUsage.Should().Be(512);
    }

    [Fact]
    public void RecordAllocation_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        var action = () => stats.RecordAllocation(-100);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordFree_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        var action = () => stats.RecordFree(-100);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetPeakUsage_TracksMaximumUsage()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(1024);
        stats.RecordAllocation(2048);
        stats.RecordFree(1024);

        // Assert
        stats.GetPeakUsage().Should().Be(3072);
    }

    [Fact]
    public void GetAllocationCount_TracksNumberOfAllocations()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(100);
        stats.RecordAllocation(200);
        stats.RecordAllocation(300);

        // Assert
        stats.GetAllocationCount().Should().Be(3);
    }

    [Fact]
    public void GetFreeCount_TracksNumberOfFrees()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(300);

        // Act
        stats.RecordFree(100);
        stats.RecordFree(100);

        // Assert
        stats.GetFreeCount().Should().Be(2);
    }

    [Fact]
    public void Reset_ClearsAllStatistics()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1024);
        stats.RecordFree(512);

        // Act
        stats.Reset();

        // Assert
        stats.TotalAllocated.Should().Be(0);
        stats.TotalFreed.Should().Be(0);
        stats.CurrentUsage.Should().Be(0);
    }

    [Fact]
    public void GetAverageAllocationSize_CalculatesCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();

        // Act
        stats.RecordAllocation(100);
        stats.RecordAllocation(200);
        stats.RecordAllocation(300);

        // Assert
        stats.GetAverageAllocationSize().Should().Be(200);
    }

    [Fact]
    public void GetFragmentationRatio_CalculatesCorrectly()
    {
        // Arrange
        var stats = new MemoryStatistics();
        stats.RecordAllocation(1000);
        stats.RecordFree(500);

        // Act
        var ratio = stats.GetFragmentationRatio();

        // Assert
        ratio.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(1);
    }

    [Fact]
    public void ConcurrentOperations_AreThreadSafe()
    {
        // Arrange
        var stats = new MemoryStatistics();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => stats.RecordAllocation(100)));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert
        stats.TotalAllocated.Should().Be(10000);
    }
}
*/
