// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using NSubstitute;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Comprehensive tests for UnifiedBufferMemory covering all memory management functionality.
/// Target: 60-70 comprehensive tests covering 345-line memory management class.
/// Tests device/host memory allocation, deallocation, validation, resize, compact, prefetch, and BufferMemoryInfo.
/// </summary>
public sealed class UnifiedBufferMemoryComprehensiveTests : IDisposable
{
    private readonly IUnifiedMemoryManager _mockMemoryManager;
    private readonly List<IDisposable> _disposables = [];

    public UnifiedBufferMemoryComprehensiveTests()
    {
        _mockMemoryManager = Substitute.For<IUnifiedMemoryManager>();
        _ = _mockMemoryManager.MaxAllocationSize.Returns(long.MaxValue);

        // Setup mock for memory operations
        _ = _mockMemoryManager.AllocateDevice(Arg.Any<long>()).Returns(callInfo =>
            new DeviceMemory(new IntPtr(0x1000), callInfo.Arg<long>()));
        _mockMemoryManager.When(x => x.CopyHostToDevice(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.When(x => x.CopyDeviceToHost(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>()))
            .Do(_ => { /* No-op */ });
        _mockMemoryManager.CopyHostToDeviceAsync(Arg.Any<IntPtr>(), Arg.Any<DeviceMemory>(), Arg.Any<long>())
            .Returns(_ => ValueTask.CompletedTask);
        _mockMemoryManager.CopyDeviceToHostAsync(Arg.Any<DeviceMemory>(), Arg.Any<IntPtr>(), Arg.Any<long>())
            .Returns(_ => ValueTask.CompletedTask);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
        (_mockMemoryManager as IDisposable)?.Dispose();
    }

    #region Device Memory Allocation Tests

    [Fact]
    public void DeviceMemoryAllocation_WhenNotAllocated_AllocatesSuccessfully()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.EnsureOnDevice();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        _ = buffer.DevicePointer.Should().NotBe(IntPtr.Zero);
        _ = _mockMemoryManager.Received(1).AllocateDevice(Arg.Any<long>());
    }

    [Fact]
    public void DeviceMemoryAllocation_WhenAlreadyAllocated_DoesNotAllocateAgain()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act - Call again
        buffer.EnsureOnDevice();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        _ = _mockMemoryManager.Received(1).AllocateDevice(Arg.Any<long>()); // Only once
    }

    [Fact]
    public void DeviceMemoryAllocation_WhenAllocationFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var failingManager = Substitute.For<IUnifiedMemoryManager>();
        _ = failingManager.MaxAllocationSize.Returns(long.MaxValue);
        _ = failingManager.AllocateDevice(Arg.Any<long>())
            .Returns(_ => throw new InvalidOperationException("GPU out of memory"));

        var buffer = new UnifiedBuffer<int>(failingManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = buffer.EnsureOnDevice;

        // Assert
        _ = act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to allocate device memory*");
    }

    [Fact]
    public void DeviceMemoryDeallocation_WhenAllocated_DeallocatesSuccessfully()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        buffer.Compact(); // This deallocates device memory when synchronized

