// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using NSubstitute;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnifiedBufferSync covering all synchronization functionality.
/// Target: 100% coverage for 513-line synchronization class.
/// Tests all state transitions, async operations, and thread safety.
/// </summary>
public sealed class UnifiedBufferSyncComprehensiveTests : IDisposable
{
    private readonly IUnifiedMemoryManager _mockMemoryManager;
    private readonly List<IDisposable> _disposables = [];

    public UnifiedBufferSyncComprehensiveTests()
    {
        _mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        _ = _mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);

        // Setup mock for memory operations
        _ = _mockMemoryManager.AllocateDevice(Arg.Any<long>()).Returns(new DeviceMemory(new IntPtr(0x1000), 1024));
        _mockMemoryManager.When(x => x.CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.When(x => x.CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
        _mockMemoryManager.CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
        _mockMemoryManager.When(x => x.MemsetDevice(Arg.Any<DeviceMemory>(), Arg.Any<byte>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.MemsetDeviceAsync(Arg.Any<DeviceMemory>(), Arg.Any<byte>(), Arg.Any<long>())
            .Returns(ValueTask.CompletedTask);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }

    #region EnsureOnHost Tests

    [Fact]
    public void EnsureOnHost_WhenHostOnly_CompletesWithoutCopy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.EnsureOnHost();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _mockMemoryManager.DidNotReceive().CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public void EnsureOnHost_WhenSynchronized_CompletesWithoutCopy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice(); // Move to synchronized state
        buffer.Synchronize();

        // Act
        buffer.EnsureOnHost();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void EnsureOnHost_WhenDeviceOnly_CopiesFromDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost(); // Make it device-only

        // Act
        buffer.EnsureOnHost();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _mockMemoryManager.Received(1).CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public void EnsureOnHost_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.EnsureOnHost;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnsureOnHost_ThreadSafety_HandlesMultipleConcurrentCalls()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);
        var tasks = new Task[10];

        // Act
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(buffer.EnsureOnHost);
        }
        Task.WaitAll(tasks);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    #endregion

    #region EnsureOnDevice Tests

    [Fact]
    public void EnsureOnDevice_WhenHostOnly_CopiestoDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.EnsureOnDevice();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        _mockMemoryManager.Received(1).CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public void EnsureOnDevice_WhenDeviceOnly_CompletesWithoutCopy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost(); // Make it device-only

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        buffer.EnsureOnDevice();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        _mockMemoryManager.DidNotReceive().CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public void EnsureOnDevice_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.EnsureOnDevice;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void EnsureOnDevice_ThreadSafety_HandlesMultipleConcurrentCalls()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);
        var tasks = new Task[10];

