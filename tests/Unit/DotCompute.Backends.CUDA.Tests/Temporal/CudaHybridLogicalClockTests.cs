// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions.Temporal;
using DotCompute.Abstractions.Timing;
using DotCompute.Backends.CUDA.Temporal;
using FluentAssertions;
using Moq;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Temporal;

/// <summary>
/// Unit tests for Hybrid Logical Clock (HLC) implementation.
/// </summary>
public sealed class CudaHybridLogicalClockTests
{
    private const long BaseTime = 1_000_000_000; // 1 second in nanoseconds

    // ==================== Basic Functionality Tests ====================

    [Fact(DisplayName = "TickAsync should advance clock for local event")]
    public async Task TickAsync_ShouldAdvanceClock()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime, BaseTime + 1000);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        // Act
        var timestamp1 = await hlc.TickAsync();
        var timestamp2 = await hlc.TickAsync();

        // Assert
        timestamp1.PhysicalTimeNanos.Should().Be(BaseTime);
        timestamp1.LogicalCounter.Should().Be(0, "first tick should reset logical counter");

        timestamp2.PhysicalTimeNanos.Should().Be(BaseTime + 1000);
        timestamp2.LogicalCounter.Should().Be(0, "physical time advanced, logical resets");
    }

    [Fact(DisplayName = "TickAsync should increment logical counter when physical time is same")]
    public async Task TickAsync_ShouldIncrementLogicalCounter_WhenPhysicalTimeSame()
    {
        // Arrange - physical time stays constant
        var timingMock = CreateTimingProvider(BaseTime, BaseTime, BaseTime, BaseTime);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        // Act
        var timestamp1 = await hlc.TickAsync();
        var timestamp2 = await hlc.TickAsync();
        var timestamp3 = await hlc.TickAsync();

        // Assert
        timestamp1.LogicalCounter.Should().Be(0);
        timestamp2.LogicalCounter.Should().Be(1, "physical time same, increment logical");
        timestamp3.LogicalCounter.Should().Be(2, "physical time same, increment logical again");
    }

    [Fact(DisplayName = "UpdateAsync should preserve causality with remote timestamp")]
    public async Task UpdateAsync_ShouldPreserveCausality()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime, BaseTime + 2000);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        var remoteTimestamp = new HlcTimestamp
        {
            PhysicalTimeNanos = BaseTime + 5000,  // Remote is ahead
            LogicalCounter = 3
        };

        // Act
        var localTimestamp = await hlc.UpdateAsync(remoteTimestamp);

        // Assert
        localTimestamp.PhysicalTimeNanos.Should().BeGreaterThanOrEqualTo(remoteTimestamp.PhysicalTimeNanos,
            "local clock should advance to match remote");
        localTimestamp.LogicalCounter.Should().Be(4, "logical counter = remote.logical + 1");
    }

    [Fact(DisplayName = "UpdateAsync should handle remote timestamp with same physical time")]
    public async Task UpdateAsync_ShouldIncrementLogical_WhenPhysicalTimeSame()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime, BaseTime);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        // Advance local clock first
        var localFirst = await hlc.TickAsync();
        localFirst.LogicalCounter.Should().Be(0);

        var remoteTimestamp = new HlcTimestamp
        {
            PhysicalTimeNanos = BaseTime,  // Same physical time
            LogicalCounter = 5  // Remote has higher logical
        };

        // Act
        var updated = await hlc.UpdateAsync(remoteTimestamp);

        // Assert
        updated.PhysicalTimeNanos.Should().Be(BaseTime);
        updated.LogicalCounter.Should().Be(6, "max(local:0, remote:5) + 1 = 6");
    }

    [Fact(DisplayName = "GetCurrent should return current timestamp without advancing clock")]
    public void GetCurrent_ShouldNotAdvanceClock()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime);
        var hlc = new CudaHybridLogicalClock(timingMock.Object, new HlcTimestamp { PhysicalTimeNanos = BaseTime, LogicalCounter = 42 });

        // Act
        var current1 = hlc.GetCurrent();
        var current2 = hlc.GetCurrent();

        // Assert
        current1.PhysicalTimeNanos.Should().Be(BaseTime);
        current1.LogicalCounter.Should().Be(42);
        current2.Should().Be(current1);
    }

    [Fact(DisplayName = "Reset should update clock to specified timestamp")]
    public void Reset_ShouldUpdateClock()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        var newTimestamp = new HlcTimestamp
        {
            PhysicalTimeNanos = BaseTime + 10000,
            LogicalCounter = 42
        };

        // Act
        hlc.Reset(newTimestamp);
        var current = hlc.GetCurrent();

        // Assert
        current.Should().Be(newTimestamp);
    }

    // ==================== Causality Tests ====================

    [Fact(DisplayName = "HappenedBefore should detect causal ordering")]
    public void HappenedBefore_ShouldDetectCausalOrdering()
    {
        // Arrange
        var earlier = new HlcTimestamp { PhysicalTimeNanos = 100, LogicalCounter = 0 };
        var later = new HlcTimestamp { PhysicalTimeNanos = 200, LogicalCounter = 0 };

        // Assert
        earlier.HappenedBefore(later).Should().BeTrue();
        later.HappenedBefore(earlier).Should().BeFalse();
    }

    [Fact(DisplayName = "HappenedBefore should use logical counter when physical time is same")]
    public void HappenedBefore_ShouldUseLogicalCounter()
    {
        // Arrange
        var earlier = new HlcTimestamp { PhysicalTimeNanos = 100, LogicalCounter = 0 };
        var later = new HlcTimestamp { PhysicalTimeNanos = 100, LogicalCounter = 5 };

        // Assert
        earlier.HappenedBefore(later).Should().BeTrue();
        later.HappenedBefore(earlier).Should().BeFalse();
    }

    [Fact(DisplayName = "CompareTo should provide total ordering")]
    public void CompareTo_ShouldProvideTotalOrdering()
    {
        // Arrange
        var timestamps = new[]
        {
            new HlcTimestamp { PhysicalTimeNanos = 200, LogicalCounter = 0 },
            new HlcTimestamp { PhysicalTimeNanos = 100, LogicalCounter = 5 },
            new HlcTimestamp { PhysicalTimeNanos = 100, LogicalCounter = 0 },
            new HlcTimestamp { PhysicalTimeNanos = 300, LogicalCounter = 2 },
        };

        // Act
        var sorted = timestamps.OrderBy(t => t).ToArray();

        // Assert
        sorted[0].Should().Be(new HlcTimestamp { PhysicalTimeNanos = 100, LogicalCounter = 0 });
        sorted[1].Should().Be(new HlcTimestamp { PhysicalTimeNanos = 100, LogicalCounter = 5 });
        sorted[2].Should().Be(new HlcTimestamp { PhysicalTimeNanos = 200, LogicalCounter = 0 });
        sorted[3].Should().Be(new HlcTimestamp { PhysicalTimeNanos = 300, LogicalCounter = 2 });
    }

    [Fact(DisplayName = "HLC should preserve causality when remote timestamp is in past")]
    public async Task HLC_ShouldPreserveCausality_WhenRemoteInPast()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime, BaseTime + 1000, BaseTime + 2000);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        var sendTimestamp = await hlc.TickAsync();  // t=BaseTime, L=0

        // Simulate receiving a message with timestamp in the past
        var receiveTimestampOld = new HlcTimestamp
        {
            PhysicalTimeNanos = BaseTime - 1000,  // Before send!
            LogicalCounter = 0
        };

        // Act
        var updated = await hlc.UpdateAsync(receiveTimestampOld);

        // Assert - HLC should advance clock to maintain causality
        updated.HappenedBefore(sendTimestamp).Should().BeFalse(
            "received timestamp should not be before send timestamp after update");
    }

    // ==================== Thread Safety Tests ====================

    [Fact(DisplayName = "TickAsync should be thread-safe under concurrent access")]
    public async Task TickAsync_ShouldBeThreadSafe()
    {
        // Arrange
        var timingMock = CreateTimingProvider(Enumerable.Range(0, 1000).Select(i => BaseTime + i).ToArray());
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        var timestamps = new List<HlcTimestamp>[10];
        for (int i = 0; i < 10; i++)
        {
            timestamps[i] = new List<HlcTimestamp>();
        }

        // Act - 10 threads each calling TickAsync 100 times
        var tasks = Enumerable.Range(0, 10).Select(threadId => Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var timestamp = await hlc.TickAsync();
                lock (timestamps[threadId])
                {
                    timestamps[threadId].Add(timestamp);
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - All timestamps should be unique and properly ordered
        var allTimestamps = timestamps.SelectMany(list => list).OrderBy(t => t).ToList();
        allTimestamps.Should().HaveCount(1000);

        for (int i = 1; i < allTimestamps.Count; i++)
        {
            allTimestamps[i - 1].Should().BeLessThan(allTimestamps[i],
                "timestamps should be strictly increasing (no duplicates)");
        }
    }

    [Fact(DisplayName = "UpdateAsync should be thread-safe under concurrent access")]
    public async Task UpdateAsync_ShouldBeThreadSafe()
    {
        // Arrange
        var timingMock = CreateTimingProvider(Enumerable.Range(0, 1000).Select(i => BaseTime + i).ToArray());
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        var remoteTimestamp = new HlcTimestamp { PhysicalTimeNanos = BaseTime + 5000, LogicalCounter = 10 };

        // Act - 10 threads each calling UpdateAsync 100 times
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await hlc.UpdateAsync(remoteTimestamp);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert - Final timestamp should be valid (no corruption)
        var final = hlc.GetCurrent();
        final.PhysicalTimeNanos.Should().BeGreaterThanOrEqualTo(remoteTimestamp.PhysicalTimeNanos);
        final.LogicalCounter.Should().BeGreaterThanOrEqualTo(remoteTimestamp.LogicalCounter);
    }

    // ==================== Disposal Tests ====================

    [Fact(DisplayName = "Dispose should allow multiple calls")]
    public void Dispose_ShouldAllowMultipleCalls()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);

        // Act & Assert
        hlc.Dispose();
        FluentActions.Invoking(() => hlc.Dispose()).Should().NotThrow();
    }

    [Fact(DisplayName = "Operations should throw after disposal")]
    public async Task Operations_ShouldThrow_AfterDisposal()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime);
        var hlc = new CudaHybridLogicalClock(timingMock.Object);
        hlc.Dispose();

        // Assert
        await FluentActions.Invoking(async () => await hlc.TickAsync()).Should().ThrowAsync<ObjectDisposedException>();
        await FluentActions.Invoking(async () => await hlc.UpdateAsync(new HlcTimestamp())).Should().ThrowAsync<ObjectDisposedException>();
        FluentActions.Invoking(() => hlc.GetCurrent()).Should().Throw<ObjectDisposedException>();
        FluentActions.Invoking(() => hlc.Reset(new HlcTimestamp())).Should().Throw<ObjectDisposedException>();
    }

    // ==================== Constructor Tests ====================

    [Fact(DisplayName = "Constructor should throw when timing provider is null")]
    public void Constructor_ShouldThrow_WhenTimingProviderIsNull()
    {
        // Assert
        FluentActions.Invoking(() => new CudaHybridLogicalClock(null!))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("timingProvider");
    }

    [Fact(DisplayName = "Constructor with initial timestamp should set state correctly")]
    public void Constructor_ShouldSetInitialState()
    {
        // Arrange
        var timingMock = CreateTimingProvider(BaseTime);
        var initialTimestamp = new HlcTimestamp { PhysicalTimeNanos = 12345, LogicalCounter = 42 };

        // Act
        var hlc = new CudaHybridLogicalClock(timingMock.Object, initialTimestamp);
        var current = hlc.GetCurrent();

        // Assert
        current.Should().Be(initialTimestamp);
    }

    // ==================== Helper Methods ====================

    private static Mock<ITimingProvider> CreateTimingProvider(params long[] timestampSequence)
    {
        var mock = new Mock<ITimingProvider>();
        var index = 0;

        mock.Setup(m => m.GetGpuTimestampAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var value = timestampSequence[Math.Min(index, timestampSequence.Length - 1)];
                index++;
                return Task.FromResult(value);
            });

        return mock;
    }
}
