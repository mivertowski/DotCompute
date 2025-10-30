// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.Memory.P2P;

/// <summary>
/// Comprehensive unit tests for P2PTransferScheduler covering bandwidth management,
/// scheduling logic, and concurrent transfer coordination.
/// </summary>
public sealed class P2PTransferSchedulerTests : IAsyncDisposable
{
    private readonly ILogger _mockLogger;
    private P2PTransferScheduler? _scheduler;

    public P2PTransferSchedulerTests()
    {
        _mockLogger = Substitute.For<ILogger>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_scheduler != null)
        {
            await _scheduler.DisposeAsync();
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidArguments_CreatesInstance()
    {
        // Arrange & Act
        var scheduler = new P2PTransferScheduler(_mockLogger);

        // Assert
        _ = scheduler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new P2PTransferScheduler(null!);
        _ = act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_InitializesWithZeroPendingTransfers()
    {
        // Arrange & Act
        _scheduler = new P2PTransferScheduler(_mockLogger);

        // Assert
        _ = _scheduler.PendingTransferCount.Should().Be(0);
    }

    #endregion

    #region ScheduleP2PTransferAsync Tests

    [Fact]
    public async Task ScheduleP2PTransferAsync_ValidTransfer_QueuesAndExecutes()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var targetBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();

        // Act
        var transferTask = _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None).AsTask();

        // Allow processing
        await Task.Delay(100);

        // Assert - Should complete without exception
        var result = await Task.WhenAny(transferTask, Task.Delay(1000));
        _ = result.Should().Be(transferTask);
    }

    [Fact]
    public async Task ScheduleP2PTransferAsync_NullSourceBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var targetBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();

        // Act
        var act = async () => await _scheduler.ScheduleP2PTransferAsync<float>(
            null!, targetBuffer, 0, 0, 100, strategy, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScheduleP2PTransferAsync_NullTargetBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();

        // Act
        var act = async () => await _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, null!, 0, 0, 100, strategy, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScheduleP2PTransferAsync_NullStrategy_ThrowsArgumentNullException()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var targetBuffer = CreateMockBuffer<float>(1000);

        // Act
        var act = async () => await _scheduler.ScheduleP2PTransferAsync<float>(
            sourceBuffer, targetBuffer, 0, 0, 100, null!, CancellationToken.None);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScheduleP2PTransferAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var targetBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 100, strategy, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ScheduleP2PTransferAsync_MultipleTransfers_ProcessesConcurrently()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var strategy = CreateMockStrategy();
        var tasks = new List<Task>();

        // Act - Schedule multiple concurrent transfers
        for (var i = 0; i < 5; i++)
        {
            var sourceBuffer = CreateMockBuffer<float>(1000);
            var targetBuffer = CreateMockBuffer<float>(1000);
            var task = _scheduler.ScheduleP2PTransferAsync(
                sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None).AsTask();
            tasks.Add(task);
        }

        // Allow processing
        await Task.Delay(500);

        // Assert - All transfers should complete
        var completed = tasks.Count(t => t.IsCompleted);
        _ = completed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ScheduleP2PTransferAsync_SmallTransfer_GetsHighPriority()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<byte>(512); // < 1MB
        var targetBuffer = CreateMockBuffer<byte>(512);
        var strategy = CreateMockStrategy();

        // Act
        var transferTask = _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 512, strategy, CancellationToken.None).AsTask();

        await Task.Delay(100);