        // Act
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(buffer.EnsureOnDevice);
        }
        Task.WaitAll(tasks);

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    #endregion

    #region EnsureOnHostAsync Tests

    [Fact]
    public async Task EnsureOnHostAsync_WhenHostOnly_CompletesWithoutCopy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        await buffer.EnsureOnHostAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        await _mockMemoryManager.DidNotReceive().CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public async Task EnsureOnHostAsync_WhenDeviceOnly_CopiesFromDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        await buffer.EnsureOnDeviceAsync();
        buffer.InvalidateHost();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        await buffer.EnsureOnHostAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        await _mockMemoryManager.Received(1).CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public async Task EnsureOnHostAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = async () => await buffer.EnsureOnHostAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task EnsureOnHostAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await buffer.EnsureOnHostAsync(default, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnsureOnHostAsync_ConcurrentCalls_HandlesProperlyWithAsyncLock()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);
        var tasks = new Task[10];

        // Act
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = buffer.EnsureOnHostAsync().AsTask();
        }
        await Task.WhenAll(tasks);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    #endregion

    #region EnsureOnDeviceAsync Tests

    [Fact]
    public async Task EnsureOnDeviceAsync_WhenHostOnly_CopiesToDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        await buffer.EnsureOnDeviceAsync();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        await _mockMemoryManager.Received(1).CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_WhenDeviceOnly_CompletesWithoutCopy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        await buffer.EnsureOnDeviceAsync();
        buffer.InvalidateHost();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        await buffer.EnsureOnDeviceAsync();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        await _mockMemoryManager.DidNotReceive().CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = async () => await buffer.EnsureOnDeviceAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await buffer.EnsureOnDeviceAsync(default, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task EnsureOnDeviceAsync_ConcurrentCalls_HandlesProperlyWithAsyncLock()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 1000);
        _disposables.Add(buffer);
        var tasks = new Task[10];

        // Act
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = buffer.EnsureOnDeviceAsync().AsTask();
        }
        await Task.WhenAll(tasks);

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    #endregion

    #region Synchronize Tests

    [Fact]
    public void Synchronize_WhenAlreadySynchronized_CompletesWithoutCopy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        buffer.Synchronize();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        _mockMemoryManager.DidNotReceive().CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
        _mockMemoryManager.DidNotReceive().CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public void Synchronize_WhenHostDirty_CopiesToDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();
        buffer.InvalidateDevice(); // Make host dirty

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        buffer.Synchronize();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        _mockMemoryManager.Received(1).CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public void Synchronize_WhenDeviceDirty_CopiesFromDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();
        buffer.InvalidateHost(); // Make device dirty

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        buffer.Synchronize();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        _mockMemoryManager.Received(1).CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public void Synchronize_WhenHostOnly_CopiesToDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.Synchronize();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        _mockMemoryManager.Received(1).CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public void Synchronize_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.Synchronize;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region SynchronizeAsync Tests

    [Fact]
    public async Task SynchronizeAsync_WhenAlreadySynchronized_CompletesWithoutCopy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        await buffer.SynchronizeAsync();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        await buffer.SynchronizeAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        await _mockMemoryManager.DidNotReceive().CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
        await _mockMemoryManager.DidNotReceive().CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public async Task SynchronizeAsync_WhenHostDirty_CopiesToDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        await buffer.SynchronizeAsync();
        buffer.InvalidateDevice();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        await buffer.SynchronizeAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        await _mockMemoryManager.Received(1).CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDeviceDirty_CopiesFromDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        await buffer.SynchronizeAsync();
        buffer.InvalidateHost();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        await buffer.SynchronizeAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        await _mockMemoryManager.Received(1).CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = async () => await buffer.SynchronizeAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SynchronizeAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await buffer.SynchronizeAsync(default, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region InvalidateDevice Tests

    [Fact]
    public void InvalidateDevice_WhenSynchronized_MarksHostDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();

        // Act
        buffer.InvalidateDevice();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        // Device should be invalidated
    }

    [Fact]
    public void InvalidateDevice_WhenHostOnly_RemainsHostOnly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.InvalidateDevice();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void InvalidateDevice_WhenDeviceOnly_CopiesAndMarksHostDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();

        _mockMemoryManager.ClearReceivedCalls();

        // Act
        buffer.InvalidateDevice();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _mockMemoryManager.Received(1).CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>());
    }

    [Fact]
    public void InvalidateDevice_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.InvalidateDevice;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region InvalidateHost Tests

    [Fact]
    public void InvalidateHost_WhenSynchronized_MarksDeviceDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.Synchronize();

        // Act
        buffer.InvalidateHost();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        // Host should be invalidated
    }

    [Fact]
    public void InvalidateHost_WhenDeviceOnly_RemainsDeviceOnly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.InvalidateHost();

        // Act
        buffer.InvalidateHost();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void InvalidateHost_WhenHostOnly_CopiesAndMarksDeviceDirty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.InvalidateHost();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        _mockMemoryManager.Received(1).CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>());
    }

    [Fact]
    public void InvalidateHost_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.InvalidateHost;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Map Tests

    [Fact]
    public void Map_WhenValid_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        using var mapped = buffer.Map();

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(100);
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void Map_WithReadMode_EnsuresDataOnHost()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        using var mapped = buffer.Map(Abstractions.Memory.MapMode.Read);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = mapped.Length.Should().Be(100);
    }

    [Fact]
    public void Map_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.Map();

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region MapRange Tests

    [Fact]
    public void MapRange_WithValidRange_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        using var mapped = buffer.MapRange(10, 50);

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(50);
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void MapRange_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.MapRange(-1, 50);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithNegativeLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.MapRange(10, -5);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WithOutOfRangeOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.MapRange(90, 20);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MapRange_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.MapRange(0, 10);

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region MapAsync Tests

    [Fact]
    public async Task MapAsync_WhenValid_ReturnsMappedMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        using var mapped = await buffer.MapAsync();

        // Assert
        _ = mapped.Should().NotBeNull();
        _ = mapped.Length.Should().Be(100);
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public async Task MapAsync_WithReadMode_EnsuresDataOnHost()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        using var mapped = await buffer.MapAsync(Abstractions.Memory.MapMode.Read);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = mapped.Length.Should().Be(100);
    }

    [Fact]
    public async Task MapAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = async () => await buffer.MapAsync();

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task MapAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await buffer.MapAsync(Abstractions.Memory.MapMode.ReadWrite, cts.Token);

        // Assert
        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void CompleteWorkflow_HostToDeviceToHostSync_MaintainsDataIntegrity()
    {
        // Arrange
        var data = new int[] { 1, 2, 3, 4, 5 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        buffer.EnsureOnHost();
        buffer.EnsureOnDevice();
        buffer.Synchronize();
        buffer.InvalidateDevice();
        buffer.Synchronize();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        var result = buffer.AsReadOnlySpan();
        _ = result.ToArray().Should().Equal(data);
    }

    [Fact]
    public async Task CompleteWorkflow_AsyncOperations_MaintainsDataIntegrity()
    {
        // Arrange
        var data = new int[] { 10, 20, 30, 40, 50 };
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, data.AsSpan());
        _disposables.Add(buffer);

        // Act
        await buffer.EnsureOnHostAsync();
        await buffer.EnsureOnDeviceAsync();
        await buffer.SynchronizeAsync();
        buffer.InvalidateHost();
        await buffer.SynchronizeAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
        var result = buffer.AsReadOnlySpan();
        _ = result.ToArray().Should().Equal(data);
    }

    [Fact]
    public void StateTransitions_MultipleInvalidations_HandlesCorrectly()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act & Assert
        buffer.Synchronize();
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();

        buffer.InvalidateDevice();
        _ = buffer.IsOnHost.Should().BeTrue();

        buffer.InvalidateHost();
        _ = buffer.IsOnDevice.Should().BeTrue();

        buffer.Synchronize();
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    #endregion
}
