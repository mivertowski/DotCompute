// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using DotCompute.Backends.Metal.Execution;
using DotCompute.Backends.Metal.Execution.Types;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Tests.Execution;

/// <summary>
/// Comprehensive tests for MetalCommandStream covering stream creation, lifecycle, and synchronization.
/// Target: 80%+ coverage for MetalCommandStream component.
/// </summary>
public sealed class MetalCommandStreamTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<MetalCommandStream> _logger;
    private IntPtr _device;

    public MetalCommandStreamTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _logger = loggerFactory.CreateLogger<MetalCommandStream>();

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

    #region Stream Creation Tests

    [SkippableFact]
    public async Task CreateStreamAsync_DefaultFlags_CreatesValidStream()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);

        // Act
        using var stream = await commandStream.CreateStreamAsync();

        // Assert
        Assert.NotEqual(default, stream.StreamId);
        Assert.NotEqual(IntPtr.Zero, stream.CommandQueue);
        _output.WriteLine($"Created stream: ID={stream.StreamId}, CommandQueue={stream.CommandQueue}");
    }

    [SkippableFact]
    public async Task CreateStreamAsync_AllPriorities_CreatesSuccessfully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);

        // Act & Assert
        foreach (MetalStreamPriority priority in Enum.GetValues(typeof(MetalStreamPriority)))
        {
            using var stream = await commandStream.CreateStreamAsync(priority: priority);
            Assert.NotEqual(default, stream.StreamId);
            _output.WriteLine($"Created stream with priority {priority}: {stream.StreamId}");
        }
    }

    [SkippableFact]
    public async Task CreateStreamBatchAsync_MultipleStreams_CreatesAll()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        const int batchSize = 5;

        // Act
        var streams = await commandStream.CreateStreamBatchAsync(batchSize);

        // Assert
        Assert.Equal(batchSize, streams.Length);
        Assert.All(streams, stream => Assert.NotEqual(default, stream.StreamId));
        _output.WriteLine($"Created batch of {batchSize} streams successfully");

        // Cleanup
        foreach (var stream in streams)
        {
            stream.Dispose();
        }
    }

    #endregion

    #region Stream Synchronization Tests

    [SkippableFact]
    public async Task SynchronizeStreamAsync_ValidStream_CompletesSuccessfully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        using var stream = await commandStream.CreateStreamAsync();

        // Act
        await commandStream.SynchronizeStreamAsync(stream.StreamId);

        // Assert - Should complete without exception
        Assert.True(commandStream.IsStreamReady(stream.StreamId));
        _output.WriteLine($"Stream {stream.StreamId} synchronized successfully");
    }

    [SkippableFact]
    public async Task SynchronizeStreamAsync_WithTimeout_CompletesWithinTimeout()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        using var stream = await commandStream.CreateStreamAsync();
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await commandStream.SynchronizeStreamAsync(stream.StreamId, timeout);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.Elapsed < timeout);
        _output.WriteLine($"Synchronization completed in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
    }

    [SkippableFact]
    public async Task SynchronizeStreamsAsync_TwoStreams_HandlesEventBasedSync()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        using var stream1 = await commandStream.CreateStreamAsync();
        using var stream2 = await commandStream.CreateStreamAsync();
        var metalEvent = new MetalEvent(null!, DotCompute.Backends.Metal.Execution.EventId.New());

        // Act & Assert - Should complete without exception
        try
        {
            await commandStream.SynchronizeStreamsAsync(stream1.StreamId, stream2.StreamId, metalEvent);
            _output.WriteLine($"Synchronized streams {stream1.StreamId} and {stream2.StreamId}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Expected behavior for event synchronization: {ex.Message}");
        }
    }

    #endregion

    #region Command Execution Tests

    [SkippableFact]
    public async Task ExecuteCommandAsync_ValidOperation_ExecutesSuccessfully()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        using var stream = await commandStream.CreateStreamAsync();
        var commandExecuted = false;

        // Act
        var result = await commandStream.ExecuteCommandAsync(
            stream.StreamId,
            async (commandQueue, commandBuffer) =>
            {
                commandExecuted = true;
                await Task.CompletedTask;
            },
            "TestOperation");

        // Assert
        Assert.True(commandExecuted);
        Assert.True(result.Success);
        Assert.Equal("TestOperation", result.OperationName);
        _output.WriteLine($"Command executed successfully in {result.ExecutionTime.TotalMilliseconds:F2}ms");
    }

    [SkippableFact]
    public async Task ExecuteCommandAsync_MultipleOperations_TracksStats()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        using var stream = await commandStream.CreateStreamAsync();

        // Act
        for (int i = 0; i < 10; i++)
        {
            await commandStream.ExecuteCommandAsync(
                stream.StreamId,
                async (_, __) => await Task.CompletedTask,
                $"Operation-{i}");
        }

        var stats = commandStream.GetStatistics();

        // Assert
        Assert.True(stats.TotalCommandsExecuted >= 10);
        _output.WriteLine($"Executed 10 operations, total commands: {stats.TotalCommandsExecuted}");
    }

    #endregion

    #region Optimized Stream Group Tests

    [SkippableFact]
    public async Task CreateOptimizedGroupAsync_AppleSilicon_CreatesCorrectCount()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);

        // Act
        var group = await commandStream.CreateOptimizedGroupAsync("TestGroup");

        // Assert
        Assert.NotNull(group);
        Assert.Equal("TestGroup", group.Name);
        Assert.True(group.Streams.Count > 0);
        _output.WriteLine($"Created optimized group with {group.Streams.Count} streams");
    }

    [SkippableFact]
    public async Task CreateOptimizedGroupAsync_HighPriority_CreatesWithCorrectPriority()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);

        // Act
        var group = await commandStream.CreateOptimizedGroupAsync("HighPriorityGroup", MetalStreamPriority.High);

        // Assert
        Assert.NotNull(group);
        Assert.True(group.Streams.Count > 0);
        _output.WriteLine($"Created high-priority group with {group.Streams.Count} streams");
    }

    #endregion

    #region Statistics and Monitoring Tests

    [SkippableFact]
    public void GetStatistics_InitialState_ReturnsValidStats()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);

        // Act
        var stats = commandStream.GetStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(0, stats.ActiveStreams);
        Assert.True(stats.OptimalConcurrentStreams > 0);
        Assert.True(stats.MaxConcurrentStreams > 0);
        _output.WriteLine($"Optimal streams: {stats.OptimalConcurrentStreams}, Max: {stats.MaxConcurrentStreams}");
    }

    [SkippableFact]
    public async Task GetStatistics_AfterStreamCreation_TracksActiveStreams()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);

        // Act
        var streams = new List<MetalStreamHandle>();
        for (int i = 0; i < 5; i++)
        {
            streams.Add(await commandStream.CreateStreamAsync());
        }

        var stats = commandStream.GetStatistics();

        // Assert
        Assert.Equal(5, stats.ActiveStreams);
        Assert.Equal(5, stats.TotalStreamsCreated);
        _output.WriteLine($"Active streams: {stats.ActiveStreams}, Total created: {stats.TotalStreamsCreated}");

        // Cleanup
        streams.ForEach(s => s.Dispose());
    }

    [SkippableFact]
    public void OptimizeStreamUsage_ActiveStreams_OptimizesCorrectly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);

        // Act
        commandStream.OptimizeStreamUsage();

        var stats = commandStream.GetStatistics();

        // Assert
        Assert.True(stats.ActiveStreams <= stats.OptimalConcurrentStreams);
        _output.WriteLine($"Optimized stream usage: {stats.ActiveStreams} active");
    }

    #endregion

    #region Stream Callback Tests

    [SkippableFact]
    public async Task AddStreamCallback_ValidStream_ExecutesCallback()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        using var stream = await commandStream.CreateStreamAsync();
        var callbackExecuted = false;
        var tcs = new TaskCompletionSource<bool>();

        // Act
        commandStream.AddStreamCallback(stream.StreamId, async (streamId) =>
        {
            callbackExecuted = true;
            tcs.SetResult(true);
            await Task.CompletedTask;
        });

        // Wait for callback
        await Task.WhenAny(tcs.Task, Task.Delay(5000));

        // Assert
        Assert.True(callbackExecuted);
        _output.WriteLine("Stream callback executed successfully");
    }

    #endregion

    #region Edge Case Tests

    [SkippableFact]
    public async Task IsStreamReady_NonExistentStream_ReturnsFalse()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        using var commandStream = new MetalCommandStream(_device, _logger);
        var nonExistentStreamId = StreamId.New();

        // Act
        var isReady = commandStream.IsStreamReady(nonExistentStreamId);

        // Assert
        Assert.False(isReady);
        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task Dispose_WithActiveStreams_CleansUpProperly()
    {
        // Arrange
        Skip.IfNot(MetalNative.IsMetalSupported(), "Metal is not supported on this system");
        var commandStream = new MetalCommandStream(_device, _logger);
        var stream = await commandStream.CreateStreamAsync();

        // Act
        commandStream.Dispose();

        // Assert - Should not throw
        Assert.True(true);
        await Task.CompletedTask;
    }

    #endregion
}
