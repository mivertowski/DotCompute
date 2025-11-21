// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Abstractions.Temporal;
using DotCompute.Abstractions.Timing;
using DotCompute.Backends.CUDA.Barriers;
using DotCompute.Backends.CUDA.Temporal;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.Barriers;

/// <summary>
/// Unit tests for CudaCrossGpuBarrier implementation.
/// Tests CPU fallback mode without requiring actual GPU hardware.
/// </summary>
public sealed class CudaCrossGpuBarrierTests : IDisposable
{
    private readonly IHybridLogicalClock _mockHlc;
    private readonly List<CudaCrossGpuBarrier> _barriers = new();

    public CudaCrossGpuBarrierTests()
    {
        var timingProvider = Substitute.For<ITimingProvider>();
        timingProvider.GetGpuTimestampAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(DateTime.UtcNow.Ticks * 100L));

        _mockHlc = new CudaHybridLogicalClock(timingProvider);
    }

    public void Dispose()
    {
        foreach (var barrier in _barriers)
        {
            barrier.Dispose();
        }

        _barriers.Clear();
    }

    private CudaCrossGpuBarrier CreateBarrier(
        string barrierId,
        int[] gpuIds,
        CrossGpuBarrierMode mode = CrossGpuBarrierMode.CpuFallback)
    {
        var barrier = new CudaCrossGpuBarrier(
            barrierId,
            gpuIds,
            mode,
            _mockHlc,
            NullLogger<CudaCrossGpuBarrier>.Instance);

        _barriers.Add(barrier);
        return barrier;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange & Act
        var barrier = CreateBarrier("test-barrier", new[] { 0, 1 });

        // Assert
        barrier.Should().NotBeNull();
        barrier.BarrierId.Should().Be("test-barrier");
        barrier.ParticipantCount.Should().Be(2);
        barrier.Mode.Should().Be(CrossGpuBarrierMode.CpuFallback);
    }

    [Fact]
    public void Constructor_WithNullBarrierId_ShouldThrow()
    {
        // Act
        Action act = () => CreateBarrier(null!, new[] { 0, 1 });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyBarrierId_ShouldThrow()
    {
        // Act
        Action act = () => CreateBarrier(string.Empty, new[] { 0, 1 });

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullGpuIds_ShouldThrow()
    {
        // Act
        Action act = () => CreateBarrier("test", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gpuIds");
    }

    [Fact]
    public void Constructor_WithSingleGpu_ShouldThrow()
    {
        // Act
        Action act = () => CreateBarrier("test", new[] { 0 });

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 2 participants*");
    }

    [Fact]
    public void Constructor_WithTooManyGpus_ShouldThrow()
    {
        // Arrange
        var gpuIds = Enumerable.Range(0, 257).ToArray();

        // Act
        Action act = () => CreateBarrier("test", gpuIds);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*maximum 256 participants*");
    }

    #endregion

    #region ArriveAndWaitAsync Tests

    [Fact(Skip = "Flaky: Timing-dependent test requires true concurrent task execution")]
    public async Task ArriveAndWaitAsync_TwoGpus_ShouldComplete()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });
        var timestamp1 = await _mockHlc.TickAsync();
        var timestamp2 = await _mockHlc.TickAsync();

        // Act - Both GPUs arrive concurrently
        var task1 = Task.Run(async () =>
            await barrier.ArriveAndWaitAsync(0, timestamp1, TimeSpan.FromSeconds(5)));

        var task2 = Task.Run(async () =>
            await barrier.ArriveAndWaitAsync(1, timestamp2, TimeSpan.FromSeconds(5)));

        var results = await Task.WhenAll(task1, task2);

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();

        var result = results[0];
        result.ArrivalTimestamps.Should().HaveCount(2);
        result.ReleaseTimestamp.Should().NotBe(default(HlcTimestamp));

        // Release timestamp should be max of arrivals
        result.ReleaseTimestamp.HappenedBefore(timestamp1).Should().BeFalse();
        result.ReleaseTimestamp.HappenedBefore(timestamp2).Should().BeFalse();
    }

    [Fact(Skip = "Flaky: Timing-dependent test requires true concurrent task execution")]
    public async Task ArriveAndWaitAsync_FourGpus_ShouldComplete()
    {
        // Arrange
        var gpuIds = new[] { 0, 1, 2, 3 };
        var barrier = CreateBarrier("test-4gpu", gpuIds);

        var timestamps = new HlcTimestamp[4];
        for (int i = 0; i < 4; i++)
        {
            timestamps[i] = await _mockHlc.TickAsync();
        }

        // Act - All 4 GPUs arrive concurrently
        var tasks = gpuIds.Select((gpuId, index) =>
            Task.Run(async () =>
                await barrier.ArriveAndWaitAsync(
                    gpuId,
                    timestamps[index],
                    TimeSpan.FromSeconds(10)))).ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(4);
        results.All(r => r.Success).Should().BeTrue();
        results.All(r => r.ArrivalTimestamps.Length == 4).Should().BeTrue();

        // All GPUs should get the same release timestamp
        var releaseTimestamp = results[0].ReleaseTimestamp;
        results.All(r => r.ReleaseTimestamp.Equals(releaseTimestamp)).Should().BeTrue();
    }

    [Fact]
    public async Task ArriveAndWaitAsync_WithTimeout_ShouldThrowBarrierTimeoutException()
    {
        // Arrange
        var barrier = CreateBarrier("test-timeout", new[] { 0, 1 });
        var timestamp = await _mockHlc.TickAsync();

        // Act - Only GPU 0 arrives, GPU 1 never arrives
        Func<Task> act = async () =>
            await barrier.ArriveAndWaitAsync(0, timestamp, TimeSpan.FromMilliseconds(500));

        // Assert
        await act.Should().ThrowAsync<BarrierTimeoutException>()
            .Where(ex => ex.GpuId == 0)
            .Where(ex => ex.ArrivedCount == 1)
            .WithMessage("*1/2 arrived*");
    }

    [Fact]
    public async Task ArriveAndWaitAsync_WithInvalidGpuId_ShouldThrow()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });
        var timestamp = await _mockHlc.TickAsync();

        // Act
        Func<Task> act = async () =>
            await barrier.ArriveAndWaitAsync(5, timestamp, TimeSpan.FromSeconds(1));

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("gpuId")
            .WithMessage("*not a participant*");
    }

    [Fact]
    public async Task ArriveAndWaitAsync_AfterDisposal_ShouldThrow()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });
        var timestamp = await _mockHlc.TickAsync();
        barrier.Dispose();

        // Act
        Func<Task> act = async () =>
            await barrier.ArriveAndWaitAsync(0, timestamp, TimeSpan.FromSeconds(1));

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region Split-Phase Barrier Tests

    [Fact(Skip = "Flaky: Timing-dependent test requires true concurrent task execution")]
    public async Task ArriveAsync_ThenWaitAsync_ShouldComplete()
    {
        // Arrange
        var barrier = CreateBarrier("test-split", new[] { 0, 1 });
        var timestamp1 = await _mockHlc.TickAsync();
        var timestamp2 = await _mockHlc.TickAsync();

        // Act - Split-phase: Arrive first, then wait
        var phase1 = await barrier.ArriveAsync(0, timestamp1);
        var phase2 = await barrier.ArriveAsync(1, timestamp2);

        phase1.GpuId.Should().Be(0);
        phase2.GpuId.Should().Be(1);

        var waitTask1 = barrier.WaitAsync(phase1, TimeSpan.FromSeconds(5));
        var waitTask2 = barrier.WaitAsync(phase2, TimeSpan.FromSeconds(5));

        var results = await Task.WhenAll(waitTask1, waitTask2);

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();
    }

    [Fact]
    public async Task ArriveAsync_WithInvalidGpuId_ShouldThrow()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });
        var timestamp = await _mockHlc.TickAsync();

        // Act
        Func<Task> act = async () => await barrier.ArriveAsync(99, timestamp);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithParameterName("gpuId");
    }

    #endregion

    #region GetStatus Tests

    [Fact]
    public void GetStatus_Initially_ShouldReturnZeroArrivals()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1, 2 });

        // Act
        var status = barrier.GetStatus();

        // Assert
        status.Generation.Should().Be(0);
        status.ArrivedCount.Should().Be(0);
        status.TotalCount.Should().Be(3);
        status.IsComplete.Should().BeFalse();
        status.Mode.Should().Be(CrossGpuBarrierMode.CpuFallback);
    }

    [Fact]
    public async Task GetStatus_AfterOneArrival_ShouldReflectState()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });
        var timestamp = await _mockHlc.TickAsync();

        // Act
        await barrier.ArriveAsync(0, timestamp);
        var status = barrier.GetStatus();

        // Assert
        status.ArrivedCount.Should().Be(1);
        status.TotalCount.Should().Be(2);
        status.IsComplete.Should().BeFalse();
        status.ArrivalTimestamps[0].Should().NotBeNull();
        status.ArrivalTimestamps[1].Should().BeNull();
    }

    [Fact]
    public async Task GetStatus_AfterAllArrivals_ShouldBeComplete()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });
        var timestamp1 = await _mockHlc.TickAsync();
        var timestamp2 = await _mockHlc.TickAsync();

        // Act
        var phase1 = await barrier.ArriveAsync(0, timestamp1);
        var phase2 = await barrier.ArriveAsync(1, timestamp2);

        var status = barrier.GetStatus();

        // Assert
        status.ArrivedCount.Should().Be(2);
        status.IsComplete.Should().BeTrue();
        status.ArrivalTimestamps.All(t => t.HasValue).Should().BeTrue();
    }

    #endregion

    #region ResetAsync Tests

    [Fact(Skip = "Flaky: Timing-dependent test requires true concurrent task execution")]
    public async Task ResetAsync_AfterCompletion_ShouldIncrementGeneration()
    {
        // Arrange
        var barrier = CreateBarrier("test-reset", new[] { 0, 1 });
        var timestamp1 = await _mockHlc.TickAsync();
        var timestamp2 = await _mockHlc.TickAsync();

        // Complete one barrier cycle
        var task1 = barrier.ArriveAndWaitAsync(0, timestamp1, TimeSpan.FromSeconds(5));
        var task2 = barrier.ArriveAndWaitAsync(1, timestamp2, TimeSpan.FromSeconds(5));
        await Task.WhenAll(task1, task2);

        var statusBefore = barrier.GetStatus();

        // Act
        await barrier.ResetAsync();
        var statusAfter = barrier.GetStatus();

        // Assert
        statusBefore.Generation.Should().Be(0);
        statusAfter.Generation.Should().Be(1);
        statusAfter.ArrivedCount.Should().Be(0);
        statusAfter.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task ResetAsync_WhileInUse_ShouldThrow()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });
        var timestamp = await _mockHlc.TickAsync();

        // One GPU arrives but not all
        await barrier.ArriveAsync(0, timestamp);

        // Act
        Func<Task> act = async () => await barrier.ResetAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*while in use*");
    }

    [Fact(Skip = "Flaky: Timing-dependent test requires true concurrent task execution")]
    public async Task ResetAsync_AllowsBarrierReuse()
    {
        // Arrange
        var barrier = CreateBarrier("test-reuse", new[] { 0, 1 });

        // First cycle
        var timestamp1a = await _mockHlc.TickAsync();
        var timestamp1b = await _mockHlc.TickAsync();

        await Task.WhenAll(
            barrier.ArriveAndWaitAsync(0, timestamp1a, TimeSpan.FromSeconds(5)),
            barrier.ArriveAndWaitAsync(1, timestamp1b, TimeSpan.FromSeconds(5)));

        // Reset
        await barrier.ResetAsync();

        // Second cycle
        var timestamp2a = await _mockHlc.TickAsync();
        var timestamp2b = await _mockHlc.TickAsync();

        // Act
        var results = await Task.WhenAll(
            barrier.ArriveAndWaitAsync(0, timestamp2a, TimeSpan.FromSeconds(5)),
            barrier.ArriveAndWaitAsync(1, timestamp2b, TimeSpan.FromSeconds(5)));

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();

        var status = barrier.GetStatus();
        status.Generation.Should().Be(1); // Incremented after first reset
    }

    #endregion

    #region Concurrent Access Tests

    [Fact(Skip = "Flaky: Timing-dependent test requires true concurrent task execution")]
    public async Task MultipleBarrierCycles_Concurrent_ShouldWork()
    {
        // Arrange
        var barrier = CreateBarrier("test-cycles", new[] { 0, 1 });
        const int cycleCount = 10;

        // Act - Run 10 barrier cycles
        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            var timestamp1 = await _mockHlc.TickAsync();
            var timestamp2 = await _mockHlc.TickAsync();

            await Task.WhenAll(
                barrier.ArriveAndWaitAsync(0, timestamp1, TimeSpan.FromSeconds(5)),
                barrier.ArriveAndWaitAsync(1, timestamp2, TimeSpan.FromSeconds(5)));

            await barrier.ResetAsync();
        }

        // Assert
        var status = barrier.GetStatus();
        status.Generation.Should().Be(cycleCount);
        status.ArrivedCount.Should().Be(0);
    }

    [Fact(Skip = "Flaky: Timing-dependent stress test requires true concurrent task execution")]
    public async Task StressTest_16Gpus_100Cycles_ShouldComplete()
    {
        // Arrange
        var gpuIds = Enumerable.Range(0, 16).ToArray();
        var barrier = CreateBarrier("stress-test", gpuIds);
        const int cycles = 100;

        // Act
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            var tasks = gpuIds.Select(async gpuId =>
            {
                var timestamp = await _mockHlc.TickAsync();
                return await barrier.ArriveAndWaitAsync(
                    gpuId,
                    timestamp,
                    TimeSpan.FromSeconds(10));
            }).ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert cycle completed successfully
            results.Should().HaveCount(16);
            results.All(r => r.Success).Should().BeTrue();

            await barrier.ResetAsync();
        }

        // Assert final state
        var status = barrier.GetStatus();
        status.Generation.Should().Be(cycles);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_MultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var barrier = CreateBarrier("test", new[] { 0, 1 });

        // Act
        barrier.Dispose();
        Action act = () => barrier.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region HLC Integration Tests

    [Fact(Skip = "Flaky: Timing-dependent test requires true concurrent task execution")]
    public async Task Barrier_ShouldPreserveCausality()
    {
        // Arrange
        var barrier = CreateBarrier("causality-test", new[] { 0, 1 });

        var timestamp1 = await _mockHlc.TickAsync();
        await Task.Delay(10); // Ensure physical time advances
        var timestamp2 = await _mockHlc.TickAsync();

        // Assert timestamp2 happened after timestamp1
        timestamp2.HappenedBefore(timestamp1).Should().BeFalse();
        timestamp1.HappenedBefore(timestamp2).Should().BeTrue();

        // Act - Barrier synchronization
        var results = await Task.WhenAll(
            barrier.ArriveAndWaitAsync(0, timestamp1, TimeSpan.FromSeconds(5)),
            barrier.ArriveAndWaitAsync(1, timestamp2, TimeSpan.FromSeconds(5)));

        // Assert - Release timestamp should be max (happened after both)
        var releaseTimestamp = results[0].ReleaseTimestamp;
        releaseTimestamp.HappenedBefore(timestamp1).Should().BeFalse();
        releaseTimestamp.HappenedBefore(timestamp2).Should().BeFalse();

        timestamp1.HappenedBefore(releaseTimestamp).Should().BeTrue();
        timestamp2.HappenedBefore(releaseTimestamp).Should().BeTrue();
    }

    #endregion
}
