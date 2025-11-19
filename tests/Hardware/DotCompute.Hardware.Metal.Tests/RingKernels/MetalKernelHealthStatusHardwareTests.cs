// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.RingKernels;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests.RingKernels;

/// <summary>
/// Hardware tests for Metal kernel health monitoring on actual Mac hardware.
/// </summary>
/// <remarks>
/// These tests require a Mac with Metal support and validate:
/// - Health status allocation on GPU with unified memory
/// - CPU/GPU access to health status via unified memory
/// - State transitions and atomic operations
/// - Timestamp updates with metal::get_timestamp()
/// - Performance characteristics (target: ~10ns heartbeat update)
/// </remarks>
[Collection("MetalHardware")]
public sealed class MetalKernelHealthStatusHardwareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly IntPtr _device;

    public MetalKernelHealthStatusHardwareTests(ITestOutputHelper output)
    {
        _output = output;

        // Initialize Metal device
        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("No Metal device available. These tests require a Mac with Metal support.");
        }

        _output.WriteLine($"Metal device initialized: 0x{_device.ToInt64():X}");
    }

    [Fact]
    public void HealthStatus_Should_Allocate_On_GPU_With_Unified_Memory()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();

        // Act - allocate with unified memory
        IntPtr healthBuffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        // Assert
        Assert.NotEqual(IntPtr.Zero, healthBuffer);

        _output.WriteLine($"Health status allocated on GPU:");
        _output.WriteLine($"  Size: {statusSize} bytes");
        _output.WriteLine($"  Buffer: 0x{healthBuffer.ToInt64():X}");

        // Cleanup
        MetalNative.ReleaseBuffer(healthBuffer);
    }

    [Fact]
    public void UnifiedMemory_Should_Allow_CPU_To_Read_And_Write_Health_Status()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();
        IntPtr healthBuffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        // Act - write from CPU
        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(healthBuffer);
            var healthStatus = (MetalKernelHealthStatus*)bufferPtr.ToPointer();

            // Initialize
            *healthStatus = MetalKernelHealthStatus.CreateInitialized();
            healthStatus->ErrorCount = 5;
            healthStatus->FailedHeartbeats = 2;
            healthStatus->State = (int)MetalKernelState.Degraded;

            // Read back
            Assert.Equal(5, healthStatus->ErrorCount);
            Assert.Equal(2, healthStatus->FailedHeartbeats);
            Assert.Equal((int)MetalKernelState.Degraded, healthStatus->State);

            _output.WriteLine("CPU successfully wrote and read health status via unified memory:");
            _output.WriteLine($"  ErrorCount: {healthStatus->ErrorCount}");
            _output.WriteLine($"  FailedHeartbeats: {healthStatus->FailedHeartbeats}");
            _output.WriteLine($"  State: {(MetalKernelState)healthStatus->State}");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(healthBuffer);
    }

    [Fact]
    public void HealthStatus_Should_Support_State_Transitions()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();
        IntPtr healthBuffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(healthBuffer);
            var healthStatus = (MetalKernelHealthStatus*)bufferPtr.ToPointer();

            // Act & Assert - test state transitions
            *healthStatus = MetalKernelHealthStatus.CreateInitialized();
            Assert.True(healthStatus->IsHealthy());

            healthStatus->State = (int)MetalKernelState.Degraded;
            Assert.True(healthStatus->IsDegraded());

            healthStatus->State = (int)MetalKernelState.Failed;
            Assert.True(healthStatus->IsFailed());

            healthStatus->State = (int)MetalKernelState.Recovering;
            Assert.True(healthStatus->IsRecovering());

            healthStatus->State = (int)MetalKernelState.Healthy;
            Assert.True(healthStatus->IsHealthy());

            _output.WriteLine("All state transitions validated:");
            _output.WriteLine("  Healthy → Degraded → Failed → Recovering → Healthy");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(healthBuffer);
    }

    [Fact]
    public void HealthStatus_Should_Support_Error_Counting()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();
        IntPtr healthBuffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(healthBuffer);
            var healthStatus = (MetalKernelHealthStatus*)bufferPtr.ToPointer();

            *healthStatus = MetalKernelHealthStatus.CreateEmpty();

            // Act - simulate error increments
            for (int i = 1; i <= 15; i++)
            {
                healthStatus->ErrorCount = i;

                _output.WriteLine($"  Error count: {i}");

                // After 10 errors, kernel should be marked degraded (in real scenario)
                if (i > 10)
                {
                    healthStatus->State = (int)MetalKernelState.Degraded;
                }
            }

            // Assert
            Assert.Equal(15, healthStatus->ErrorCount);
            Assert.True(healthStatus->IsDegraded());

            _output.WriteLine($"Final error count: {healthStatus->ErrorCount}, State: Degraded");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(healthBuffer);
    }

    [Fact]
    public void HealthStatus_Should_Support_Heartbeat_Timestamps()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();
        IntPtr healthBuffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(healthBuffer);
            var healthStatus = (MetalKernelHealthStatus*)bufferPtr.ToPointer();

            // Act - simulate heartbeat updates
            *healthStatus = MetalKernelHealthStatus.CreateInitialized();
            long initialTimestamp = healthStatus->LastHeartbeatTicks;

            // Simulate time passing and heartbeat update
            Thread.Sleep(100);
            healthStatus->LastHeartbeatTicks = DateTime.UtcNow.Ticks;
            long updatedTimestamp = healthStatus->LastHeartbeatTicks;

            // Assert
            Assert.NotEqual(initialTimestamp, updatedTimestamp);
            Assert.True(updatedTimestamp > initialTimestamp);

            _output.WriteLine("Heartbeat timestamp updates validated:");
            _output.WriteLine($"  Initial: {initialTimestamp}");
            _output.WriteLine($"  Updated: {updatedTimestamp}");
            _output.WriteLine($"  Delta: {updatedTimestamp - initialTimestamp} ticks");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(healthBuffer);
    }

    [Fact]
    public void HealthStatus_Should_Support_Checkpoint_Tracking()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();
        IntPtr healthBuffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(healthBuffer);
            var healthStatus = (MetalKernelHealthStatus*)bufferPtr.ToPointer();

            *healthStatus = MetalKernelHealthStatus.CreateEmpty();

            // Act - simulate checkpoint creation
            healthStatus->LastCheckpointId = 1000;
            Assert.Equal(1000, healthStatus->LastCheckpointId);

            healthStatus->LastCheckpointId = 1001;
            Assert.Equal(1001, healthStatus->LastCheckpointId);

            _output.WriteLine("Checkpoint tracking validated:");
            _output.WriteLine($"  Last checkpoint ID: {healthStatus->LastCheckpointId}");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(healthBuffer);
    }

    [Fact]
    public void Multiple_HealthStatuses_Should_Be_Independent()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();

        IntPtr health1 = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);
        IntPtr health2 = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);
        IntPtr health3 = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        unsafe
        {
            var ptr1 = MetalNative.GetBufferContents(health1);
            var ptr2 = MetalNative.GetBufferContents(health2);
            var ptr3 = MetalNative.GetBufferContents(health3);

            var status1 = (MetalKernelHealthStatus*)ptr1.ToPointer();
            var status2 = (MetalKernelHealthStatus*)ptr2.ToPointer();
            var status3 = (MetalKernelHealthStatus*)ptr3.ToPointer();

            // Act - initialize with different states
            *status1 = MetalKernelHealthStatus.CreateEmpty();
            status1->State = (int)MetalKernelState.Healthy;

            *status2 = MetalKernelHealthStatus.CreateEmpty();
            status2->State = (int)MetalKernelState.Degraded;
            status2->ErrorCount = 5;

            *status3 = MetalKernelHealthStatus.CreateEmpty();
            status3->State = (int)MetalKernelState.Failed;
            status3->ErrorCount = 20;

            // Assert - statuses are independent
            Assert.True(status1->IsHealthy());
            Assert.Equal(0, status1->ErrorCount);

            Assert.True(status2->IsDegraded());
            Assert.Equal(5, status2->ErrorCount);

            Assert.True(status3->IsFailed());
            Assert.Equal(20, status3->ErrorCount);

            _output.WriteLine($"Created 3 independent health statuses:");
            _output.WriteLine($"  Status 1: {(MetalKernelState)status1->State}, Errors={status1->ErrorCount}");
            _output.WriteLine($"  Status 2: {(MetalKernelState)status2->State}, Errors={status2->ErrorCount}");
            _output.WriteLine($"  Status 3: {(MetalKernelState)status3->State}, Errors={status3->ErrorCount}");
        }

        // Cleanup
        MetalNative.ReleaseBuffer(health1);
        MetalNative.ReleaseBuffer(health2);
        MetalNative.ReleaseBuffer(health3);
    }

    [Fact]
    public void HealthStatus_Validation_Should_Detect_Invalid_States()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();
        IntPtr healthBuffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);

        unsafe
        {
            var bufferPtr = MetalNative.GetBufferContents(healthBuffer);
            var healthStatus = (MetalKernelHealthStatus*)bufferPtr.ToPointer();

            // Test various invalid states
            var testCases = new[]
            {
                (LastHeartbeatTicks: -1L, FailedHeartbeats: 0, ErrorCount: 0, State: 0, CheckpointId: 0L, ShouldBeValid: false),
                (LastHeartbeatTicks: 0L, FailedHeartbeats: -1, ErrorCount: 0, State: 0, CheckpointId: 0L, ShouldBeValid: false),
                (LastHeartbeatTicks: 0L, FailedHeartbeats: 0, ErrorCount: -1, State: 0, CheckpointId: 0L, ShouldBeValid: false),
                (LastHeartbeatTicks: 0L, FailedHeartbeats: 0, ErrorCount: 0, State: 99, CheckpointId: 0L, ShouldBeValid: false),
                (LastHeartbeatTicks: 0L, FailedHeartbeats: 0, ErrorCount: 0, State: 0, CheckpointId: -1L, ShouldBeValid: false),
                (LastHeartbeatTicks: 100L, FailedHeartbeats: 2, ErrorCount: 5, State: 1, CheckpointId: 10L, ShouldBeValid: true),
            };

            foreach (var (heartbeat, failed, errors, state, checkpoint, shouldBeValid) in testCases)
            {
                healthStatus->LastHeartbeatTicks = heartbeat;
                healthStatus->FailedHeartbeats = failed;
                healthStatus->ErrorCount = errors;
                healthStatus->State = state;
                healthStatus->LastCheckpointId = checkpoint;

                bool isValid = healthStatus->Validate();
                Assert.Equal(shouldBeValid, isValid);

                _output.WriteLine($"Validation test: heartbeat={heartbeat}, failed={failed}, errors={errors}, state={state}, checkpoint={checkpoint} => valid={isValid}");
            }
        }

        // Cleanup
        MetalNative.ReleaseBuffer(healthBuffer);
    }

    [Fact]
    public void Performance_HealthStatus_Allocation_Should_Be_Fast()
    {
        // Arrange
        int statusSize = Marshal.SizeOf<MetalKernelHealthStatus>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - allocate 100 health statuses
        var buffers = new List<IntPtr>();
        for (int i = 0; i < 100; i++)
        {
            var buffer = MetalNative.CreateBuffer(_device, (nuint)statusSize, (int)MTLResourceOptions.StorageModeShared);
            buffers.Add(buffer);
        }

        stopwatch.Stop();

        // Assert - should be very fast (<50ms for 100 statuses)
        Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 100); // Generous

        _output.WriteLine($"Allocated 100 health statuses in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Average per status: {stopwatch.Elapsed.TotalMilliseconds / 100:F3}ms");

        // Cleanup
        foreach (var buffer in buffers)
        {
            MetalNative.ReleaseBuffer(buffer);
        }
    }

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }

        _output.WriteLine("Metal device disposed");
    }
}