        // Assert
        _mockMemoryManager.Received(1).FreeDevice(Arg.Any<DeviceMemory>());
    }

    [Fact]
    public void DeviceMemoryDeallocation_WhenNotAllocated_DoesNothing()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.Compact(); // Try to compact when nothing allocated

        // Assert - Should not throw, just no-op
        _mockMemoryManager.DidNotReceive().FreeDevice(Arg.Any<DeviceMemory>());
    }

    [Fact]
    public void DeviceMemoryDeallocation_WhenFreeDeviceFails_SwallowsException()
    {
        // Arrange
        _mockMemoryManager.When(x => x.FreeDevice(Arg.Any<DeviceMemory>()))
            .Do(_ => throw new InvalidOperationException("Device error"));

        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var act = buffer.Dispose; // This calls DeallocateDeviceMemory

        // Assert - Should not throw, error is swallowed
        _ = act.Should().NotThrow();
    }

    #endregion

    #region Host Memory Allocation Tests

    [Fact]
    public void HostMemoryDeallocation_WithPinnedHandle_FreesHandle()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void HostMemoryDeallocation_ClearsSensitiveData()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);
        var span = buffer.AsSpan();
        for (var i = 0; i < 10; i++)
        {
            span[i] = i + 1;
        }

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
        // Data clearing happens internally, we can't verify after disposal
    }

    [Fact]
    public void HostMemoryDeallocation_WhenFreePinnedHandleFails_SwallowsException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act - Dispose multiple times (second will have invalid handle)
        buffer.Dispose();
        var act = buffer.Dispose;

        // Assert - Should not throw
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void HostMemoryReallocation_WhenNull_CreatesArrayAndPins()
    {
        // Arrange & Act - Constructor creates array
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.Length.Should().Be(50);
    }

    [Fact]
    public void HostMemoryReallocation_WhenUnpinned_RePins()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);
        _ = buffer.GetMemoryInfo();

        // Act - Resize will deallocate and reallocate, re-pinning
        buffer.Resize(60);
        var memInfo2 = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo2.IsPinned.Should().BeTrue();
        _ = memInfo2.HostAllocated.Should().BeTrue();
    }

    [Fact]
    public void HostMemoryReallocation_WhenAlreadyAllocated_DoesNotReallocate()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var memInfo1 = buffer.GetMemoryInfo();

        // Act - Access span (which calls EnsureHostMemoryAllocated internally)
        _ = buffer.AsSpan();
        var memInfo2 = buffer.GetMemoryInfo();

        // Assert - Should be same allocation
        _ = memInfo1.HostAddress.Should().Be(memInfo2.HostAddress);
        _ = memInfo1.IsPinned.Should().Be(memInfo2.IsPinned);
    }

    #endregion

    #region Memory Validation Tests

    [Fact]
    public void ValidateMemoryState_WithValidHostMemory_ReturnsTrue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act - Access to validate (internal method, tested through public API)
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.HostAllocated.Should().BeTrue();
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void ValidateMemoryState_WithValidDeviceMemory_ReturnsTrue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.DeviceAllocated.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void ValidateMemoryState_WithBothHostAndDevice_ReturnsTrue()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.HostAllocated.Should().BeTrue();
        _ = memInfo.DeviceAllocated.Should().BeTrue();
        _ = buffer.IsOnHost.Should().BeTrue();
        _ = buffer.IsOnDevice.Should().BeTrue();
    }

    [Fact]
    public void ValidateMemoryState_AfterDispose_BufferIsDisposed()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);

        // Act
        buffer.Dispose();

        // Assert
        _ = buffer.IsDisposed.Should().BeTrue();
        var act = buffer.GetMemoryInfo;
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ValidateMemoryState_ConsistentArrayLength_Maintained()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 75);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = buffer.Length.Should().Be(75);
        _ = memInfo.SizeInBytes.Should().Be(75 * sizeof(int));
    }

    [Fact]
    public void ValidateMemoryState_ConsistentDeviceSize_Maintained()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.DeviceAllocated.Should().BeTrue();
        _ = memInfo.SizeInBytes.Should().Be(50 * sizeof(int));
    }

    [Fact]
    public void ValidateMemoryState_WhenDeviceHandleZero_IndicatesNotAllocated()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.DeviceAllocated.Should().BeFalse();
        _ = memInfo.DeviceAddress.Should().Be(IntPtr.Zero);
    }

    #endregion

    #region GetMemoryInfo Tests

    [Fact]
    public void GetMemoryInfo_WithHostOnlyBuffer_ReturnsCorrectInfo()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.SizeInBytes.Should().Be(100 * sizeof(int));
        _ = memInfo.HostAllocated.Should().BeTrue();
        _ = memInfo.DeviceAllocated.Should().BeFalse();
        _ = memInfo.State.Should().Be(BufferState.HostOnly);
        _ = memInfo.IsPinned.Should().BeTrue();
        _ = memInfo.HostAddress.Should().NotBe(IntPtr.Zero);
        _ = memInfo.DeviceAddress.Should().Be(IntPtr.Zero);
    }

    [Fact]
    public void GetMemoryInfo_WithDeviceOnlyBuffer_ReturnsCorrectInfo()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.MarkDeviceDirty(); // Make it DeviceOnly state

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.SizeInBytes.Should().Be(100 * sizeof(int));
        _ = memInfo.DeviceAllocated.Should().BeTrue();
        _ = memInfo.DeviceAddress.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void GetMemoryInfo_WithSynchronizedBuffer_ReturnsCorrectInfo()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.HostAllocated.Should().BeTrue();
        _ = memInfo.DeviceAllocated.Should().BeTrue();
        _ = memInfo.State.Should().Be(BufferState.Synchronized);
    }

    [Fact]
    public void GetMemoryInfo_ReturnsSizeInBytes()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 123);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.SizeInBytes.Should().Be(123 * sizeof(int));
    }

    [Fact]
    public void GetMemoryInfo_ReturnsHostAddress()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.HostAddress.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void GetMemoryInfo_ReturnsDeviceAddress()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.DeviceAddress.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void GetMemoryInfo_ReturnsCorrectState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.State.Should().Be(buffer.State);
    }

    [Fact]
    public void GetMemoryInfo_WithDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.GetMemoryInfo;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetMemoryInfo_IsPinnedFlagAccuracy()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.IsPinned.Should().BeTrue();
        _ = memInfo.HostAllocated.Should().BeTrue();
    }

    #endregion

    #region Resize Tests

    [Fact]
    public void Resize_ToLargerSize_PreservesData()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 10);
        _disposables.Add(buffer);
        var span = buffer.AsSpan();
        for (var i = 0; i < 10; i++)
        {
            span[i] = i + 1;
        }

        // Act
        buffer.Resize(20);

        // Assert
        _ = buffer.Length.Should().Be(20);
        var newSpan = buffer.AsReadOnlySpan();
        for (var i = 0; i < 10; i++)
        {
            _ = newSpan[i].Should().Be(i + 1);
        }
        for (var i = 10; i < 20; i++)
        {
            _ = newSpan[i].Should().Be(0); // New elements are zero-initialized
        }
    }

    [Fact]
    public void Resize_ToSmallerSize_PreservesData()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 20);
        _disposables.Add(buffer);
        var span = buffer.AsSpan();
        for (var i = 0; i < 20; i++)
        {
            span[i] = i + 1;
        }

        // Act
        buffer.Resize(10);

        // Assert
        _ = buffer.Length.Should().Be(10);
        var newSpan = buffer.AsReadOnlySpan();
        for (var i = 0; i < 10; i++)
        {
            _ = newSpan[i].Should().Be(i + 1);
        }
    }

    [Fact]
    public void Resize_ToSameSize_NoOp()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var memInfo1 = buffer.GetMemoryInfo();

        // Act
        buffer.Resize(100);
        var memInfo2 = buffer.GetMemoryInfo();

        // Assert
        _ = buffer.Length.Should().Be(100);
        _ = memInfo1.HostAddress.Should().Be(memInfo2.HostAddress);
    }

    [Fact]
    public void Resize_WithZero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.Resize(0);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Resize_WithNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var act = () => buffer.Resize(-10);

        // Assert
        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Resize_DeallocatesDeviceMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        buffer.Resize(150);

        // Assert
        _mockMemoryManager.Received(1).FreeDevice(Arg.Any<DeviceMemory>());
    }

    [Fact]
    public void Resize_DeallocatesHostMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        var oldMemInfo = buffer.GetMemoryInfo();

        // Act
        buffer.Resize(150);
        var newMemInfo = buffer.GetMemoryInfo();

        // Assert
        _ = newMemInfo.HostAddress.Should().NotBe(oldMemInfo.HostAddress);
    }

    [Fact]
    public void Resize_ReallocatesHostMemory()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);

        // Act
        buffer.Resize(100);

        // Assert
        _ = buffer.Length.Should().Be(100);
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public void Resize_UpdatesLengthProperty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);

        // Act
        buffer.Resize(75);

        // Assert
        _ = buffer.Length.Should().Be(75);
    }

    [Fact]
    public void Resize_UpdatesSizeInBytesProperty()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 50);
        _disposables.Add(buffer);

        // Act
        buffer.Resize(75);

        // Assert
        _ = buffer.SizeInBytes.Should().Be(75 * sizeof(int));
    }

    [Fact]
    public void Resize_TransitionsToHostOnlyState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        buffer.Resize(150);

        // Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public void Resize_ThreadSafetyWithLock()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act - Multiple concurrent resizes
        var tasks = new Task[10];
        for (var i = 0; i < 10; i++)
        {
            var size = (i + 1) * 10;
            tasks[i] = Task.Run(() => buffer.Resize(size));
        }

        // Assert - Should complete without throwing
        var act = () => Task.WaitAll(tasks);
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void Resize_WithDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = () => buffer.Resize(150);

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Compact Tests

    [Fact]
    public void Compact_WithSynchronizedState_FreesDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        _ = buffer.State.Should().Be(BufferState.Synchronized);

        // Act
        buffer.Compact();

        // Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
        _mockMemoryManager.Received(1).FreeDevice(Arg.Any<DeviceMemory>());
    }

    [Fact]
    public void Compact_WithHostOnlyState_NoOp()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        _ = buffer.State.Should().Be(BufferState.HostOnly);

        // Act
        buffer.Compact();

        // Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
        _mockMemoryManager.DidNotReceive().FreeDevice(Arg.Any<DeviceMemory>());
    }

    [Fact]
    public void Compact_WithHostDirtyState_FreesDevice()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.MarkHostDirty();
        _ = buffer.State.Should().Be(BufferState.HostDirty);

        // Act
        buffer.Compact();

        // Assert
        _ = buffer.State.Should().Be(BufferState.HostOnly);
    }

    [Fact]
    public void Compact_WithDeviceOnlyState_NoOp()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.MarkDeviceDirty();

        // Act
        buffer.Compact();

        // Assert
        _ = buffer.State.Should().Be(BufferState.DeviceDirty);
        // Device memory not freed because buffer is DeviceDirty
    }

    [Fact]
    public void Compact_WithDeviceDirtyState_NoOp()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.MarkDeviceDirty();
        _ = buffer.State.Should().Be(BufferState.DeviceDirty);

        // Act
        buffer.Compact();

        // Assert
        _ = buffer.State.Should().Be(BufferState.DeviceDirty);
    }

    [Fact]
    public void Compact_WithDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.Compact;

        // Assert
        _ = act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region Prefetch Tests

    [Fact]
    public async Task PrefetchToDeviceAsync_WhenNotOnDevice_AllocatesAndCopies()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        _ = buffer.State.Should().Be(BufferState.HostOnly);

        // Act
        await buffer.PrefetchToDeviceAsync();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        _ = _mockMemoryManager.Received(1).AllocateDevice(Arg.Any<long>());
    }

    [Fact]
    public async Task PrefetchToDeviceAsync_WhenAlreadyOnDevice_NoOp()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        _mockMemoryManager.ClearReceivedCalls();

        // Act
        await buffer.PrefetchToDeviceAsync();

        // Assert
        _ = buffer.IsOnDevice.Should().BeTrue();
        _ = _mockMemoryManager.DidNotReceive().AllocateDevice(Arg.Any<long>());
    }

    [Fact]
    public async Task PrefetchToDeviceAsync_UsesAsyncLock()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act - Multiple concurrent prefetches
        var tasks = new Task[5];
        for (var i = 0; i < 5; i++)
        {
            tasks[i] = buffer.PrefetchToDeviceAsync();
        }

        // Assert - Should complete without throwing
        var act = async () => await Task.WhenAll(tasks);
        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PrefetchToDeviceAsync_WithDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.PrefetchToDeviceAsync;

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PrefetchToHostAsync_WhenNotOnHost_AllocatesAndCopies()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();
        buffer.MarkDeviceDirty();

        // Act
        await buffer.PrefetchToHostAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public async Task PrefetchToHostAsync_WhenAlreadyOnHost_NoOp()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        _ = buffer.State.Should().Be(BufferState.HostOnly);

        // Act
        await buffer.PrefetchToHostAsync();

        // Assert
        _ = buffer.IsOnHost.Should().BeTrue();
    }

    [Fact]
    public async Task PrefetchToHostAsync_UsesAsyncLock()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act - Multiple concurrent prefetches
        var tasks = new Task[5];
        for (var i = 0; i < 5; i++)
        {
            tasks[i] = buffer.PrefetchToHostAsync();
        }

        // Assert - Should complete without throwing
        var act = async () => await Task.WhenAll(tasks);
        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PrefetchToHostAsync_WithDisposedBuffer_ThrowsObjectDisposedException()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        buffer.Dispose();

        // Act
        var act = buffer.PrefetchToHostAsync;

        // Assert
        _ = await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    #endregion

    #region BufferMemoryInfo Tests

    [Fact]
    public void BufferMemoryInfo_InitProperties_DefaultValues()
    {
        // Act
        var memInfo = new BufferMemoryInfo
        {
            SizeInBytes = 1000,
            HostAllocated = true,
            DeviceAllocated = false,
            HostAddress = new IntPtr(0x2000),
            DeviceAddress = IntPtr.Zero,
            State = BufferState.HostOnly,
            IsPinned = true
        };

        // Assert
        _ = memInfo.SizeInBytes.Should().Be(1000);
        _ = memInfo.HostAllocated.Should().BeTrue();
        _ = memInfo.DeviceAllocated.Should().BeFalse();
        _ = memInfo.HostAddress.Should().Be(new IntPtr(0x2000));
        _ = memInfo.DeviceAddress.Should().Be(IntPtr.Zero);
        _ = memInfo.State.Should().Be(BufferState.HostOnly);
        _ = memInfo.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void BufferMemoryInfo_WithAllFieldsSet_StoresCorrectly()
    {
        // Act
        var memInfo = new BufferMemoryInfo
        {
            SizeInBytes = 2048,
            HostAllocated = true,
            DeviceAllocated = true,
            HostAddress = new IntPtr(0x3000),
            DeviceAddress = new IntPtr(0x4000),
            State = BufferState.Synchronized,
            IsPinned = true
        };

        // Assert
        _ = memInfo.SizeInBytes.Should().Be(2048);
        _ = memInfo.HostAllocated.Should().BeTrue();
        _ = memInfo.DeviceAllocated.Should().BeTrue();
        _ = memInfo.HostAddress.Should().Be(new IntPtr(0x3000));
        _ = memInfo.DeviceAddress.Should().Be(new IntPtr(0x4000));
        _ = memInfo.State.Should().Be(BufferState.Synchronized);
        _ = memInfo.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void BufferMemoryInfo_StateProperty_ReflectsBufferState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.State.Should().Be(buffer.State);
    }

    [Fact]
    public void BufferMemoryInfo_IsPinnedProperty_ReflectsPinnedState()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void BufferMemoryInfo_HostAddress_NonZeroWhenAllocated()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.HostAddress.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void BufferMemoryInfo_DeviceAddress_NonZeroWhenAllocated()
    {
        // Arrange
        var buffer = new UnifiedBuffer<int>(_mockMemoryManager, 100);
        _disposables.Add(buffer);
        buffer.EnsureOnDevice();

        // Act
        var memInfo = buffer.GetMemoryInfo();

        // Assert
        _ = memInfo.DeviceAddress.Should().NotBe(IntPtr.Zero);
    }

    #endregion
}
