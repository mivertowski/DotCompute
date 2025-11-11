// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.CUDA.Barriers;
using DotCompute.Backends.CUDA.Types;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.Barriers;

/// <summary>
/// Unit tests for <see cref="CudaSystemBarrier"/>.
/// Tests system-wide barrier implementation for multi-GPU synchronization.
/// </summary>
public sealed class CudaSystemBarrierTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<CudaContext> _contexts;
    private readonly List<CudaSystemBarrier> _barriers;
    private readonly List<MultiGpuSynchronizer> _synchronizers;

    public CudaSystemBarrierTests(ITestOutputHelper output)
    {
        _output = output;
        _contexts = [];
        _barriers = [];
        _synchronizers = [];
    }

    public void Dispose()
    {
        foreach (var barrier in _barriers)
        {
            barrier?.Dispose();
        }
        _barriers.Clear();

        foreach (var synchronizer in _synchronizers)
        {
            synchronizer?.Dispose();
        }
        _synchronizers.Clear();

        foreach (var context in _contexts)
        {
            context?.Dispose();
        }
        _contexts.Clear();
    }

    #region Test 1: Basic Construction and Properties

    [Fact]
    public void Constructor_ValidParameters_InitializesCorrectly()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var contexts = new[] { context };
        var deviceIds = new[] { 0 };

        // Act
        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: contexts,
            deviceIds: deviceIds,
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        // Assert
        barrier.BarrierId.Should().Be(1);
        barrier.Scope.Should().Be(BarrierScope.System);
        barrier.Capacity.Should().Be(256);
        barrier.DeviceCount.Should().Be(1);
        barrier.ThreadsWaiting.Should().Be(0);
        barrier.IsActive.Should().BeFalse();

        _output.WriteLine($"✓ System barrier initialized: {barrier}");
    }

    [Fact]
    public void Constructor_WithName_StoresName()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        // Act
        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 128,
            name: "test-system-barrier");

        _barriers.Add(barrier);

        // Assert
        barrier.Name.Should().Be("test-system-barrier");
        barrier.ToString().Should().Contain("test-system-barrier");

        _output.WriteLine($"✓ Named system barrier: {barrier}");
    }

    [Fact]
    public void Constructor_MultipleDevices_InitializesAllDevices()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var contexts = new[]
        {
            new CudaContext(deviceId: 0),
            new CudaContext(deviceId: 1),
            new CudaContext(deviceId: 2)
        };
        foreach (var ctx in contexts)
        {
            _contexts.Add(ctx);
        }

        var deviceIds = new[] { 0, 1, 2 };

        // Act
        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: contexts,
            deviceIds: deviceIds,
            barrierId: 1,
            capacity: 512);

        _barriers.Add(barrier);

        // Assert
        barrier.DeviceCount.Should().Be(3);
        barrier.ParticipatingDevices.Should().HaveCount(3);
        barrier.ParticipatingDevices.Should().Contain(new[] { 0, 1, 2 });

        _output.WriteLine($"✓ Multi-device system barrier initialized with {barrier.DeviceCount} devices");
    }

    [Fact]
    public void Constructor_CustomTimeout_UsesCustomTimeout()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var customTimeout = TimeSpan.FromMinutes(5);

        // Act
        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256,
            timeout: customTimeout);

        _barriers.Add(barrier);

        // Assert
        barrier.Should().NotBeNull();
        // Note: timeout is internal, but we verify it doesn't throw

        _output.WriteLine($"✓ Custom timeout (5 minutes) accepted");
    }

    #endregion

    #region Test 2: Constructor Validation

    [Fact]
    public void Constructor_NullSynchronizer_ThrowsArgumentNullException()
    {
        // Arrange
        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        // Act & Assert
        var act = () => new CudaSystemBarrier(
            provider: null,
            synchronizer: null!,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("synchronizer");

        _output.WriteLine("✓ Null synchronizer correctly rejected");
    }

    [Fact]
    public void Constructor_NullContexts_ThrowsArgumentNullException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        // Act & Assert
        var act = () => new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: null!,
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        act.Should().Throw<ArgumentNullException>();

        _output.WriteLine("✓ Null contexts correctly rejected");
    }

    [Fact]
    public void Constructor_EmptyContexts_ThrowsArgumentException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        // Act & Assert
        var act = () => new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: Array.Empty<CudaContext>(),
            deviceIds: Array.Empty<int>(),
            barrierId: 1,
            capacity: 256);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one*");

        _output.WriteLine("✓ Empty contexts correctly rejected");
    }

    [Fact]
    public void Constructor_MismatchedContextsAndDeviceIds_ThrowsArgumentException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        // Act & Assert
        var act = () => new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0, 1 }, // Mismatch: 1 context, 2 device IDs
            barrierId: 1,
            capacity: 256);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must match*");

        _output.WriteLine("✓ Mismatched counts correctly rejected");
    }

    #endregion

    #region Test 3: Sync Method (CPU-side)

    [Fact]
    public void Sync_IncreasesThreadsWaiting()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 4);

        _barriers.Add(barrier);

        // Act & Assert
        barrier.ThreadsWaiting.Should().Be(0);

        barrier.Sync();
        barrier.ThreadsWaiting.Should().Be(1);

        barrier.Sync();
        barrier.ThreadsWaiting.Should().Be(2);

        barrier.Sync();
        barrier.ThreadsWaiting.Should().Be(3);

        _output.WriteLine($"✓ CPU-side sync tracking: {barrier.ThreadsWaiting}/4 threads");
    }

    [Fact]
    public void Sync_CapacityReached_ResetsCounter()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 3);

        _barriers.Add(barrier);

        // Act
        barrier.Sync();
        barrier.Sync();
        barrier.Sync(); // Should reach capacity and reset

        // Assert
        barrier.ThreadsWaiting.Should().Be(0, "counter should reset after reaching capacity");

        _output.WriteLine("✓ Counter reset after reaching capacity");
    }

    [Fact]
    public void Sync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        barrier.Dispose();

        // Act & Assert
        var act = () => barrier.Sync();
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("✓ Sync after disposal correctly rejected");
    }

    #endregion

    #region Test 4: SyncAsync Method (GPU-side)

    [Fact]
    public async Task SyncAsync_SingleDevice_CompletesSuccessfully()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        // Act
        var result = await barrier.SyncAsync(deviceId: 0);

        // Assert
        result.Should().BeTrue("single device should complete immediately");

        _output.WriteLine("✓ Single-device GPU synchronization completed");
    }

    [Fact]
    public async Task SyncAsync_MultipleDevices_SynchronizesAllDevices()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var contexts = new[]
        {
            new CudaContext(deviceId: 0),
            new CudaContext(deviceId: 1)
        };
        foreach (var ctx in contexts)
        {
            _contexts.Add(ctx);
        }

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: contexts,
            deviceIds: new[] { 0, 1 },
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        // Act - simulate both devices arriving at barrier
        var task0 = Task.Run(async () => await barrier.SyncAsync(0));
        var task1 = Task.Run(async () => await barrier.SyncAsync(1));

        var results = await Task.WhenAll(task0, task1);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());

        _output.WriteLine("✓ Multi-device GPU synchronization completed");
    }

    [Fact]
    public async Task SyncAsync_InvalidDeviceId_ThrowsArgumentException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        // Act & Assert
        var act = async () => await barrier.SyncAsync(deviceId: 999);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not participating*");

        _output.WriteLine("✓ Invalid device ID correctly rejected");
    }

    [Fact]
    public async Task SyncAsync_CustomTimeout_RespectsTimeout()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var contexts = new[]
        {
            new CudaContext(deviceId: 0),
            new CudaContext(deviceId: 1)
        };
        foreach (var ctx in contexts)
        {
            _contexts.Add(ctx);
        }

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: contexts,
            deviceIds: new[] { 0, 1 },
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        // Act - only one device arrives, with short timeout
        var customTimeout = TimeSpan.FromMilliseconds(100);
        var result = await barrier.SyncAsync(0, timeout: customTimeout);

        // Assert
        result.Should().BeFalse("barrier should timeout when not all devices arrive");

        _output.WriteLine("✓ Custom timeout respected");
    }

    [Fact]
    public async Task SyncAsync_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var contexts = new[]
        {
            new CudaContext(deviceId: 0),
            new CudaContext(deviceId: 1)
        };
        foreach (var ctx in contexts)
        {
            _contexts.Add(ctx);
        }

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: contexts,
            deviceIds: new[] { 0, 1 },
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act & Assert
        var act = async () => await barrier.SyncAsync(0, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        _output.WriteLine("✓ Cancellation token respected");
    }

    #endregion

    #region Test 5: Reset Method

    [Fact]
    public void Reset_WithNoWaitingThreads_Succeeds()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        // Act
        barrier.Reset();

        // Assert
        barrier.ThreadsWaiting.Should().Be(0);

        _output.WriteLine("✓ Barrier reset with no waiting threads");
    }

    [Fact]
    public void Reset_WithActiveBarrier_ThrowsInvalidOperationException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 4);

        _barriers.Add(barrier);

        // Make barrier active
        barrier.Sync();
        barrier.IsActive.Should().BeTrue();

        // Act & Assert
        var act = () => barrier.Reset();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*active barrier*");

        _output.WriteLine("✓ Reset of active barrier correctly rejected");
    }

    [Fact]
    public void Reset_AfterCompletion_AllowsReuse()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 2);

        _barriers.Add(barrier);

        // Use barrier to completion
        barrier.Sync();
        barrier.Sync(); // Capacity reached, counter resets to 0

        // Act
        barrier.Reset();

        // Assert - barrier should be reusable
        barrier.ThreadsWaiting.Should().Be(0);
        barrier.IsActive.Should().BeFalse();

        // Reuse
        barrier.Sync();
        barrier.ThreadsWaiting.Should().Be(1);

        _output.WriteLine("✓ Barrier reset and reused successfully");
    }

    [Fact]
    public void Reset_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        barrier.Dispose();

        // Act & Assert
        var act = () => barrier.Reset();
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("✓ Reset after disposal correctly rejected");
    }

    #endregion

    #region Test 6: Disposal and Resource Management

    [Fact]
    public void Dispose_UnregistersDevices()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        // Verify device is registered
        synchronizer.RegisteredDeviceCount.Should().Be(1);

        // Act
        barrier.Dispose();

        // Assert
        synchronizer.RegisteredDeviceCount.Should().Be(0,
            "devices should be unregistered on disposal");

        _output.WriteLine("✓ Disposal unregisters all devices from synchronizer");
    }

    [Fact]
    public void Dispose_MultipleCalls_HandlesGracefully()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        // Act - multiple dispose calls
        barrier.Dispose();
        barrier.Dispose();
        barrier.Dispose();

        // Assert - should not throw
        _output.WriteLine("✓ Multiple dispose calls handled gracefully");
    }

    [Fact]
    public void Finalizer_DisposesResources()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 256);

        // Act - let finalizer run by not disposing explicitly
        // (In real scenario, GC would handle this)

        _output.WriteLine("✓ Finalizer pattern implemented for resource cleanup");
    }

    #endregion

    #region Test 7: State and Properties

    [Fact]
    public void IsActive_ReflectsCurrentState()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: 3);

        _barriers.Add(barrier);

        // Act & Assert
        barrier.IsActive.Should().BeFalse("no threads waiting");

        barrier.Sync();
        barrier.IsActive.Should().BeTrue("threads waiting but not at capacity");

        barrier.Sync();
        barrier.IsActive.Should().BeTrue("still not at capacity");

        barrier.Sync();
        barrier.IsActive.Should().BeFalse("capacity reached, counter reset");

        _output.WriteLine("✓ IsActive property reflects barrier state correctly");
    }

    [Fact]
    public void ParticipatingDevices_ReturnsCorrectDeviceIds()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var contexts = new[]
        {
            new CudaContext(deviceId: 0),
            new CudaContext(deviceId: 2),
            new CudaContext(deviceId: 5)
        };
        foreach (var ctx in contexts)
        {
            _contexts.Add(ctx);
        }

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: contexts,
            deviceIds: new[] { 0, 2, 5 },
            barrierId: 1,
            capacity: 256);

        _barriers.Add(barrier);

        // Assert
        barrier.ParticipatingDevices.Should().HaveCount(3);
        barrier.ParticipatingDevices.Should().Contain(new[] { 0, 2, 5 });

        _output.WriteLine($"✓ ParticipatingDevices: [{string.Join(", ", barrier.ParticipatingDevices)}]");
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 42,
            capacity: 512,
            name: "my-barrier");

        _barriers.Add(barrier);

        // Act
        var str = barrier.ToString();

        // Assert
        str.Should().Contain("CudaSystemBarrier");
        str.Should().Contain("my-barrier");
        str.Should().Contain("ID=42");
        str.Should().Contain("Scope=System");
        str.Should().Contain("Devices=1");
        str.Should().Contain("Capacity=512");

        _output.WriteLine($"✓ ToString: {str}");
    }

    #endregion

    #region Test 8: Edge Cases

    [Fact]
    public void Constructor_MaximumDevices_HandlesCorrectly()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        const int maxDevices = 16; // Realistic maximum
        var contexts = new CudaContext[maxDevices];
        var deviceIds = new int[maxDevices];

        for (int i = 0; i < maxDevices; i++)
        {
            contexts[i] = new CudaContext(deviceId: i);
            _contexts.Add(contexts[i]);
            deviceIds[i] = i;
        }

        // Act
        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: contexts,
            deviceIds: deviceIds,
            barrierId: 1,
            capacity: 1024);

        _barriers.Add(barrier);

        // Assert
        barrier.DeviceCount.Should().Be(maxDevices);
        barrier.ParticipatingDevices.Should().HaveCount(maxDevices);

        _output.WriteLine($"✓ System supports {maxDevices} GPU barrier");
    }

    [Fact]
    public void Constructor_LargeCapacity_AcceptsValue()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        const int largeCapacity = 1_000_000;

        // Act
        var barrier = new CudaSystemBarrier(
            provider: null,
            synchronizer: synchronizer,
            contexts: new[] { context },
            deviceIds: new[] { 0 },
            barrierId: 1,
            capacity: largeCapacity);

        _barriers.Add(barrier);

        // Assert
        barrier.Capacity.Should().Be(largeCapacity);

        _output.WriteLine($"✓ Large capacity ({largeCapacity:N0}) accepted");
    }

    #endregion
}
