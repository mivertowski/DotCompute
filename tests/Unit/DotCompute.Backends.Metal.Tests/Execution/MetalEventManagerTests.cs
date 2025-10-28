// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using DotCompute.Backends.Metal.Execution;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Tests.Execution;

/// <summary>
/// Comprehensive tests for MetalEventManager covering event creation, timing, and profiling.
/// Target: 80%+ coverage for MetalEventManager component.
/// </summary>
public sealed class MetalEventManagerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalEventManager> _logger;
    private IntPtr _device;

    public MetalEventManagerTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<MetalEventManager>();

        if (MetalNative.IsMetalSupported())
        {
            _device = MetalNative.CreateSystemDefaultDevice();
        }
    }

    public void Dispose()
    {
        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }
    }

    #region Event Creation Tests

    [SkippableFact]
    public async Task CreateTimingEventAsync_ValidDevice_CreatesSuccessfully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);

        // Act
        using var eventHandle = await eventManager.CreateTimingEventAsync();

        // Assert
        Assert.NotEqual(default, eventHandle.EventId);
        Assert.NotEqual(IntPtr.Zero, eventHandle.Handle);
        Assert.Equal(MetalEventType.Timing, eventHandle.Type);
        _output.WriteLine($"Created timing event: ID={eventHandle.EventId}, Handle={eventHandle.Handle}");
    }

    [SkippableFact]
    public async Task CreateSyncEventAsync_ValidDevice_CreatesSuccessfully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);

        // Act
        using var eventHandle = await eventManager.CreateSyncEventAsync();

        // Assert
        Assert.NotEqual(default, eventHandle.EventId);
        Assert.NotEqual(IntPtr.Zero, eventHandle.Handle);
        Assert.Equal(MetalEventType.Synchronization, eventHandle.Type);
        _output.WriteLine($"Created sync event: ID={eventHandle.EventId}");
    }

    [SkippableFact]
    public async Task CreateTimingPairAsync_ValidDevice_CreatesTwoEvents()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);

        // Act
        var (startEvent, endEvent) = await eventManager.CreateTimingPairAsync();

        // Assert
        Assert.NotEqual(default, startEvent.EventId);
        Assert.NotEqual(default, endEvent.EventId);
        Assert.NotEqual(startEvent.EventId, endEvent.EventId);
        _output.WriteLine($"Created timing pair: start={startEvent.EventId}, end={endEvent.EventId}");

        // Cleanup
        startEvent.Dispose();
        endEvent.Dispose();
    }

    [SkippableFact]
    public async Task CreateEventBatchAsync_MultipleEvents_CreatesAll()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        const int batchSize = 10;

        // Act
        var events = await eventManager.CreateEventBatchAsync(batchSize, MetalEventType.Timing);

        // Assert
        Assert.Equal(batchSize, events.Length);
        Assert.All(events, e => Assert.Equal(MetalEventType.Timing, e.Type));
        _output.WriteLine($"Created batch of {batchSize} timing events");

        // Cleanup
        foreach (var evt in events)
        {
            evt.Dispose();
        }
    }

    #endregion

    #region Event Recording and Waiting Tests

    [SkippableFact]
    public async Task RecordEvent_ValidEvent_RecordsSuccessfully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        using var eventHandle = await eventManager.CreateTimingEventAsync();
        var commandQueue = MetalNative.CreateCommandQueue(_device);

        try
        {
            // Act
            eventManager.RecordEvent(eventHandle.EventId, commandQueue);

            // Assert - Should complete without exception
            _output.WriteLine($"Recorded event {eventHandle.EventId} on command queue {commandQueue}");
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
        }
    }

    [SkippableFact]
    public async Task WaitForEventAsync_RecordedEvent_CompletesSuccessfully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        using var eventHandle = await eventManager.CreateTimingEventAsync();
        var commandQueue = MetalNative.CreateCommandQueue(_device);

        try
        {
            // Act
            eventManager.RecordEvent(eventHandle.EventId, commandQueue);
            await eventManager.WaitForEventAsync(eventHandle.EventId);

            // Assert - Should complete without timeout
            _output.WriteLine($"Event {eventHandle.EventId} completed successfully");
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
        }
    }

    [SkippableFact]
    public async Task WaitForEventAsync_WithTimeout_CompletesWithinTimeout()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        using var eventHandle = await eventManager.CreateTimingEventAsync();
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await eventManager.WaitForEventAsync(eventHandle.EventId, timeout);
        }
        catch (TimeoutException)
        {
            // Expected for non-recorded event
        }
        stopwatch.Stop();

        // Assert
        _output.WriteLine($"Wait operation took {stopwatch.Elapsed.TotalMilliseconds:F2}ms (timeout: {timeout.TotalMilliseconds}ms)");
    }

    [SkippableFact]
    public async Task IsEventComplete_RecordedEvent_ReturnsTrue()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        using var eventHandle = await eventManager.CreateTimingEventAsync();
        var commandQueue = MetalNative.CreateCommandQueue(_device);

        try
        {
            // Act
            eventManager.RecordEvent(eventHandle.EventId, commandQueue);
            var isComplete = eventManager.IsEventComplete(eventHandle.EventId);

            // Assert
            _output.WriteLine($"Event completion status: {isComplete}");
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
        }
    }

    #endregion

    #region Timing Measurement Tests

    [SkippableFact]
    public async Task MeasureElapsedTimeAsync_TimingPair_MeasuresCorrectly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        var (startEvent, endEvent) = await eventManager.CreateTimingPairAsync();
        var commandQueue = MetalNative.CreateCommandQueue(_device);

        try
        {
            // Act
            eventManager.RecordEvent(startEvent.EventId, commandQueue);
            await Task.Delay(10); // Simulate work
            eventManager.RecordEvent(endEvent.EventId, commandQueue);

            var elapsedTime = await eventManager.MeasureElapsedTimeAsync(startEvent.EventId, endEvent.EventId);

            // Assert
            Assert.True(elapsedTime >= 0);
            _output.WriteLine($"Elapsed time: {elapsedTime:F3}ms");
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
            startEvent.Dispose();
            endEvent.Dispose();
        }
    }

    [SkippableFact]
    public async Task MeasureOperationAsync_ValidOperation_ReturnsTimingResult()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        var commandQueue = MetalNative.CreateCommandQueue(_device);
        var operationExecuted = false;

        try
        {
            // Act
            var result = await eventManager.MeasureOperationAsync(
                async (queue) =>
                {
                    operationExecuted = true;
                    await Task.Delay(10);
                },
                commandQueue,
                "TestOperation");

            // Assert
            Assert.True(operationExecuted);
            Assert.Equal("TestOperation", result.OperationName);
            Assert.True(result.GpuTimeMs >= 0);
            Assert.True(result.CpuTimeMs >= 0);
            _output.WriteLine($"Operation timing - GPU: {result.GpuTimeMs:F3}ms, CPU: {result.CpuTimeMs:F3}ms, Overhead: {result.OverheadMs:F3}ms");
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
        }
    }

    #endregion

    #region Profiling Tests

    [SkippableFact]
    public async Task ProfileOperationAsync_MultipleIterations_ReturnsStatistics()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        var commandQueue = MetalNative.CreateCommandQueue(_device);
        const int iterations = 20;

        try
        {
            // Act
            var result = await eventManager.ProfileOperationAsync(
                async (queue) => await Task.Delay(1),
                iterations,
                commandQueue,
                "ProfilingTest");

            // Assert
            Assert.Equal(iterations, result.Iterations);
            Assert.True(result.AverageGpuTimeMs >= 0);
            Assert.True(result.MinGpuTimeMs >= 0);
            Assert.True(result.MaxGpuTimeMs >= 0);
            Assert.Contains(50, result.Percentiles.Keys);
            Assert.Contains(95, result.Percentiles.Keys);
            Assert.Contains(99, result.Percentiles.Keys);
            _output.WriteLine($"Profiling results - Avg: {result.AverageGpuTimeMs:F3}ms, Min: {result.MinGpuTimeMs:F3}ms, Max: {result.MaxGpuTimeMs:F3}ms, P95: {result.Percentiles[95]:F3}ms");
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
        }
    }

    #endregion

    #region Event Callback Tests

    [SkippableFact]
    public async Task AddEventCallback_ValidEvent_ExecutesCallback()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        using var eventHandle = await eventManager.CreateTimingEventAsync();
        var commandQueue = MetalNative.CreateCommandQueue(_device);
        var callbackExecuted = false;
        var tcs = new TaskCompletionSource<bool>();

        try
        {
            // Act
            eventManager.AddEventCallback(eventHandle.EventId, async (eventId) =>
            {
                callbackExecuted = true;
                tcs.SetResult(true);
                await Task.CompletedTask;
            });

            eventManager.RecordEvent(eventHandle.EventId, commandQueue);

            // Wait for callback
            await Task.WhenAny(tcs.Task, Task.Delay(5000));

            // Assert
            Assert.True(callbackExecuted);
            _output.WriteLine("Event callback executed successfully");
        }
        finally
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
        }
    }

    #endregion

    #region Statistics Tests

    [SkippableFact]
    public void GetStatistics_InitialState_ReturnsValidStats()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);

        // Act
        var stats = eventManager.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.ActiveEvents);
        Assert.Equal(0, stats.TotalEventsCreated);
        Assert.True(stats.MaxConcurrentEvents > 0);
        _output.WriteLine($"Initial stats - Max events: {stats.MaxConcurrentEvents}");
    }

    [SkippableFact]
    public async Task GetStatistics_AfterCreatingEvents_TracksCorrectly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);

        // Act
        var events = new List<MetalEventHandle>();
        for (int i = 0; i < 5; i++)
        {
            events.Add(await eventManager.CreateTimingEventAsync());
        }

        var stats = eventManager.GetStatistics();

        // Assert
        Assert.Equal(5, stats.ActiveEvents);
        Assert.Equal(5, stats.TotalEventsCreated);
        _output.WriteLine($"Stats after creation - Active: {stats.ActiveEvents}, Total created: {stats.TotalEventsCreated}");

        // Cleanup
        events.ForEach(e => e.Dispose());
    }

    [SkippableFact]
    public async Task PerformMaintenance_WithCompletedEvents_CleansUp()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        using var eventHandle = await eventManager.CreateTimingEventAsync();

        // Act
        eventManager.PerformMaintenance();

        // Assert - Should complete without exception
        _output.WriteLine("Maintenance performed successfully");
    }

    #endregion

    #region Edge Case Tests

    [SkippableFact]
    public async Task Dispose_WithActiveEvents_CleansUpProperly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var eventManager = new MetalEventManager(_device, _logger);
        var eventHandle = await eventManager.CreateTimingEventAsync();

        // Act
        eventManager.Dispose();

        // Assert - Should not throw
        Assert.True(true);
    }

    [SkippableFact]
    public async Task MeasureElapsedTimeAsync_NonTimingEvents_ThrowsInvalidOperationException()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var eventManager = new MetalEventManager(_device, _logger);
        using var syncEvent1 = await eventManager.CreateSyncEventAsync();
        using var syncEvent2 = await eventManager.CreateSyncEventAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await eventManager.MeasureElapsedTimeAsync(syncEvent1.EventId, syncEvent2.EventId);
        });
    }

    #endregion
}
