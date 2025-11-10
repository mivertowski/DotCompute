// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.CUDA.Barriers;
using DotCompute.Backends.CUDA.Types;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.Barriers;

/// <summary>
/// Unit tests for <see cref="CudaBarrierHandle"/>.
/// </summary>
public sealed class CudaBarrierHandleTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CudaContext _context;

    public CudaBarrierHandleTests(ITestOutputHelper output)
    {
        _output = output;
        _context = new CudaContext(deviceId: 0);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    #region Basic Properties Tests

    [Fact]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Arrange & Act
        using var handle = new CudaBarrierHandle(_context, null,barrierId: 1, BarrierScope.ThreadBlock, capacity: 256);

        // Assert
        handle.BarrierId.Should().Be(1);
        handle.Scope.Should().Be(BarrierScope.ThreadBlock);
        handle.Capacity.Should().Be(256);
        handle.ThreadsWaiting.Should().Be(0);
        handle.IsActive.Should().BeFalse();

        _output.WriteLine($"Barrier initialized: {handle}");
    }

    [Fact]
    public void Constructor_WithName_StoresName()
    {
        // Arrange & Act
        using var handle = new CudaBarrierHandle(
            _context, null, 1, BarrierScope.ThreadBlock, 128, "test-barrier");

        // Assert
        handle.ToString().Should().Contain("test-barrier");
        _output.WriteLine($"Named barrier: {handle}");
    }

    [Fact]
    public void Constructor_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CudaBarrierHandle(null!, null, 1, BarrierScope.ThreadBlock, 256);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    #endregion

    #region Sync Behavior Tests

    [Fact]
    public void Sync_IncreasesThreadsWaiting()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 4);
        handle.ThreadsWaiting.Should().Be(0);

        // Act & Assert
        handle.Sync();
        handle.ThreadsWaiting.Should().Be(1);

        handle.Sync();
        handle.ThreadsWaiting.Should().Be(2);

        handle.Sync();
        handle.ThreadsWaiting.Should().Be(3);

        _output.WriteLine($"Threads waiting: {handle.ThreadsWaiting}/4");
    }

    [Fact]
    public void Sync_WhenCapacityReached_ResetsCounter()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 3);

        // Act
        handle.Sync(); // 1/3
        handle.Sync(); // 2/3
        handle.ThreadsWaiting.Should().Be(2);

        handle.Sync(); // 3/3 - should reset
        handle.ThreadsWaiting.Should().Be(0, "counter should reset when capacity reached");

        _output.WriteLine("Barrier correctly reset after all threads synced");
    }

    [Fact]
    public void IsActive_ReturnsTrueWhenPartiallyFilled()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 5);

        // Act & Assert
        handle.IsActive.Should().BeFalse("no threads waiting yet");

        handle.Sync();
        handle.IsActive.Should().BeTrue("some threads waiting");

        handle.Sync();
        handle.Sync();
        handle.IsActive.Should().BeTrue("still waiting for more threads");

        handle.Sync();
        handle.Sync(); // All 5 threads synced
        handle.IsActive.Should().BeFalse("all threads released");

        _output.WriteLine("IsActive correctly tracks barrier state");
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_WhenNotActive_ResetsSuccessfully()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 10);

        // Act
        handle.Reset();

        // Assert
        handle.ThreadsWaiting.Should().Be(0);
        handle.IsActive.Should().BeFalse();

        _output.WriteLine("Barrier reset successfully when inactive");
    }

    [Fact]
    public void Reset_WhenActive_ThrowsInvalidOperationException()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 10);
        handle.Sync(); // Make active
        handle.IsActive.Should().BeTrue();

        // Act & Assert
        var act = () => handle.Reset();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*active barrier*");

        _output.WriteLine("Reset correctly rejected on active barrier");
    }

    [Fact]
    public void Reset_AfterFullCycle_AllowsReuse()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 2);

        // First cycle
        handle.Sync();
        handle.Sync(); // Complete
        handle.IsActive.Should().BeFalse();

        // Act - reset between cycles
        handle.Reset();

        // Second cycle
        handle.Sync();
        handle.ThreadsWaiting.Should().Be(1);
        handle.Sync(); // Complete again
        handle.ThreadsWaiting.Should().Be(0);

        _output.WriteLine("Barrier successfully reused after reset");
    }

    #endregion

    #region Grid Barrier Specific Tests

    [Fact]
    public void Constructor_GridScope_AllocatesDeviceMemory()
    {
        // Arrange & Act
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.Grid, capacity: 1024);

        // Assert
        handle.DeviceBarrierPtr.Should().NotBe(IntPtr.Zero, "grid barriers need device memory");
        _output.WriteLine($"Grid barrier device ptr: 0x{handle.DeviceBarrierPtr:X}");
    }

    [Fact]
    public void Constructor_ThreadBlockScope_NoDeviceMemory()
    {
        // Arrange & Act
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 256);

        // Assert
        handle.DeviceBarrierPtr.Should().Be(IntPtr.Zero, "thread-block barriers use hardware, no device memory");
        _output.WriteLine("Thread-block barrier correctly uses no device memory");
    }

    [Fact]
    public void Reset_GridBarrier_ResetsDeviceMemory()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.Grid, capacity: 1024);
        handle.DeviceBarrierPtr.Should().NotBe(IntPtr.Zero);

        // Act
        handle.Reset();

        // Assert
        handle.ThreadsWaiting.Should().Be(0);
        handle.DeviceBarrierPtr.Should().NotBe(IntPtr.Zero, "device memory should still be allocated");

        _output.WriteLine("Grid barrier device memory reset successfully");
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var handle = new CudaBarrierHandle(_context, null, 1, BarrierScope.ThreadBlock, capacity: 256);

        // Act
        handle.Dispose();

        // Assert
        var act = () => handle.Sync();
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("Disposed barrier correctly rejects operations");
    }

    [Fact]
    public void Dispose_GridBarrier_FreesDeviceMemory()
    {
        // Arrange
        var handle = new CudaBarrierHandle(_context, null, 1, BarrierScope.Grid, capacity: 1024);
        var devicePtr = handle.DeviceBarrierPtr;
        devicePtr.Should().NotBe(IntPtr.Zero);

        // Act
        handle.Dispose();

        // Assert
        handle.DeviceBarrierPtr.Should().Be(IntPtr.Zero, "device memory should be freed");
        _output.WriteLine("Grid barrier device memory freed on disposal");
    }

    [Fact]
    public void Dispose_MultipleCalls_IsSafe()
    {
        // Arrange
        var handle = new CudaBarrierHandle(_context, null, 1, BarrierScope.ThreadBlock, capacity: 256);

        // Act
        handle.Dispose();
        handle.Dispose();
        handle.Dispose();

        // Assert - no exception thrown
        _output.WriteLine("Multiple Dispose() calls handled safely");
    }

    [Fact]
    public void Sync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var handle = new CudaBarrierHandle(_context, null, 1, BarrierScope.ThreadBlock, capacity: 256);
        handle.Dispose();

        // Act & Assert
        var act = () => handle.Sync();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Reset_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var handle = new CudaBarrierHandle(_context, null, 1, BarrierScope.ThreadBlock, capacity: 256);
        handle.Dispose();

        // Act & Assert
        var act = () => handle.Reset();
        act.Should().Throw<ObjectDisposedException>();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ContainsUsefulInformation()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,42, BarrierScope.ThreadBlock, 512, "my-barrier");

        // Act
        var str = handle.ToString();

        // Assert
        str.Should().Contain("42"); // Barrier ID
        str.Should().Contain("ThreadBlock"); // Scope
        str.Should().Contain("512"); // Capacity
        str.Should().Contain("my-barrier"); // Name

        _output.WriteLine($"ToString output: {str}");
    }

    [Fact]
    public void ToString_WithoutName_ExcludesName()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.Warp, 32);

        // Act
        var str = handle.ToString();

        // Assert
        str.Should().Contain("Warp");
        str.Should().Contain("32");
        str.Should().NotContain("'"); // No name quotes

        _output.WriteLine($"Anonymous barrier: {str}");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ThreadsWaiting_ConcurrentAccess_IsThreadSafe()
    {
        // Arrange
        using var handle = new CudaBarrierHandle(_context, null,1, BarrierScope.ThreadBlock, capacity: 1000);
        const int threadCount = 10;
        const int syncsPerThread = 10;

        // Act
        var tasks = Enumerable.Range(0, threadCount)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < syncsPerThread; i++)
                {
                    handle.Sync();
                    _ = handle.ThreadsWaiting; // Read concurrently
                    Thread.Sleep(1); // Small delay to increase contention
                }
            }))
            .ToArray();

        Task.WaitAll(tasks);

        // Assert
        // Total syncs = threadCount * syncsPerThread = 100
        // With capacity 1000, we should have 100 threads waiting
        handle.ThreadsWaiting.Should().Be(100);

        _output.WriteLine($"Concurrent syncs completed: {handle.ThreadsWaiting} threads waiting");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Properties_AfterDispose_StillAccessible()
    {
        // Arrange
        var handle = new CudaBarrierHandle(_context, null, 123, BarrierScope.ThreadBlock, 256);
        handle.Dispose();

        // Act & Assert - properties should still be readable
        handle.BarrierId.Should().Be(123);
        handle.Scope.Should().Be(BarrierScope.ThreadBlock);
        handle.Capacity.Should().Be(256);

        _output.WriteLine("Barrier properties accessible after disposal");
    }

    #endregion
}
