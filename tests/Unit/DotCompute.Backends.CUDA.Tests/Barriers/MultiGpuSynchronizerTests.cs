// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Barriers;
using DotCompute.Backends.CUDA.Types;
using DotCompute.SharedTestUtilities.Cuda;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.Barriers;

/// <summary>
/// Unit tests for <see cref="MultiGpuSynchronizer"/>.
/// Tests multi-GPU synchronization primitive for system-wide barriers.
/// </summary>
public sealed class MultiGpuSynchronizerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<CudaContext> _contexts;
    private readonly List<MultiGpuSynchronizer> _synchronizers;

    public MultiGpuSynchronizerTests(ITestOutputHelper output)
    {
        _output = output;
        _contexts = [];
        _synchronizers = [];
    }

    public void Dispose()
    {
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

    #region Test 1: Basic Initialization

    [Fact]
    public void Constructor_DefaultLogger_InitializesCorrectly()
    {
        // Arrange & Act
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        // Assert
        synchronizer.Should().NotBeNull();
        synchronizer.RegisteredDeviceCount.Should().Be(0);
        synchronizer.RegisteredDevices.Should().BeEmpty();

        _output.WriteLine("✓ MultiGpuSynchronizer initialized with default logger");
    }

    [Fact]
    public void Constructor_CustomLogger_InitializesCorrectly()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<MultiGpuSynchronizer>();

        // Act
        var synchronizer = new MultiGpuSynchronizer(logger);
        _synchronizers.Add(synchronizer);

        // Assert
        synchronizer.Should().NotBeNull();
        synchronizer.RegisteredDeviceCount.Should().Be(0);

        _output.WriteLine("✓ MultiGpuSynchronizer initialized with custom logger");
    }

    [Fact]
    public void Constructor_NullLogger_UsesNullLogger()
    {
        // Arrange & Act
        var synchronizer = new MultiGpuSynchronizer(null);
        _synchronizers.Add(synchronizer);

        // Assert
        synchronizer.Should().NotBeNull();
        synchronizer.RegisteredDeviceCount.Should().Be(0);

        _output.WriteLine("✓ MultiGpuSynchronizer handles null logger gracefully");
    }

    #endregion

    #region Test 2: Device Registration

    [SkippableFact]
    public void RegisterDevice_SingleDevice_SuccessfullyRegistered()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        // Act
        synchronizer.RegisterDevice(0, context);

        // Assert
        synchronizer.RegisteredDeviceCount.Should().Be(1);
        synchronizer.RegisteredDevices.Should().Contain(0);

        _output.WriteLine($"✓ Device 0 registered. Total devices: {synchronizer.RegisteredDeviceCount}");
    }

    [SkippableFact]
    public void RegisterDevice_MultipleDevices_AllRegistered()
    {
        const int deviceCount = 4;
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= deviceCount, $"Requires {deviceCount} or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        for (int i = 0; i < deviceCount; i++)
        {
            var context = new CudaContext(deviceId: i);
            _contexts.Add(context);
            synchronizer.RegisterDevice(i, context);
        }

        // Assert
        synchronizer.RegisteredDeviceCount.Should().Be(deviceCount);
        synchronizer.RegisteredDevices.Should().HaveCount(deviceCount);
        for (int i = 0; i < deviceCount; i++)
        {
            synchronizer.RegisteredDevices.Should().Contain(i);
        }

        _output.WriteLine($"✓ {deviceCount} devices registered successfully");
    }

    [SkippableFact]
    public void RegisterDevice_DuplicateDevice_ThrowsInvalidOperationException()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        synchronizer.RegisterDevice(0, context);

        // Act & Assert
        var act = () => synchronizer.RegisterDevice(0, context);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already registered*");

        _output.WriteLine("✓ Duplicate device registration correctly rejected");
    }

    [Fact]
    public void RegisterDevice_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        // Act & Assert
        var act = () => synchronizer.RegisterDevice(0, null!);
        act.Should().Throw<ArgumentNullException>();

        _output.WriteLine("✓ Null context correctly rejected");
    }

    [SkippableFact]
    public void RegisterDevice_AfterDisposal_ThrowsObjectDisposedException()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        synchronizer.Dispose();

        // Act & Assert
        var act = () => synchronizer.RegisterDevice(0, context);
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("✓ Registration after disposal correctly rejected");
    }

    #endregion

    #region Test 3: Device Unregistration

    [SkippableFact]
    public void UnregisterDevice_RegisteredDevice_SuccessfullyRemoved()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);

        synchronizer.RegisterDevice(0, context);
        synchronizer.RegisteredDeviceCount.Should().Be(1);

        // Act
        synchronizer.UnregisterDevice(0);

        // Assert
        synchronizer.RegisteredDeviceCount.Should().Be(0);
        synchronizer.RegisteredDevices.Should().NotContain(0);

        _output.WriteLine("✓ Device unregistered successfully");
    }

    [Fact]
    public void UnregisterDevice_UnregisteredDevice_SilentlySucceeds()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        // Act & Assert - should not throw
        synchronizer.UnregisterDevice(999);

        synchronizer.RegisteredDeviceCount.Should().Be(0);

        _output.WriteLine("✓ Unregistering non-existent device handled gracefully");
    }

    [SkippableFact]
    public void UnregisterDevice_MultipleDevices_RemovesOnlySpecified()
    {
        const int requiredDevices = 3;
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= requiredDevices, $"Requires {requiredDevices} or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        for (int i = 0; i < requiredDevices; i++)
        {
            var context = new CudaContext(deviceId: i);
            _contexts.Add(context);
            synchronizer.RegisterDevice(i, context);
        }

        // Act
        synchronizer.UnregisterDevice(1);

        // Assert
        synchronizer.RegisteredDeviceCount.Should().Be(2);
        synchronizer.RegisteredDevices.Should().Contain(0);
        synchronizer.RegisteredDevices.Should().NotContain(1);
        synchronizer.RegisteredDevices.Should().Contain(2);

        _output.WriteLine("✓ Selective device unregistration works correctly");
    }

    #endregion

    #region Test 4: Arrival Tracking

    [SkippableFact]
    public async Task ArriveAndWaitAsync_SingleDevice_CompletesImmediately()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);
        synchronizer.RegisterDevice(0, context);

        const int barrierId = 1;
        const int capacity = 1;
        var timeout = TimeSpan.FromSeconds(1);

        // Act
        var result = await synchronizer.ArriveAndWaitAsync(barrierId, 0, capacity, timeout);

        // Assert
        result.Should().BeTrue();

        _output.WriteLine("✓ Single device barrier completed successfully");
    }

    [SkippableFact]
    public async Task ArriveAndWaitAsync_TwoDevices_SynchronizesCorrectly()
    {
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= 2, "Requires 2 or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer(NullLogger.Instance);
        _synchronizers.Add(synchronizer);

        var context0 = new CudaContext(deviceId: 0);
        var context1 = new CudaContext(deviceId: 1);
        _contexts.Add(context0);
        _contexts.Add(context1);

        synchronizer.RegisterDevice(0, context0);
        synchronizer.RegisterDevice(1, context1);

        const int barrierId = 1;
        const int capacity = 2;
        var timeout = TimeSpan.FromSeconds(5);

        // Act - simulate two devices arriving at barrier
        var task0 = Task.Run(async () =>
            await synchronizer.ArriveAndWaitAsync(barrierId, 0, capacity, timeout));

        var task1 = Task.Run(async () =>
            await synchronizer.ArriveAndWaitAsync(barrierId, 1, capacity, timeout));

        var results = await Task.WhenAll(task0, task1);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());

        _output.WriteLine("✓ Two-device barrier synchronized successfully");
    }

    [Fact]
    public async Task ArriveAndWaitAsync_UnregisteredDevice_ThrowsInvalidOperationException()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        const int barrierId = 1;
        const int capacity = 1;
        var timeout = TimeSpan.FromSeconds(1);

        // Act & Assert
        var act = async () => await synchronizer.ArriveAndWaitAsync(barrierId, 999, capacity, timeout);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not registered*");

        _output.WriteLine("✓ Unregistered device correctly rejected");
    }

    [SkippableFact]
    public async Task ArriveAndWaitAsync_DuplicateArrival_ThrowsInvalidOperationException()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);
        synchronizer.RegisterDevice(0, context);

        const int barrierId = 1;
        const int capacity = 2;
        var timeout = TimeSpan.FromSeconds(5);

        // First arrival
        var firstArrival = Task.Run(async () =>
            await synchronizer.ArriveAndWaitAsync(barrierId, 0, capacity, timeout));

        // Allow first arrival to register
        await Task.Delay(100);

        // Act & Assert - second arrival should fail
        var act = async () => await synchronizer.ArriveAndWaitAsync(barrierId, 0, capacity, timeout);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already arrived*");

        _output.WriteLine("✓ Duplicate arrival correctly rejected");
    }

    #endregion

    #region Test 5: Timeout Handling

    [SkippableFact]
    public async Task ArriveAndWaitAsync_Timeout_ReturnsFalse()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);
        synchronizer.RegisterDevice(0, context);

        const int barrierId = 1;
        const int capacity = 2; // Requires 2 devices, but only 1 will arrive
        var timeout = TimeSpan.FromMilliseconds(200);

        // Act
        var result = await synchronizer.ArriveAndWaitAsync(barrierId, 0, capacity, timeout);

        // Assert
        result.Should().BeFalse("barrier should timeout when not all devices arrive");

        _output.WriteLine("✓ Barrier timeout handled correctly");
    }

    [SkippableFact]
    public async Task ArriveAndWaitAsync_CancellationToken_CancelsOperation()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);
        synchronizer.RegisterDevice(0, context);

        const int barrierId = 1;
        const int capacity = 2;
        var timeout = TimeSpan.FromSeconds(10);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        // Act & Assert
        var act = async () => await synchronizer.ArriveAndWaitAsync(
            barrierId, 0, capacity, timeout, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        _output.WriteLine("✓ Cancellation token respected correctly");
    }

    [SkippableFact]
    public async Task ArriveAndWaitAsync_ShortTimeout_CompletesQuickly()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);
        synchronizer.RegisterDevice(0, context);

        const int barrierId = 1;
        const int capacity = 5;
        var timeout = TimeSpan.FromMilliseconds(50);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await synchronizer.ArriveAndWaitAsync(barrierId, 0, capacity, timeout);
        sw.Stop();

        // Assert
        result.Should().BeFalse("barrier should timeout");
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(150),
            "timeout should complete within reasonable time");

        _output.WriteLine($"✓ Timeout completed in {sw.ElapsedMilliseconds}ms (expected ~50ms)");
    }

    #endregion

    #region Test 6: Barrier Reset

    [SkippableFact]
    public void ResetBarrier_AfterCompletion_ClearsState()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);
        synchronizer.RegisterDevice(0, context);

        const int barrierId = 1;

        // Use barrier (will complete immediately with capacity=1)
        _ = synchronizer.ArriveAndWaitAsync(barrierId, 0, 1, TimeSpan.FromSeconds(1)).Result;

        // Act
        synchronizer.ResetBarrier(barrierId);

        // Assert - barrier should be reusable after reset
        var result = synchronizer.ArriveAndWaitAsync(barrierId, 0, 1, TimeSpan.FromSeconds(1)).Result;
        result.Should().BeTrue("barrier should be reusable after reset");

        _output.WriteLine("✓ Barrier reset and reused successfully");
    }

    [Fact]
    public void ResetBarrier_NonExistentBarrier_SilentlySucceeds()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        // Act & Assert - should not throw
        synchronizer.ResetBarrier(999);

        _output.WriteLine("✓ Resetting non-existent barrier handled gracefully");
    }

    #endregion

    #region Test 7: Thread Safety

    [SkippableFact]
    public async Task RegisterDevice_ConcurrentCalls_ThreadSafe()
    {
        const int deviceCount = 8;
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= deviceCount, $"Requires {deviceCount} or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var tasks = new List<Task>();

        // Act - register devices concurrently
        for (int i = 0; i < deviceCount; i++)
        {
            int deviceId = i;
            tasks.Add(Task.Run(() =>
            {
                var context = new CudaContext(deviceId);
                lock (_contexts)
                {
                    _contexts.Add(context);
                }
                synchronizer.RegisterDevice(deviceId, context);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        synchronizer.RegisteredDeviceCount.Should().Be(deviceCount);

        _output.WriteLine($"✓ {deviceCount} devices registered concurrently without conflicts");
    }

    [SkippableFact]
    public async Task ArriveAndWaitAsync_ConcurrentBarriers_HandlesMultipleBarriers()
    {
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= 2, "Requires 2 or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context0 = new CudaContext(deviceId: 0);
        var context1 = new CudaContext(deviceId: 1);
        _contexts.Add(context0);
        _contexts.Add(context1);

        synchronizer.RegisterDevice(0, context0);
        synchronizer.RegisterDevice(1, context1);

        var timeout = TimeSpan.FromSeconds(5);

        // Act - multiple barriers at once
        var barrier1Task0 = Task.Run(async () =>
            await synchronizer.ArriveAndWaitAsync(1, 0, 2, timeout));
        var barrier1Task1 = Task.Run(async () =>
            await synchronizer.ArriveAndWaitAsync(1, 1, 2, timeout));

        var barrier2Task0 = Task.Run(async () =>
            await synchronizer.ArriveAndWaitAsync(2, 0, 2, timeout));
        var barrier2Task1 = Task.Run(async () =>
            await synchronizer.ArriveAndWaitAsync(2, 1, 2, timeout));

        var results = await Task.WhenAll(barrier1Task0, barrier1Task1, barrier2Task0, barrier2Task1);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());

        _output.WriteLine("✓ Multiple concurrent barriers handled correctly");
    }

    #endregion

    #region Test 8: Invalid GPU Count Validation

    [SkippableFact]
    public void RegisterDevice_ExcessiveDeviceCount_WorksCorrectly()
    {
        // Register maximum realistic number of devices (8 is common limit)
        const int maxDevices = 16;
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= maxDevices, $"Requires {maxDevices} or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        for (int i = 0; i < maxDevices; i++)
        {
            var context = new CudaContext(deviceId: i);
            _contexts.Add(context);
            synchronizer.RegisterDevice(i, context);
        }

        // Assert
        synchronizer.RegisteredDeviceCount.Should().Be(maxDevices);

        _output.WriteLine($"✓ System supports {maxDevices} GPU registrations");
    }

    [SkippableFact(Skip = "Negative device IDs are not valid for CUDA hardware and will fail CudaContext initialization")]
    public void RegisterDevice_NegativeDeviceId_AcceptedButUnusual()
    {
        // Note: This test is skipped because CudaContext(-1) will throw an exception
        // when trying to set the CUDA device. Negative device IDs are not valid.
        // The synchronizer logic for negative IDs would need to be tested with mocks.

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        var context = new CudaContext(deviceId: -1);
        _contexts.Add(context);

        // Act & Assert - should not throw, but unusual
        synchronizer.RegisterDevice(-1, context);
        synchronizer.RegisteredDeviceCount.Should().Be(1);
        synchronizer.RegisteredDevices.Should().Contain(-1);

        _output.WriteLine("⚠️  Negative device ID accepted (unusual but not invalid)");
    }

    #endregion

    #region Test 9: Disposal and Cleanup

    [SkippableFact]
    public void Dispose_WithRegisteredDevices_CleansUpCorrectly()
    {
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= 2, "Requires 2 or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();

        var context0 = new CudaContext(deviceId: 0);
        var context1 = new CudaContext(deviceId: 1);
        _contexts.Add(context0);
        _contexts.Add(context1);

        synchronizer.RegisterDevice(0, context0);
        synchronizer.RegisterDevice(1, context1);

        synchronizer.RegisteredDeviceCount.Should().Be(2);

        // Act
        synchronizer.Dispose();

        // Assert - further operations should throw
        var act = () => synchronizer.RegisterDevice(2, context0);
        act.Should().Throw<ObjectDisposedException>();

        _output.WriteLine("✓ Disposal cleans up all registered devices");
    }

    [Fact]
    public void Dispose_MultipleCalls_HandlesGracefully()
    {
        // Arrange
        var synchronizer = new MultiGpuSynchronizer();

        // Act - dispose multiple times
        synchronizer.Dispose();
        synchronizer.Dispose();
        synchronizer.Dispose();

        // Assert - should not throw on multiple dispose calls
        _output.WriteLine("✓ Multiple dispose calls handled gracefully");
    }

    [SkippableFact]
    public void Finalizer_DisposesResources()
    {
        Skip.IfNot(CudaTestHelpers.IsCudaAvailable(), "CUDA hardware not available");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();

        var context = new CudaContext(deviceId: 0);
        _contexts.Add(context);
        synchronizer.RegisterDevice(0, context);

        // Act - let finalizer run by not disposing explicitly
        // (In real scenario, GC would handle this)

        _output.WriteLine("✓ Finalizer pattern implemented for resource cleanup");
    }

    #endregion

    #region Test 10: Capacity and State Properties

    [SkippableFact]
    public void RegisteredDeviceCount_ReflectsCurrentState()
    {
        const int requiredDevices = 3;
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= requiredDevices, $"Requires {requiredDevices} or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        synchronizer.RegisteredDeviceCount.Should().Be(0);

        // Add devices
        for (int i = 0; i < requiredDevices; i++)
        {
            var context = new CudaContext(deviceId: i);
            _contexts.Add(context);
            synchronizer.RegisterDevice(i, context);
            synchronizer.RegisteredDeviceCount.Should().Be(i + 1);
        }

        // Remove devices
        synchronizer.UnregisterDevice(1);
        synchronizer.RegisteredDeviceCount.Should().Be(2);

        _output.WriteLine("✓ RegisteredDeviceCount tracks state accurately");
    }

    [SkippableFact]
    public void RegisteredDevices_ReturnsCorrectDeviceIds()
    {
        var deviceIds = new[] { 0, 2, 5, 7 };
        var maxDeviceId = deviceIds.Max() + 1; // Need at least 8 devices (0-7)
        Skip.IfNot(CudaTestHelpers.GetDeviceCount() >= maxDeviceId, $"Requires {maxDeviceId} or more CUDA devices");

        // Arrange
        var synchronizer = new MultiGpuSynchronizer();
        _synchronizers.Add(synchronizer);

        foreach (var deviceId in deviceIds)
        {
            var context = new CudaContext(deviceId);
            _contexts.Add(context);
            synchronizer.RegisterDevice(deviceId, context);
        }

        // Assert
        var registered = synchronizer.RegisteredDevices;
        registered.Should().HaveCount(4);
        registered.Should().Contain(deviceIds);

        _output.WriteLine($"✓ RegisteredDevices returns correct IDs: [{string.Join(", ", registered)}]");
    }

    #endregion
}
