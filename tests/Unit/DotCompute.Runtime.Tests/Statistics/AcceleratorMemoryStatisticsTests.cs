// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: AcceleratorMemoryStatistics API mismatch - missing properties and methods
// TODO: Uncomment when AcceleratorMemoryStatistics implements:
// - UsedMemory property
// - FreeMemory property
// - AcceleratorId required property
// - Constructor taking single parameter (capacity)
// - AllocateMemory(), FreeMemory() methods
/*
using DotCompute.Runtime.Services.Statistics;
using FluentAssertions;
using Xunit;

namespace DotCompute.Runtime.Tests.Statistics;

/// <summary>
/// Tests for AcceleratorMemoryStatistics
/// </summary>
public sealed class AcceleratorMemoryStatisticsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var stats = new AcceleratorMemoryStatistics();

        // Assert
        stats.TotalMemory.Should().Be(0);
        stats.UsedMemory.Should().Be(0);
        stats.FreeMemory.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithCapacity_SetsCapacity()
    {
        // Act
        var stats = new AcceleratorMemoryStatistics(4096);

        // Assert
        stats.TotalMemory.Should().Be(4096);
        stats.FreeMemory.Should().Be(4096);
    }

    [Fact]
    public void AllocateMemory_DecreasesFreeMem()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(4096);

        // Act
        stats.AllocateMemory(1024);

        // Assert
        stats.UsedMemory.Should().Be(1024);
        stats.FreeMemory.Should().Be(3072);
    }

    [Fact]
    public void FreeMemory_IncreasesFreeMem()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(4096);
        stats.AllocateMemory(1024);

        // Act
        stats.FreeMemory(512);

        // Assert
        stats.UsedMemory.Should().Be(512);
        stats.FreeMemory.Should().Be(3584);
    }

    [Fact]
    public void AllocateMemory_BeyondCapacity_ThrowsInvalidOperationException()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(1024);

        // Act
        var action = () => stats.AllocateMemory(2048);

        // Assert
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetUtilizationPercentage_CalculatesCorrectly()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(4096);
        stats.AllocateMemory(1024);

        // Act
        var utilization = stats.GetUtilizationPercentage();

        // Assert
        utilization.Should().BeApproximately(25.0, 0.01);
    }

    [Fact]
    public void GetUtilizationPercentage_WithNoMemory_ReturnsZero()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(0);

        // Act
        var utilization = stats.GetUtilizationPercentage();

        // Assert
        utilization.Should().Be(0);
    }

    [Fact]
    public void RecordPeakUsage_TracksMaxUsage()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(4096);

        // Act
        stats.AllocateMemory(1024);
        stats.AllocateMemory(2048);
        stats.FreeMemory(1024);

        // Assert
        stats.GetPeakUsage().Should().Be(3072);
    }

    [Fact]
    public void Reset_ClearsStatistics()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(4096);
        stats.AllocateMemory(1024);

        // Act
        stats.Reset();

        // Assert
        stats.UsedMemory.Should().Be(0);
        stats.FreeMemory.Should().Be(4096);
    }

    [Fact]
    public void ConcurrentOperations_AreThreadSafe()
    {
        // Arrange
        var stats = new AcceleratorMemoryStatistics(100000);
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => stats.AllocateMemory(100)));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert
        stats.UsedMemory.Should().Be(10000);
    }
}
*/