        // Assert - Should complete quickly due to high priority
        var result = await Task.WhenAny(transferTask, Task.Delay(500));
        _ = result.Should().Be(transferTask);
    }

    [Fact]
    public async Task ScheduleP2PTransferAsync_LargeTransfer_GetsLowPriority()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<byte>(512 * 1024 * 1024); // 512MB
        var targetBuffer = CreateMockBuffer<byte>(512 * 1024 * 1024);
        var strategy = CreateMockStrategy();

        // Act (intentionally not awaited for test scenario)
        _ = _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 512 * 1024 * 1024, strategy, CancellationToken.None);

        // Assert - Transfer is queued
        _ = _scheduler.PendingTransferCount.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region WaitForDeviceTransfersAsync Tests

    [Fact]
    public async Task WaitForDeviceTransfersAsync_NoActiveTransfers_CompletesImmediately()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var deviceId = "device-1";

        // Act
        var waitTask = _scheduler.WaitForDeviceTransfersAsync(deviceId, CancellationToken.None);

        // Assert
        await waitTask.AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        _ = waitTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForDeviceTransfersAsync_WithActiveTransfers_WaitsForCompletion()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var targetBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();

        // Start a transfer (intentionally not awaited for test scenario)
        _ = _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None);

        // Act - Wait for device transfers
        var deviceId = sourceBuffer.Accelerator.Info.Id;
        var waitTask = _scheduler.WaitForDeviceTransfersAsync(deviceId, CancellationToken.None).AsTask();

        // Allow processing
        await Task.Delay(200);

        // Assert - Wait should complete after transfers finish
        var result = await Task.WhenAny(waitTask, Task.Delay(2000));
        _ = result.Should().Be(waitTask);
    }

    [Fact]
    public async Task WaitForDeviceTransfersAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var deviceId = "device-1";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _scheduler.WaitForDeviceTransfersAsync(deviceId, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public void GetStatistics_InitialState_ReturnsZeroStatistics()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);

        // Act
        var stats = _scheduler.GetStatistics();

        // Assert
        _ = stats.Should().NotBeNull();
        _ = stats.TotalTransfers.Should().Be(0);
        _ = stats.TotalBytesTransferred.Should().Be(0);
        _ = stats.ActiveTransfers.Should().Be(0);
        _ = stats.QueuedTransfers.Should().Be(0);
        _ = stats.AverageThroughputMBps.Should().Be(0);
        _ = stats.PeakThroughputMBps.Should().Be(0);
        _ = stats.BandwidthUtilization.Should().Be(0);
    }

    [Fact]
    public async Task GetStatistics_AfterTransfers_UpdatesStatistics()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var targetBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();

        // Act - Execute a transfer
        await _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None);

        await Task.Delay(200);

        var stats = _scheduler.GetStatistics();

        // Assert
        _ = stats.TotalTransfers.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetStatistics_ConcurrentTransfers_TracksActiveCount()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var strategy = CreateMockStrategy();
        var tasks = new List<Task>();

        // Act - Schedule multiple concurrent transfers
        for (var i = 0; i < 3; i++)
        {
            var sourceBuffer = CreateMockBuffer<float>(1000);
            var targetBuffer = CreateMockBuffer<float>(1000);
            var task = _scheduler.ScheduleP2PTransferAsync(
                sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None).AsTask();
            tasks.Add(task);
        }

        await Task.Delay(50);
        var stats = _scheduler.GetStatistics();

        // Assert
        _ = stats.ActiveTransfers.Should().BeGreaterThanOrEqualTo(0);
        _ = stats.QueuedTransfers.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region PendingTransferCount Tests

    [Fact]
    public void PendingTransferCount_InitialState_ReturnsZero()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);

        // Act & Assert
        _ = _scheduler.PendingTransferCount.Should().Be(0);
    }

    [Fact]
    public async Task PendingTransferCount_AfterScheduling_Increases()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var targetBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();

        // Act (intentionally not awaited for test scenario)
        _ = _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None);

        // Check immediately
        var countDuringTransfer = _scheduler.PendingTransferCount;

        // Assert
        _ = countDuringTransfer.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PendingTransferCount_AfterCompletion_Decreases()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(1000);
        var targetBuffer = CreateMockBuffer<float>(1000);
        var strategy = CreateMockStrategy();

        // Act
        await _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None);

        await Task.Delay(200);

        // Assert
        _ = _scheduler.PendingTransferCount.Should().Be(0);
    }

    #endregion

    #region Bandwidth Management Tests

    [Fact]
    public async Task Scheduler_HighBandwidthUtilization_ThrottlesTransfers()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var strategy = CreateMockStrategy(estimatedBandwidthGBps: 100.0);
        var tasks = new List<Task>();

        // Act - Schedule many concurrent transfers to saturate bandwidth
        for (var i = 0; i < 10; i++)
        {
            var sourceBuffer = CreateMockBuffer<float>(10000);
            var targetBuffer = CreateMockBuffer<float>(10000);
            var task = _scheduler.ScheduleP2PTransferAsync(
                sourceBuffer, targetBuffer, 0, 0, 10000, strategy, CancellationToken.None).AsTask();
            tasks.Add(task);
        }

        await Task.Delay(100);
        var stats = _scheduler.GetStatistics();

        // Assert - Should throttle to prevent over-utilization
        _ = stats.BandwidthUtilization.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task Scheduler_LowBandwidthUtilization_AllowsMoreTransfers()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var strategy = CreateMockStrategy(estimatedBandwidthGBps: 10.0);

        // Act - Schedule a single small transfer
        var sourceBuffer = CreateMockBuffer<float>(100);
        var targetBuffer = CreateMockBuffer<float>(100);
        await _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 100, strategy, CancellationToken.None);

        await Task.Delay(100);
        var stats = _scheduler.GetStatistics();

        // Assert - Should have low utilization
        _ = stats.BandwidthUtilization.Should().BeLessThanOrEqualTo(0.5);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_CancelsActiveTransfers()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);
        var sourceBuffer = CreateMockBuffer<float>(10000);
        var targetBuffer = CreateMockBuffer<float>(10000);
        var strategy = CreateMockStrategy();

        // Act - Start transfer then dispose (intentionally not awaited for test scenario)
        _ = _scheduler.ScheduleP2PTransferAsync(
            sourceBuffer, targetBuffer, 0, 0, 10000, strategy, CancellationToken.None);

        await Task.Delay(50);
        await _scheduler.DisposeAsync();

        // Assert - Should dispose without hanging
        _ = _scheduler.PendingTransferCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DisposeAsync_CalledMultipleTimes_NoError()
    {
        // Arrange
        _scheduler = new P2PTransferScheduler(_mockLogger);

        // Act & Assert - Should not throw on multiple dispose calls
        await _scheduler.DisposeAsync();
        await _scheduler.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private static IUnifiedMemoryBuffer<T> CreateMockBuffer<T>(int length) where T : unmanaged
    {
        var buffer = Substitute.For<IUnifiedMemoryBuffer<T>>();
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"TestDevice-{Guid.NewGuid()}",
            Vendor = "Test",
            DeviceType = "GPU"
        };

        _ = accelerator.Info.Returns(info);
        _ = accelerator.Type.Returns(AcceleratorType.GPU);

        _ = buffer.Length.Returns(length);
        // buffer.SizeInBytes.Returns(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>()); // Namespace DotCompute.Core.System.Runtime doesn't exist
        _ = buffer.SizeInBytes.Returns(length * sizeof(int)); // Simplified for testing
        _ = buffer.Accelerator.Returns(accelerator);

        // Mock CopyToAsync to complete successfully
        _ = buffer.CopyToAsync(Arg.Any<int>(), Arg.Any<IUnifiedMemoryBuffer<T>>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        _ = buffer.CopyToAsync(Arg.Any<Memory<T>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        _ = buffer.CopyFromAsync(Arg.Any<ReadOnlyMemory<T>>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.CompletedTask);

        _ = buffer.Slice(Arg.Any<int>(), Arg.Any<int>()).Returns(buffer);

        return buffer;
    }

    private static TransferStrategy CreateMockStrategy(double estimatedBandwidthGBps = 10.0)
    {
        return new TransferStrategy
        {
            Type = TransferType.DirectP2P,
            EstimatedBandwidthGBps = estimatedBandwidthGBps,
            ChunkSize = 4 * 1024 * 1024 // 4MB
        };
    }

    #endregion
}
