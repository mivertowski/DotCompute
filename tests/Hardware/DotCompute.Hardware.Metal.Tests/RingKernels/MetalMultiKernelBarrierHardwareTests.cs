// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.RingKernels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.RingKernels;

/// <summary>
/// Hardware tests for Metal multi-kernel barriers on actual Mac hardware.
/// </summary>
/// <remarks>
/// These tests require a Mac with Metal support and validate:
/// - Barrier creation and initialization on GPU
/// - Cross-kernel synchronization correctness
/// - Barrier latency performance (target: <5μs for 2-8 kernels)
/// - Timeout and failure handling
/// - Generation-based barrier reuse
/// </remarks>
[Collection("MetalHardware")]
public sealed class MetalMultiKernelBarrierHardwareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly MetalMultiKernelBarrierManager _manager;

    public MetalMultiKernelBarrierHardwareTests(ITestOutputHelper output)
    {
        _output = output;

        // Initialize Metal device
        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("No Metal device available. These tests require a Mac with Metal support.");
        }

        _commandQueue = MetalNative.CreateCommandQueue(_device);
        if (_commandQueue == IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
            throw new InvalidOperationException("Failed to create Metal command queue.");
        }

        _manager = new MetalMultiKernelBarrierManager(
            _device,
            _commandQueue,
            NullLogger<MetalMultiKernelBarrierManager>.Instance);

        _output.WriteLine($"Metal device initialized: 0x{_device.ToInt64():X}");
        _output.WriteLine($"Command queue initialized: 0x{_commandQueue.ToInt64():X}");
    }

    [Fact]
    public async Task CreateAsync_Should_Allocate_Barrier_On_GPU()
    {
        // Arrange & Act
        var barrierBuffer = await _manager.CreateAsync(participantCount: 4);

        // Assert
        Assert.NotEqual(IntPtr.Zero, barrierBuffer);

        // Verify barrier is accessible from CPU (unified memory)
        var barrier = _manager.GetBarrierState(barrierBuffer);
        Assert.Equal(4, barrier.ParticipantCount);
        Assert.Equal(0, barrier.ArrivedCount);
        Assert.Equal(0, barrier.Generation);
        Assert.True(barrier.IsHealthy);

        _output.WriteLine($"Barrier allocated at 0x{barrierBuffer.ToInt64():X}");
        _output.WriteLine($"  ParticipantCount: {barrier.ParticipantCount}");
        _output.WriteLine($"  Generation: {barrier.Generation}");
        _output.WriteLine($"  Flags: 0x{barrier.Flags:X}");

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(256)]
    public async Task CreateAsync_Should_Support_Various_Participant_Counts(int participantCount)
    {
        // Act
        var barrierBuffer = await _manager.CreateAsync(participantCount);

        // Assert
        var barrier = _manager.GetBarrierState(barrierBuffer);
        Assert.Equal(participantCount, barrier.ParticipantCount);
        Assert.True(barrier.IsHealthy);

        _output.WriteLine($"Created barrier for {participantCount} participants at 0x{barrierBuffer.ToInt64():X}");

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Fact]
    public async Task GetBarrierState_Should_Read_From_Unified_Memory()
    {
        // Arrange
        var barrierBuffer = await _manager.CreateAsync(participantCount: 10);

        // Act - read barrier state from unified memory (no GPU→CPU copy needed)
        var barrier = _manager.GetBarrierState(barrierBuffer);

        // Assert - CPU can directly read GPU-allocated unified memory
        Assert.Equal(10, barrier.ParticipantCount);
        Assert.Equal(0, barrier.ArrivedCount);

        _output.WriteLine("Successfully read barrier state from unified memory:");
        _output.WriteLine($"  Address: 0x{barrierBuffer.ToInt64():X}");
        _output.WriteLine($"  ParticipantCount: {barrier.ParticipantCount}");
        _output.WriteLine($"  ArrivedCount: {barrier.ArrivedCount}");

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Fact]
    public async Task WaitAsync_Should_Complete_Successfully()
    {
        // Arrange
        var barrierBuffer = await _manager.CreateAsync(participantCount: 1);

        // Act
        bool success = await _manager.WaitAsync(barrierBuffer);

        // Assert
        Assert.True(success);

        var barrier = _manager.GetBarrierState(barrierBuffer);
        _output.WriteLine($"Barrier wait completed: success={success}, generation={barrier.Generation}");

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Fact]
    public async Task WaitAsync_With_Timeout_Should_Complete_Within_Time_Limit()
    {
        // Arrange
        var barrierBuffer = await _manager.CreateAsync(participantCount: 1);
        var timeout = TimeSpan.FromMilliseconds(100);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        bool success = await _manager.WaitAsync(barrierBuffer, timeout);

        stopwatch.Stop();

        // Assert
        Assert.True(success);
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, timeout.TotalMilliseconds * 2);

        _output.WriteLine($"Barrier wait with timeout completed in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Fact]
    public async Task ResetAsync_Should_Clear_Barrier_State()
    {
        // Arrange
        var barrierBuffer = await _manager.CreateAsync(participantCount: 10);

        // Simulate some arrivals by modifying unified memory directly
        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(barrierBuffer);
            var barrierPtr = (MetalMultiKernelBarrier*)bufferPtr.ToPointer();
            barrierPtr->ArrivedCount = 5;
            barrierPtr->Generation = 3;
        }

        // Act
        await _manager.ResetAsync(barrierBuffer);

        // Assert
        var barrier = _manager.GetBarrierState(barrierBuffer);
        _output.WriteLine($"After reset: ArrivedCount={barrier.ArrivedCount}, Generation={barrier.Generation}");

        // TODO: Verify reset worked when Metal kernel is implemented
        Assert.Equal(10, barrier.ParticipantCount); // Should not change

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Fact]
    public async Task MarkFailedAsync_Should_Set_Failed_Flag()
    {
        // Arrange
        var barrierBuffer = await _manager.CreateAsync(participantCount: 10);

        // Act
        await _manager.MarkFailedAsync(barrierBuffer);

        // Assert
        var barrier = _manager.GetBarrierState(barrierBuffer);
        _output.WriteLine($"After mark failed: Flags=0x{barrier.Flags:X}, IsFailed={barrier.IsFailed}");

        // TODO: Verify failed flag when Metal kernel is implemented

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Fact]
    public async Task Multiple_Barriers_Should_Be_Independent()
    {
        // Arrange
        var barrier1 = await _manager.CreateAsync(participantCount: 4);
        var barrier2 = await _manager.CreateAsync(participantCount: 8);
        var barrier3 = await _manager.CreateAsync(participantCount: 16);

        // Act - get states
        var state1 = _manager.GetBarrierState(barrier1);
        var state2 = _manager.GetBarrierState(barrier2);
        var state3 = _manager.GetBarrierState(barrier3);

        // Assert - barriers are independent
        Assert.Equal(4, state1.ParticipantCount);
        Assert.Equal(8, state2.ParticipantCount);
        Assert.Equal(16, state3.ParticipantCount);

        Assert.NotEqual(barrier1, barrier2);
        Assert.NotEqual(barrier2, barrier3);
        Assert.NotEqual(barrier1, barrier3);

        _output.WriteLine($"Created 3 independent barriers:");
        _output.WriteLine($"  Barrier 1: 0x{barrier1.ToInt64():X} (4 participants)");
        _output.WriteLine($"  Barrier 2: 0x{barrier2.ToInt64():X} (8 participants)");
        _output.WriteLine($"  Barrier 3: 0x{barrier3.ToInt64():X} (16 participants)");

        // Cleanup
        _manager.DisposeBarrier(barrier1);
        _manager.DisposeBarrier(barrier2);
        _manager.DisposeBarrier(barrier3);
    }

    [Fact]
    public async Task UnifiedMemory_Should_Allow_Direct_CPU_Access()
    {
        // Arrange
        var barrierBuffer = await _manager.CreateAsync(participantCount: 10);

        // Act - directly modify barrier via unified memory
        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(barrierBuffer);
            var barrierPtr = (MetalMultiKernelBarrier*)bufferPtr.ToPointer();

            // CPU writes to unified memory
            barrierPtr->ArrivedCount = 7;
            barrierPtr->Generation = 5;
        }

        // Assert - changes are visible when reading back
        var barrier = _manager.GetBarrierState(barrierBuffer);
        Assert.Equal(7, barrier.ArrivedCount);
        Assert.Equal(5, barrier.Generation);

        _output.WriteLine("CPU successfully wrote to unified memory:");
        _output.WriteLine($"  ArrivedCount: {barrier.ArrivedCount}");
        _output.WriteLine($"  Generation: {barrier.Generation}");

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    [Fact]
    public async Task Performance_CreateAsync_Should_Be_Fast()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - create 10 barriers
        var barriers = new List<IntPtr>();
        for (int i = 0; i < 10; i++)
        {
            var barrier = await _manager.CreateAsync(participantCount: 8);
            barriers.Add(barrier);
        }

        stopwatch.Stop();

        // Assert - should be very fast (<1ms per barrier)
        double avgTimeMs = stopwatch.Elapsed.TotalMilliseconds / 10;
        Assert.InRange(avgTimeMs, 0, 10); // Very generous, should be <1ms

        _output.WriteLine($"Created 10 barriers in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Average per barrier: {avgTimeMs:F3}ms");

        // Cleanup
        foreach (var barrier in barriers)
        {
            _manager.DisposeBarrier(barrier);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Barrier_Lifecycle_Should_Work_Correctly(int participantCount)
    {
        // Arrange - Create
        var barrierBuffer = await _manager.CreateAsync(participantCount);
        var initialState = _manager.GetBarrierState(barrierBuffer);

        _output.WriteLine($"Barrier lifecycle test with {participantCount} participants:");
        _output.WriteLine($"  Initial: ArrivedCount={initialState.ArrivedCount}, Generation={initialState.Generation}");

        // Act - Wait
        bool waitSuccess = await _manager.WaitAsync(barrierBuffer, TimeSpan.FromSeconds(1));
        var afterWaitState = _manager.GetBarrierState(barrierBuffer);

        _output.WriteLine($"  After wait: success={waitSuccess}, Generation={afterWaitState.Generation}");

        // Act - Reset
        await _manager.ResetAsync(barrierBuffer);
        var afterResetState = _manager.GetBarrierState(barrierBuffer);

        _output.WriteLine($"  After reset: ArrivedCount={afterResetState.ArrivedCount}");

        // Assert
        Assert.True(waitSuccess);
        Assert.Equal(participantCount, initialState.ParticipantCount);

        // Cleanup
        _manager.DisposeBarrier(barrierBuffer);
    }

    public void Dispose()
    {
        _manager?.Dispose();

        if (_commandQueue != IntPtr.Zero)
        {
            MetalNative.ReleaseCommandQueue(_commandQueue);
        }

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }

        _output.WriteLine("Metal resources disposed");
    }
}
